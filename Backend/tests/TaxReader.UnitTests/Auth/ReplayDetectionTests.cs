namespace TaxReader.UnitTests.Auth;

/// <summary>
/// AUTH-01 D-03: Replay detection. Presenting a revoked refresh token revokes
/// ALL of the user's active tokens and emits a Sentry warning. Wave 0 stubs —
/// un-skipped by Task 5.
/// </summary>
public class ReplayDetectionTests
{
    [Fact(Skip = "Pending implementation in Wave 1 tasks 2-4")]
    public void ValidateAndRotateAsync_RevokedTokenPresented_RevokesAllUserTokens()
    {
        // Will seed two active tokens, revoke one, present its plaintext;
        // assert every row for the user has RevokedAt != null afterwards.
    }

    [Fact(Skip = "Pending implementation in Wave 1 tasks 2-4")]
    public void ValidateAndRotateAsync_RevokedTokenPresented_ReturnsGenericFailure()
    {
        // Will assert result.Error == "Ungültiges oder abgelaufenes Refresh-Token."
        // (D-04 — same message as not-found so no information disclosure).
    }
}
