using TaxReader.Domain.Enums;

namespace TaxReader.Domain.Entities;

public class ItemClassification
{
    public Guid Id { get; set; }
    public Guid ReceiptItemId { get; set; }
    public Category Category { get; set; }
    public ClassificationMethod Method { get; set; }
    public ClassificationStatus Status { get; set; }
    public string Reason { get; set; } = string.Empty;
    public DateTime ClassifiedAt { get; set; } = DateTime.UtcNow;
    public string? ClassifiedBy { get; set; }

    // Nullable because manual confirmations and rule-based classifications have no AI-derived confidence score.
    public double? Confidence { get; set; }

    public ReceiptItem ReceiptItem { get; set; } = null!;
}
