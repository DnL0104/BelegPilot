namespace TaxReader.Domain.Entities;

/// <summary>
/// Represents the AI credit balance. For the current single-user local app,
/// a single row (UserKey = "default") is maintained.
/// </summary>
public class UserTokenBalance
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string UserKey { get; set; } = "default";
    public int Balance { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public User User { get; set; } = null!;
}
