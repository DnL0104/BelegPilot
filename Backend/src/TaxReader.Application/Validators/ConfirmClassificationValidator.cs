using FluentValidation;
using TaxReader.Application.Commands;
using TaxReader.Domain.Enums;

namespace TaxReader.Application.Validators;

public class ConfirmClassificationValidator : AbstractValidator<ConfirmClassificationCommand>
{
    public ConfirmClassificationValidator()
    {
        RuleFor(x => x.ReceiptItemId)
            .NotEqual(Guid.Empty)
            .WithMessage("ReceiptItemId must not be empty.");

        RuleFor(x => x.Category)
            .IsInEnum()
            .WithMessage("Category must be a valid value.");

        RuleFor(x => x.Category)
            .NotEqual(Category.Unknown)
            .WithMessage("Category must not be Unknown when confirming.");
    }
}
