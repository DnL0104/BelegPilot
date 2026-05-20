namespace TaxReader.Application.DTOs;

public record CategoryTotalDto(
    string Category,
    decimal TotalAmount,
    int ItemCount);
