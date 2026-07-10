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

public class CorrectReceiptItemHandlerTests : IDisposable
{
    private static readonly Guid TestUserId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
    private static readonly Guid OtherUserId = Guid.Parse("ffffffff-1111-2222-3333-444444444444");

    private readonly AppDbContext _dbContext;
    private readonly Mock<ICurrentUser> _currentUserMock;
    private readonly Mock<IAuditLogger> _auditLoggerMock;
    private readonly CorrectReceiptItemHandler _handler;

    public CorrectReceiptItemHandlerTests()
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

        _handler = new CorrectReceiptItemHandler(_dbContext, _currentUserMock.Object, _auditLoggerMock.Object);
    }

    [Fact]
    public async Task HandleAsync_OwnerCorrectsItem_UpdatesFieldsAndRecordsAudit()
    {
        var item = TestDataFactory.CreateReceiptItem(description: "Old Description", unitPrice: 5.00m);
        var receipt = TestDataFactory.CreateReceipt(id: item.ReceiptId);
        var file = TestDataFactory.CreateReceiptFile(id: receipt.ReceiptFileId);
        file.UserId = TestUserId;

        _dbContext.ReceiptFiles.Add(file);
        _dbContext.Receipts.Add(receipt);
        _dbContext.ReceiptItems.Add(item);
        await _dbContext.SaveChangesAsync();

        var command = new CorrectReceiptItemCommand(item.Id, "New Description", 7.50m, 7.50m);

        var result = await _handler.HandleAsync(command);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Description.Should().Be("New Description");
        result.Value.UnitPrice.Should().Be(7.50m);
        result.Value.TotalPrice.Should().Be(7.50m);

        var reloaded = await _dbContext.ReceiptItems.FirstAsync(i => i.Id == item.Id);
        reloaded.Description.Should().Be("New Description");
        reloaded.UnitPrice.Should().Be(7.50m);
        reloaded.TotalPrice.Should().Be(7.50m);

        _auditLoggerMock.Verify(
            a => a.RecordAsync(
                AuditAction.ItemCorrected,
                TestUserId,
                TestUserId,
                It.Is<Dictionary<string, object?>>(m =>
                    (string)m["old_description"]! == "Old Description"
                    && (string)m["new_description"]! == "New Description"
                    && (decimal)m["old_unit_price"]! == 5.00m
                    && (decimal)m["new_unit_price"]! == 7.50m
                    && (decimal)m["old_total_price"]! == 5.00m
                    && (decimal)m["new_total_price"]! == 7.50m),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_OtherUsersItem_ReturnsNotFoundAndDoesNotMutateOrAudit()
    {
        var item = TestDataFactory.CreateReceiptItem(description: "Untouched", unitPrice: 5.00m);
        var receipt = TestDataFactory.CreateReceipt(id: item.ReceiptId);
        var file = TestDataFactory.CreateReceiptFile(id: receipt.ReceiptFileId);
        file.UserId = OtherUserId;

        _dbContext.ReceiptFiles.Add(file);
        _dbContext.Receipts.Add(receipt);
        _dbContext.ReceiptItems.Add(item);
        await _dbContext.SaveChangesAsync();

        var command = new CorrectReceiptItemCommand(item.Id, "Hacked Description", 999m, 999m);

        var result = await _handler.HandleAsync(command);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("nicht gefunden");

        var reloaded = await _dbContext.ReceiptItems.FirstAsync(i => i.Id == item.Id);
        reloaded.Description.Should().Be("Untouched");
        reloaded.UnitPrice.Should().Be(5.00m);

        _auditLoggerMock.Verify(
            a => a.RecordAsync(
                It.IsAny<AuditAction>(),
                It.IsAny<Guid?>(),
                It.IsAny<Guid?>(),
                It.IsAny<Dictionary<string, object?>>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_ItemDoesNotExist_ReturnsGenericNotFound()
    {
        var command = new CorrectReceiptItemCommand(Guid.NewGuid(), "Anything", 1m, 1m);

        var result = await _handler.HandleAsync(command);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("nicht gefunden");
    }

    public void Dispose() => _dbContext.Dispose();
}
