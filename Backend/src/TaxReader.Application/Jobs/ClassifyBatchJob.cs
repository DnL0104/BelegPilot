using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Serilog.Context;
using TaxReader.Application.Common;
using TaxReader.Application.Interfaces;
using TaxReader.Domain.Enums;

namespace TaxReader.Application.Jobs;

/// <summary>
/// D-01: cross-batch AI classification. Runs ONE Anthropic call per upload batch,
/// preserving the wallclock win that the old UploadReceiptFilesHandler had (Haiku
/// roundtrip ~1s vs N×1s). Token pre-charge + per-item refund + AI-failure refund
/// branches are preserved verbatim inside AiOnlyClassificationService (which now
/// accepts an explicit userId since Hangfire jobs are HTTP-context-free).
///
/// Retry policy (D-04): 0 Hangfire retries — the existing "refund + mark Unknown"
/// branch inside AiOnlyClassificationService handles AI failures gracefully; we
/// don't want 3× the token-refund churn or 3× the Anthropic load.
/// </summary>
public class ClassifyBatchJob(
    IAppDbContext dbContext,
    IClassificationService classificationService,
    ILogger<ClassifyBatchJob> logger)
{
    [AutomaticRetry(Attempts = 0)] // D-04: AI failure refund branch handles retries internally
    public async Task HandleAsync(
        Guid uploadBatchId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        // D-05 + Phase 1 D-18: every log line emitted while classifying carries the JobId.
        using var _scope = LogContext.PushProperty("JobId", uploadBatchId);

        // Race mitigation (RESEARCH Pitfall 2): if any run for this batch is already
        // Classifying or beyond, another concurrent ClassifyBatchJob already started.
        // Exit early — idempotent at entry.
        var alreadyClassifying = await dbContext.ProcessingRuns
            .Where(r => r.ReceiptFile.UploadBatchId == uploadBatchId && r.Status >= ProcessingStatus.Classifying)
            .AnyAsync(cancellationToken);
        if (alreadyClassifying)
        {
            logger.LogInformation("ClassifyBatchJob for {UploadBatchId} already started by another worker — exiting idempotently", uploadBatchId);
            return;
        }

        var runs = await dbContext.ProcessingRuns
            .Include(r => r.ReceiptFile)
                .ThenInclude(f => f.Receipt)
                    .ThenInclude(r => r!.Items)
            .Where(r => r.ReceiptFile.UploadBatchId == uploadBatchId
                     && r.Status == ProcessingStatus.Parsing) // only successfully-parsed runs
            .ToListAsync(cancellationToken);

        if (runs.Count == 0)
        {
            logger.LogInformation("No parsed runs for batch {UploadBatchId} — nothing to classify", uploadBatchId);
            return;
        }

        // Mark all runs as Classifying before the AI call
        foreach (var r in runs) r.Status = ProcessingStatus.Classifying;
        await dbContext.SaveChangesAsync(cancellationToken);

        var allItems = runs
            .Where(r => r.ReceiptFile.Receipt is not null)
            .SelectMany(r => r.ReceiptFile.Receipt!.Items)
            .ToList();
        if (allItems.Count == 0)
        {
            logger.LogInformation("Parsed receipts in batch {UploadBatchId} carry zero items — skipping AI", uploadBatchId);
        }
        else
        {
            try
            {
                var classifications = await classificationService.ClassifyItemsAsync(allItems, userId, cancellationToken);
                foreach (var classification in classifications)
                {
                    classification.Id = Guid.NewGuid();
                    dbContext.ItemClassifications.Add(classification);
                }
            }
            catch (Exception ex)
            {
                // Catalog maps the exception to a safe (code, German message) pair (D-21).
                // Raw exception messages NEVER reach processing_runs.error_message.
                var error = UploadErrorCatalog.Classify(ex, cancellationToken.IsCancellationRequested);

                logger.LogError(ex,
                    "{ErrorCode} during ClassifyBatchJob for batch {UploadBatchId}",
                    error.Code, uploadBatchId);

                var terminalStatus = cancellationToken.IsCancellationRequested
                    ? ProcessingStatus.Cancelled
                    : ProcessingStatus.Failed;

                // D-12: cancellation and AI failures both need every run finalized.
                // The token refund branch inside AiOnlyClassificationService runs before
                // the exception propagates here (it's inside ClassifyItemsAsync's catch).
                foreach (var r in runs)
                {
                    r.Status = terminalStatus;
                    r.CompletedAt = DateTime.UtcNow;
                    r.ErrorCode = error.Code;
                    r.ErrorMessage = error.GermanMessage;
                    r.ReceiptFile.Status = FileStatus.Failed;
                }

                // WR-07: run sum validation on the failure path too — partial classifications
                // may have been added before the failure, so the flag must be accurate.
                foreach (var run in runs.Where(r => r.ReceiptFile.Receipt is not null))
                {
                    var receipt = run.ReceiptFile.Receipt!;
                    var itemsSum = receipt.Items.Sum(i => i.TotalPrice);
                    if (Math.Abs(itemsSum - receipt.TotalAmount) > 0.50m)
                        receipt.HasSumMismatch = true;
                }

                await dbContext.SaveChangesAsync(CancellationToken.None);

                // For user-initiated cancellation: suppress re-throw (Hangfire marks Succeeded).
                // For AI failures (HttpRequestException etc.): re-throw (Hangfire marks Failed, no retry per D-04).
                if (cancellationToken.IsCancellationRequested) return;
                throw;
            }
        }

        // D-16: sum validation — compare item totals against receipt total (€0.50 absolute tolerance).
        // Runs in the same SaveChangesAsync as the Completed status update.
        foreach (var run in runs.Where(r => r.ReceiptFile.Receipt is not null))
        {
            var receipt = run.ReceiptFile.Receipt!;
            var itemsSum = receipt.Items.Sum(i => i.TotalPrice);
            if (Math.Abs(itemsSum - receipt.TotalAmount) > 0.50m)
                receipt.HasSumMismatch = true;
        }

        // Finalize: mark every run as Completed
        var now = DateTime.UtcNow;
        foreach (var r in runs)
        {
            r.Status = ProcessingStatus.Completed;
            r.CompletedAt = now;
            r.ReceiptFile.Status = FileStatus.Processed;
        }
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
