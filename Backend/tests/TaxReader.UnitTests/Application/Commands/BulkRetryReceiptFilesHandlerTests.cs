using System.Linq.Expressions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using TaxReader.Application.Commands;
using TaxReader.Application.Interfaces;
using TaxReader.Application.Jobs;
using TaxReader.Domain.Entities;
using TaxReader.Domain.Enums;
using TaxReader.Infrastructure.Data;

namespace TaxReader.UnitTests.Application.Commands;

/// <summary>
/// Tests for the bulk "retry several stuck receipt files at once" feature. Composes
/// RetryReceiptFileHandler per file, so these tests focus on the aggregation
/// behaviour (count, partial success) rather than re-verifying per-file retry rules
/// already covered by RetryReceiptFileHandlerTests.
/// </summary>
public class BulkRetryReceiptFilesHandlerTests : IDisposable
{
    private static readonly Guid TestUserId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");

    private readonly AppDbContext _dbContext;
    private readonly Mock<IBackgroundJobClient> _jobClientMock = new();
    private readonly Mock<ICurrentUser> _currentUserMock = new();
    private readonly BulkRetryReceiptFilesHandler _handler;

    public BulkRetryReceiptFilesHandlerTests()
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
            .ReturnsAsync("new-job-id");

        var retryHandler = new RetryReceiptFileHandler(
            _dbContext,
            _jobClientMock.Object,
            _currentUserMock.Object,
            Mock.Of<ILogger<RetryReceiptFileHandler>>());
        _handler = new BulkRetryReceiptFilesHandler(retryHandler);
    }

    private ReceiptFile Seed(ProcessingStatus runStatus)
    {
        var file = new ReceiptFile
        {
            Id = Guid.NewGuid(),
            UserId = TestUserId,
            OriginalFileName = "stuck.pdf",
            ContentHash = Guid.NewGuid().ToString("N"),
            FileSize = 3,
            UploadedAt = DateTime.UtcNow,
            UploadBatchId = Guid.NewGuid(),
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
        return file;
    }

    [Fact]
    public async Task HandleAsync_AllRetryable_RetriesEveryFileAndReturnsFullCount()
    {
        var a = Seed(ProcessingStatus.Pending);
        var b = Seed(ProcessingStatus.Parsing);

        var result = await _handler.HandleAsync(
            new BulkRetryReceiptFilesCommand([a.Id, b.Id]), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(2);
        _jobClientMock.Verify(
            c => c.EnqueueAsync<ProcessReceiptFileJob>(
                It.IsAny<Expression<Func<ProcessReceiptFileJob, Task>>>(),
                It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task HandleAsync_MixOfRetryableAndTerminal_RetriesOnlyRetryableAndReturnsPartialCount()
    {
        // Best-effort semantics: a file that's already Completed (e.g. finished between
        // the frontend listing it and this call) is silently skipped, not a failure.
        var retryable = Seed(ProcessingStatus.Pending);
        var alreadyDone = Seed(ProcessingStatus.Completed);

        var result = await _handler.HandleAsync(
            new BulkRetryReceiptFilesCommand([retryable.Id, alreadyDone.Id]), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(1);
        _jobClientMock.Verify(
            c => c.EnqueueAsync<ProcessReceiptFileJob>(
                It.IsAny<Expression<Func<ProcessReceiptFileJob, Task>>>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_EmptyIdList_ReturnsFailure()
    {
        var result = await _handler.HandleAsync(
            new BulkRetryReceiptFilesCommand([]), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
    }

    public void Dispose() => _dbContext.Dispose();
}
