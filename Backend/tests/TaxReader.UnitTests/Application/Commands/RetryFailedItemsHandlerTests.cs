using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using TaxReader.Application.Commands;
using TaxReader.Application.DTOs;
using TaxReader.Application.Interfaces;
using TaxReader.Domain.Common;
using TaxReader.Domain.Entities;
using TaxReader.Domain.Enums;
using TaxReader.Infrastructure.Data;
using TaxReader.UnitTests.Helpers;

namespace TaxReader.UnitTests.Application.Commands;

public class RetryFailedItemsHandlerTests : IDisposable
{
    private static readonly Guid TestUserId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
    private static readonly Guid OtherUserId = Guid.Parse("bbbbbbbb-cccc-dddd-eeee-ffffffffffff");

    private readonly AppDbContext _dbContext;
    private readonly Mock<ICurrentUser> _currentUserMock;
    private readonly Mock<IClassificationService> _classificationServiceMock;
    private readonly RetryFailedItemsHandler _handler;

    public RetryFailedItemsHandlerTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContext = new AppDbContext(options);
        _currentUserMock = new Mock<ICurrentUser>();
        _currentUserMock.Setup(u => u.UserId).Returns(TestUserId);
        _classificationServiceMock = new Mock<IClassificationService>();
        _handler = new RetryFailedItemsHandler(_dbContext, _classificationServiceMock.Object, _currentUserMock.Object);
    }

    [Fact]
    public async Task HandleAsync_OwnedReceiptWithFailedItems_RetriesOnlyFailed()
    {
        // Arrange: receipt owned by TestUserId with one Failed item and one Suggested item.
        var (file, receipt, failedItem, suggestedItem) = await CreateReceiptWithMixedItems(TestUserId);

        // The service should be called with only the Failed item.
        var newClassification = TestDataFactory.CreateClassification(
            receiptItemId: failedItem.Id,
            category: Category.WerbungskostenBueromaterial,
            status: ClassificationStatus.Suggested,
            confidence: 0.92);

        _classificationServiceMock
            .Setup(s => s.ClassifyItemsAsync(
                It.Is<IEnumerable<ReceiptItem>>(items => items.Count() == 1 && items.First().Id == failedItem.Id),
                TestUserId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([newClassification]);

        // Act
        var result = await _handler.HandleAsync(new RetryFailedItemsCommand(receipt.Id));

        // Assert
        result.IsSuccess.Should().BeTrue();
        _classificationServiceMock.Verify(
            s => s.ClassifyItemsAsync(
                It.Is<IEnumerable<ReceiptItem>>(items => items.Count() == 1),
                TestUserId,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_ReceiptOwnedByAnotherUser_ReturnsFailure()
    {
        // Arrange: receipt owned by OtherUserId — TestUserId cannot access it.
        var file = TestDataFactory.CreateReceiptFile();
        file.UserId = OtherUserId;
        var receipt = TestDataFactory.CreateReceipt(receiptFileId: file.Id);
        _dbContext.ReceiptFiles.Add(file);
        _dbContext.Receipts.Add(receipt);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _handler.HandleAsync(new RetryFailedItemsCommand(receipt.Id));

        // Assert: IDOR — no leak of receipt existence, no classification work done.
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain(receipt.Id.ToString());
        _classificationServiceMock.Verify(
            s => s.ClassifyItemsAsync(It.IsAny<IEnumerable<ReceiptItem>>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_NoFailedItems_ReturnsGermanFailure()
    {
        // Arrange: receipt with only Suggested items — nothing to retry.
        var file = TestDataFactory.CreateReceiptFile();
        file.UserId = TestUserId;
        var receipt = TestDataFactory.CreateReceipt(receiptFileId: file.Id);
        var item = TestDataFactory.CreateReceiptItem(receiptId: receipt.Id);
        var suggestedClassification = TestDataFactory.CreateClassification(
            receiptItemId: item.Id,
            category: Category.WerbungskostenBueromaterial,
            status: ClassificationStatus.Suggested,
            confidence: 0.90);
        item.Classifications.Add(suggestedClassification);
        receipt.Items.Add(item);

        _dbContext.ReceiptFiles.Add(file);
        _dbContext.Receipts.Add(receipt);
        _dbContext.ReceiptItems.Add(item);
        _dbContext.ItemClassifications.Add(suggestedClassification);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _handler.HandleAsync(new RetryFailedItemsCommand(receipt.Id));

        // Assert: German failure message for empty failed set.
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("fehlgeschlagen");
        _classificationServiceMock.Verify(
            s => s.ClassifyItemsAsync(It.IsAny<IEnumerable<ReceiptItem>>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private async Task<(ReceiptFile file, Receipt receipt, ReceiptItem failedItem, ReceiptItem suggestedItem)>
        CreateReceiptWithMixedItems(Guid userId)
    {
        var file = TestDataFactory.CreateReceiptFile();
        file.UserId = userId;
        var receipt = TestDataFactory.CreateReceipt(receiptFileId: file.Id);

        var failedItem = TestDataFactory.CreateReceiptItem(receiptId: receipt.Id, lineNumber: 1);
        var failedClassification = TestDataFactory.CreateClassification(
            receiptItemId: failedItem.Id,
            category: Category.Unbekannt,
            status: ClassificationStatus.Failed);
        failedItem.Classifications.Add(failedClassification);

        var suggestedItem = TestDataFactory.CreateReceiptItem(receiptId: receipt.Id, lineNumber: 2);
        var suggestedClassification = TestDataFactory.CreateClassification(
            receiptItemId: suggestedItem.Id,
            category: Category.WerbungskostenBueromaterial,
            status: ClassificationStatus.Suggested,
            confidence: 0.88);
        suggestedItem.Classifications.Add(suggestedClassification);

        receipt.Items.Add(failedItem);
        receipt.Items.Add(suggestedItem);

        _dbContext.ReceiptFiles.Add(file);
        _dbContext.Receipts.Add(receipt);
        _dbContext.ReceiptItems.Add(failedItem);
        _dbContext.ReceiptItems.Add(suggestedItem);
        _dbContext.ItemClassifications.Add(failedClassification);
        _dbContext.ItemClassifications.Add(suggestedClassification);
        await _dbContext.SaveChangesAsync();

        return (file, receipt, failedItem, suggestedItem);
    }

    public void Dispose() => _dbContext.Dispose();
}
