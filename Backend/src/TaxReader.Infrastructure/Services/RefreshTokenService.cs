using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sentry;
using Serilog.Context;
using TaxReader.Application.Interfaces;
using TaxReader.Domain.Common;
using TaxReader.Domain.Entities;
using TaxReader.Infrastructure.Configuration;

namespace TaxReader.Infrastructure.Services;

/// <summary>
/// Hashes refresh tokens with an HMAC-SHA256 pepper (D-01) so a DB-only leak
/// cannot forge a valid token. Rotates on every successful refresh (D-02 chain
/// via ReplacedByTokenId) and revokes ALL of the user's tokens when a revoked
/// token is replayed (D-03 — surfaced silently per D-04 to avoid leaking
/// detection signal).
/// </summary>
public class RefreshTokenService(
    IAppDbContext dbContext,
    IOptions<RefreshTokenOptions> refreshTokenOptions,
    IOptions<JwtOptions> jwtOptions,
    ILogger<RefreshTokenService> logger) : IRefreshTokenService
{
    private const string GenericFailure = "Ungültiges oder abgelaufenes Refresh-Token.";

    private readonly byte[] _pepper = Convert.FromBase64String(refreshTokenOptions.Value.HashKey);
    private readonly JwtOptions _jwt = jwtOptions.Value;

    public async Task<string> IssueAsync(
        Guid userId,
        string? userAgent,
        string? ipAddress,
        CancellationToken cancellationToken = default)
    {
        var (plaintext, hash) = GenerateAndHash();

        var row = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = hash,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(_jwt.RefreshTokenExpirationDays),
            UserAgent = userAgent?.Length > 500 ? userAgent[..500] : userAgent,
            IpAddress = ParseIpOrNull(ipAddress)
        };

        dbContext.RefreshTokens.Add(row);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Refresh token issued for {UserId}", userId);
        return plaintext;
    }

    public async Task<Result<(Guid UserId, string PlaintextToken)>> ValidateAndRotateAsync(
        string plaintextToken,
        string? userAgent,
        string? ipAddress,
        CancellationToken cancellationToken = default)
    {
        var tokenHash = ComputeHash(plaintextToken);

        var existing = await dbContext.RefreshTokens
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);

        if (existing is null)
        {
            // Never issued or rotated long ago — same generic message as replay (D-04).
            return Result<(Guid, string)>.Failure(GenericFailure);
        }

        using (LogContext.PushProperty("UserId", existing.UserId))
        {
            if (existing.ExpiresAt < DateTime.UtcNow)
            {
                logger.LogInformation("Refresh token expired");
                return Result<(Guid, string)>.Failure(GenericFailure);
            }

            // REPLAY DETECTION (D-03): revoked row was presented — revoke ALL the user's tokens.
            if (existing.RevokedAt is not null)
            {
                logger.LogWarning("Refresh token replay detected");
                SentrySdk.CaptureMessage(
                    "Refresh token replay detected",
                    scope => scope.SetExtra("user.id_hash", HashUserId(existing.UserId)),
                    SentryLevel.Warning);

                await RevokeAllForUserAsync(existing.UserId, cancellationToken);
                return Result<(Guid, string)>.Failure(GenericFailure);
            }

            // Happy path — rotate.
            var (newPlaintext, newHash) = GenerateAndHash();
            var newRow = new RefreshToken
            {
                Id = Guid.NewGuid(),
                UserId = existing.UserId,
                TokenHash = newHash,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddDays(_jwt.RefreshTokenExpirationDays),
                UserAgent = userAgent?.Length > 500 ? userAgent[..500] : userAgent,
                IpAddress = ParseIpOrNull(ipAddress)
            };
            dbContext.RefreshTokens.Add(newRow);

            existing.RevokedAt = DateTime.UtcNow;
            existing.LastUsedAt = DateTime.UtcNow;
            existing.ReplacedByTokenId = newRow.Id;

            await dbContext.SaveChangesAsync(cancellationToken);

            logger.LogInformation("Refresh token rotated successfully");
            return Result<(Guid, string)>.Success((existing.UserId, newPlaintext));
        }
    }

    public async Task<int> RevokeAllForUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null)
            .ExecuteUpdateAsync(
                s => s.SetProperty(t => t.RevokedAt, DateTime.UtcNow),
                cancellationToken);
    }

    private string ComputeHash(string plaintextToken)
    {
        var plaintextBytes = Encoding.UTF8.GetBytes(plaintextToken);
        var hash = HMACSHA256.HashData(_pepper, plaintextBytes);
        return Convert.ToBase64String(hash);
    }

    private (string Plaintext, string Hash) GenerateAndHash()
    {
        var plaintext = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        var hash = ComputeHash(plaintext);
        return (plaintext, hash);
    }

    private static string HashUserId(Guid userId)
    {
        // Phase 1 D-14 PII allow-list — `user.id_hash` is permitted as an Extra.
        // SHA-256 (not HMAC) suffices here: this is a correlation tag, not a secret.
        var bytes = SHA256.HashData(userId.ToByteArray());
        return Convert.ToHexString(bytes)[..16].ToLowerInvariant();
    }

    private static IPAddress? ParseIpOrNull(string? ip)
        => IPAddress.TryParse(ip, out var addr) ? addr : null;
}
