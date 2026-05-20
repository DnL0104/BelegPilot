namespace TaxReader.Application.DTOs;

public record ReceiptFileDto(
    Guid Id,
    string OriginalFileName,
    long FileSize,
    string? SourceHint,
    int? YearHint,
    string? UploadedBy,
    DateTime UploadedAt,
    string Status);
