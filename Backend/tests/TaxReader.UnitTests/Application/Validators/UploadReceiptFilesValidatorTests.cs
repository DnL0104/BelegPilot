using FluentAssertions;
using FluentValidation.TestHelper;
using TaxReader.Application.Commands;
using TaxReader.Application.Validators;

namespace TaxReader.UnitTests.Application.Validators;

public class UploadReceiptFilesValidatorTests
{
    private readonly UploadReceiptFilesValidator _validator = new();

    [Fact]
    public void Validate_NoFiles_HasError()
    {
        var command = new UploadReceiptFilesCommand([], null, null, null);

        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Files);
    }

    [Fact]
    public void Validate_ValidPdfFile_NoErrors()
    {
        var files = new List<FileUploadItem>
        {
            new("receipt.pdf", 1024, Stream.Null)
        };
        var command = new UploadReceiptFilesCommand(files, null, null, null);

        var result = _validator.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("photo.jpg")]
    [InlineData("photo.jpeg")]
    [InlineData("photo.PNG")]
    [InlineData("photo.webp")]
    public void Validate_SupportedImageFile_NoErrors(string fileName)
    {
        var files = new List<FileUploadItem> { new(fileName, 1024, Stream.Null) };
        var command = new UploadReceiptFilesCommand(files, null, null, null);

        var result = _validator.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_UnsupportedFileType_HasError()
    {
        var files = new List<FileUploadItem>
        {
            new("document.docx", 1024, Stream.Null)
        };
        var command = new UploadReceiptFilesCommand(files, null, null, null);

        var result = _validator.TestValidate(command);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_FileTooLarge_HasError()
    {
        var files = new List<FileUploadItem>
        {
            new("receipt.pdf", 11 * 1024 * 1024, Stream.Null)
        };
        var command = new UploadReceiptFilesCommand(files, null, null, null);

        var result = _validator.TestValidate(command);

        result.IsValid.Should().BeFalse();
    }
}
