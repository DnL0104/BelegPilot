using FluentValidation;
using TaxReader.Application.Commands;

namespace TaxReader.Application.Validators;

public class UploadReceiptFilesValidator : AbstractValidator<UploadReceiptFilesCommand>
{
    private static readonly HashSet<string> SupportedExtensions =
        [".pdf", ".jpg", ".jpeg", ".png", ".webp"];

    private static bool IsSupportedFileType(string name) =>
        SupportedExtensions.Contains(
            Path.GetExtension(name).ToLowerInvariant());

    public UploadReceiptFilesValidator()
    {
        RuleFor(x => x.Files)
            .NotEmpty()
            .WithMessage("At least one file must be provided.");

        RuleForEach(x => x.Files).ChildRules(file =>
        {
            file.RuleFor(f => f.FileName)
                .Must(IsSupportedFileType)
                .WithMessage("Only PDF, JPG, PNG and WEBP files are supported.");

            file.RuleFor(f => f.Length)
                .LessThanOrEqualTo(10 * 1024 * 1024)
                .WithMessage("File size must not exceed 10 MB.");
        });
    }
}
