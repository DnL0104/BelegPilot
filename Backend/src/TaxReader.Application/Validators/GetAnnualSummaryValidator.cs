using FluentValidation;
using TaxReader.Application.Queries;

namespace TaxReader.Application.Validators;

public class GetAnnualSummaryValidator : AbstractValidator<GetAnnualSummaryQuery>
{
    public GetAnnualSummaryValidator()
    {
        RuleFor(x => x.Year)
            .GreaterThan(2000)
            .WithMessage("Year must be greater than 2000.")
            .LessThanOrEqualTo(DateTime.UtcNow.Year + 1)
            .WithMessage("Year must not be more than one year in the future.");
    }
}
