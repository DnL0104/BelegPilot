using FluentValidation.TestHelper;
using TaxReader.Application.Queries;
using TaxReader.Application.Validators;

namespace TaxReader.UnitTests.Application.Validators;

public class GetCategoryTotalsValidatorTests
{
    private readonly GetCategoryTotalsValidator _validator = new();

    [Fact]
    public void Validate_ValidYear_NoErrors()
    {
        var query = new GetCategoryTotalsQuery(2025);

        var result = _validator.TestValidate(query);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_YearTooLow_HasError()
    {
        var query = new GetCategoryTotalsQuery(1999);

        var result = _validator.TestValidate(query);

        result.ShouldHaveValidationErrorFor(x => x.Year);
    }

    [Fact]
    public void Validate_YearTooHigh_HasError()
    {
        var query = new GetCategoryTotalsQuery(DateTime.UtcNow.Year + 5);

        var result = _validator.TestValidate(query);

        result.ShouldHaveValidationErrorFor(x => x.Year);
    }
}
