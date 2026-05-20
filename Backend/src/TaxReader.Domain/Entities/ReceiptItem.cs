namespace TaxReader.Domain.Entities;

public class ReceiptItem
{
    public Guid Id { get; set; }
    public Guid ReceiptId { get; set; }
    public string Description { get; set; } = string.Empty;
    public int Quantity { get; set; } = 1;
    public decimal UnitPrice { get; set; }
    public decimal TotalPrice { get; set; }
    public int LineNumber { get; set; }

    public Receipt Receipt { get; set; } = null!;
    public ICollection<ItemClassification> Classifications { get; set; } = [];

    public ItemClassification? LatestClassification =>
        Classifications.OrderByDescending(c => c.ClassifiedAt).FirstOrDefault();
}
