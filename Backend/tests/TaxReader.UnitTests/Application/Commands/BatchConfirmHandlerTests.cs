using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using TaxReader.Application.Commands;
using TaxReader.Application.Interfaces;
using TaxReader.Domain.Entities;
using TaxReader.Domain.Enums;
using TaxReader.Infrastructure.Data;
using TaxReader.UnitTests.Helpers;

namespace TaxReader.UnitTests.Application.Commands;

public class BatchConfirmHandlerTests : IDisposable
{
    private static readonly Guid TestUserId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");

    private readonly AppDbContext _dbContext;
    private readonly Mock<ICurrentUser> _currentUserMock;
    private readonly BatchConfirmHandler _handler;

    public BatchConfirmHandlerTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContext = new AppDbContext(options);
        _currentUserMock = new Mock<ICurrentUser>();
        _currentUserMock.Setup(u => u.UserId).Returns(TestUserId);
        _handler = new BatchConfirmHandler(_dbContext, _currentUserMock.Object);
    }

    [Fact]
    public async Task HandleAsync_FailedItems_ConfirmsNone()
    {
        // Pitfall 4: the existing Status != Suggested filter must skip Failed items.
        var item = await CreateItemWithClassification(
            category: Category.WerbungskostenBueromaterial,
            status: ClassificationStatus.Failed,
            confidence: null);

        var result = await _handler.HandleAsync(new BatchConfirmCommand([item.Id]));

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(0);
        _dbContext.ItemClassifications.Count().Should().Be(1, "no new ItemClassification row added");
    }

    [Fact]
    public async Task HandleAsync_MediumOrLowConfidenceItems_ConfirmsNone()
    {
        // D-03 server-side HIGH-only guard: a forged request with MEDIUM/LOW item ids must be blocked.
        var mediumItem = await CreateItemWithClassification(
            category: Category.WerbungskostenBueromaterial,
            status: ClassificationStatus.Suggested,
            confidence: 0.70);

        var lowItem = await CreateItemWithClassification(
            category: Category.WerbungskostenFachliteratur,
            status: ClassificationStatus.Suggested,
            confidence: 0.40);

        var result = await _handler.HandleAsync(new BatchConfirmCommand([mediumItem.Id, lowItem.Id]));

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(0, "MEDIUM (0.70) and LOW (0.40) items must not be confirmed server-side");
        _dbContext.ItemClassifications.Count().Should().Be(2, "no new ItemClassification rows added");
    }

    [Fact]
    public async Task HandleAsync_HighConfidenceItems_ConfirmsThem()
    {
        // HIGH-confidence item (>=0.85) must be confirmable — no over-blocking.
        var highItem = await CreateItemWithClassification(
            category: Category.WerbungskostenBueromaterial,
            status: ClassificationStatus.Suggested,
            confidence: 0.92);

        var result = await _handler.HandleAsync(new BatchConfirmCommand([highItem.Id]));

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(1);
    }

    [Fact]
    public async Task HandleAsync_NullConfidenceItem_ConfirmsIt()
    {
        // Items with Confidence == null (manual/rule classifications) must remain confirmable —
        // the HIGH-only guard must NOT block null-Confidence items (pre-existing behavior preserved).
        var ruleItem = await CreateItemWithClassification(
            category: Category.WerbungskostenBueromaterial,
            status: ClassificationStatus.Suggested,
            confidence: null,
            method: ClassificationMethod.Rule);

        var result = await _handler.HandleAsync(new BatchConfirmCommand([ruleItem.Id]));

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(1, "null-Confidence (rule/manual) items are still confirmable");
    }

    private async Task<ReceiptItem> CreateItemWithClassification(
        Category category,
        ClassificationStatus status,
        double? confidence,
        ClassificationMethod method = ClassificationMethod.AI)
    {
        var file = TestDataFactory.CreateReceiptFile();
        file.UserId = TestUserId;
        var receipt = TestDataFactory.CreateReceipt(receiptFileId: file.Id);
        var item = TestDataFactory.CreateReceiptItem(receiptId: receipt.Id);
        var classification = TestDataFactory.CreateClassification(
            receiptItemId: item.Id,
            category: category,
            method: method,
            status: status,
            confidence: confidence);
        item.Classifications.Add(classification);
        receipt.Items.Add(item);

        _dbContext.ReceiptFiles.Add(file);
        _dbContext.Receipts.Add(receipt);
        _dbContext.ReceiptItems.Add(item);
        _dbContext.ItemClassifications.Add(classification);
        await _dbContext.SaveChangesAsync();

        return item;
    }

    public void Dispose() => _dbContext.Dispose();
}
