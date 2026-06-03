using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using TaxReader.Application.Interfaces;
using TaxReader.Application.Jobs;
using TaxReader.Domain.Entities;
using TaxReader.Domain.Enums;
using TaxReader.Infrastructure.Data;

namespace TaxReader.UnitTests.Jobs;

/// <summary>
/// Behavioural tests for GrantTokensJob (PAY-01).
/// Verifies token crediting, balance creation for new users, Payment status transition,
/// and LEG-08 audit logging.
/// </summary>
public class GrantTokensJobTests : IDisposable
{
    private static readonly Guid TestUserId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
    private readonly AppDbContext _dbContext;
    private readonly Mock<IAuditLogger> _auditLoggerMock;
    private readonly GrantTokensJob _job;

    public GrantTokensJobTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new AppDbContext(options);

        // Seed user so FK constraints pass
        _dbContext.Users.Add(new User
        {
            Id = TestUserId,
            Email = "test@example.com",
            DisplayName = "Test",
            PasswordHash = "hash"
        });
        _dbContext.SaveChanges();

        _auditLoggerMock = new Mock<IAuditLogger>();
        _auditLoggerMock
            .Setup(a => a.RecordAsync(
                It.IsAny<AuditAction>(),
                It.IsAny<Guid?>(),
                It.IsAny<Guid?>(),
                It.IsAny<Dictionary<string, object?>>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _job = new GrantTokensJob(_dbContext, Mock.Of<ILogger<GrantTokensJob>>(), _auditLoggerMock.Object);
    }

    [Fact]
    public async Task HandleAsync_NewUser_CreatesBalanceAndCreditsTokens()
    {
        // Arrange — no UserTokenBalance exists
        const int credits = 50;

        // Act
        await _job.HandleAsync(TestUserId, credits, CancellationToken.None);

        // Assert
        var balance = await _dbContext.UserTokenBalances
            .FirstOrDefaultAsync(b => b.UserId == TestUserId);
        balance.Should().NotBeNull();
        balance!.Balance.Should().Be(credits);
    }

    [Fact]
    public async Task HandleAsync_ExistingUser_AddsToBalance()
    {
        // Arrange — existing balance = 10
        _dbContext.UserTokenBalances.Add(new UserTokenBalance
        {
            Id = Guid.NewGuid(),
            UserId = TestUserId,
            UserKey = TestUserId.ToString(),
            Balance = 10,
            UpdatedAt = DateTime.UtcNow
        });
        await _dbContext.SaveChangesAsync();

        // Act
        await _job.HandleAsync(TestUserId, 50, CancellationToken.None);

        // Assert
        var balance = await _dbContext.UserTokenBalances
            .FirstAsync(b => b.UserId == TestUserId);
        balance.Balance.Should().Be(60);
    }

    [Fact]
    public async Task HandleAsync_UpdatesPendingPaymentToGranted()
    {
        // Arrange — insert a Pending payment for this user
        const int credits = 200;
        _dbContext.Payments.Add(new Payment
        {
            Id = Guid.NewGuid(),
            UserId = TestUserId,
            StripeEventId = "evt_test_001",
            StripeSessionId = "cs_test_001",
            CreditsGranted = credits,
            AmountCents = 1499,
            Currency = "eur",
            Status = PaymentStatus.Pending,
            CreatedAt = DateTime.UtcNow
        });
        await _dbContext.SaveChangesAsync();

        // Act
        await _job.HandleAsync(TestUserId, credits, CancellationToken.None);

        // Assert
        var payment = await _dbContext.Payments
            .FirstAsync(p => p.UserId == TestUserId);
        payment.Status.Should().Be(PaymentStatus.Granted);
    }

    [Fact]
    public async Task HandleAsync_SuccessfulGrant_RecordsTokensGrantedAuditEntry()
    {
        // Arrange
        const int credits = 50;

        // Act
        await _job.HandleAsync(TestUserId, credits, CancellationToken.None);

        // Assert — LEG-08
        _auditLoggerMock.Verify(
            a => a.RecordAsync(
                AuditAction.TokensGranted,
                null,
                TestUserId,
                It.Is<Dictionary<string, object?>>(m => m.ContainsKey("credits")),
                It.IsAny<CancellationToken>()),
            Times.Once,
            "LEG-08: TokensGranted audit entry must be recorded after successful grant");
    }

    public void Dispose() => _dbContext.Dispose();
}
