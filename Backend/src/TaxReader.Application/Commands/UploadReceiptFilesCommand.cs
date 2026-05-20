namespace TaxReader.Application.Commands;

public record FileUploadItem(string FileName, long Length, Stream Stream);

public record UploadReceiptFilesCommand(
    IReadOnlyList<FileUploadItem> Files,
    string? SourceHint,
    int? YearHint,
    string? UploadedBy);
