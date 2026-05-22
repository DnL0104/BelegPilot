using FluentAssertions;
using FluentValidation.TestHelper;
using TaxReader.Application.Commands;
using TaxReader.Application.Validators;
using TaxReader.Domain.Enums;

namespace TaxReader.UnitTests.Application.Validators;

public class ConfirmClassificationValidatorTests
{
    private readonly ConfirmClassificationValidator _validator = new();

    [Fact]
    public void Validate_ValidCommand_NoErrors()
    {
        var command = new ConfirmClassificationCommand(
            Guid.NewGuid(),
            Category.WerbungskostenBueromaterial);

        var result = _validator.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_EmptyReceiptItemId_HasError()
    {
        var command = new ConfirmClassificationCommand(
            Guid.Empty,
            Category.WerbungskostenBueromaterial);

        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.ReceiptItemId);
    }

    [Fact]
    public void Validate_UnknownCategory_HasError()
    {
        var command = new ConfirmClassificationCommand(
            Guid.NewGuid(),
            Category.Unbekannt);

        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Category);
    }
}
