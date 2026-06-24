using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using TaxReader.Domain.Entities;
using TaxReader.Domain.Enums;
using TaxReader.Infrastructure.Data;
using TaxReader.IntegrationTests.Fixtures;
using TaxReader.UnitTests.Helpers;

namespace TaxReader.IntegrationTests;

/// <summary>
/// LEGAL-06: proves GDPR Art. 17 account deletion cascades all PII while retaining anonymized
/// Payment rows (§257 HGB / §147 AO Aufbewahrungspflicht; DSGVO Art. 17(3)(b)).
/// D-01: Payment rows retained. D-02: TokenTransaction rows deleted. D-03: UserId nulled.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public sealed class GdprDeletionTests(PostgresContainerFixture fixture) : IAsyncLifetime
{
    public Task InitializeAsync() => fixture.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    private AppDbContext CreateContext() => new(
        new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(fixture.ConnectionString)
            .UseSnakeCaseNamingConvention()
            .Options);

    [Fact]
    public async Task DeleteAccount_CascadesAllUserData_RetainsAnonymizedPayment()
    {
        // Arrange — register user via API to get a valid BCrypt hash + token balance.
        const string email = "gdpr-test@test.local";
        const string password = "Test-Password-1234!";

        await using var factory = new IntegrationTestWebAppFactory(fixture.ConnectionString);
        var client = factory.CreateClient();

        var registerResponse = await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            email,
            password,
            displayName = "GDPR Test User"
        });
        registerResponse.StatusCode.Should().Be(HttpStatusCode.OK, "registration must succeed");

        // Login to get JWT.
        var loginResponse = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password });
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK, "login must succeed");
        var loginBody = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();
        loginBody.Should().NotBeNull();
        var jwt = loginBody!.AccessToken;
        jwt.Should().NotBeNullOrEmpty();

        // Seed the full data chain: ReceiptFile → Receipt → ReceiptItem → ItemClassification + TokenTransaction + Payment.
        Guid userId;
        Guid paymentId;

        await using (var ctx = CreateContext())
        {
            // The user was inserted by the API; look it up so we have the exact GUID.
            var user = await ctx.Users.FirstAsync(u => u.Email == email);
            userId = user.Id;

            var fileId = Guid.NewGuid();
            var file = TestDataFactory.CreateReceiptFile(id: fileId, contentHash: $"gdpr-hash-{fileId:N}");
            file.UserId = userId;
            ctx.ReceiptFiles.Add(file);

            var receiptId = Guid.NewGuid();
            ctx.Receipts.Add(TestDataFactory.CreateReceipt(id: receiptId, receiptFileId: fileId));

            var itemId = Guid.NewGuid();
            ctx.ReceiptItems.Add(TestDataFactory.CreateReceiptItem(id: itemId, receiptId: receiptId));

            ctx.ItemClassifications.Add(TestDataFactory.CreateClassification(receiptItemId: itemId));

            // TokenTransaction — must be deleted (D-02).
            ctx.TokenTransactions.Add(new TokenTransaction
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Amount = -10,
                BalanceAfter = 90,
                Type = TokenTransactionType.Consumption,
                Description = "GDPR test consumption",
                CreatedAt = DateTime.UtcNow
            });

            // Payment — must be retained with UserId nulled (D-01/D-03).
            // Insert directly (no Stripe mock needed — seeding for retention verification only).
            paymentId = Guid.NewGuid();
            ctx.Payments.Add(new Payment
            {
                Id = paymentId,
                UserId = userId,
                StripeEventId = $"evt_gdpr_test_{paymentId:N}",
                StripeSessionId = $"cs_gdpr_test_{paymentId:N}",
                CreditsGranted = 100,
                AmountCents = 499,
                Currency = "eur",
                Status = PaymentStatus.Granted,
                CreatedAt = DateTime.UtcNow
            });

            await ctx.SaveChangesAsync();
        }

        // Act — DELETE /api/v1/auth/account with password re-auth.
        var deleteRequest = new HttpRequestMessage(HttpMethod.Delete, "/api/v1/auth/account")
        {
            Content = JsonContent.Create(new { password })
        };
        deleteRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", jwt);
        var deleteResponse = await client.SendAsync(deleteRequest);
        deleteResponse.StatusCode.Should().BeOneOf(
            [HttpStatusCode.OK, HttpStatusCode.NoContent],
            "account deletion must succeed with correct password");

        // Assert — all user-scoped tables must be empty; Payment retained and anonymized.
        await using var verifyCtx = CreateContext();

        (await verifyCtx.Users.CountAsync()).Should().Be(0, "GDPR Art. 17: user row deleted");
        (await verifyCtx.ReceiptFiles.CountAsync()).Should().Be(0, "FK cascade: receipt_files deleted");
        (await verifyCtx.Receipts.CountAsync()).Should().Be(0, "FK cascade: receipts deleted");
        (await verifyCtx.ReceiptItems.CountAsync()).Should().Be(0, "FK cascade: receipt_items deleted");
        (await verifyCtx.ItemClassifications.CountAsync()).Should().Be(0, "FK cascade: item_classifications deleted");

        (await verifyCtx.TokenTransactions.CountAsync()).Should().Be(0,
            "D-02: TokenTransaction is data minimization, not Buchungsbeleg — must be deleted");

        (await verifyCtx.Payments.CountAsync()).Should().BeGreaterThan(0,
            "D-01: Payment rows retained for §257 HGB / §147 AO Aufbewahrungspflicht");

        (await verifyCtx.Payments.AllAsync(p => p.UserId == null)).Should().BeTrue(
            "D-03: user_id must be nulled so no PII linkage remains (DSGVO Art. 17(3)(b))");
    }

    // Internal DTO for deserialising the login response — avoids a direct reference to the auth DTO assembly.
    private sealed record LoginResponse(string AccessToken, string RefreshToken);
}
