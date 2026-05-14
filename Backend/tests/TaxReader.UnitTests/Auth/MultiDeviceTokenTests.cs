namespace TaxReader.UnitTests.Auth;

/// <summary>
/// AUTH-01: Two active refresh tokens for the same user (phone + laptop)
/// must both validate independently and rotate without affecting the other.
/// Wave 0 stub — un-skipped by Task 5.
/// </summary>
public class MultiDeviceTokenTests
{
    [Fact(Skip = "Pending implementation in Wave 1 tasks 2-4")]
    public void TwoActiveTokens_BothValidate()
    {
        // Will issue token A then token B for same user, rotate A then rotate B,
        // assert both rotations succeed and neither revokes the other device's chain.
    }
}
