using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using TaxReader.Application.DTOs;
using TaxReader.Application.Interfaces;
using TaxReader.Application.Jobs;
using TaxReader.Application.Mapping;
using TaxReader.Domain.Entities;
using TaxReader.Domain.Enums;
using TaxReader.Infrastructure.Data;
using TaxReader.UnitTests.Helpers;

namespace TaxReader.UnitTests.Pipeline;

/// <summary>
/// D-16: sum validation — ClassifyBatchJob sets Receipt.HasSumMismatch = true
/// when Math.Abs(itemsSum - receipt.TotalAmount) > 0.50m.
/// Also covers ReceiptDto.HasSumMismatch and DtoMappingExtensions.ToDto() wiring.
/// </summary>
public class SumValidationTests : IDisposable
{
    private static readonly Guid TestUserId = Guid.Parse("bbbbbbbb-cccc-dddd-eeee-ffffffffffff");
    private readonly AppDbContext _dbContext;
    private readonly Mock<IClassificationService> _classificationMock = new();
    private readonly ClassifyBatchJob _job;

    public SumValidationTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new AppDbContext(options);

        _job = new ClassifyBatchJob(
            _dbContext,
            _classificationMock.Object,
            Mock.Of<ILogger<ClassifyBatchJob>>());
    }

    private (ReceiptFile file, ProcessingRun run, Receipt receipt) SeedParsedFileWithItems(
        Guid uploadBatchId,
        decimal receiptTotal,
        decimal[] itemPrices)
    {
        var file = new ReceiptFile
        {
            Id = Guid.NewGuid(),
            UserId = TestUserId,
            OriginalFileName = $"file-{Guid.NewGuid():N}.pdf",
            ContentHash = Guid.NewGuid().ToString("N"),
            FileSize = 3,
            UploadedAt = DateTime.UtcNow,
            UploadBatchId = uploadBatchId,
            Status = FileStatus.Processing
        };

        var run = new ProcessingRun
        {
            Id = Guid.NewGuid(),
            ReceiptFileId = file.Id,
            Status = ProcessingStatus.Parsing,
            StartedAt = DateTime.UtcNow
        };

        var receipt = new Receipt
        {
            Id = Guid.NewGuid(),
            ReceiptFileId = file.Id,
            Vendor = "TestVendor",
            PurchaseDate = new DateOnly(2025, 6, 15),
            SubTotal = receiptTotal * 0.81m,
            TaxAmount = receiptTotal * 0.19m,
            TotalAmount = receiptTotal,
            Currency = "EUR",
            RawExtractedText = "test",
            ParsedAt = DateTime.UtcNow
        };

        foreach (var price in itemPrices)
        {
            receipt.Items.Add(new ReceiptItem
            {
                Id = Guid.NewGuid(),
                ReceiptId = receipt.Id,
                Description = "Test Item",
                Quantity = 1,
                UnitPrice = price,
                TotalPrice = price,
                LineNumber = 1
            });
        }

        _dbContext.ReceiptFiles.Add(file);
        _dbContext.ProcessingRuns.Add(run);
        _dbContext.Receipts.Add(receipt);
        _dbContext.SaveChanges();

        return (file, run, receipt);
    }

    [Fact]
    public async Task HandleAsync_ItemSumsMatchTotal_HasSumMismatchIsFalse()
    {
        // Arrange: receipt total = 30.00, items sum = 30.00 → no mismatch
        var batchId = Guid.NewGuid();
        var (_, _, _) = SeedParsedFileWithItems(batchId, receiptTotal: 30.00m, itemPrices: [10.00m, 20.00m]);

        _classificationMock
            .Setup(c => c.ClassifyItemsAsync(It.IsAny<IEnumerable<ReceiptItem>>(), TestUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        // Act
        await _job.HandleAsync(batchId, TestUserId, CancellationToken.None);

        // Assert
        var receipt = await _dbContext.Receipts.FirstAsync();
        receipt.HasSumMismatch.Should().BeFalse();
    }

    [Fact]
    public async Task HandleAsync_ItemSumsExceedToleranceDifference_HasSumMismatchIsTrue()
    {
        // Arrange: receipt total = 30.00, items sum = 10.00 → difference = 20.00 > 0.50
        var batchId = Guid.NewGuid();
        var (_, _, _) = SeedParsedFileWithItems(batchId, receiptTotal: 30.00m, itemPrices: [10.00m]);

        _classificationMock
            .Setup(c => c.ClassifyItemsAsync(It.IsAny<IEnumerable<ReceiptItem>>(), TestUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        // Act
        await _job.HandleAsync(batchId, TestUserId, CancellationToken.None);

        // Assert
        var receipt = await _dbContext.Receipts.FirstAsync();
        receipt.HasSumMismatch.Should().BeTrue();
    }

    [Fact]
    public async Task HandleAsync_ItemSumDifferenceExactlyAtTolerance_HasSumMismatchIsFalse()
    {
        // Arrange: receipt total = 30.00, items sum = 29.50 → difference = 0.50 → NOT > 0.50 → no mismatch
        var batchId = Guid.NewGuid();
        var (_, _, _) = SeedParsedFileWithItems(batchId, receiptTotal: 30.00m, itemPrices: [29.50m]);

        _classificationMock
            .Setup(c => c.ClassifyItemsAsync(It.IsAny<IEnumerable<ReceiptItem>>(), TestUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        // Act
        await _job.HandleAsync(batchId, TestUserId, CancellationToken.None);

        // Assert: D-16 tolerance is > 0.50m (strict), so exactly 0.50 difference is NOT a mismatch
        var receipt = await _dbContext.Receipts.FirstAsync();
        receipt.HasSumMismatch.Should().BeFalse();
    }

    [Fact]
    public async Task HandleAsync_ItemSumDifferenceJustOverTolerance_HasSumMismatchIsTrue()
    {
        // Arrange: receipt total = 30.00, items sum = 29.49 → difference = 0.51 > 0.50 → mismatch
        var batchId = Guid.NewGuid();
        var (_, _, _) = SeedParsedFileWithItems(batchId, receiptTotal: 30.00m, itemPrices: [29.49m]);

        _classificationMock
            .Setup(c => c.ClassifyItemsAsync(It.IsAny<IEnumerable<ReceiptItem>>(), TestUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        // Act
        await _job.HandleAsync(batchId, TestUserId, CancellationToken.None);

        // Assert
        var receipt = await _dbContext.Receipts.FirstAsync();
        receipt.HasSumMismatch.Should().BeTrue();
    }

    [Fact]
    public async Task HandleAsync_SumValidation_SavedInSameCallAsCompletedStatus()
    {
        // Arrange: we verify HasSumMismatch is saved by checking it after HandleAsync completes.
        // If it's saved in the same SaveChangesAsync as Completed, the value will be persisted.
        var batchId = Guid.NewGuid();
        var (_, _, _) = SeedParsedFileWithItems(batchId, receiptTotal: 30.00m, itemPrices: [10.00m]);

        _classificationMock
            .Setup(c => c.ClassifyItemsAsync(It.IsAny<IEnumerable<ReceiptItem>>(), TestUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        // Act
        await _job.HandleAsync(batchId, TestUserId, CancellationToken.None);

        // Assert: both Completed AND HasSumMismatch must be persisted after the same call
        var run = await _dbContext.ProcessingRuns.FirstAsync();
        run.Status.Should().Be(ProcessingStatus.Completed);

        var receipt = await _dbContext.Receipts.FirstAsync();
        receipt.HasSumMismatch.Should().BeTrue();
    }

    public void Dispose() => _dbContext.Dispose();
}

/// <summary>
/// D-16: ReceiptDto.HasSumMismatch field wiring via DtoMappingExtensions.ToDto().
/// </summary>
public class ReceiptDtoSumMismatchMappingTests
{
    [Fact]
    public void ToDto_Receipt_HasSumMismatchFalse_MapsFalse()
    {
        var entity = TestDataFactory.CreateReceipt(totalAmount: 30.00m);
        entity.HasSumMismatch = false;

        var dto = entity.ToDto();

        dto.HasSumMismatch.Should().BeFalse();
    }

    [Fact]
    public void ToDto_Receipt_HasSumMismatchTrue_MapsTrue()
    {
        var entity = TestDataFactory.CreateReceipt(totalAmount: 30.00m);
        entity.HasSumMismatch = true;

        var dto = entity.ToDto();

        dto.HasSumMismatch.Should().BeTrue();
    }

    [Fact]
    public void ReceiptDto_HasSumMismatchField_Exists()
    {
        // Structural test: ReceiptDto record has HasSumMismatch property
        var dto = new ReceiptDto(
            Id: Guid.NewGuid(),
            ReceiptFileId: Guid.NewGuid(),
            Vendor: "Test",
            PurchaseDate: new DateOnly(2025, 1, 1),
            SubTotal: 10m,
            TaxAmount: 1.9m,
            TotalAmount: 11.9m,
            Currency: "EUR",
            ParsedAt: DateTime.UtcNow,
            ItemCount: 0,
            SuggestedCount: 0,
            UnknownCount: 0,
            FailedCount: 0,
            HasSumMismatch: true,
            ExtractionSource: "Ocr");

        dto.HasSumMismatch.Should().BeTrue();
    }
}
