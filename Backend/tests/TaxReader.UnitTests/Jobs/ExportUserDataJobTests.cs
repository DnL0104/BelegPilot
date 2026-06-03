using System.IO.Compression;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using TaxReader.Application.Interfaces;
using TaxReader.Application.Jobs;
using TaxReader.Domain.Entities;
using TaxReader.Domain.Enums;
using TaxReader.Infrastructure.Data;
using TaxReader.Infrastructure.Services;

namespace TaxReader.UnitTests.Jobs;

/// <summary>
/// Behavioural tests for ExportUserDataJob (DSGVO Art. 20 zip bundle generation).
/// LEG-07 / D-11 / T-06-44 / T-06-46.
/// </summary>
public class ExportUserDataJobTests : IDisposable
{
    private static readonly Guid OwnerUserId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
    private static readonly Guid OtherUserId = Guid.Parse("bbbbbbbb-cccc-dddd-eeee-ffffffffffff");

    private readonly AppDbContext _dbContext;
    private readonly ExportTokenStore _tokenStore;
    private readonly ExportUserDataJob _job;
    private readonly string _exportToken;
    private readonly string _zipPath;

    public ExportUserDataJobTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new AppDbContext(options);

        // Seed owner user
        _dbContext.Users.Add(new User
        {
            Id = OwnerUserId,
            Email = "owner@example.com",
            DisplayName = "Owner",
            PasswordHash = "should_not_appear_in_export"
        });

        // Seed other user
        _dbContext.Users.Add(new User
        {
            Id = OtherUserId,
            Email = "other@example.com",
            DisplayName = "Other",
            PasswordHash = "other_hash"
        });

        // Seed a receipt file + receipt + item + classification for the owner
        var fileId = Guid.NewGuid();
        var receiptId = Guid.NewGuid();
        var itemId = Guid.NewGuid();

        _dbContext.ReceiptFiles.Add(new ReceiptFile
        {
            Id = fileId,
            UserId = OwnerUserId,
            OriginalFileName = "amazon-2024.pdf",
            ContentHash = "abc123",
            FileSize = 1234,
            Status = FileStatus.Processed
        });
        _dbContext.Receipts.Add(new Receipt
        {
            Id = receiptId,
            ReceiptFileId = fileId,
            Vendor = "Amazon",
            PurchaseDate = new DateOnly(2024, 3, 15),
            TotalAmount = 29.99m,
            Currency = "EUR"
        });
        _dbContext.ReceiptItems.Add(new ReceiptItem
        {
            Id = itemId,
            ReceiptId = receiptId,
            Description = "Druckerpatrone",
            Quantity = 2,
            UnitPrice = 14.995m,
            TotalPrice = 29.99m,
            LineNumber = 1
        });
        _dbContext.ItemClassifications.Add(new ItemClassification
        {
            Id = Guid.NewGuid(),
            ReceiptItemId = itemId,
            Category = Category.ConsumablesAndOfficeSupplies,
            Method = ClassificationMethod.AI,
            Status = ClassificationStatus.Confirmed,
            Reason = "Office supplies keyword match"
        });

        // Seed token transactions for the owner
        _dbContext.TokenTransactions.Add(new TokenTransaction
        {
            Id = Guid.NewGuid(),
            UserId = OwnerUserId,
            UserKey = OwnerUserId.ToString(),
            Type = TokenTransactionType.Purchase,
            Amount = 100,
            BalanceAfter = 100,
            Description = "Test purchase",
            CreatedAt = DateTime.UtcNow
        });

        // Seed audit log entries: one for owner, one for another user
        _dbContext.AuditLogEntries.Add(new AuditLogEntry
        {
            Id = Guid.NewGuid(),
            Action = AuditAction.TokensGranted.ToString(),
            ActorUserId = null,
            SubjectUserId = OwnerUserId,
            Metadata = new Dictionary<string, object?> { ["credits"] = 100 },
            CreatedAt = DateTime.UtcNow
        });
        _dbContext.AuditLogEntries.Add(new AuditLogEntry
        {
            Id = Guid.NewGuid(),
            Action = AuditAction.AccountDeleted.ToString(),
            ActorUserId = OtherUserId,
            SubjectUserId = OtherUserId,
            Metadata = new Dictionary<string, object?> { ["email_hash"] = "other_hash" },
            CreatedAt = DateTime.UtcNow
        });

        _dbContext.SaveChanges();

        _tokenStore = new ExportTokenStore();
        _job = new ExportUserDataJob(_dbContext, Mock.Of<ILogger<ExportUserDataJob>>(), _tokenStore);

        _exportToken = Guid.NewGuid().ToString("N");
        var exportsDir = Path.Combine(Path.GetTempPath(), "taxreader-exports");
        _zipPath = Path.Combine(exportsDir, _exportToken + ".zip");
    }

    [Fact]
    public async Task HandleAsync_ValidUser_CreatesZipAtExpectedPath()
    {
        // Act
        await _job.HandleAsync(OwnerUserId, _exportToken, CancellationToken.None);

        // Assert
        File.Exists(_zipPath).Should().BeTrue($"zip should be created at {_zipPath}");
    }

    [Fact]
    public async Task HandleAsync_ValidUser_ZipContainsAllRequiredEntries()
    {
        // Act
        await _job.HandleAsync(OwnerUserId, _exportToken, CancellationToken.None);

        // Assert — open the zip and check entry names
        using var zipStream = new FileStream(_zipPath, FileMode.Open, FileAccess.Read);
        using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read);

        var entryNames = archive.Entries.Select(e => e.Name).ToList();
        entryNames.Should().Contain("receipts.json");
        entryNames.Should().Contain("receipts.csv");
        entryNames.Should().Contain("items.json");
        entryNames.Should().Contain("items.csv");
        entryNames.Should().Contain("classifications.json");
        entryNames.Should().Contain("classifications.csv");
        entryNames.Should().Contain("token_transactions.json");
        entryNames.Should().Contain("token_transactions.csv");
        entryNames.Should().Contain("audit_log.json");
        entryNames.Should().Contain("audit_log.csv");
        entryNames.Should().Contain("README.txt");
    }

    [Fact]
    public async Task HandleAsync_ValidUser_BundleDoesNotContainPasswordHash()
    {
        // Act
        await _job.HandleAsync(OwnerUserId, _exportToken, CancellationToken.None);

        // Assert — read all text content from the zip and check no PasswordHash appears
        using var zipStream = new FileStream(_zipPath, FileMode.Open, FileAccess.Read);
        using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read);

        foreach (var entry in archive.Entries)
        {
            using var reader = new StreamReader(entry.Open());
            var content = await reader.ReadToEndAsync();
            content.Should().NotContain("should_not_appear_in_export",
                $"entry '{entry.Name}' must not contain the owner's password hash");
            content.Should().NotContain("passwordHash",
                $"entry '{entry.Name}' must not contain password hash field");
            content.Should().NotContain("PasswordHash",
                $"entry '{entry.Name}' must not contain PasswordHash field");
        }
    }

    [Fact]
    public async Task HandleAsync_ValidUser_MarksTokenStoreAsReady()
    {
        // Arrange — mark as generating first (as the endpoint would)
        _tokenStore.MarkGenerating(_exportToken, OwnerUserId, DateTime.UtcNow.AddHours(24));

        // Act
        await _job.HandleAsync(OwnerUserId, _exportToken, CancellationToken.None);

        // Assert
        _tokenStore.TryGet(_exportToken, out var record).Should().BeTrue();
        record!.Status.Should().Be(ExportStatus.Ready);
    }

    [Fact]
    public async Task HandleAsync_AuditLog_ContainsOnlyOwnerRows()
    {
        // Act
        await _job.HandleAsync(OwnerUserId, _exportToken, CancellationToken.None);

        // Assert — audit_log.json should only have the owner's entry
        using var zipStream = new FileStream(_zipPath, FileMode.Open, FileAccess.Read);
        using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read);

        var auditEntry = archive.Entries.Single(e => e.Name == "audit_log.json");
        using var reader = new StreamReader(auditEntry.Open());
        var content = await reader.ReadToEndAsync();

        // The content should reference the owner's subject_user_id
        content.Should().Contain(OwnerUserId.ToString(),
            "audit_log should contain the owner's user ID");
        content.Should().NotContain(OtherUserId.ToString(),
            "audit_log must NOT contain rows belonging to other users (T-06-46)");
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        // Clean up the temp zip if it exists
        if (File.Exists(_zipPath))
        {
            try { File.Delete(_zipPath); } catch { /* best-effort cleanup */ }
        }
    }
}
