namespace TaxReader.Domain.Entities;

public class User
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// If set (0.0–1.0), AI classifications at or above this confidence
    /// are automatically confirmed without manual review.
    /// Null means auto-confirm is disabled (all AI results stay Suggested).
    /// </summary>
    public double? AutoConfirmThreshold { get; set; }

    public ICollection<ReceiptFile> ReceiptFiles { get; set; } = [];
    public UserTokenBalance? TokenBalance { get; set; }
    public ICollection<TokenTransaction> TokenTransactions { get; set; } = [];
    public ICollection<RefreshToken> RefreshTokens { get; set; } = [];
}
