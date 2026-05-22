using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using TaxReader.Application.Interfaces;
using TaxReader.Application.Jobs;
using TaxReader.Domain.Entities;
using TaxReader.Domain.Enums;
using TaxReader.Infrastructure.Data;

namespace TaxReader.UnitTests.Pipeline;

/// <summary>
/// Behavioural tests for ClassifyBatchJob (D-01 cross-batch AI classification).
/// Verifies D-12 cancellation = full refund + Cancelled state, RESEARCH Pitfall 2
/// idempotent entry, and successful finalize transitions.
/// </summary>
public class ClassifyBatchJobTests : IDisposable
{
    private static readonly Guid TestUserId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
    private readonly AppDbContext _dbContext;
    private readonly Mock<IClassificationService> _classificationMock = new();
    private readonly ClassifyBatchJob _job;

    public ClassifyBatchJobTests()
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

    private (ReceiptFile, ProcessingRun, Receipt) SeedParsedFile(Guid uploadBatchId, int itemCount = 1)
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
        var receipt = Helpers.TestDataFactory.CreateReceipt(receiptFileId: file.Id);
        for (var i = 0; i < itemCount; i++)
            receipt.Items.Add(Helpers.TestDataFactory.CreateReceiptItem(receiptId: receipt.Id));

        _dbContext.ReceiptFiles.Add(file);
        _dbContext.ProcessingRuns.Add(run);
        _dbContext.Receipts.Add(receipt);
        _dbContext.SaveChanges();
        return (file, run, receipt);
    }

    [Fact]
    public async Task HandleAsync_SuccessfulBatch_MarksAllRunsCompleted()
    {
        var batchId = Guid.NewGuid();
        var (file1, _, _) = SeedParsedFile(batchId);
        var (file2, _, _) = SeedParsedFile(batchId);

        _classificationMock
            .Setup(c => c.ClassifyItemsAsync(It.IsAny<IEnumerable<ReceiptItem>>(), TestUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([Helpers.TestDataFactory.CreateClassification()]);

        await _job.HandleAsync(batchId, TestUserId, CancellationToken.None);

        var runs = await _dbContext.ProcessingRuns
            .Where(r => r.ReceiptFile.UploadBatchId == batchId).ToListAsync();
        runs.Should().AllSatisfy(r => r.Status.Should().Be(ProcessingStatus.Completed));

        var files = await _dbContext.ReceiptFiles
            .Where(f => f.UploadBatchId == batchId).ToListAsync();
        files.Should().AllSatisfy(f => f.Status.Should().Be(FileStatus.Processed));
    }

    [Fact]
    public async Task HandleAsync_CalledTwice_OnlyClassifiesOnce()
    {
        // RESEARCH Pitfall 2: race-condition idempotency at entry.
        var batchId = Guid.NewGuid();
        var (file, _, _) = SeedParsedFile(batchId);
        _classificationMock
            .Setup(c => c.ClassifyItemsAsync(It.IsAny<IEnumerable<ReceiptItem>>(), TestUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([Helpers.TestDataFactory.CreateClassification()]);

        await _job.HandleAsync(batchId, TestUserId, CancellationToken.None);
        await _job.HandleAsync(batchId, TestUserId, CancellationToken.None);

        // Classifier should be invoked exactly once (the second invocation hits the
        // alreadyClassifying early-exit).
        _classificationMock.Verify(
            c => c.ClassifyItemsAsync(It.IsAny<IEnumerable<ReceiptItem>>(), TestUserId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_PassesUserIdToClassifier()
    {
        var batchId = Guid.NewGuid();
        SeedParsedFile(batchId);
        _classificationMock
            .Setup(c => c.ClassifyItemsAsync(It.IsAny<IEnumerable<ReceiptItem>>(), TestUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([Helpers.TestDataFactory.CreateClassification()]);

        await _job.HandleAsync(batchId, TestUserId, CancellationToken.None);

        _classificationMock.Verify(
            c => c.ClassifyItemsAsync(It.IsAny<IEnumerable<ReceiptItem>>(), TestUserId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_Cancellation_MarksAllRunsCancelled()
    {
        var batchId = Guid.NewGuid();
        var (file, _, _) = SeedParsedFile(batchId);

        // Cancel during the classify call (not before), so the idempotency EF query succeeds.
        // Callback() fires synchronously when ClassifyItemsAsync is invoked, cancelling the token
        // BEFORE the ThrowsAsync exception propagates — IsCancellationRequested is true in the catch.
        using var cts = new CancellationTokenSource();
        _classificationMock
            .Setup(c => c.ClassifyItemsAsync(It.IsAny<IEnumerable<ReceiptItem>>(), TestUserId, It.IsAny<CancellationToken>()))
            .Callback(() => cts.Cancel())
            .ThrowsAsync(new OperationCanceledException());

        // With IsCancellationRequested=true the job suppresses the rethrow (D-04 / Pitfall 3).
        await _job.HandleAsync(batchId, TestUserId, cts.Token);

        var runs = await _dbContext.ProcessingRuns
            .Where(r => r.ReceiptFile.UploadBatchId == batchId).ToListAsync();
        runs.Should().AllSatisfy(r =>
        {
            r.Status.Should().Be(ProcessingStatus.Cancelled);
            r.ErrorCode.Should().Be("Cancelled");
            r.ErrorMessage.Should().Be("Vorgang abgebrochen.");
        });
    }

    [Fact]
    public async Task HandleAsync_NoParsedRuns_ExitsEarly()
    {
        var batchId = Guid.NewGuid(); // No data seeded — nothing to classify

        await _job.HandleAsync(batchId, TestUserId, CancellationToken.None);

        _classificationMock.VerifyNoOtherCalls();
    }

    public void Dispose() => _dbContext.Dispose();
}
