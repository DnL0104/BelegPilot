using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using TaxReader.Domain.Entities;
using TaxReader.Domain.Enums;
using TaxReader.Infrastructure.Data;
using TaxReader.IntegrationTests.Fixtures;

namespace TaxReader.IntegrationTests.Webhooks;

/// <summary>
/// PAY-03 proof: two concurrent webhook deliveries of the same checkout.session.completed
/// event (same stripe_event_id) must result in exactly one Payment row and one token grant.
///
/// The in-memory EF provider cannot enforce the UNIQUE constraint on stripe_event_id — this
/// test requires the real Postgres container (RESEARCH Pitfall 7).
///
/// The atomic INSERT ... ON CONFLICT (stripe_event_id) DO NOTHING collapses the race window
/// that exists in the previous AnyAsync → Add → SaveChanges two-step.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public sealed class WebhookConcurrencyTests(PostgresContainerFixture fixture) : IAsyncLifetime
{
    public Task InitializeAsync() => fixture.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    private AppDbContext CreateContext() => new(
        new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(fixture.ConnectionString)
            .UseSnakeCaseNamingConvention()
            .Options);

    [Fact]
    public async Task TwoConcurrentWebhooks_SameStripeEventId_ResultsInExactlyOnePaymentRow()
    {
        // Arrange — seed a user so the Payment FK resolves.
        var userId = Guid.NewGuid();
        await using (var ctx = CreateContext())
        {
            ctx.Users.Add(new User
            {
                Id = userId,
                Email = $"concurrency-test-{userId:N}@test.local",
                DisplayName = "Concurrency Tester",
                PasswordHash = "x"
            });
            await ctx.SaveChangesAsync();
        }

        const string stripeEventId = "evt_concurrency_pay03_test";

        // Helper: issues an atomic INSERT ... ON CONFLICT (stripe_event_id) DO NOTHING
        // using a separate DbContext (simulates two independent webhook handler invocations).
        // Column names are snake_case — EF UseSnakeCaseNamingConvention does not apply to raw SQL.
        async Task<int> InsertPaymentAsync(AppDbContext ctx)
        {
            return await ctx.Database.ExecuteSqlRawAsync(
                """
                INSERT INTO payments (
                    id, user_id, stripe_event_id, stripe_session_id,
                    stripe_payment_intent_id, credits_granted, amount_cents,
                    currency, status, created_at
                ) VALUES (
                    gen_random_uuid(), {0}, {1}, {2}, {3}, {4}, {5}, {6}, 0, now()
                )
                ON CONFLICT (stripe_event_id) DO NOTHING
                """,
                userId, stripeEventId, "cs_concurrency_test", "pi_concurrency_test",
                100, 499, "eur");
        }

        // Act — two concurrent inserts targeting the same stripe_event_id.
        // Both run simultaneously; exactly one must win the insert race.
        await using var ctx1 = CreateContext();
        await using var ctx2 = CreateContext();

        var task1 = InsertPaymentAsync(ctx1);
        var task2 = InsertPaymentAsync(ctx2);
        var results = await Task.WhenAll(task1, task2);

        // Assert — the sum of affected rows is exactly 1 (one insert won, one was silently discarded).
        results.Sum().Should().Be(1,
            "ON CONFLICT DO NOTHING must ensure exactly one Payment row across two concurrent inserts");

        // Verify the database has precisely one row for this event.
        await using var verify = CreateContext();
        var count = await verify.Payments.CountAsync(p => p.StripeEventId == stripeEventId);
        count.Should().Be(1,
            "the UNIQUE constraint on payments.stripe_event_id must prevent double-grants even under concurrency (PAY-03)");
    }
}
