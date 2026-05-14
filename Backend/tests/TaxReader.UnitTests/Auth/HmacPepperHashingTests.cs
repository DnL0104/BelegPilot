namespace TaxReader.UnitTests.Auth;

/// <summary>
/// AUTH-01 D-01: HMAC-SHA256 pepper hashing. Tests verify determinism and
/// key-sensitivity. Wave 0 stubs — un-skipped by Task 5 once RefreshTokenService
/// is wired (Tasks 2-3).
/// </summary>
public class HmacPepperHashingTests
{
    [Fact(Skip = "Pending implementation in Wave 1 tasks 2-4")]
    public void ComputeHash_SameInputSamePepper_ReturnsIdenticalHash()
    {
        // Will exercise RefreshTokenService's HMAC pepper path via IssueAsync + observed TokenHash.
    }

    [Fact(Skip = "Pending implementation in Wave 1 tasks 2-4")]
    public void ComputeHash_SameInputDifferentPeppers_ReturnsDifferentHashes()
    {
        // Will verify the pepper is actually mixed into the hash (key-sensitivity).
    }

    [Fact(Skip = "Pending implementation in Wave 1 tasks 2-4")]
    public void ComputeHash_DifferentInputs_ReturnsDifferentHashes()
    {
        // Will verify SHA256 collision resistance on two distinct plaintexts.
    }
}
