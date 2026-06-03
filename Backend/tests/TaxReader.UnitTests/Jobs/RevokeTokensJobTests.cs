using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using TaxReader.Application.Interfaces;
using TaxReader.Application.Jobs;
using TaxReader.Domain.Entities;
using TaxReader.Domain.Enums;
using TaxReader.Infrastructure.Data;
using Xunit;

namespace TaxReader.UnitTests.Jobs;

public class RevokeTokensJobTests
{
    private static Mock<IAuditLogger> CreateAuditLoggerMock()
    {
        var mock = new Mock<IAuditLogger>();
        mock.Setup(a => a.RecordAsync(
                It.IsAny<AuditAction>(),
                It.IsAny<Guid?>(),
                It.IsAny<Guid?>(),
                It.IsAny<Dictionary<string, object?>>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        return mock;
    }

    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task HandleAsync_ExistingBalance_DeductsCredits()
    {
        var db = CreateDb();
        var userId = Guid.NewGuid();
        db.UserTokenBalances.Add(new UserTokenBalance
        {
            Id = Guid.NewGuid(), UserId = userId, UserKey = userId.ToString(),
            Balance = 100, UpdatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var job = new RevokeTokensJob(db, NullLogger<RevokeTokensJob>.Instance, CreateAuditLoggerMock().Object);
        await job.HandleAsync(userId, 50, CancellationToken.None);

        var balance = await db.UserTokenBalances.FirstAsync(b => b.UserId == userId);
        balance.Balance.Should().Be(50);
    }

    [Fact]
    public async Task HandleAsync_CanGoNegative()
    {
        // D-11: balance is allowed to go negative after refund
        var db = CreateDb();
        var userId = Guid.NewGuid();
        db.UserTokenBalances.Add(new UserTokenBalance
        {
            Id = Guid.NewGuid(), UserId = userId, UserKey = userId.ToString(),
            Balance = 20, UpdatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var job = new RevokeTokensJob(db, NullLogger<RevokeTokensJob>.Instance, CreateAuditLoggerMock().Object);
        await job.HandleAsync(userId, 50, CancellationToken.None);

        var balance = await db.UserTokenBalances.FirstAsync(b => b.UserId == userId);
        balance.Balance.Should().Be(-30);
    }

    [Fact]
    public async Task HandleAsync_UpdatesGrantedPaymentToRevoked()
    {
        var db = CreateDb();
        var userId = Guid.NewGuid();
        db.UserTokenBalances.Add(new UserTokenBalance
        {
            Id = Guid.NewGuid(), UserId = userId, UserKey = userId.ToString(),
            Balance = 200, UpdatedAt = DateTime.UtcNow
        });
        db.Payments.Add(new Payment
        {
            Id = Guid.NewGuid(), UserId = userId, StripeEventId = "evt_test",
            StripeSessionId = "cs_test", StripePaymentIntentId = "pi_test",
            CreditsGranted = 200, AmountCents = 1499,
            Currency = "eur", Status = PaymentStatus.Granted, CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var job = new RevokeTokensJob(db, NullLogger<RevokeTokensJob>.Instance, CreateAuditLoggerMock().Object);
        await job.HandleAsync(userId, 200, CancellationToken.None);

        var payment = await db.Payments.FirstAsync();
        payment.Status.Should().Be(PaymentStatus.Revoked);
        payment.RevokedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task HandleAsync_NoBalance_CreatesNegativeBalance()
    {
        var db = CreateDb();
        var userId = Guid.NewGuid();

        var job = new RevokeTokensJob(db, NullLogger<RevokeTokensJob>.Instance, CreateAuditLoggerMock().Object);
        await job.HandleAsync(userId, 50, CancellationToken.None);

        var balance = await db.UserTokenBalances.FirstAsync(b => b.UserId == userId);
        balance.Balance.Should().Be(-50);
    }

    [Fact]
    public async Task HandleAsync_SuccessfulRevoke_RecordsTokensRevokedAuditEntry()
    {
        // Arrange
        var db = CreateDb();
        var userId = Guid.NewGuid();
        db.UserTokenBalances.Add(new UserTokenBalance
        {
            Id = Guid.NewGuid(), UserId = userId, UserKey = userId.ToString(),
            Balance = 100, UpdatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var auditLoggerMock = CreateAuditLoggerMock();
        var job = new RevokeTokensJob(db, NullLogger<RevokeTokensJob>.Instance, auditLoggerMock.Object);

        // Act
        await job.HandleAsync(userId, 50, CancellationToken.None);

        // Assert — LEG-08
        auditLoggerMock.Verify(
            a => a.RecordAsync(
                AuditAction.TokensRevoked,
                null,
                userId,
                It.Is<Dictionary<string, object?>>(m => m.ContainsKey("credits")),
                It.IsAny<CancellationToken>()),
            Times.Once,
            "LEG-08: TokensRevoked audit entry must be recorded after successful revocation");
    }
}
