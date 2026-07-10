namespace TaxReader.Application.DTOs;

public record ReceiptDto(
    Guid Id,
    Guid ReceiptFileId,
    string Vendor,
    DateOnly PurchaseDate,
    decimal SubTotal,
    decimal TaxAmount,
    decimal TotalAmount,
    string Currency,
    DateTime ParsedAt,
    int ItemCount,
    int SuggestedCount,
    int UnknownCount,
    int FailedCount,         // items with ClassificationStatus.Failed — distinct from UnknownCount
    bool HasSumMismatch,
    string ExtractionSource,
    string? RawExtractedText = null);
