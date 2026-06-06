using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using TaxReader.Domain.Entities;
using TaxReader.Domain.Enums;
using TaxReader.Infrastructure.Data;
using TaxReader.IntegrationTests.Fixtures;

namespace TaxReader.IntegrationTests;

/// <summary>
/// QA-01 Pitfall 7: asserts that payments.stripe_event_id carries a real UNIQUE constraint
/// in Postgres. The in-memory EF provider ignores index/unique enforcement, so this test
/// can only pass by running against a real Postgres container.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public sealed class PaymentIdempotencyTests(PostgresContainerFixture fixture) : IAsyncLifetime
{
    public Task InitializeAsync() => fixture.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    private AppDbContext CreateContext() => new(
        new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(fixture.ConnectionString)
            .UseSnakeCaseNamingConvention()
            .Options);

    [Fact]
    public async Task SecondInsert_SameStripeEventId_ViolatesUniqueConstraint()
    {
        // Arrange — seed a user so the Payment FK resolves.
        var userId = Guid.NewGuid();
        await using (var ctx = CreateContext())
        {
            ctx.Users.Add(new User
            {
                Id = userId,
                Email = $"pay-test-{userId:N}@test.local",
                DisplayName = "Pay Tester",
                PasswordHash = "x"
            });
            await ctx.SaveChangesAsync();
        }

        const string stripeEventId = "evt_idempotency_real_postgres_test";

        // Insert first payment — must succeed.
        await using (var ctx = CreateContext())
        {
            ctx.Payments.Add(new Payment
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                StripeEventId = stripeEventId,
                StripeSessionId = "cs_first",
                CreditsGranted = 50,
                AmountCents = 499,
                Currency = "eur",
                Status = PaymentStatus.Granted,
                CreatedAt = DateTime.UtcNow
            });
            await ctx.SaveChangesAsync();
        }

        // Act — attempt to insert a second payment with the identical stripe_event_id.
        var act = async () =>
        {
            await using var ctx = CreateContext();
            ctx.Payments.Add(new Payment
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                StripeEventId = stripeEventId,
                StripeSessionId = "cs_duplicate",
                CreditsGranted = 50,
                AmountCents = 499,
                Currency = "eur",
                Status = PaymentStatus.Granted,
                CreatedAt = DateTime.UtcNow
            });
            await ctx.SaveChangesAsync();
        };

        // Assert — real Postgres 23505 unique_violation surfaces as DbUpdateException.
        await act.Should().ThrowAsync<DbUpdateException>(
            "payments.stripe_event_id has a UNIQUE index; the in-memory provider hides this constraint (RESEARCH Pitfall 7)");
    }
}
