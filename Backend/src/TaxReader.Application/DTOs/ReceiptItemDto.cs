namespace TaxReader.Application.DTOs;

public record ReceiptItemDto(
    Guid Id,
    Guid ReceiptId,
    string Description,
    int Quantity,
    decimal UnitPrice,
    decimal TotalPrice,
    int LineNumber,
    ItemClassificationDto? LatestClassification);
