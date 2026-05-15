using FluentValidation;
using TaxReader.Application.DTOs;

namespace TaxReader.Application.Validators;

public class DeleteAccountValidator : AbstractValidator<DeleteAccountRequest>
{
    public DeleteAccountValidator()
    {
        RuleFor(x => x.Password)
            .NotEmpty()
            .WithMessage("Passwort ist erforderlich.");
    }
}
