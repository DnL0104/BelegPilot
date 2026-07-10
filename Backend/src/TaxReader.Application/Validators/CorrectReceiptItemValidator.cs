using FluentValidation;
using TaxReader.Application.Commands;

namespace TaxReader.Application.Validators;

public class CorrectReceiptItemValidator : AbstractValidator<CorrectReceiptItemCommand>
{
    public CorrectReceiptItemValidator()
    {
        RuleFor(x => x.Description)
            .NotEmpty()
            .WithMessage("Beschreibung darf nicht leer sein.");

        RuleFor(x => x.UnitPrice)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Einzelpreis darf nicht negativ sein.");

        RuleFor(x => x.TotalPrice)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Gesamtpreis darf nicht negativ sein.");
    }
}
