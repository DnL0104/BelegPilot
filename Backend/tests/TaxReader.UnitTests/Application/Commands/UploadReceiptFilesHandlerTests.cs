using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using TaxReader.Application.Commands;
using TaxReader.Application.DTOs;
using TaxReader.Application.Interfaces;
using TaxReader.Domain.Entities;
using TaxReader.Domain.Enums;
using TaxReader.Infrastructure.Data;
using TaxReader.UnitTests.Helpers;

namespace TaxReader.UnitTests.Application.Commands;

public class UploadReceiptFilesHandlerTests : IDisposable
{
    private static readonly Guid TestUserId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");

    private readonly AppDbContext _dbContext;
    private readonly Mock<IPdfTextExtractor> _pdfExtractorMock;
    private readonly Mock<IImageTextExtractor> _imageExtractorMock;
    private readonly Mock<IReceiptParser> _parserMock;
    private readonly Mock<IClassificationService> _classificationMock;
    private readonly Mock<ICurrentUser> _currentUserMock;
    private readonly UploadReceiptFilesHandler _handler;

    public UploadReceiptFilesHandlerTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContext = new AppDbContext(options);
        _pdfExtractorMock = new Mock<IPdfTextExtractor>();
        _imageExtractorMock = new Mock<IImageTextExtractor>();
        _parserMock = new Mock<IReceiptParser>();
        _classificationMock = new Mock<IClassificationService>();
        _currentUserMock = new Mock<ICurrentUser>();
        _currentUserMock.Setup(u => u.UserId).Returns(TestUserId);

        _handler = new UploadReceiptFilesHandler(
            _dbContext,
            _pdfExtractorMock.Object,
            _imageExtractorMock.Object,
            new[] { _parserMock.Object },
            _classificationMock.Object,
            _currentUserMock.Object,
            Mock.Of<ILogger<UploadReceiptFilesHandler>>());
    }

    [Fact]
    public async Task HandleAsync_ValidPdf_ProcessesAndReturnsReceiptDto()
    {
        SetupSuccessfulPipeline("Amazon.de Invoice 2025 Tinte EUR 9.99", "Amazon");

        var command = MakeCommand("test.pdf");
        var result = await _handler.HandleAsync(command);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Successful.Should().HaveCount(1);
        result.Value!.Failed.Should().BeEmpty();
        result.Value!.Successful[0].FileName.Should().Be("test.pdf");
        result.Value!.Successful[0].Receipt.Vendor.Should().Be("Amazon");

        var savedFile = await _dbContext.ReceiptFiles.FirstAsync();
        savedFile.Status.Should().Be(FileStatus.Processed);
        savedFile.ContentHash.Should().NotBeNullOrWhiteSpace();

        var receipt = await _dbContext.Receipts.FirstAsync();
        receipt.ReceiptFileId.Should().Be(savedFile.Id);
    }

    [Fact]
    public async Task HandleAsync_DuplicateFile_ReportsFailureWithoutAbortingBatch()
    {
        // Pre-seed a file with the same hash
        var stream = new MemoryStream([1, 2, 3]);
        var hash = Convert.ToHexString(
            await System.Security.Cryptography.SHA256.HashDataAsync(stream));

        _dbContext.ReceiptFiles.Add(new ReceiptFile
        {
            Id = Guid.NewGuid(),
            UserId = TestUserId,
            OriginalFileName = "existing.pdf",
            ContentHash = hash,
            FileSize = 3,
            UploadedAt = DateTime.UtcNow,
            Status = FileStatus.Processed
        });
        await _dbContext.SaveChangesAsync();

        var command = MakeCommand("duplicate.pdf", new MemoryStream([1, 2, 3]));
        var result = await _handler.HandleAsync(command);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Successful.Should().BeEmpty();
        result.Value!.Failed.Should().HaveCount(1);
        result.Value!.Failed[0].FileName.Should().Be("duplicate.pdf");
        result.Value!.Failed[0].Kind.Should().Be(FailureKind.Duplicate);
        result.Value!.Failed[0].Reason.Should().Contain("existing.pdf");
    }

    [Theory]
    [InlineData(FileStatus.Failed)]
    [InlineData(FileStatus.Processing)]
    [InlineData(FileStatus.Uploaded)]
    public async Task HandleAsync_NonSuccessfulDuplicateFile_RetrySucceeds(FileStatus stuckStatus)
    {
        // Pre-seed a file with the same hash in a non-terminal-success state
        var stream = new MemoryStream([1, 2, 3]);
        var hash = Convert.ToHexString(
            await System.Security.Cryptography.SHA256.HashDataAsync(stream));

        _dbContext.ReceiptFiles.Add(new ReceiptFile
        {
            Id = Guid.NewGuid(),
            UserId = TestUserId,
            OriginalFileName = "stuck.pdf",
            ContentHash = hash,
            FileSize = 3,
            UploadedAt = DateTime.UtcNow,
            Status = stuckStatus
        });
        await _dbContext.SaveChangesAsync();

        // Now upload the same bytes again — should succeed
        SetupSuccessfulPipeline("Amazon.de Tinte EUR 9.99", "Amazon");
        var command = MakeCommand("retry.pdf", new MemoryStream([1, 2, 3]));
        var result = await _handler.HandleAsync(command);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Successful.Should().HaveCount(1);
        result.Value!.Failed.Should().BeEmpty();

        // Old stuck record should be gone, new one with Processed status
        var files = await _dbContext.ReceiptFiles.ToListAsync();
        files.Should().HaveCount(1);
        files[0].Status.Should().Be(FileStatus.Processed);
        files[0].OriginalFileName.Should().Be("retry.pdf");
    }

    [Fact]
    public async Task HandleAsync_EmptyExtractedText_MarksFailedAndContinues()
    {
        _pdfExtractorMock
            .Setup(e => e.ExtractTextAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(string.Empty);

        var command = MakeCommand("empty.pdf");
        var result = await _handler.HandleAsync(command);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Successful.Should().BeEmpty();
        result.Value!.Failed.Should().HaveCount(1);
        result.Value!.Failed[0].Kind.Should().Be(FailureKind.ProcessingError);
        result.Value!.Failed[0].Reason.Should().Contain("extract any text");

        var savedFile = await _dbContext.ReceiptFiles.FirstAsync();
        savedFile.Status.Should().Be(FileStatus.Failed);
    }

    [Fact]
    public async Task HandleAsync_NoMatchingParser_MarksFailedAndContinues()
    {
        _pdfExtractorMock
            .Setup(e => e.ExtractTextAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("some extracted text");

        _parserMock.Setup(p => p.CanParse(It.IsAny<string>(), It.IsAny<string?>()))
            .Returns(false);

        var command = MakeCommand("unknown.pdf");
        var result = await _handler.HandleAsync(command);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Successful.Should().BeEmpty();
        result.Value!.Failed.Should().HaveCount(1);
        result.Value!.Failed[0].Reason.Should().Contain("No suitable parser");
    }

    [Fact]
    public async Task HandleAsync_ImageFile_UsesImageExtractor()
    {
        _imageExtractorMock
            .Setup(e => e.ExtractTextAsync(It.IsAny<Stream>(), "image/jpeg", It.IsAny<CancellationToken>()))
            .ReturnsAsync("Amazon.de Tinte EUR 9.99");

        var parsedReceipt = TestDataFactory.CreateReceipt(vendor: "Amazon");
        parsedReceipt.Items.Add(TestDataFactory.CreateReceiptItem());
        _parserMock.Setup(p => p.CanParse(It.IsAny<string>(), It.IsAny<string?>())).Returns(true);
        _parserMock.Setup(p => p.Parse(It.IsAny<string>(), It.IsAny<ReceiptFile>())).Returns(parsedReceipt);
        _classificationMock
            .Setup(c => c.ClassifyItemsAsync(It.IsAny<IEnumerable<ReceiptItem>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([TestDataFactory.CreateClassification()]);

        var command = MakeCommand("receipt.jpg");
        var result = await _handler.HandleAsync(command);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Successful.Should().HaveCount(1);
        _imageExtractorMock.Verify(
            e => e.ExtractTextAsync(It.IsAny<Stream>(), "image/jpeg", It.IsAny<CancellationToken>()),
            Times.Once);
        _pdfExtractorMock.Verify(
            e => e.ExtractTextAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_SetsProcessedStatusAndCreatesProcessingRun()
    {
        SetupSuccessfulPipeline("text", "Vendor");

        await _handler.HandleAsync(MakeCommand("r.pdf"));

        var run = await _dbContext.ProcessingRuns.FirstAsync();
        run.Status.Should().Be(ProcessingStatus.Completed);
        run.CompletedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task HandleAsync_BatchWithMixedOutcomes_ReturnsSuccessesAndFailures()
    {
        // Pre-seed a duplicate for one of the files in the batch
        var duplicateBytes = new byte[] { 9, 9, 9 };
        var duplicateHash = Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(duplicateBytes));

        _dbContext.ReceiptFiles.Add(new ReceiptFile
        {
            Id = Guid.NewGuid(),
            UserId = TestUserId,
            OriginalFileName = "already-there.pdf",
            ContentHash = duplicateHash,
            FileSize = duplicateBytes.Length,
            UploadedAt = DateTime.UtcNow,
            Status = FileStatus.Processed
        });
        await _dbContext.SaveChangesAsync();

        SetupSuccessfulPipeline("Amazon text", "Amazon");

        var files = new List<FileUploadItem>
        {
            new("good1.pdf", 3, new MemoryStream([1, 2, 3])),
            new("duplicate.pdf", duplicateBytes.Length, new MemoryStream(duplicateBytes)),
            new("good2.pdf", 3, new MemoryStream([4, 5, 6]))
        };
        var command = new UploadReceiptFilesCommand(files, null, null, null);

        var result = await _handler.HandleAsync(command);

        // Batch must not abort on the duplicate — both good files should still process.
        result.IsSuccess.Should().BeTrue();
        result.Value!.Successful.Should().HaveCount(2);
        result.Value!.Failed.Should().HaveCount(1);
        result.Value!.Failed[0].FileName.Should().Be("duplicate.pdf");
        result.Value!.Failed[0].Kind.Should().Be(FailureKind.Duplicate);
    }

    // ── helpers ──────────────────────────────────────────────────────

    private void SetupSuccessfulPipeline(string extractedText, string vendor)
    {
        _pdfExtractorMock
            .Setup(e => e.ExtractTextAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(extractedText);

        _parserMock.Setup(p => p.CanParse(It.IsAny<string>(), It.IsAny<string?>())).Returns(true);
        // Return a fresh receipt per call so repeated invocations in a batch don't
        // share the same entity instance (EF would fail on duplicate key tracking).
        _parserMock.Setup(p => p.Parse(It.IsAny<string>(), It.IsAny<ReceiptFile>()))
            .Returns(() =>
            {
                var r = TestDataFactory.CreateReceipt(vendor: vendor);
                r.Items.Add(TestDataFactory.CreateReceiptItem());
                return r;
            });
        _classificationMock
            .Setup(c => c.ClassifyItemsAsync(It.IsAny<IEnumerable<ReceiptItem>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => [TestDataFactory.CreateClassification()]);
    }

    private static UploadReceiptFilesCommand MakeCommand(
        string fileName, MemoryStream? stream = null)
    {
        var files = new List<FileUploadItem>
        {
            new(fileName, 3, stream ?? new MemoryStream([1, 2, 3]))
        };
        return new UploadReceiptFilesCommand(files, null, null, null);
    }

    public void Dispose() => _dbContext.Dispose();
}
