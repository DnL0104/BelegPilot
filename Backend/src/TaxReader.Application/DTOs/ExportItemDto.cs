namespace TaxReader.Application.DTOs;

public record ExportItemDto(
    DateOnly PurchaseDate,
    string Vendor,
    string Description,
    int Quantity,
    decimal UnitPrice,
    decimal TotalPrice,
    string Category,
    string ClassificationStatus,
    string ClassificationMethod,
    string? Reason);
