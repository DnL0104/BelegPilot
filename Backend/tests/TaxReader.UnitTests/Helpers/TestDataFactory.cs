using TaxReader.Domain.Entities;
using TaxReader.Domain.Enums;

namespace TaxReader.UnitTests.Helpers;

public static class TestDataFactory
{
    public static ReceiptFile CreateReceiptFile(
        Guid? id = null,
        string fileName = "test.pdf",
        string contentHash = "ABC123",
        long fileSize = 1024,
        string? sourceHint = null,
        int? yearHint = null,
        FileStatus status = FileStatus.Uploaded)
    {
        return new ReceiptFile
        {
            Id = id ?? Guid.NewGuid(),
            OriginalFileName = fileName,
            ContentHash = contentHash,
            FileSize = fileSize,
            SourceHint = sourceHint,
            YearHint = yearHint,
            UploadedBy = "test-user",
            UploadedAt = DateTime.UtcNow,
            Status = status
        };
    }

    public static Receipt CreateReceipt(
        Guid? id = null,
        Guid? receiptFileId = null,
        string vendor = "TestVendor",
        DateOnly? purchaseDate = null,
        decimal totalAmount = 29.99m)
    {
        return new Receipt
        {
            Id = id ?? Guid.NewGuid(),
            ReceiptFileId = receiptFileId ?? Guid.NewGuid(),
            Vendor = vendor,
            PurchaseDate = purchaseDate ?? new DateOnly(2025, 6, 15),
            SubTotal = totalAmount * 0.81m,
            TaxAmount = totalAmount * 0.19m,
            TotalAmount = totalAmount,
            Currency = "EUR",
            RawExtractedText = "Sample extracted text",
            ParsedAt = DateTime.UtcNow
        };
    }

    public static ReceiptItem CreateReceiptItem(
        Guid? id = null,
        Guid? receiptId = null,
        string description = "Test Item",
        int quantity = 1,
        decimal unitPrice = 9.99m,
        int lineNumber = 1)
    {
        return new ReceiptItem
        {
            Id = id ?? Guid.NewGuid(),
            ReceiptId = receiptId ?? Guid.NewGuid(),
            Description = description,
            Quantity = quantity,
            UnitPrice = unitPrice,
            TotalPrice = unitPrice * quantity,
            LineNumber = lineNumber
        };
    }

    public static ItemClassification CreateClassification(
        Guid? id = null,
        Guid? receiptItemId = null,
        Category category = Category.ConsumablesAndOfficeSupplies,
        ClassificationMethod method = ClassificationMethod.Rule,
        ClassificationStatus status = ClassificationStatus.Suggested,
        string reason = "Matched rule: 'test'")
    {
        return new ItemClassification
        {
            Id = id ?? Guid.NewGuid(),
            ReceiptItemId = receiptItemId ?? Guid.NewGuid(),
            Category = category,
            Method = method,
            Status = status,
            Reason = reason,
            ClassifiedAt = DateTime.UtcNow
        };
    }

    public static ClassificationRule CreateRule(
        Guid? id = null,
        string pattern = "Tinte",
        Category category = Category.ConsumablesAndOfficeSupplies,
        int priority = 10,
        bool isActive = true)
    {
        return new ClassificationRule
        {
            Id = id ?? Guid.NewGuid(),
            Pattern = pattern,
            Category = category,
            Priority = priority,
            IsActive = isActive,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }
}
