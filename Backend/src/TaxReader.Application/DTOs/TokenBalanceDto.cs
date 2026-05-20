namespace TaxReader.Application.DTOs;

public record TokenBalanceDto(int Balance, DateTime UpdatedAt);

public record TokenTransactionDto(
    Guid Id,
    string Type,
    int Amount,
    int BalanceAfter,
    string Description,
    Guid? RelatedItemId,
    DateTime CreatedAt);
