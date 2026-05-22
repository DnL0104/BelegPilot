using TaxReader.Domain.Entities;
using TaxReader.Domain.Enums;

namespace TaxReader.UnitTests.Helpers;

public static class TestDataFactory
{
    /// <summary>
    /// D-07: builds a User with IsAdmin=true for Hangfire dashboard tests.
    /// Email is lowercase-normalized (matches AuthService.RegisterAsync).
    /// BCrypt hash is precomputed against "test-password-1234" so WAF login
    /// flows produce a successful authentication.
    /// </summary>
    public static User CreateAdminUser(string email = "admin@test.local")
    {
        return new User
        {
            Id = Guid.NewGuid(),
            Email = email.ToLowerInvariant(),
            DisplayName = "Admin",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("test-password-1234"),
            CreatedAt = DateTime.UtcNow,
            IsAdmin = true
        };
    }

    public static User CreateRegularUser(string email = "user@test.local")
    {
        return new User
        {
            Id = Guid.NewGuid(),
            Email = email.ToLowerInvariant(),
            DisplayName = "User",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("test-password-1234"),
            CreatedAt = DateTime.UtcNow,
            IsAdmin = false
        };
    }

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
        Category category = Category.WerbungskostenBueromaterial,
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
        string? descriptionPattern = "Tinte",
        string? vendorPattern = null,
        string? sourceFilePattern = null,
        Guid? userId = null,
        Category category = Category.WerbungskostenBueromaterial,
        int priority = 10,
        bool isActive = true)
    {
        return new ClassificationRule
        {
            Id = id ?? Guid.NewGuid(),
            UserId = userId,
            DescriptionPattern = descriptionPattern,
            VendorPattern = vendorPattern,
            SourceFilePattern = sourceFilePattern,
            Category = category,
            Priority = priority,
            IsActive = isActive,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }
}
