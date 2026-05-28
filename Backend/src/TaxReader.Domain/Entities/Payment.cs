namespace TaxReader.Domain.Entities;

public class Payment
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string StripeEventId { get; set; } = string.Empty;
    public string StripeSessionId { get; set; } = string.Empty;
    /// <summary>
    /// Populated from session.PaymentIntentId on checkout.session.completed.
    /// Used by charge.refunded handler to correlate refunds without relying on AmountCents matching.
    /// </summary>
    public string? StripePaymentIntentId { get; set; }
    public int CreditsGranted { get; set; }
    public int AmountCents { get; set; }
    public string Currency { get; set; } = "eur";
    public Domain.Enums.PaymentStatus Status { get; set; } = Domain.Enums.PaymentStatus.Pending;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? RevokedAt { get; set; }
    public User User { get; set; } = null!;
}
