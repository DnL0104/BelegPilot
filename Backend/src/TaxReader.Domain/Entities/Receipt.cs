namespace TaxReader.Domain.Entities;

public class Receipt
{
    public Guid Id { get; set; }
    public Guid ReceiptFileId { get; set; }
    public string Vendor { get; set; } = string.Empty;
    public DateOnly PurchaseDate { get; set; }
    public decimal SubTotal { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public string Currency { get; set; } = "EUR";
    public string RawExtractedText { get; set; } = string.Empty;
    public DateTime ParsedAt { get; set; } = DateTime.UtcNow;

    public ReceiptFile ReceiptFile { get; set; } = null!;
    public ICollection<ReceiptItem> Items { get; set; } = [];
}
