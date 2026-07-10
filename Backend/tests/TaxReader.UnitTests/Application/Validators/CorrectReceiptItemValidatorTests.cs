using FluentAssertions;
using FluentValidation.TestHelper;
using TaxReader.Application.Commands;
using TaxReader.Application.Validators;

namespace TaxReader.UnitTests.Application.Validators;

public class CorrectReceiptItemValidatorTests
{
    private readonly CorrectReceiptItemValidator _validator = new();

    [Fact]
    public void Validate_ValidCommand_NoErrors()
    {
        var command = new CorrectReceiptItemCommand(Guid.NewGuid(), "Kaffee", 2.50m, 2.50m);

        var result = _validator.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_EmptyDescription_HasError()
    {
        var command = new CorrectReceiptItemCommand(Guid.NewGuid(), string.Empty, 2.50m, 2.50m);

        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Description);
    }

    [Fact]
    public void Validate_WhitespaceDescription_HasError()
    {
        var command = new CorrectReceiptItemCommand(Guid.NewGuid(), "   ", 2.50m, 2.50m);

        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Description);
    }

    [Fact]
    public void Validate_NegativeUnitPrice_HasError()
    {
        var command = new CorrectReceiptItemCommand(Guid.NewGuid(), "Kaffee", -1.00m, 2.50m);

        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.UnitPrice);
    }

    [Fact]
    public void Validate_NegativeTotalPrice_HasError()
    {
        var command = new CorrectReceiptItemCommand(Guid.NewGuid(), "Kaffee", 2.50m, -1.00m);

        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.TotalPrice);
    }
}
