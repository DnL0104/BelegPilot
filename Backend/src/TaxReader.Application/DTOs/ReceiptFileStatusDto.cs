using System.Text.Json.Serialization;
using TaxReader.Domain.Enums;

namespace TaxReader.Application.DTOs;

/// <summary>D-13 polling response. PascalCase enum serialisation matches frontend
/// ProcessingStatus union type.</summary>
public record ReceiptFileStatusDto(
    [property: JsonConverter(typeof(JsonStringEnumConverter))] ProcessingStatus Status,
    DateTime UpdatedAt,
    string? ErrorCode,
    string? ErrorMessage,
    Guid? ReceiptId);
