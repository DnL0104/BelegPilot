using TaxReader.Domain.Enums;

namespace TaxReader.Domain.Entities;

public class TokenTransaction
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string UserKey { get; set; } = "default";
    public TokenTransactionType Type { get; set; }
    /// <summary>Positive for Purchase/Refund, negative for Consumption.</summary>
    public int Amount { get; set; }
    public int BalanceAfter { get; set; }
    public string Description { get; set; } = string.Empty;
    public Guid? RelatedItemId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public User User { get; set; } = null!;
}
