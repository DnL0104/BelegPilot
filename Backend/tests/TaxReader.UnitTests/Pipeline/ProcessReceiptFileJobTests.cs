using System.Linq.Expressions;
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
/// Behavioural tests for the per-file ProcessReceiptFileJob (D-01 parent job).
/// Verifies the extract → parse → barrier-enqueue flow without spinning up a real
/// Hangfire server (the IBackgroundJobClient port lets us mock the enqueue side
/// without touching Hangfire-storage).
/// </summary>
public class ProcessReceiptFileJobTests : IDisposable
{
    private static readonly Guid TestUserId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
    private readonly AppDbContext _dbContext;
    private readonly Mock<IPdfTextExtractor> _pdfExtractorMock = new();
    private readonly Mock<IImageTextExtractor> _imageExtractorMock = new();
    private readonly Mock<IReceiptParser> _parserMock = new();
    private readonly Mock<IBackgroundJobClient> _jobClientMock = new();
    private readonly Mock<IUploadBlobStore> _blobStoreMock = new();
    private readonly ProcessReceiptFileJob _job;

    public ProcessReceiptFileJobTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new AppDbContext(options);

        _job = new ProcessReceiptFileJob(
            _dbContext,
            _pdfExtractorMock.Object,
            _imageExtractorMock.Object,
            new[] { _parserMock.Object },
            _jobClientMock.Object,
            _blobStoreMock.Object,
            Mock.Of<ILogger<ProcessReceiptFileJob>>());

        // Default: blob store returns a fake PDF stream
        _blobStoreMock
            .Setup(s => s.OpenReadAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new MemoryStream(new byte[] { 1, 2, 3 }));
    }

    private (ReceiptFile, ProcessingRun) SeedFile(
        Guid uploadBatchId,
        string fileName = "test.pdf",
        ProcessingStatus runStatus = ProcessingStatus.Queued)
    {
        var file = new ReceiptFile
        {
            Id = Guid.NewGuid(),
            UserId = TestUserId,
            OriginalFileName = fileName,
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
            Status = runStatus,
            StartedAt = DateTime.UtcNow
        };
        _dbContext.ReceiptFiles.Add(file);
        _dbContext.ProcessingRuns.Add(run);
        _dbContext.SaveChanges();
        return (file, run);
    }

    private void SetupSuccessfulPipeline(string vendor = "Amazon")
    {
        _pdfExtractorMock
            .Setup(e => e.ExtractTextAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Amazon.de Invoice 2025 Tinte EUR 9.99");
        _parserMock.Setup(p => p.CanParse(It.IsAny<string>(), It.IsAny<string?>())).Returns(true);
        _parserMock.Setup(p => p.Parse(It.IsAny<string>(), It.IsAny<ReceiptFile>()))
            .Returns((string text, ReceiptFile rf) =>
            {
                var r = Helpers.TestDataFactory.CreateReceipt(vendor: vendor, receiptFileId: rf.Id);
                r.Items.Add(Helpers.TestDataFactory.CreateReceiptItem(receiptId: r.Id));
                return r;
            });
    }

    [Fact]
    public async Task HandleAsync_PdfFile_ExtractsParsesAndPersists()
    {
        var batchId = Guid.NewGuid();
        var (file, run) = SeedFile(batchId);
        SetupSuccessfulPipeline();

        await _job.HandleAsync(file.Id, batchId, batchSize: 1, CancellationToken.None);

        var savedRun = await _dbContext.ProcessingRuns.FirstAsync(r => r.Id == run.Id);
        savedRun.Status.Should().Be(ProcessingStatus.Parsing);

        var savedReceipt = await _dbContext.Receipts.FirstAsync();
        savedReceipt.ReceiptFileId.Should().Be(file.Id);
        savedReceipt.Vendor.Should().Be("Amazon");
    }

    [Fact]
    public async Task HandleAsync_LastInBatch_EnqueuesClassifyBatchJob()
    {
        var batchId = Guid.NewGuid();
        var (file, _) = SeedFile(batchId);
        SetupSuccessfulPipeline();

        await _job.HandleAsync(file.Id, batchId, batchSize: 1, CancellationToken.None);

        _jobClientMock.Verify(
            c => c.EnqueueAsync<ClassifyBatchJob>(
                It.IsAny<Expression<Func<ClassifyBatchJob, Task>>>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_NotLastInBatch_DoesNotEnqueueClassifyBatchJob()
    {
        var batchId = Guid.NewGuid();
        var (file1, _) = SeedFile(batchId);
        SeedFile(batchId); // Sibling — still in Queued, hasn't started Parsing
        SetupSuccessfulPipeline();

        await _job.HandleAsync(file1.Id, batchId, batchSize: 2, CancellationToken.None);

        // Only 1 of 2 files reached Parsing — barrier not yet met.
        _jobClientMock.Verify(
            c => c.EnqueueAsync<ClassifyBatchJob>(
                It.IsAny<Expression<Func<ClassifyBatchJob, Task>>>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_CancelledRun_ExitsEarly()
    {
        var batchId = Guid.NewGuid();
        var (file, _) = SeedFile(batchId, runStatus: ProcessingStatus.Cancelled);

        await _job.HandleAsync(file.Id, batchId, batchSize: 1, CancellationToken.None);

        // No extraction attempted.
        _pdfExtractorMock.Verify(
            e => e.ExtractTextAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()),
            Times.Never);

        // No classify enqueue.
        _jobClientMock.Verify(
            c => c.EnqueueAsync<ClassifyBatchJob>(
                It.IsAny<Expression<Func<ClassifyBatchJob, Task>>>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_EmptyExtractedText_MarksFailedAndSetsErrorCode()
    {
        var batchId = Guid.NewGuid();
        var (file, run) = SeedFile(batchId);
        _pdfExtractorMock
            .Setup(e => e.ExtractTextAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(string.Empty);

        await _job.HandleAsync(file.Id, batchId, batchSize: 1, CancellationToken.None);

        var savedRun = await _dbContext.ProcessingRuns.FirstAsync(r => r.Id == run.Id);
        savedRun.Status.Should().Be(ProcessingStatus.Failed);
        savedRun.ErrorCode.Should().Be("NoTextExtracted");
        savedRun.ErrorMessage.Should().Contain("kein Text");
    }

    [Fact]
    public async Task HandleAsync_NoMatchingParser_MarksFailedWithParserMissing()
    {
        var batchId = Guid.NewGuid();
        var (file, run) = SeedFile(batchId);
        _pdfExtractorMock
            .Setup(e => e.ExtractTextAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("some text");
        _parserMock.Setup(p => p.CanParse(It.IsAny<string>(), It.IsAny<string?>())).Returns(false);

        await _job.HandleAsync(file.Id, batchId, batchSize: 1, CancellationToken.None);

        var savedRun = await _dbContext.ProcessingRuns.FirstAsync(r => r.Id == run.Id);
        savedRun.Status.Should().Be(ProcessingStatus.Failed);
        savedRun.ErrorCode.Should().Be("ParserMissing");
    }

    [Fact]
    public async Task HandleAsync_NoContent_MarksFailedWithNoContent()
    {
        var batchId = Guid.NewGuid();
        var (file, run) = SeedFile(batchId);
        _blobStoreMock
            .Setup(s => s.OpenReadAsync(file.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Stream?)null);

        await _job.HandleAsync(file.Id, batchId, batchSize: 1, CancellationToken.None);

        var savedRun = await _dbContext.ProcessingRuns.FirstAsync(r => r.Id == run.Id);
        savedRun.Status.Should().Be(ProcessingStatus.Failed);
        savedRun.ErrorCode.Should().Be("NoContent");
    }

    public void Dispose() => _dbContext.Dispose();
}
