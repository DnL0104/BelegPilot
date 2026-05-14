using TaxReader.Domain.Common;

namespace TaxReader.Application.Interfaces;

/// <summary>
/// Issues, validates, rotates, and revokes refresh tokens. Tokens are stored as
/// HMAC-SHA256-keyed hashes (D-01 pepper) so a DB-only leak does not allow forgery.
/// Rotation produces a new plaintext on every successful validation and revokes
/// the old row (D-02 chain via ReplacedByTokenId). Presenting a revoked token
/// triggers RevokeAllForUserAsync as replay defence (D-03), surfaced as a generic
/// 401 to avoid leaking detection signal (D-04).
/// </summary>
public interface IRefreshTokenService
{
    /// <summary>
    /// Generate a new plaintext refresh token, persist its HMAC hash, and return
    /// the plaintext to the caller for embedding in an AuthResponse.
    /// </summary>
    Task<string> IssueAsync(
        Guid userId,
        string? userAgent,
        string? ipAddress,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Hash the incoming plaintext, look up the matching row, and either rotate
    /// (happy path: returns new plaintext + the owning user id) or fail
    /// silently (not-found, expired, revoked-replay all return the same German
    /// error message per D-04). Replay revokes ALL of the user's tokens.
    /// </summary>
    Task<Result<(Guid UserId, string PlaintextToken)>> ValidateAndRotateAsync(
        string plaintextToken,
        string? userAgent,
        string? ipAddress,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Bulk-revoke every non-revoked refresh token for the given user.
    /// Returns the number of rows updated. Used both by replay detection and
    /// (later) by AUTH-02 account deletion.
    /// </summary>
    Task<int> RevokeAllForUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}
