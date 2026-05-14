namespace TaxReader.UnitTests.Auth;

/// <summary>
/// AUTH-01: RefreshTokenService happy-path behaviour — Issue, ValidateAndRotate,
/// RevokeAllForUser. Wave 0 stubs — un-skipped by Task 5.
/// </summary>
public class RefreshTokenServiceTests
{
    [Fact(Skip = "Pending implementation in Wave 1 tasks 2-4")]
    public void IssueAsync_ValidUser_StoresHashedRowAndReturnsPlaintext()
    {
        // Will assert one row exists with non-empty hash + null RevokedAt + future ExpiresAt + captured UA/IP.
    }

    [Fact(Skip = "Pending implementation in Wave 1 tasks 2-4")]
    public void ValidateAndRotateAsync_ValidToken_ReturnsNewPlaintextAndRevokesOld()
    {
        // Will assert old row.RevokedAt != null, new row exists, ReplacedByTokenId chain wired.
    }

    [Fact(Skip = "Pending implementation in Wave 1 tasks 2-4")]
    public void ValidateAndRotateAsync_NonExistentToken_ReturnsFailure()
    {
        // Will assert German generic 401 failure for unknown plaintext (D-04 silent).
    }

    [Fact(Skip = "Pending implementation in Wave 1 tasks 2-4")]
    public void ValidateAndRotateAsync_ExpiredToken_ReturnsFailure()
    {
        // Will assert same German 401 failure for expired-not-yet-revoked row.
    }

    [Fact(Skip = "Pending implementation in Wave 1 tasks 2-4")]
    public void RevokeAllForUserAsync_MultipleActiveTokens_RevokesAll()
    {
        // Will assert all non-revoked rows for the user are flipped in one call.
    }
}
