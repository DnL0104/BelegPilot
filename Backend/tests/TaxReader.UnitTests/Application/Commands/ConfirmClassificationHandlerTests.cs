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

public class ConfirmClassificationHandlerTests : IDisposable
{
    private static readonly Guid TestUserId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");

    private readonly AppDbContext _dbContext;
    private readonly Mock<ICurrentUser> _currentUserMock;
    private readonly ConfirmClassificationHandler _handler;

    public ConfirmClassificationHandlerTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContext = new AppDbContext(options);
        _currentUserMock = new Mock<ICurrentUser>();
        _currentUserMock.Setup(u => u.UserId).Returns(TestUserId);
        _handler = new ConfirmClassificationHandler(_dbContext, _currentUserMock.Object);
    }

    [Fact]
    public async Task HandleAsync_ValidItem_CreatesConfirmedClassification()
    {
        var item = TestDataFactory.CreateReceiptItem();
        var receipt = TestDataFactory.CreateReceipt(id: item.ReceiptId);
        var file = TestDataFactory.CreateReceiptFile(id: receipt.ReceiptFileId);
        file.UserId = TestUserId;

        _dbContext.ReceiptFiles.Add(file);
        _dbContext.Receipts.Add(receipt);
        _dbContext.ReceiptItems.Add(item);
        await _dbContext.SaveChangesAsync();

        var command = new ConfirmClassificationCommand(item.Id, Category.WerbungskostenFachliteratur);

        var result = await _handler.HandleAsync(command);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Category.Should().Be("WerbungskostenFachliteratur");
        result.Value.Method.Should().Be("Manual");
        result.Value.Status.Should().Be("Confirmed");
    }

    [Fact]
    public async Task HandleAsync_ItemNotFound_ReturnsFailure()
    {
        var command = new ConfirmClassificationCommand(Guid.NewGuid(), Category.WerbungskostenFachliteratur);

        var result = await _handler.HandleAsync(command);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("nicht gefunden");
    }

    public void Dispose() => _dbContext.Dispose();
}
