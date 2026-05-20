namespace TaxReader.Application.DTOs;

public record PendingSuggestionDto(
    Guid ItemId,
    Guid ReceiptId,
    string Description,
    decimal TotalPrice,
    string Vendor,
    DateOnly PurchaseDate,
    string Category,
    string Reason,
    double? Confidence,
    DateTime ClassifiedAt);
