using System.Linq.Expressions;
using System.Net.Http;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using TaxReader.Application.Common;
using TaxReader.Application.Exceptions;
using TaxReader.Application.Interfaces;
using TaxReader.Application.Jobs;
using TaxReader.Domain.Entities;
using TaxReader.Domain.Enums;
using TaxReader.Infrastructure.Data;

namespace TaxReader.UnitTests.Pipeline;

/// <summary>
/// Proves that raw exception messages never appear in ProcessingRun columns.
/// D-21 invariant: catalog-controlled German strings are the only text that flows
/// into processing_runs.error_message; raw ex.Message / ex.StackTrace stays in Serilog.
/// </summary>
[Collection(PipelineTestCollection.Name)]
public class JobErrorLeakageTests : IDisposable
{
    private static readonly Guid TestUserId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
    private readonly AppDbContext _dbContext;
    private readonly Mock<IPdfTextExtractor> _pdfExtractorMock = new();
    private readonly Mock<IImageTextExtractor> _imageExtractorMock = new();
    private readonly Mock<IReceiptParser> _parserMock = new();
    private readonly Mock<IBackgroundJobClient> _jobClientMock = new();
    private readonly Mock<IUploadBlobStore> _blobStoreMock = new();
    private readonly Mock<IClassificationService> _classificationMock = new();

    public JobErrorLeakageTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new AppDbContext(options);

        _blobStoreMock
            .Setup(s => s.OpenReadAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new MemoryStream(new byte[] { 1, 2, 3 }));
    }

    private ProcessReceiptFileJob CreateProcessJob() =>
        new ProcessReceiptFileJob(
            _dbContext,
            _pdfExtractorMock.Object,
            _imageExtractorMock.Object,
            new[] { _parserMock.Object },
            _jobClientMock.Object,
            _blobStoreMock.Object,
            Mock.Of<ILogger<ProcessReceiptFileJob>>());

    private ClassifyBatchJob CreateClassifyJob() =>
        new ClassifyBatchJob(
            _dbContext,
            _classificationMock.Object,
            Mock.Of<ILogger<ClassifyBatchJob>>());

    private (ReceiptFile, ProcessingRun) SeedFile(Guid uploadBatchId, ProcessingStatus runStatus = ProcessingStatus.Queued)
    {
        var file = new ReceiptFile
        {
            Id = Guid.NewGuid(),
            UserId = TestUserId,
            OriginalFileName = "test.pdf",
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

    [Fact]
    public async Task ProcessReceiptFileJob_NoTextExtractedException_DoesNotLeakRawMessage()
    {
        var batchId = Guid.NewGuid();
        var (file, run) = SeedFile(batchId);

        // Extractor throws with an internal detail
        _pdfExtractorMock
            .Setup(e => e.ExtractTextAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NoTextExtractedException("internal detail leak attempt"));

        var job = CreateProcessJob();
        var act = async () => await job.HandleAsync(file.Id, batchId, 1, CancellationToken.None);
        await act.Should().ThrowAsync<NoTextExtractedException>();

        var savedRun = await _dbContext.ProcessingRuns.FirstAsync(r => r.Id == run.Id);
        savedRun.Status.Should().Be(ProcessingStatus.Failed);
        savedRun.ErrorCode.Should().Be(UploadErrorCatalog.CodeNoTextExtracted);
        savedRun.ErrorMessage.Should().Contain("Aus diesem Dokument");
        savedRun.ErrorMessage.Should().NotContain("internal detail leak attempt",
            "raw exception message must not appear in ProcessingRun.ErrorMessage");
    }

    [Fact]
    public async Task ClassifyBatchJob_HttpRequestException_DoesNotLeakRawMessage()
    {
        var batchId = Guid.NewGuid();
        var (file, _) = SeedParsedFile(batchId);

        _classificationMock
            .Setup(c => c.ClassifyItemsAsync(It.IsAny<IEnumerable<ReceiptItem>>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Connection refused at 10.0.0.5:443"));

        var job = CreateClassifyJob();
        var act = async () => await job.HandleAsync(batchId, TestUserId, CancellationToken.None);
        await act.Should().ThrowAsync<HttpRequestException>();

        var savedRun = await _dbContext.ProcessingRuns.FirstAsync(r => r.ReceiptFile.UploadBatchId == batchId);
        savedRun.ErrorCode.Should().Be(UploadErrorCatalog.CodeAiUnavailable);
        savedRun.ErrorMessage.Should().Contain("Die Klassifizierung ist vorübergehend nicht verfügbar");
        savedRun.ErrorMessage.Should().NotContain("10.0.0.5",
            "internal IP address must not appear in ProcessingRun.ErrorMessage");
    }

    [Fact]
    public async Task ClassifyBatchJob_Cancellation_MarksRunsCancelledAndSuppressesRethrow()
    {
        var batchId = Guid.NewGuid();
        SeedParsedFile(batchId);

        using var cts = new CancellationTokenSource();

        // Simulate cancellation happening DURING the classify call (not at job entry).
        // The token is not yet cancelled when the job starts so EF queries succeed.
        _classificationMock
            .Setup(c => c.ClassifyItemsAsync(It.IsAny<IEnumerable<ReceiptItem>>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Callback(() => cts.Cancel()) // cancel during the call
            .ThrowsAsync(new OperationCanceledException());

        var job = CreateClassifyJob();
        // With cancellationRequested=true (after the cancel), the job should NOT re-throw
        await job.HandleAsync(batchId, TestUserId, cts.Token);

        var savedRun = await _dbContext.ProcessingRuns.FirstAsync(r => r.ReceiptFile.UploadBatchId == batchId);
        savedRun.Status.Should().Be(ProcessingStatus.Cancelled);
        savedRun.ErrorCode.Should().Be(UploadErrorCatalog.CodeCancelled);
        savedRun.ErrorMessage.Should().Be("Vorgang abgebrochen.");
    }

    [Fact]
    public async Task ProcessReceiptFileJob_GenericException_DoesNotLeakInternalDetails()
    {
        var batchId = Guid.NewGuid();
        var (file, run) = SeedFile(batchId);

        _pdfExtractorMock
            .Setup(e => e.ExtractTextAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("DB connection ChM-90s-T1 expired"));

        var job = CreateProcessJob();
        var act = async () => await job.HandleAsync(file.Id, batchId, 1, CancellationToken.None);
        await act.Should().ThrowAsync<Exception>();

        var savedRun = await _dbContext.ProcessingRuns.FirstAsync(r => r.Id == run.Id);
        savedRun.ErrorCode.Should().Be(UploadErrorCatalog.CodeUnknown);
        savedRun.ErrorMessage.Should().Contain("Verarbeitung fehlgeschlagen");
        savedRun.ErrorMessage.Should().NotContain("ChM-90s-T1",
            "internal exception detail must not appear in ProcessingRun.ErrorMessage");
    }

    [Fact]
    public void NoRawMessageInJobSources_ProcessReceiptFileJob()
    {
        // Structural regex: verify no line in ProcessReceiptFileJob writes .Message to ErrorMessage
        var src = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "src", "TaxReader.Application", "Jobs", "ProcessReceiptFileJob.cs"));

        // Check for patterns like: run.ErrorMessage = ex.Message  OR  run.ErrorMessage = e.Message etc.
        src.Should().NotMatchRegex(
            @"ErrorMessage\s*=\s*\w+\.Message",
            "raw exception message must not be directly assigned to ProcessingRun.ErrorMessage");
    }

    [Fact]
    public void NoRawMessageInJobSources_ClassifyBatchJob()
    {
        var src = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "src", "TaxReader.Application", "Jobs", "ClassifyBatchJob.cs"));

        src.Should().NotMatchRegex(
            @"ErrorMessage\s*=\s*\w+\.Message",
            "raw exception message must not be directly assigned to ProcessingRun.ErrorMessage");
    }

    // ── helpers ──────────────────────────────────────────────────────

    private (ReceiptFile, ProcessingRun) SeedParsedFile(Guid uploadBatchId)
    {
        var file = new ReceiptFile
        {
            Id = Guid.NewGuid(),
            UserId = TestUserId,
            OriginalFileName = "test.pdf",
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
        receipt.Items.Add(Helpers.TestDataFactory.CreateReceiptItem(receiptId: receipt.Id));

        _dbContext.ReceiptFiles.Add(file);
        _dbContext.ProcessingRuns.Add(run);
        _dbContext.Receipts.Add(receipt);
        _dbContext.SaveChanges();
        return (file, run);
    }

    public void Dispose() => _dbContext.Dispose();
}
