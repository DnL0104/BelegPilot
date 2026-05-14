using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TaxReader.Domain.Entities;
using TaxReader.Infrastructure.Configuration;
using TaxReader.Infrastructure.Data;
using TaxReader.Infrastructure.Services;

namespace TaxReader.UnitTests.Auth;

/// <summary>
/// AUTH-01 D-03 + D-04: Presenting a revoked refresh token triggers
/// RevokeAllForUserAsync (all of the user's active tokens flip) and surfaces
/// the SAME German error as not-found / expired — no information disclosure.
/// </summary>
public class ReplayDetectionTests : IDisposable
{
    private static readonly Guid TestUserId = Guid.Parse("bbbbbbbb-1111-2222-3333-444444444444");

    private readonly AppDbContext _dbContext;
    private readonly RefreshTokenService _service;

    public ReplayDetectionTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new AppDbContext(options);

        _dbContext.Users.Add(new User
        {
            Id = TestUserId,
            Email = "replay@test.local",
            DisplayName = "Replay Tester",
            PasswordHash = "x"
        });
        _dbContext.SaveChanges();

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
            NullLogger<RefreshTokenService>.Instance);
    }

    [Fact]
    public async Task ValidateAndRotateAsync_RevokedTokenPresented_RevokesAllUserTokens()
    {
        // Issue two active tokens (e.g. phone + laptop).
        var plaintextA = await _service.IssueAsync(TestUserId, "phone", null);
        var plaintextB = await _service.IssueAsync(TestUserId, "laptop", null);

        // Simulate prior rotation of A — mark the row as revoked.
        var rowA = await _dbContext.RefreshTokens.FirstAsync(t => t.UserAgent == "phone");
        rowA.RevokedAt = DateTime.UtcNow.AddMinutes(-1);
        await _dbContext.SaveChangesAsync();

        // Attacker replays A's plaintext.
        var rotation = await _service.ValidateAndRotateAsync(plaintextA, null, null);

        rotation.IsFailure.Should().BeTrue("replay must fail");

        var stillActive = await _dbContext.RefreshTokens
            .Where(t => t.UserId == TestUserId && t.RevokedAt == null)
            .CountAsync();

        stillActive.Should().Be(0,
            "D-03: every non-revoked token for the user must be revoked when a replay is detected");
    }

    [Fact]
    public async Task ValidateAndRotateAsync_RevokedTokenPresented_ReturnsGenericFailure()
    {
        var plaintext = await _service.IssueAsync(TestUserId, null, null);
        var row = await _dbContext.RefreshTokens.SingleAsync(t => t.UserId == TestUserId);
        row.RevokedAt = DateTime.UtcNow.AddMinutes(-1);
        await _dbContext.SaveChangesAsync();

        var rotation = await _service.ValidateAndRotateAsync(plaintext, null, null);

        rotation.IsFailure.Should().BeTrue();
        rotation.Error.Should().Be("Ungültiges oder abgelaufenes Refresh-Token.",
            "D-04: replay must surface the SAME error as not-found and expired to avoid information disclosure");
    }

    public void Dispose() => _dbContext.Dispose();
}
