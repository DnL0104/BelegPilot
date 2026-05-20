using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using TaxReader.Application.Interfaces;
using TaxReader.Application.Queries;
using TaxReader.Domain.Enums;
using TaxReader.Infrastructure.Data;
using TaxReader.UnitTests.Helpers;

namespace TaxReader.UnitTests.Application.Queries;

public class GetCategoryTotalsHandlerTests : IDisposable
{
    private static readonly Guid TestUserId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");

    private readonly AppDbContext _dbContext;
    private readonly Mock<ICurrentUser> _currentUserMock;
    private readonly GetCategoryTotalsHandler _handler;

    public GetCategoryTotalsHandlerTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContext = new AppDbContext(options);
        _currentUserMock = new Mock<ICurrentUser>();
        _currentUserMock.Setup(u => u.UserId).Returns(TestUserId);
        _handler = new GetCategoryTotalsHandler(_dbContext, _currentUserMock.Object);
    }

    [Fact]
    public async Task HandleAsync_WithClassifiedItems_ReturnsCategoryTotals()
    {
        var file = TestDataFactory.CreateReceiptFile();
        file.UserId = TestUserId;
        var receipt = TestDataFactory.CreateReceipt(
            receiptFileId: file.Id,
            purchaseDate: new DateOnly(2025, 3, 15));

        var item1 = TestDataFactory.CreateReceiptItem(
            receiptId: receipt.Id, description: "Tinte", unitPrice: 15.00m);
        var item2 = TestDataFactory.CreateReceiptItem(
            receiptId: receipt.Id, description: "Fachbuch", unitPrice: 25.00m, lineNumber: 2);

        var class1 = TestDataFactory.CreateClassification(
            receiptItemId: item1.Id, category: Category.ConsumablesAndOfficeSupplies);
        var class2 = TestDataFactory.CreateClassification(
            receiptItemId: item2.Id, category: Category.SpecialistLiterature);

        _dbContext.ReceiptFiles.Add(file);
        _dbContext.Receipts.Add(receipt);
        _dbContext.ReceiptItems.AddRange(item1, item2);
        _dbContext.ItemClassifications.AddRange(class1, class2);
        await _dbContext.SaveChangesAsync();

        var result = await _handler.HandleAsync(new GetCategoryTotalsQuery(2025));

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        result.Value!.Should().Contain(c => c.Category == "ConsumablesAndOfficeSupplies" && c.TotalAmount == 15.00m);
        result.Value.Should().Contain(c => c.Category == "SpecialistLiterature" && c.TotalAmount == 25.00m);
    }

    [Fact]
    public async Task HandleAsync_NoItemsForYear_ReturnsEmptyList()
    {
        var result = await _handler.HandleAsync(new GetCategoryTotalsQuery(2025));

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsync_UnknownCategory_ExcludedFromTotals()
    {
        var file = TestDataFactory.CreateReceiptFile();
        file.UserId = TestUserId;
        var receipt = TestDataFactory.CreateReceipt(
            receiptFileId: file.Id,
            purchaseDate: new DateOnly(2025, 1, 1));
        var item = TestDataFactory.CreateReceiptItem(receiptId: receipt.Id, unitPrice: 10.00m);
        var classification = TestDataFactory.CreateClassification(
            receiptItemId: item.Id, category: Category.Unknown);

        _dbContext.ReceiptFiles.Add(file);
        _dbContext.Receipts.Add(receipt);
        _dbContext.ReceiptItems.Add(item);
        _dbContext.ItemClassifications.Add(classification);
        await _dbContext.SaveChangesAsync();

        var result = await _handler.HandleAsync(new GetCategoryTotalsQuery(2025));

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    public void Dispose() => _dbContext.Dispose();
}
