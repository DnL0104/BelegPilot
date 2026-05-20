namespace TaxReader.Application.DTOs;

public record AnnualSummaryDto(
    int Year,
    int TotalReceipts,
    decimal TotalAmount,
    List<CategoryTotalDto> CategoryBreakdown,
    int UnclassifiedItemCount);
