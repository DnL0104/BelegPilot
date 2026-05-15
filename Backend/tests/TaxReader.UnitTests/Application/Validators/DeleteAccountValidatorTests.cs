using FluentValidation.TestHelper;
using TaxReader.Application.DTOs;
using TaxReader.Application.Validators;

namespace TaxReader.UnitTests.Application.Validators;

/// <summary>
/// AUTH-02: DeleteAccountValidator pins the password-not-empty rule
/// + the German validation message ("Passwort ist erforderlich.").
/// </summary>
public class DeleteAccountValidatorTests
{
    private readonly DeleteAccountValidator _validator = new();

    [Fact]
    public void Validate_EmptyPassword_FailsWithGermanMessage()
    {
        var result = _validator.TestValidate(new DeleteAccountRequest(""));

        result.ShouldHaveValidationErrorFor(x => x.Password)
            .WithErrorMessage("Passwort ist erforderlich.");
    }

    [Fact]
    public void Validate_NonEmptyPassword_Passes()
    {
        var result = _validator.TestValidate(new DeleteAccountRequest("anypassword"));

        result.ShouldNotHaveValidationErrorFor(x => x.Password);
    }
}
