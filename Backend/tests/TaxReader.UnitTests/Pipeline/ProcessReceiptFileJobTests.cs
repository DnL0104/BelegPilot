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
    private readonly Mock<ITokenService> _tokenServiceMock = new();
    private readonly Mock<IReceiptVisionExtractor> _visionExtractorMock = new();
    private readonly Mock<IVisionFallbackSettings> _visionSettingsMock = new();
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
            _tokenServiceMock.Object,
            _visionExtractorMock.Object,
            _visionSettingsMock.Object,
            Mock.Of<ILogger<ProcessReceiptFileJob>>());

        // Default: blob store returns a fake PDF stream
        _blobStoreMock
            .Setup(s => s.OpenReadAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new MemoryStream(new byte[] { 1, 2, 3 }));

        // Default: vision extractor not configured — existing (pre-vision) tests must see
        // zero change in behavior; only the new vision-fallback tests below opt in.
        _visionExtractorMock.Setup(v => v.IsConfigured).Returns(false);
        _visionSettingsMock.Setup(s => s.RetryDelay).Returns(TimeSpan.Zero);
        _visionSettingsMock.Setup(s => s.CostPerVisionExtraction).Returns(1);
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
    public async Task HandleAsync_ExtractedTextContainsNulByte_StripsItBeforePersisting()
    {
        // Regression test: PostgreSQL text columns reject embedded NUL bytes (SqlState 22021),
        // which PdfPig/Tesseract can emit for glyphs their font/OCR mapping can't resolve. An
        // unsanitized NUL reaching SaveChangesAsync crashed the job and left the ReceiptFile
        // stuck (MarkFailedAsync's own save then failed identically on the still-tracked entity).
        var batchId = Guid.NewGuid();
        var (file, _) = SeedFile(batchId);
        _pdfExtractorMock
            .Setup(e => e.ExtractTextAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Amazon.de\0 Invoice 2025 Tinte EUR 9.99");
        _parserMock.Setup(p => p.CanParse(It.IsAny<string>(), It.IsAny<string?>())).Returns(true);
        _parserMock.Setup(p => p.Parse(It.IsAny<string>(), It.IsAny<ReceiptFile>()))
            .Returns((string text, ReceiptFile rf) =>
            {
                var r = Helpers.TestDataFactory.CreateReceipt(vendor: "Amazon", receiptFileId: rf.Id);
                r.RawExtractedText = text;
                return r;
            });

        await _job.HandleAsync(file.Id, batchId, batchSize: 1, CancellationToken.None);

        var savedReceipt = await _dbContext.Receipts.FirstAsync();
        savedReceipt.RawExtractedText.Should().NotContain("\0");
    }

    [Fact]
    public async Task HandleAsync_ReceiptAlreadyExists_SkipsReparsingAndStillRunsBarrier()
    {
        // Regression test for the manual/automatic retry feature: if a Receipt already
        // exists for this file (parsing already succeeded on a prior attempt — e.g. the
        // barrier step below threw and Hangfire or the user retried), re-running must
        // NOT re-extract/re-parse (would violate the unique index on
        // receipts.receipt_file_id and mark an already-successful parse as Failed). It
        // must still perform the barrier check so the batch can progress to classify.
        //
        // Seeded at Pending (not Parsing) deliberately — this is the real shape of the
        // upload-enqueue race a prior fix addressed: a Receipt gets created but the run
        // never advances past Pending. Live-testing the retry endpoint against exactly
        // this scenario first surfaced that the barrier query only counts
        // Status >= Parsing, so without bumping the status here a manually-retried file
        // would silently never satisfy the batch barrier despite its Receipt existing.
        var batchId = Guid.NewGuid();
        var (file, run) = SeedFile(batchId, runStatus: ProcessingStatus.Pending);
        _dbContext.Receipts.Add(Helpers.TestDataFactory.CreateReceipt(receiptFileId: file.Id));
        _dbContext.SaveChanges();
        SetupSuccessfulPipeline(); // would only matter if re-parsing were (incorrectly) attempted

        await _job.HandleAsync(file.Id, batchId, batchSize: 1, CancellationToken.None);

        _pdfExtractorMock.Verify(
            e => e.ExtractTextAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()),
            Times.Never, "an already-parsed file must not be re-extracted/re-parsed");

        (await _dbContext.Receipts.CountAsync(r => r.ReceiptFileId == file.Id)).Should().Be(1,
            "retrying an already-parsed file must never create a duplicate Receipt");

        // Barrier still ran: this is the only (and thus last) parent in the batch.
        _jobClientMock.Verify(
            c => c.EnqueueAsync<ClassifyBatchJob>(
                It.IsAny<Expression<Func<ClassifyBatchJob, Task>>>(),
                It.IsAny<CancellationToken>()),
            Times.Once,
            "a Receipt already existing must count as done for the barrier even though the run started below Parsing");

        var savedRun = await _dbContext.ProcessingRuns.FirstAsync(r => r.Id == run.Id);
        savedRun.Status.Should().Be(ProcessingStatus.Parsing,
            "bumped up from Pending so the barrier's Status >= Parsing check counts this run");
    }

    [Fact]
    public async Task HandleAsync_BarrierWithCompletedSiblings_StillCountsThemAndFiresClassify()
    {
        // Regression test: ProcessingRun.Status is persisted as a string
        // (HasConversion<string>()), so the barrier's "r.Status >= ProcessingStatus.Parsing"
        // compiles fine but translates to a SQL string comparison — under which
        // "Completed" sorts BEFORE "Parsing" lexicographically (C < P), silently excluding
        // already-completed siblings from the count. This never surfaced on the plain
        // happy path (every sibling sits at exactly "Parsing" when the barrier runs there),
        // but breaks the moment one file in a batch is retried after its siblings already
        // finished — exactly the shape of a real user's stuck-upload batch (2 files stuck
        // at Pending, 1 sibling already Completed).
        var batchId = Guid.NewGuid();
        SeedFile(batchId, fileName: "sibling-a.pdf", runStatus: ProcessingStatus.Completed);
        SeedFile(batchId, fileName: "sibling-b.pdf", runStatus: ProcessingStatus.Completed);
        var (file, _) = SeedFile(batchId, fileName: "retried.pdf", runStatus: ProcessingStatus.Pending);
        SetupSuccessfulPipeline();

        await _job.HandleAsync(file.Id, batchId, batchSize: 3, CancellationToken.None);

        _jobClientMock.Verify(
            c => c.EnqueueAsync<ClassifyBatchJob>(
                It.IsAny<Expression<Func<ClassifyBatchJob, Task>>>(),
                It.IsAny<CancellationToken>()),
            Times.Once,
            "2 already-Completed siblings + this newly-Parsing run must satisfy batchSize=3");
    }

    [Fact]
    public async Task HandleAsync_ReceiptFileNotFound_ThrowsForHangfireRetry()
    {
        // Regression test: a fast Hangfire worker can dequeue and execute this job
        // before the enqueueing request's SaveChangesAsync commits the row. Silently
        // returning here let Hangfire mark the job Succeeded despite doing nothing,
        // leaving the file stuck forever with zero error. Throwing lets
        // [AutomaticRetry] give the commit time to land.
        var act = () => _job.HandleAsync(Guid.NewGuid(), Guid.NewGuid(), 1, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
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

    // ── Vision fallback (05-03, VIS-02/VIS-03/VIS-04) ──────────────────────

    private void SetupVisionSucceeds(string vendor = "REWE", string description = "Kaffee", decimal price = 4.99m)
    {
        _visionExtractorMock.Setup(v => v.IsConfigured).Returns(true);
        _tokenServiceMock
            .Setup(t => t.TryConsumeManyAsync(It.IsAny<IReadOnlyList<TokenLedgerEntry>>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _visionExtractorMock
            .Setup(v => v.ExtractAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VisionExtractionResult(vendor, [new VisionExtractedItem(description, price)], price));
    }

    [Fact]
    public async Task HandleAsync_NoTextExtractedImage_VisionSucceeds_PersistsVisionReceipt()
    {
        var batchId = Guid.NewGuid();
        var (file, run) = SeedFile(batchId, fileName: "test.jpg");
        _imageExtractorMock
            .Setup(e => e.ExtractTextAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(string.Empty);
        SetupVisionSucceeds();

        await _job.HandleAsync(file.Id, batchId, batchSize: 1, CancellationToken.None);

        var savedRun = await _dbContext.ProcessingRuns.FirstAsync(r => r.Id == run.Id);
        savedRun.Status.Should().NotBe(ProcessingStatus.Failed);

        var savedReceipt = await _dbContext.Receipts.FirstAsync(r => r.ReceiptFileId == file.Id);
        savedReceipt.ExtractionSource.Should().Be(ExtractionSource.Vision);
        savedReceipt.Items.Should().ContainSingle(i => i.Description == "Kaffee" && i.Quantity == 1);

        _visionExtractorMock.Verify(
            v => v.ExtractAsync(It.IsAny<Stream>(), It.IsAny<string>(), false, It.IsAny<CancellationToken>()),
            Times.Once, "an image file must route through the image content-block path (isPdf=false)");
    }

    [Fact]
    public async Task HandleAsync_NoTextExtractedPdf_VisionSucceeds_UsesDocumentContentBlock()
    {
        var batchId = Guid.NewGuid();
        var (file, run) = SeedFile(batchId, fileName: "scanned.pdf");
        _pdfExtractorMock
            .Setup(e => e.ExtractTextAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(string.Empty);
        SetupVisionSucceeds();

        await _job.HandleAsync(file.Id, batchId, batchSize: 1, CancellationToken.None);

        var savedRun = await _dbContext.ProcessingRuns.FirstAsync(r => r.Id == run.Id);
        savedRun.Status.Should().NotBe(ProcessingStatus.Failed);

        var savedReceipt = await _dbContext.Receipts.FirstAsync(r => r.ReceiptFileId == file.Id);
        savedReceipt.ExtractionSource.Should().Be(ExtractionSource.Vision);

        _visionExtractorMock.Verify(
            v => v.ExtractAsync(It.IsAny<Stream>(), It.IsAny<string>(), true, It.IsAny<CancellationToken>()),
            Times.Once, "a scanned PDF with no text layer must route through the document content-block path (isPdf=true)");
    }

    [Fact]
    public async Task HandleAsync_NoMatchingParser_VisionSucceeds_PersistsVisionReceipt()
    {
        var batchId = Guid.NewGuid();
        var (file, run) = SeedFile(batchId);
        _pdfExtractorMock
            .Setup(e => e.ExtractTextAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("some unrecognized text");
        _parserMock.Setup(p => p.CanParse(It.IsAny<string>(), It.IsAny<string?>())).Returns(false);
        SetupVisionSucceeds();

        await _job.HandleAsync(file.Id, batchId, batchSize: 1, CancellationToken.None);

        var savedRun = await _dbContext.ProcessingRuns.FirstAsync(r => r.Id == run.Id);
        savedRun.Status.Should().NotBe(ProcessingStatus.Failed);

        var savedReceipt = await _dbContext.Receipts.FirstAsync(r => r.ReceiptFileId == file.Id);
        savedReceipt.ExtractionSource.Should().Be(ExtractionSource.Vision);
    }

    [Fact]
    public async Task HandleAsync_CleanParse_NeverInvokesVisionExtractor()
    {
        var batchId = Guid.NewGuid();
        var (file, run) = SeedFile(batchId);
        SetupSuccessfulPipeline();
        SetupVisionSucceeds(); // configured + would succeed if called — proves it's simply never invoked

        await _job.HandleAsync(file.Id, batchId, batchSize: 1, CancellationToken.None);

        _visionExtractorMock.Verify(
            v => v.ExtractAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()),
            Times.Never, "vision must never fire when OCR+parser already succeed (cost containment, VIS-03)");

        var savedReceipt = await _dbContext.Receipts.FirstAsync(r => r.ReceiptFileId == file.Id);
        savedReceipt.ExtractionSource.Should().Be(ExtractionSource.Ocr);
    }

    [Fact]
    public async Task HandleAsync_VisionExtractionFailsTwice_RefundsTokensAndStaysFailed()
    {
        var batchId = Guid.NewGuid();
        var (file, run) = SeedFile(batchId);
        _pdfExtractorMock
            .Setup(e => e.ExtractTextAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(string.Empty);
        _visionExtractorMock.Setup(v => v.IsConfigured).Returns(true);
        _tokenServiceMock
            .Setup(t => t.TryConsumeManyAsync(It.IsAny<IReadOnlyList<TokenLedgerEntry>>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _visionExtractorMock
            .Setup(v => v.ExtractAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("vision call failed"));

        await _job.HandleAsync(file.Id, batchId, batchSize: 1, CancellationToken.None);

        var savedRun = await _dbContext.ProcessingRuns.FirstAsync(r => r.Id == run.Id);
        savedRun.Status.Should().Be(ProcessingStatus.Failed);
        savedRun.ErrorCode.Should().Be("NoTextExtracted");

        _visionExtractorMock.Verify(
            v => v.ExtractAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2), "the retry-once-then-degrade wrapper must attempt exactly twice before giving up");
        _tokenServiceMock.Verify(
            t => t.RefundManyAsync(It.IsAny<IReadOnlyList<TokenLedgerEntry>>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Once, "the pre-charged vision tokens must be refunded when vision exhausts its retry budget");

        (await _dbContext.Receipts.CountAsync(r => r.ReceiptFileId == file.Id)).Should().Be(0);
    }

    [Fact]
    public async Task HandleAsync_InsufficientTokens_SkipsVisionAndStaysFailed()
    {
        var batchId = Guid.NewGuid();
        var (file, run) = SeedFile(batchId);
        _pdfExtractorMock
            .Setup(e => e.ExtractTextAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(string.Empty);
        _visionExtractorMock.Setup(v => v.IsConfigured).Returns(true);
        _tokenServiceMock
            .Setup(t => t.TryConsumeManyAsync(It.IsAny<IReadOnlyList<TokenLedgerEntry>>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        await _job.HandleAsync(file.Id, batchId, batchSize: 1, CancellationToken.None);

        var savedRun = await _dbContext.ProcessingRuns.FirstAsync(r => r.Id == run.Id);
        savedRun.Status.Should().Be(ProcessingStatus.Failed);
        savedRun.ErrorCode.Should().Be("NoTextExtracted");

        _visionExtractorMock.Verify(
            v => v.ExtractAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()),
            Times.Never, "the extractor must never be called when the token pre-charge gate fails");
    }

    public void Dispose() => _dbContext.Dispose();
}
