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
using TaxReader.IntegrationTests.Fixtures;

namespace TaxReader.IntegrationTests;

/// <summary>
/// QA-01 T-07-01: proves refresh-token rotation and replay detection against real Postgres,
/// anchored by the token_hash UNIQUE index. The in-memory provider does NOT enforce this
/// constraint, giving false confidence. Only real Postgres proves the security invariant.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public sealed class RefreshTokenRotationReplayTests(PostgresContainerFixture fixture) : IAsyncLifetime
{
    private static readonly Guid TestUserId = Guid.NewGuid();

    public async Task InitializeAsync()
    {
        await fixture.ResetAsync();

        // Seed the test user into the real container once per test (Respawn wipes data).
        await using var ctx = CreateContext();
        ctx.Users.Add(new User
        {
            Id = TestUserId,
            Email = $"rotation-{TestUserId:N}@test.local",
            DisplayName = "Rotation Tester",
            PasswordHash = "x"
        });
        await ctx.SaveChangesAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private AppDbContext CreateContext() => new(
        new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(fixture.ConnectionString)
            .UseSnakeCaseNamingConvention()
            .Options);

    private RefreshTokenService CreateService()
    {
        var auditLoggerMock = new Mock<IAuditLogger>();
        auditLoggerMock
            .Setup(a => a.RecordAsync(
                It.IsAny<AuditAction>(),
                It.IsAny<Guid?>(),
                It.IsAny<Guid?>(),
                It.IsAny<Dictionary<string, object?>>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var ctx = CreateContext();
        return new RefreshTokenService(
            ctx,
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
    public async Task Rotation_NewToken_RevokesOldRowAndChains()
    {
        // Arrange — issue the original token.
        var service = CreateService();
        var originalPlaintext = await service.IssueAsync(TestUserId, "device-A", "10.0.0.1");

        // Act — rotate.
        var rotationService = CreateService();
        var rotation = await rotationService.ValidateAndRotateAsync(originalPlaintext, "device-B", "10.0.0.2");

        // Assert rotation succeeded and produced a new token.
        rotation.IsSuccess.Should().BeTrue();
        var newPlaintext = rotation.Value.PlaintextToken;
        newPlaintext.Should().NotBeNullOrEmpty();
        newPlaintext.Should().NotBe(originalPlaintext, "rotation must mint a brand-new plaintext");
        rotation.Value.UserId.Should().Be(TestUserId);

        // Verify DB state with a fresh context.
        await using var ctx = CreateContext();
        var rows = await ctx.RefreshTokens
            .Where(t => t.UserId == TestUserId)
            .OrderBy(t => t.CreatedAt)
            .ToListAsync();

        rows.Should().HaveCount(2, "rotation inserts a new row — does not replace the old one");

        var oldRow = rows[0];
        var newRow = rows[1];

        oldRow.RevokedAt.Should().NotBeNull("the rotated row must be marked revoked");
        oldRow.ReplacedByTokenId.Should().Be(newRow.Id, "the chain pointer must link old → new");
        newRow.RevokedAt.Should().BeNull("the newly-minted row is active");
    }

    [Fact]
    public async Task Replay_RotatedToken_RevokesAllUserTokens()
    {
        // Arrange — issue original token, rotate it (so original is now revoked).
        var service = CreateService();
        var originalPlaintext = await service.IssueAsync(TestUserId, "device-A", null);

        var rotationService = CreateService();
        var rotation = await rotationService.ValidateAndRotateAsync(originalPlaintext, "device-B", null);
        rotation.IsSuccess.Should().BeTrue("initial rotation must succeed before the replay test");

        // Verify old row is revoked before replay attempt.
        await using (var ctx = CreateContext())
        {
            var active = await ctx.RefreshTokens
                .Where(t => t.UserId == TestUserId && t.RevokedAt == null)
                .CountAsync();
            active.Should().Be(1, "after one rotation, exactly the new token is active");
        }

        // Act — replay the original (now-revoked) token; this is an attacker replaying a stolen token.
        var replayService = CreateService();
        var replayResult = await replayService.ValidateAndRotateAsync(originalPlaintext, "attacker", null);

        // Assert replay returns failure (same generic error — D-04 no information disclosure).
        replayResult.IsFailure.Should().BeTrue("replay of a rotated token must fail");
        replayResult.Error.Should().Be("Ungültiges oder abgelaufenes Refresh-Token.",
            "D-04: replay surfaces the same generic message as not-found/expired");

        // Assert all of the user's tokens are now revoked (D-03 replay-revoke-all).
        await using var verifyCtx = CreateContext();
        var stillActive = await verifyCtx.RefreshTokens
            .Where(t => t.UserId == TestUserId && t.RevokedAt == null)
            .CountAsync();
        stillActive.Should().Be(0,
            "replay detection must revoke ALL of the user's tokens — real token_hash UNIQUE anchors this");
    }
}
