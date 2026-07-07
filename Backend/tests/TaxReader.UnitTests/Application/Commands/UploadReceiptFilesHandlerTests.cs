using System.Linq.Expressions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using TaxReader.Application.Commands;
using TaxReader.Application.DTOs;
using TaxReader.Application.Interfaces;
using TaxReader.Application.Jobs;
using TaxReader.Domain.Entities;
using TaxReader.Domain.Enums;
using TaxReader.Infrastructure.Data;
using TaxReader.UnitTests.Helpers;

namespace TaxReader.UnitTests.Application.Commands;

/// <summary>
/// Tests for the refactored UploadReceiptFilesHandler that returns 202 Accepted
/// and enqueues Hangfire jobs instead of synchronous processing.
/// The old synchronous tests are superseded by the Hangfire pipeline tests
/// (ProcessReceiptFileJobTests + ClassifyBatchJobTests).
/// </summary>
public class UploadReceiptFilesHandlerTests : IDisposable
{
    private static readonly Guid TestUserId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");

    private readonly AppDbContext _dbContext;
    private readonly Mock<IUploadBlobStore> _blobStoreMock = new();
    private readonly Mock<IBackgroundJobClient> _jobClientMock = new();
    private readonly Mock<ICurrentUser> _currentUserMock = new();
    private readonly UploadReceiptFilesHandler _handler;

    public UploadReceiptFilesHandlerTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new AppDbContext(options);
        _currentUserMock.Setup(u => u.UserId).Returns(TestUserId);
        _jobClientMock
            .Setup(c => c.EnqueueAsync<ProcessReceiptFileJob>(
                It.IsAny<Expression<Func<ProcessReceiptFileJob, Task>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("job-123");

        _handler = new UploadReceiptFilesHandler(
            _dbContext,
            _blobStoreMock.Object,
            _jobClientMock.Object,
            _currentUserMock.Object,
            Mock.Of<ILogger<UploadReceiptFilesHandler>>());
    }

    [Fact]
    public async Task HandleAsync_ThreeFiles_ReturnsThreedAcceptedEntries()
    {
        var command = MakeCommand("a.pdf", "b.pdf", "c.pdf");

        var result = await _handler.HandleAsync(command);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Files.Should().HaveCount(3);
        result.Value!.Files.Should().AllSatisfy(f =>
        {
            f.ReceiptFileId.Should().NotBeEmpty();
            f.JobId.Should().NotBeNullOrEmpty();
            f.FileName.Should().NotBeNullOrEmpty();
        });
    }

    [Fact]
    public async Task HandleAsync_ThreeFiles_PersistsThreeReceiptFilesAndThreeRuns()
    {
        var command = MakeCommand("a.pdf", "b.pdf", "c.pdf");

        await _handler.HandleAsync(command);

        var files = await _dbContext.ReceiptFiles.ToListAsync();
        files.Should().HaveCount(3);
        files.Should().AllSatisfy(f =>
        {
            f.Status.Should().Be(FileStatus.Processing);
            f.UploadBatchId.Should().NotBeNull();
        });

        // All files share the same UploadBatchId
        var batchIds = files.Select(f => f.UploadBatchId).Distinct().ToList();
        batchIds.Should().HaveCount(1);

        var runs = await _dbContext.ProcessingRuns.ToListAsync();
        runs.Should().HaveCount(3);
        runs.Should().AllSatisfy(r => r.Status.Should().Be(ProcessingStatus.Pending));
    }

    [Fact]
    public async Task HandleAsync_ThreeFiles_SavesThreeBlobsToStore()
    {
        var command = MakeCommand("a.pdf", "b.pdf", "c.pdf");

        await _handler.HandleAsync(command);

        _blobStoreMock.Verify(
            b => b.SaveAsync(It.IsAny<Guid>(), It.IsAny<Stream>(), It.IsAny<CancellationToken>()),
            Times.Exactly(3));
    }

    [Fact]
    public async Task HandleAsync_ThreeFiles_EnqueuesThreeProcessReceiptFileJobs()
    {
        var command = MakeCommand("a.pdf", "b.pdf", "c.pdf");

        await _handler.HandleAsync(command);

        _jobClientMock.Verify(
            c => c.EnqueueAsync<ProcessReceiptFileJob>(
                It.IsAny<Expression<Func<ProcessReceiptFileJob, Task>>>(),
                It.IsAny<CancellationToken>()),
            Times.Exactly(3));
    }

    [Fact]
    public async Task HandleAsync_BlobStoreThrows_ReturnsFailureAndRollsBackBlobs()
    {
        _blobStoreMock
            .Setup(b => b.SaveAsync(It.IsAny<Guid>(), It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new IOException("Disk full"));

        var command = MakeCommand("a.pdf");

        var result = await _handler.HandleAsync(command);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Upload fehlgeschlagen");

        // No receipt file rows should have been committed
        var files = await _dbContext.ReceiptFiles.ToListAsync();
        files.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsync_EnqueuesJobsOnlyAfterRowsAreCommitted()
    {
        // Regression test: jobs were previously enqueued inside the same loop that
        // created the tracked (not-yet-saved) rows, with the actual commit deferred
        // until after the whole loop — a fast Hangfire worker could dequeue and
        // execute ProcessReceiptFileJob before that commit landed, hit "row not
        // found", and (before the ProcessReceiptFileJob fix) silently no-op while
        // Hangfire still marked the job Succeeded, leaving the file stuck forever
        // with no error surfaced. Verify every row is already saved (not still
        // Added/untracked-pending) by the time the first job is enqueued.
        var sawUnsavedRowAtEnqueueTime = false;
        _jobClientMock
            .Setup(c => c.EnqueueAsync<ProcessReceiptFileJob>(
                It.IsAny<Expression<Func<ProcessReceiptFileJob, Task>>>(),
                It.IsAny<CancellationToken>()))
            .Callback(() =>
            {
                var anyUnsaved = _dbContext.ChangeTracker.Entries<ReceiptFile>().Any(e => e.State == EntityState.Added)
                    || _dbContext.ChangeTracker.Entries<ProcessingRun>().Any(e => e.State == EntityState.Added);
                if (anyUnsaved) sawUnsavedRowAtEnqueueTime = true;
            })
            .ReturnsAsync("job-xyz");

        var command = MakeCommand("a.pdf", "b.pdf", "c.pdf");
        await _handler.HandleAsync(command);

        sawUnsavedRowAtEnqueueTime.Should().BeFalse(
            "every ReceiptFile/ProcessingRun row must be committed before any job is enqueued");
    }

    [Fact]
    public async Task HandleAsync_DuplicateOfExistingFile_SkipsItAndReportsExistingFileName()
    {
        // Regression test: uploading a file whose content already exists for this user
        // previously threw a DB unique-constraint violation (caught generically as
        // "Upload fehlgeschlagen — bitte erneut versuchen.") with no indication of which
        // file, or that it was a duplicate at all.
        await _handler.HandleAsync(MakeCommand("original.pdf"));

        var duplicateContent = new FileUploadItem("copy.pdf", 3, new MemoryStream([1, 2, 0]));
        var command = new UploadReceiptFilesCommand([duplicateContent], null, null, null);

        var result = await _handler.HandleAsync(command);

        result.IsSuccess.Should().BeTrue("a duplicate is a normal outcome, not a system failure");
        result.Value!.Files.Should().BeEmpty();
        result.Value!.Duplicates.Should().ContainSingle();
        result.Value!.Duplicates[0].FileName.Should().Be("copy.pdf");
        result.Value!.Duplicates[0].Reason.Should().Contain("original.pdf");
    }

    [Fact]
    public async Task HandleAsync_DuplicateWithinSameBatch_AcceptsFirstAndSkipsSecond()
    {
        var sameContentTwice = new List<FileUploadItem>
        {
            new("first.pdf", 3, new MemoryStream([5, 5, 5])),
            new("second.pdf", 3, new MemoryStream([5, 5, 5])),
        };
        var command = new UploadReceiptFilesCommand(sameContentTwice, null, null, null);

        var result = await _handler.HandleAsync(command);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Files.Should().ContainSingle(f => f.FileName == "first.pdf");
        result.Value!.Duplicates.Should().ContainSingle();
        result.Value!.Duplicates[0].FileName.Should().Be("second.pdf");
        result.Value!.Duplicates[0].Reason.Should().Contain("first.pdf");
    }

    [Fact]
    public async Task HandleAsync_AllFilesAreDuplicates_ReturnsSuccessWithEmptyFilesList()
    {
        await _handler.HandleAsync(MakeCommand("original.pdf"));

        var duplicateContent = new FileUploadItem("copy.pdf", 3, new MemoryStream([1, 2, 0]));
        var command = new UploadReceiptFilesCommand([duplicateContent], null, null, null);

        var result = await _handler.HandleAsync(command);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Files.Should().BeEmpty();
        result.Value!.Duplicates.Should().HaveCount(1);

        // No stray rows/jobs for the skipped duplicate
        var files = await _dbContext.ReceiptFiles.CountAsync(f => f.OriginalFileName == "copy.pdf");
        files.Should().Be(0);
    }

    [Fact]
    public async Task HandleAsync_DoesNotReferenceClassificationService()
    {
        // D-02 invariant: upload time MUST NOT touch ITokenService or IClassificationService.
        // Verify by ensuring the handler builds without those interfaces (constructor check).
        var handlerType = typeof(UploadReceiptFilesHandler);
        var ctor = handlerType.GetConstructors().Single();
        var paramNames = ctor.GetParameters().Select(p => p.ParameterType.Name);

        paramNames.Should().NotContain("IClassificationService",
            "D-02: pre-charge deferred to ClassifyBatchJob — upload handler must not reference classification");
        paramNames.Should().NotContain("ITokenService",
            "D-02: no token charging at upload time");
    }

    // ── helpers ──────────────────────────────────────────────────────

    private static UploadReceiptFilesCommand MakeCommand(params string[] fileNames)
    {
        // Distinct byte content per file — these tests exercise "N different files",
        // and duplicate-content detection (added for the "which file already exists"
        // feedback fix) would otherwise treat same-content files as duplicates of
        // each other even though the test intends them to be unrelated uploads.
        var files = fileNames
            .Select((name, i) => new FileUploadItem(name, 3, new MemoryStream([1, 2, (byte)i])))
            .ToList();
        return new UploadReceiptFilesCommand(files, null, null, null);
    }

    public void Dispose() => _dbContext.Dispose();
}
