using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using TaxReader.Application.Commands;
using TaxReader.Application.Interfaces;
using TaxReader.Domain.Entities;
using TaxReader.Domain.Enums;
using TaxReader.Infrastructure.Data;

namespace TaxReader.UnitTests.Application.Commands;

/// <summary>
/// LEG-08: SaveClassificationRuleHandler calls IAuditLogger after a rule is saved.
/// Also covers happy-path rule creation and 409 duplicate detection.
/// </summary>
public class SaveClassificationRuleHandlerTests : IDisposable
{
    private static readonly Guid TestUserId = Guid.Parse("cccccccc-1111-2222-3333-444444444444");

    private readonly AppDbContext _dbContext;
    private readonly Mock<ICurrentUser> _currentUserMock;
    private readonly Mock<IAuditLogger> _auditLoggerMock;
    private readonly SaveClassificationRuleHandler _handler;

    // We need a receipt item seeded in the DB for the handler to find
    private readonly Guid _receiptItemId;

    public SaveClassificationRuleHandlerTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new AppDbContext(options);

        _currentUserMock = new Mock<ICurrentUser>();
        _currentUserMock.Setup(u => u.UserId).Returns(TestUserId);

        _auditLoggerMock = new Mock<IAuditLogger>();
        _auditLoggerMock
            .Setup(a => a.RecordAsync(
                It.IsAny<AuditAction>(),
                It.IsAny<Guid?>(),
                It.IsAny<Guid?>(),
                It.IsAny<Dictionary<string, object?>>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _handler = new SaveClassificationRuleHandler(
            _dbContext,
            _currentUserMock.Object,
            _auditLoggerMock.Object);

        // Seed minimal data: user -> receipt_file -> receipt -> receipt_item
        var user = new User { Id = TestUserId, Email = "rule@test.local", DisplayName = "Rule Tester", PasswordHash = "x" };
        _dbContext.Users.Add(user);

        var receiptFile = new ReceiptFile
        {
            Id = Guid.NewGuid(),
            UserId = TestUserId,
            OriginalFileName = "test.pdf",
            ContentHash = "abc",
            FileSize = 1024,
            Status = FileStatus.Processed,
            UploadedAt = DateTime.UtcNow
        };
        _dbContext.ReceiptFiles.Add(receiptFile);

        var receipt = new Receipt
        {
            Id = Guid.NewGuid(),
            ReceiptFileId = receiptFile.Id,
            Vendor = "TestVendor",
            PurchaseDate = DateOnly.FromDateTime(DateTime.UtcNow),
            TotalAmount = 10.00m,
            RawExtractedText = "test",
            ParsedAt = DateTime.UtcNow
        };
        _dbContext.Receipts.Add(receipt);

        _receiptItemId = Guid.NewGuid();
        var item = new ReceiptItem
        {
            Id = _receiptItemId,
            ReceiptId = receipt.Id,
            Description = "Test item",
            UnitPrice = 10.00m,
            Quantity = 1,
            TotalPrice = 10.00m
        };
        _dbContext.ReceiptItems.Add(item);

        _dbContext.SaveChanges();
    }

    [Fact]
    public async Task HandleAsync_ValidRule_CallsAuditLoggerWithClassificationRuleCreated()
    {
        // Arrange
        var command = new SaveClassificationRuleCommand(
            ReceiptItemId: _receiptItemId,
            VendorPattern: "TestVendor",
            DescriptionPattern: null,
            SourceFilePattern: null,
            Category: Category.WerbungskostenFachliteratur);

        Guid? capturedRuleId = null;
        _auditLoggerMock
            .Setup(a => a.RecordAsync(
                AuditAction.ClassificationRuleCreated,
                TestUserId,
                TestUserId,
                It.Is<Dictionary<string, object?>>(m => m.ContainsKey("rule_id") && m.ContainsKey("category")),
                It.IsAny<CancellationToken>()))
            .Callback<AuditAction, Guid?, Guid?, Dictionary<string, object?>, CancellationToken>(
                (_, _, _, meta, _) => capturedRuleId = (Guid?)meta["rule_id"])
            .Returns(Task.CompletedTask);

        // Act
        var result = await _handler.HandleAsync(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _auditLoggerMock.Verify(
            a => a.RecordAsync(
                AuditAction.ClassificationRuleCreated,
                TestUserId,
                TestUserId,
                It.Is<Dictionary<string, object?>>(m => m.ContainsKey("rule_id") && m.ContainsKey("category")),
                It.IsAny<CancellationToken>()),
            Times.Once,
            "audit entry must be recorded after successful rule save");

        capturedRuleId.Should().NotBeNull("rule_id must be in audit metadata");

        var savedRule = await _dbContext.ClassificationRules.FirstOrDefaultAsync();
        savedRule.Should().NotBeNull();
        savedRule!.Id.Should().Be(capturedRuleId!.Value, "rule_id in metadata must match the saved rule's ID");
    }

    [Fact]
    public async Task HandleAsync_DuplicateRule_DoesNotCallAuditLogger()
    {
        // Arrange — seed an identical rule
        _dbContext.ClassificationRules.Add(new ClassificationRule
        {
            Id = Guid.NewGuid(),
            UserId = TestUserId,
            VendorPattern = "TestVendor",
            DescriptionPattern = null,
            SourceFilePattern = null,
            Category = Category.WerbungskostenFachliteratur,
            Priority = 10,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await _dbContext.SaveChangesAsync();

        var command = new SaveClassificationRuleCommand(
            ReceiptItemId: _receiptItemId,
            VendorPattern: "TestVendor",
            DescriptionPattern: null,
            SourceFilePattern: null,
            Category: Category.WerbungskostenFachliteratur);

        // Act
        var result = await _handler.HandleAsync(command, CancellationToken.None);

        // Assert — 409 conflict, no audit
        result.IsFailure.Should().BeTrue();
        _auditLoggerMock.Verify(
            a => a.RecordAsync(
                It.IsAny<AuditAction>(),
                It.IsAny<Guid?>(),
                It.IsAny<Guid?>(),
                It.IsAny<Dictionary<string, object?>>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        GC.SuppressFinalize(this);
    }
}
