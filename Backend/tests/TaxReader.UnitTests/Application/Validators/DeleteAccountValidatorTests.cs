namespace TaxReader.UnitTests.Application.Validators;

/// <summary>
/// AUTH-02: Stub scaffolding for DeleteAccountValidator. Validator + tests
/// are activated in Tasks 2 and 5 respectively.
/// </summary>
public class DeleteAccountValidatorTests
{
    [Fact(Skip = "Pending implementation in Task 2-4")]
    public void Validate_EmptyPassword_FailsWithGermanMessage()
    {
        // Will: instantiate DeleteAccountValidator; TestValidate(new DeleteAccountRequest(""));
        // assert ShouldHaveValidationErrorFor(x => x.Password).WithErrorMessage("Passwort ist erforderlich.")
    }

    [Fact(Skip = "Pending implementation in Task 2-4")]
    public void Validate_NonEmptyPassword_Passes()
    {
        // Will: TestValidate(new DeleteAccountRequest("anypassword"));
        // assert ShouldNotHaveValidationErrorFor(x => x.Password)
    }
}
