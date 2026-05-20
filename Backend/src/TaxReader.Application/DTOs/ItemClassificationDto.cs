namespace TaxReader.Application.DTOs;

public record ItemClassificationDto(
    Guid Id,
    string Category,
    string Method,
    string Status,
    string Reason,
    DateTime ClassifiedAt);
