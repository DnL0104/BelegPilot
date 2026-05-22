using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using TaxReader.Application.Interfaces;
using TaxReader.Application.Queries;
using TaxReader.Domain.Enums;
using TaxReader.Infrastructure.Data;
using TaxReader.UnitTests.Helpers;

namespace TaxReader.UnitTests.Application.Queries;

public class GetAnnualSummaryHandlerTests : IDisposable
{
    private static readonly Guid TestUserId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");

    private readonly AppDbContext _dbContext;
    private readonly Mock<ICurrentUser> _currentUserMock;
    private readonly GetAnnualSummaryHandler _handler;

    public GetAnnualSummaryHandlerTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContext = new AppDbContext(options);
        _currentUserMock = new Mock<ICurrentUser>();
        _currentUserMock.Setup(u => u.UserId).Returns(TestUserId);
        _handler = new GetAnnualSummaryHandler(_dbContext, _currentUserMock.Object);
    }

    [Fact]
    public async Task HandleAsync_WithData_ReturnsSummary()
    {
        var file = TestDataFactory.CreateReceiptFile();
        file.UserId = TestUserId;
        var receipt = TestDataFactory.CreateReceipt(
            receiptFileId: file.Id,
            purchaseDate: new DateOnly(2025, 6, 1),
            totalAmount: 30.00m);
        var item = TestDataFactory.CreateReceiptItem(receiptId: receipt.Id, unitPrice: 30.00m);
        var classification = TestDataFactory.CreateClassification(
            receiptItemId: item.Id, category: Category.WerbungskostenBueromaterial);

        _dbContext.ReceiptFiles.Add(file);
        _dbContext.Receipts.Add(receipt);
        _dbContext.ReceiptItems.Add(item);
        _dbContext.ItemClassifications.Add(classification);
        await _dbContext.SaveChangesAsync();

        var result = await _handler.HandleAsync(new GetAnnualSummaryQuery(2025));

        result.IsSuccess.Should().BeTrue();
        result.Value!.Year.Should().Be(2025);
        result.Value.TotalReceipts.Should().Be(1);
        result.Value.TotalAmount.Should().Be(30.00m);
        result.Value.CategoryBreakdown.Should().HaveCount(1);
        result.Value.UnclassifiedItemCount.Should().Be(0);
    }

    [Fact]
    public async Task HandleAsync_WithUnclassifiedItems_CountsThem()
    {
        var file = TestDataFactory.CreateReceiptFile();
        file.UserId = TestUserId;
        var receipt = TestDataFactory.CreateReceipt(
            receiptFileId: file.Id,
            purchaseDate: new DateOnly(2025, 1, 1));
        var item = TestDataFactory.CreateReceiptItem(receiptId: receipt.Id);
        var classification = TestDataFactory.CreateClassification(
            receiptItemId: item.Id, category: Category.Unbekannt);

        _dbContext.ReceiptFiles.Add(file);
        _dbContext.Receipts.Add(receipt);
        _dbContext.ReceiptItems.Add(item);
        _dbContext.ItemClassifications.Add(classification);
        await _dbContext.SaveChangesAsync();

        var result = await _handler.HandleAsync(new GetAnnualSummaryQuery(2025));

        result.IsSuccess.Should().BeTrue();
        result.Value!.UnclassifiedItemCount.Should().Be(1);
    }

    [Fact]
    public async Task HandleAsync_EmptyYear_ReturnsZeroSummary()
    {
        var result = await _handler.HandleAsync(new GetAnnualSummaryQuery(2025));

        result.IsSuccess.Should().BeTrue();
        result.Value!.TotalReceipts.Should().Be(0);
        result.Value.TotalAmount.Should().Be(0);
        result.Value.CategoryBreakdown.Should().BeEmpty();
    }

    public void Dispose() => _dbContext.Dispose();
}
