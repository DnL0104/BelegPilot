using Hangfire; // for [AutomaticRetry] attribute
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Serilog.Context;
using TaxReader.Application.Common;
using TaxReader.Application.Interfaces;
using TaxReader.Domain.Entities;
using TaxReader.Domain.Enums;

// Hangfire's IBackgroundJobClient and our Application port have the same simple name.
// Alias to disambiguate: this file uses the Application port exclusively.
using IBackgroundJobClient = TaxReader.Application.Interfaces.IBackgroundJobClient;

namespace TaxReader.Application.Jobs;

/// <summary>
/// D-01 per-file parent in the upload pipeline. Extracts text + runs the parser chain
/// + persists Receipt + ReceiptItem rows for ONE file. The barrier at the bottom
/// counts completed siblings in the same UploadBatchId; the LAST parent to finish
/// enqueues the single ClassifyBatchJob for the whole batch (D-01 preserves cross-
/// receipt AI batching from UploadReceiptFilesHandler.cs:173-202).
///
/// Retry policy (D-04): 3 retries with backoff via [AutomaticRetry(Attempts = 3,
/// DelaysInSeconds = new[] { 30, 120, 300 })]. Re-runs are safe because the job
/// reads from IUploadBlobStore (persistent across container restarts).
/// </summary>
public class ProcessReceiptFileJob(
    IAppDbContext dbContext,
    IPdfTextExtractor pdfExtractor,
    IImageTextExtractor imageExtractor,
    IEnumerable<IReceiptParser> parsers,
    IBackgroundJobClient jobClient,
    IUploadBlobStore blobStore,
    ILogger<ProcessReceiptFileJob> logger)
{
    private static readonly HashSet<string> ImageExtensions = [".jpg", ".jpeg", ".png", ".webp"];
    private static bool IsImageFile(string fileName) =>
        ImageExtensions.Contains(Path.GetExtension(fileName).ToLowerInvariant());
    private static string GetMediaType(string fileName) =>
        Path.GetExtension(fileName).ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".webp" => "image/webp",
            _ => throw new InvalidOperationException($"Unsupported image format: {Path.GetExtension(fileName)}")
        };

    [AutomaticRetry(Attempts = 3, DelaysInSeconds = new[] { 30, 120, 300 })] // D-04
    public async Task HandleAsync(
        Guid receiptFileId,
        Guid uploadBatchId,
        int batchSize,
        CancellationToken cancellationToken)
    {
        // D-05 + Phase 1 D-18: every log line emitted while processing carries the JobId.
        using var _scope = LogContext.PushProperty("JobId", receiptFileId);

        var receiptFile = await dbContext.ReceiptFiles
            .FirstOrDefaultAsync(f => f.Id == receiptFileId, cancellationToken);
        if (receiptFile is null)
        {
            logger.LogWarning("ReceiptFile {ReceiptFileId} not found — job exiting", receiptFileId);
            return;
        }

        var run = await dbContext.ProcessingRuns
            .Where(r => r.ReceiptFileId == receiptFileId)
            .OrderByDescending(r => r.StartedAt)
            .FirstOrDefaultAsync(cancellationToken);
        if (run is null)
        {
            logger.LogWarning("No ProcessingRun for ReceiptFile {ReceiptFileId} — job exiting", receiptFileId);
            return;
        }

        // Cancellation observed at boundary (D-11): CancelReceiptFileHandler commits
        // Status=Cancelled BEFORE deleting the Hangfire job (Pitfall 3 mitigation),
        // so by the time the worker observes the row it knows to bail.
        if (run.Status == ProcessingStatus.Cancelled)
        {
            logger.LogInformation("ReceiptFile {ReceiptFileId} already Cancelled — job exiting", receiptFileId);
            return;
        }

        try
        {
            // Step 1: Extract
            run.Status = ProcessingStatus.Extracting;
            await dbContext.SaveChangesAsync(cancellationToken);

            await using var blobStream = await blobStore.OpenReadAsync(receiptFile.Id, cancellationToken);
            if (blobStream is null)
            {
                await MarkFailedAsync(run, receiptFile, ProcessingStatus.Failed, "NoContent", "Datei-Inhalt konnte nicht gelesen werden.");
                return;
            }

            using var ms = new MemoryStream();
            await blobStream.CopyToAsync(ms, cancellationToken);
            ms.Position = 0;
            var rawText = IsImageFile(receiptFile.OriginalFileName)
                ? await imageExtractor.ExtractTextAsync(ms, GetMediaType(receiptFile.OriginalFileName), cancellationToken)
                : await pdfExtractor.ExtractTextAsync(ms, cancellationToken);

            if (string.IsNullOrWhiteSpace(rawText))
            {
                await MarkFailedAsync(run, receiptFile, ProcessingStatus.Failed, "NoTextExtracted", "Aus dieser Datei konnte kein Text gelesen werden.");
                return;
            }

            // Step 2: Parse
            run.Status = ProcessingStatus.Parsing;
            await dbContext.SaveChangesAsync(cancellationToken);

            var parser = parsers.FirstOrDefault(p => p.CanParse(rawText, receiptFile.SourceHint));
            if (parser is null)
            {
                await MarkFailedAsync(run, receiptFile, ProcessingStatus.Failed, "ParserMissing", "Format der Datei wird derzeit nicht unterstützt.");
                return;
            }

            var receipt = parser.Parse(rawText, receiptFile);
            receipt.Id = Guid.NewGuid();
            receipt.ReceiptFileId = receiptFile.Id;
            receipt.RawExtractedText = rawText;
            receipt.ParsedAt = DateTime.UtcNow;

            foreach (var item in receipt.Items)
            {
                item.Id = Guid.NewGuid();
                item.ReceiptId = receipt.Id;
            }

            dbContext.Receipts.Add(receipt);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            var error = UploadErrorCatalog.Classify(ex, cancellationToken.IsCancellationRequested);

            logger.LogError(ex,
                "{ErrorCode} during ProcessReceiptFileJob for ReceiptFile {ReceiptFileId}",
                error.Code, receiptFileId);

            var terminalStatus = cancellationToken.IsCancellationRequested
                ? ProcessingStatus.Cancelled
                : ProcessingStatus.Failed;

            await MarkFailedAsync(run, receiptFile, terminalStatus, error.Code, error.GermanMessage);

            // For user-initiated cancellation: suppress re-throw so Hangfire marks the job
            // Succeeded rather than triggering the retry backoff (D-04 tiered retries are for
            // transient failures, not user actions).
            if (cancellationToken.IsCancellationRequested) return;
            throw;
        }

        // Barrier: count completed parents in this batch. Last one enqueues the classify job.
        // Includes Parsing (just-finished current run), Classifying, Completed; excludes
        // Failed/Cancelled (those don't go through classify).
        var completedCount = await dbContext.ProcessingRuns
            .Where(r => r.ReceiptFile.UploadBatchId == uploadBatchId
                     && r.Status >= ProcessingStatus.Parsing
                     && r.Status != ProcessingStatus.Failed
                     && r.Status != ProcessingStatus.Cancelled)
            .CountAsync(cancellationToken);

        if (completedCount >= batchSize)
        {
            logger.LogInformation("Last parent finished for batch {UploadBatchId}; enqueueing ClassifyBatchJob", uploadBatchId);
            await jobClient.EnqueueAsync<ClassifyBatchJob>(
                j => j.HandleAsync(uploadBatchId, receiptFile.UserId, CancellationToken.None),
                cancellationToken);
        }
    }

    private async Task MarkFailedAsync(
        ProcessingRun run,
        ReceiptFile file,
        ProcessingStatus terminalStatus,
        string errorCode,
        string germanMessage)
    {
        run.Status = terminalStatus;
        run.CompletedAt = DateTime.UtcNow;
        run.ErrorCode = errorCode;
        run.ErrorMessage = germanMessage;
        file.Status = FileStatus.Failed;
        try
        {
            await dbContext.SaveChangesAsync(CancellationToken.None); // persist failure even if request cancelled
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to persist failure status for ReceiptFile {ReceiptFileId}", file.Id);
        }
    }
}
