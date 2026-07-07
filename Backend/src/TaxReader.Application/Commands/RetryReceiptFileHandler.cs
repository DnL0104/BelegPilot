using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TaxReader.Application.Interfaces;
using TaxReader.Application.Jobs;
using TaxReader.Domain.Common;
using TaxReader.Domain.Enums;

namespace TaxReader.Application.Commands;

/// <summary>
/// Manually re-triggers processing for a ReceiptFile stuck in a non-terminal state —
/// e.g. a race between the upload transaction's commit and a Hangfire worker dequeuing
/// the job left it at Pending forever with no error surfaced. Re-enqueues
/// ProcessReceiptFileJob; its own idempotency check (Receipt already exists?) makes
/// this safe to call even if the original job is still genuinely in flight.
/// </summary>
public class RetryReceiptFileHandler(
    IAppDbContext dbContext,
    IBackgroundJobClient jobClient,
    ICurrentUser currentUser,
    ILogger<RetryReceiptFileHandler> logger)
{
    // Deliberately narrower than CancelReceiptFileHandler's cancellable states:
    // Classifying is excluded because re-running ProcessReceiptFileJob can't recover
    // it — ClassifyBatchJob (not this job) owns that step, and it only classifies runs
    // still at exactly Parsing, so a run already at Classifying would never be picked
    // up again this way. That case needs a classify-level retry, not this one.
    private static readonly HashSet<ProcessingStatus> RetryableStates =
    [
        ProcessingStatus.Pending,
        ProcessingStatus.Queued,
        ProcessingStatus.Extracting,
        ProcessingStatus.Parsing
    ];

    public async Task<Result<bool>> HandleAsync(
        RetryReceiptFileCommand command,
        CancellationToken cancellationToken)
    {
        var file = await dbContext.ReceiptFiles
            .Where(f => f.Id == command.ReceiptFileId && f.UserId == currentUser.UserId)
            .FirstOrDefaultAsync(cancellationToken);
        if (file is null)
            return Result<bool>.Failure("NotFound");

        var run = await dbContext.ProcessingRuns
            .Where(r => r.ReceiptFileId == file.Id)
            .OrderByDescending(r => r.StartedAt)
            .FirstOrDefaultAsync(cancellationToken);
        if (run is null || !RetryableStates.Contains(run.Status))
            return Result<bool>.Failure("Dieser Beleg befindet sich nicht in einem Status, der eine erneute Verarbeitung erlaubt.");

        // Always a fresh, standalone batch of 1 — decoupled from whatever the original
        // upload's siblings are doing by now. Reusing the original UploadBatchId broke
        // in practice: if any sibling had already finished classification,
        // ClassifyBatchJob's own idempotency guard ("a run in this batch is already
        // Classifying/Completed") treated the whole batch as already done and silently
        // skipped re-classifying just this one retried file, leaving it stuck at
        // Parsing forever. A dedicated single-file batch always classifies cleanly.
        var uploadBatchId = Guid.NewGuid();
        file.UploadBatchId = uploadBatchId;

        var jobId = await jobClient.EnqueueAsync<ProcessReceiptFileJob>(
            j => j.HandleAsync(file.Id, uploadBatchId, batchSize: 1, CancellationToken.None),
            cancellationToken);

        run.HangfireJobId = jobId;
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Manually retried ReceiptFile {ReceiptFileId} with new Hangfire job {JobId} in standalone batch {UploadBatchId}",
            file.Id, jobId, uploadBatchId);
        return Result<bool>.Success(true);
    }
}
