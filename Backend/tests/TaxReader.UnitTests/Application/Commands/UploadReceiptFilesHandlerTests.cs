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
        var files = fileNames
            .Select(name => new FileUploadItem(name, 3, new MemoryStream([1, 2, 3])))
            .ToList();
        return new UploadReceiptFilesCommand(files, null, null, null);
    }

    public void Dispose() => _dbContext.Dispose();
}
