using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TaxReader.Application.Interfaces;
using TaxReader.Domain.Common;
using TaxReader.Domain.Enums;

namespace TaxReader.Application.Commands;

public class CancelReceiptFileHandler(
    IAppDbContext dbContext,
    IBackgroundJobClient jobClient,
    ICurrentUser currentUser,
    ILogger<CancelReceiptFileHandler> logger)
{
    // D-11: states that accept cancellation
    private static readonly HashSet<ProcessingStatus> CancellableStates =
    [
        ProcessingStatus.Pending,
        ProcessingStatus.Queued,
        ProcessingStatus.Extracting,
        ProcessingStatus.Parsing,
        ProcessingStatus.Classifying
    ];

    public async Task<Result<bool>> HandleAsync(
        CancelReceiptFileCommand command,
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

        // Idempotent re-cancel: already Cancelled → success (204)
        if (run is { Status: ProcessingStatus.Cancelled })
            return Result<bool>.Success(true);

        // D-11: terminal-but-not-Cancelled states reject with 409
        if (run is not null && !CancellableStates.Contains(run.Status))
            return Result<bool>.Failure("TerminalState");

        // Pitfall 3 mitigation: commit terminal-state BEFORE signalling Hangfire to delete.
        // Worker observing the row sees status=Cancelled and exits its own cancellation
        // branch; the catalog catch in ProcessReceiptFileJob handles the rest.
        if (run is not null)
        {
            run.Status = ProcessingStatus.Cancelled;
            run.ErrorCode = "Cancelled";
            run.ErrorMessage = "Vorgang abgebrochen.";
            run.CompletedAt = DateTime.UtcNow;
        }
        // FileStatus.Failed is the coarse file-level status for Cancelled (no dedicated Failed/Cancelled split at file level)
        file.Status = FileStatus.Failed;
        await dbContext.SaveChangesAsync(cancellationToken);

        // Now signal Hangfire — order matters per Pitfall 3.
        if (run?.HangfireJobId is { Length: > 0 } jobId)
            await jobClient.DeleteAsync(jobId, cancellationToken);

        logger.LogInformation("Cancelled ReceiptFile {ReceiptFileId} (HangfireJob {JobId})",
            file.Id, run?.HangfireJobId);
        return Result<bool>.Success(true);
    }
}
