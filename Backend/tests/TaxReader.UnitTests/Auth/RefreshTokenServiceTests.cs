using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using TaxReader.Application.Interfaces;
using TaxReader.Domain.Entities;
using TaxReader.Domain.Enums;
using TaxReader.Infrastructure.Configuration;
using TaxReader.Infrastructure.Data;
using TaxReader.Infrastructure.Services;

namespace TaxReader.UnitTests.Auth;

/// <summary>
/// AUTH-01: Happy-path coverage for IRefreshTokenService — Issue, ValidateAndRotate,
/// RevokeAllForUser. In-memory EF DB; one user seeded per test instance.
/// </summary>
public class RefreshTokenServiceTests : IDisposable
{
    private static readonly Guid TestUserId = Guid.Parse("aaaaaaaa-1111-2222-3333-444444444444");

    private readonly AppDbContext _dbContext;
    private readonly RefreshTokenService _service;

    public RefreshTokenServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new AppDbContext(options);

        _dbContext.Users.Add(new User
        {
            Id = TestUserId,
            Email = "rts@test.local",
            DisplayName = "Refresh Tester",
            PasswordHash = "x"
        });
        _dbContext.SaveChanges();

        var auditLoggerMock = new Mock<IAuditLogger>();
        auditLoggerMock
            .Setup(a => a.RecordAsync(
                It.IsAny<AuditAction>(),
                It.IsAny<Guid?>(),
                It.IsAny<Guid?>(),
                It.IsAny<Dictionary<string, object?>>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _service = new RefreshTokenService(
            _dbContext,
            Options.Create(new RefreshTokenOptions { HashKey = Convert.ToBase64String(new byte[32]) }),
            Options.Create(new JwtOptions
            {
                Secret = "test-secret-test-secret-test-secret-12",
                Issuer = "test",
                Audience = "test",
                RefreshTokenExpirationDays = 30
            }),
            NullLogger<RefreshTokenService>.Instance,
            auditLoggerMock.Object);
    }

    [Fact]
    public async Task IssueAsync_ValidUser_StoresHashedRowAndReturnsPlaintext()
    {
        var plaintext = await _service.IssueAsync(TestUserId, "ua-string", "127.0.0.1");

        plaintext.Should().NotBeNullOrEmpty();

        var rows = await _dbContext.RefreshTokens
            .Where(t => t.UserId == TestUserId)
            .ToListAsync();

        rows.Should().HaveCount(1);
        var row = rows.Single();

        row.TokenHash.Should().NotBeNullOrEmpty();
        row.TokenHash.Length.Should().Be(44, "Base64-encoded 32-byte HMAC-SHA256 is exactly 44 chars");
        row.RevokedAt.Should().BeNull();
        row.ExpiresAt.Should().BeAfter(DateTime.UtcNow);
        row.UserAgent.Should().Be("ua-string");
        row.IpAddress.Should().NotBeNull();
        row.IpAddress!.ToString().Should().Be("127.0.0.1");
    }

    [Fact]
    public async Task ValidateAndRotateAsync_ValidToken_ReturnsNewPlaintextAndRevokesOld()
    {
        var original = await _service.IssueAsync(TestUserId, "ua-A", "10.0.0.1");

        var rotation = await _service.ValidateAndRotateAsync(original, "ua-B", "10.0.0.2");

        rotation.IsSuccess.Should().BeTrue();
        rotation.Value.UserId.Should().Be(TestUserId);
        rotation.Value.PlaintextToken.Should().NotBeNullOrEmpty();
        rotation.Value.PlaintextToken.Should().NotBe(original,
            "rotation must mint a brand-new plaintext, not echo the old one");

        var rows = await _dbContext.RefreshTokens
            .Where(t => t.UserId == TestUserId)
            .OrderBy(t => t.CreatedAt)
            .ToListAsync();

        rows.Should().HaveCount(2, "rotation inserts a new row, doesn't replace");

        var oldRow = rows[0];
        var newRow = rows[1];

        oldRow.RevokedAt.Should().NotBeNull("the rotated row must be marked revoked");
        oldRow.LastUsedAt.Should().NotBeNull("LastUsedAt must be stamped on rotation");
        oldRow.ReplacedByTokenId.Should().Be(newRow.Id, "the chain pointer must link old → new");

        newRow.RevokedAt.Should().BeNull("the new row is the active one");
        newRow.UserAgent.Should().Be("ua-B", "the new row captures the rotation's UA");
    }

    [Fact]
    public async Task ValidateAndRotateAsync_NonExistentToken_ReturnsFailure()
    {
        var randomPlaintext = Convert.ToBase64String(new byte[64]);

        var rotation = await _service.ValidateAndRotateAsync(randomPlaintext, null, null);

        rotation.IsFailure.Should().BeTrue();
        rotation.Error.Should().Contain("Ungültiges oder abgelaufenes");
    }

    [Fact]
    public async Task ValidateAndRotateAsync_ExpiredToken_ReturnsFailure()
    {
        // Issue then back-date manually — the in-memory DB lets us mutate ExpiresAt directly.
        var plaintext = await _service.IssueAsync(TestUserId, null, null);
        var row = await _dbContext.RefreshTokens.SingleAsync(t => t.UserId == TestUserId);
        row.ExpiresAt = DateTime.UtcNow.AddMinutes(-5);
        await _dbContext.SaveChangesAsync();

        var rotation = await _service.ValidateAndRotateAsync(plaintext, null, null);

        rotation.IsFailure.Should().BeTrue();
        rotation.Error.Should().Contain("Ungültiges oder abgelaufenes");
    }

    [Fact]
    public async Task RevokeAllForUserAsync_MultipleActiveTokens_RevokesAll()
    {
        await _service.IssueAsync(TestUserId, "device-A", null);
        await _service.IssueAsync(TestUserId, "device-B", null);
        await _service.IssueAsync(TestUserId, "device-C", null);

        var affected = await _service.RevokeAllForUserAsync(TestUserId);

        affected.Should().Be(3);

        var stillActive = await _dbContext.RefreshTokens
            .Where(t => t.UserId == TestUserId && t.RevokedAt == null)
            .CountAsync();

        stillActive.Should().Be(0, "RevokeAllForUserAsync must flip every non-revoked row");
    }

    public void Dispose() => _dbContext.Dispose();
}
