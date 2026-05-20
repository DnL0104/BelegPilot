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
    string? RawExtractedText = null);
