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
/// Tests for the manual "retry a stuck receipt file" feature — lets a user re-trigger
/// ProcessReceiptFileJob for a file stuck in a non-terminal state (e.g. from the
/// upload-enqueue race a prior fix addressed) instead of deleting and re-uploading.
/// </summary>
public class RetryReceiptFileHandlerTests : IDisposable
{
    private static readonly Guid TestUserId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
    private static readonly Guid OtherUserId = Guid.Parse("ffffffff-1111-2222-3333-444444444444");

    private readonly AppDbContext _dbContext;
    private readonly Mock<IBackgroundJobClient> _jobClientMock = new();
    private readonly Mock<ICurrentUser> _currentUserMock = new();
    private readonly RetryReceiptFileHandler _handler;

    public RetryReceiptFileHandlerTests()
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

        _handler = new RetryReceiptFileHandler(
            _dbContext,
            _jobClientMock.Object,
            _currentUserMock.Object,
            Mock.Of<ILogger<RetryReceiptFileHandler>>());
    }

    private (ReceiptFile File, ProcessingRun Run) Seed(
        ProcessingStatus runStatus,
        Guid? userId = null,
        Guid? uploadBatchId = null)
    {
        var file = new ReceiptFile
        {
            Id = Guid.NewGuid(),
            UserId = userId ?? TestUserId,
            OriginalFileName = "stuck.pdf",
            ContentHash = Guid.NewGuid().ToString("N"),
            FileSize = 3,
            UploadedAt = DateTime.UtcNow,
            UploadBatchId = uploadBatchId ?? Guid.NewGuid(),
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
    public async Task HandleAsync_PendingRun_EnqueuesNewJobAndStoresJobId()
    {
        var (file, run) = Seed(ProcessingStatus.Pending);

        var result = await _handler.HandleAsync(new RetryReceiptFileCommand(file.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _jobClientMock.Verify(
            c => c.EnqueueAsync<ProcessReceiptFileJob>(
                It.IsAny<Expression<Func<ProcessReceiptFileJob, Task>>>(),
                It.IsAny<CancellationToken>()),
            Times.Once);

        var updatedRun = await _dbContext.ProcessingRuns.FirstAsync(r => r.Id == run.Id);
        updatedRun.HangfireJobId.Should().Be("new-job-id");
    }

    [Theory]
    [InlineData(ProcessingStatus.Completed)]
    [InlineData(ProcessingStatus.Failed)]
    [InlineData(ProcessingStatus.Cancelled)]
    [InlineData(ProcessingStatus.Classifying)]
    public async Task HandleAsync_TerminalRun_ReturnsFailureAndDoesNotEnqueue(ProcessingStatus terminalStatus)
    {
        // Classifying included deliberately: re-running ProcessReceiptFileJob can't
        // recover it (ClassifyBatchJob owns that step and only classifies runs still
        // at exactly Parsing), so it must be rejected the same as a true terminal state.
        var (file, _) = Seed(terminalStatus);

        var result = await _handler.HandleAsync(new RetryReceiptFileCommand(file.Id), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        _jobClientMock.Verify(
            c => c.EnqueueAsync<ProcessReceiptFileJob>(
                It.IsAny<Expression<Func<ProcessReceiptFileJob, Task>>>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_GivesFileAFreshStandaloneBatchId()
    {
        // Regression test: reusing the original UploadBatchId broke in practice —
        // if any sibling in that batch had already reached Classifying/Completed,
        // ClassifyBatchJob's own idempotency guard treated the whole batch as already
        // done and silently skipped re-classifying just this retried file, leaving it
        // stuck at Parsing forever. A fresh, standalone batch of 1 avoids that
        // entirely by decoupling the retry from whatever the original siblings did.
        var originalBatchId = Guid.NewGuid();
        var (file, _) = Seed(ProcessingStatus.Pending, uploadBatchId: originalBatchId);

        await _handler.HandleAsync(new RetryReceiptFileCommand(file.Id), CancellationToken.None);

        var updatedFile = await _dbContext.ReceiptFiles.FirstAsync(f => f.Id == file.Id);
        updatedFile.UploadBatchId.Should().NotBe(originalBatchId);
    }

    [Fact]
    public async Task HandleAsync_FileNotFound_ReturnsNotFound()
    {
        var result = await _handler.HandleAsync(new RetryReceiptFileCommand(Guid.NewGuid()), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("NotFound");
    }

    [Fact]
    public async Task HandleAsync_AnotherUsersFile_ReturnsNotFound()
    {
        // IDOR guard: a stuck file owned by a different user must not be retryable
        // or even distinguishable from "doesn't exist".
        var (file, _) = Seed(ProcessingStatus.Pending, userId: OtherUserId);

        var result = await _handler.HandleAsync(new RetryReceiptFileCommand(file.Id), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("NotFound");
    }

    public void Dispose() => _dbContext.Dispose();
}
