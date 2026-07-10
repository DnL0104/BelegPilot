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
    ITokenService tokenService,
    IReceiptVisionExtractor visionExtractor,
    IVisionFallbackSettings visionSettings,
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
            // Thrown (not silently returned) so [AutomaticRetry] (D-04) gives the
            // enqueueing transaction time to commit if this worker raced ahead of it —
            // a silent return here counts as Hangfire Succeeded despite doing nothing,
            // leaving the file stuck with zero error surfaced (the legitimate case,
            // the file having been hard-deleted mid-queue, is rare enough that
            // exhausting the retry budget before failing is an acceptable trade-off).
            logger.LogWarning("ReceiptFile {ReceiptFileId} not found — retrying", receiptFileId);
            throw new InvalidOperationException($"ReceiptFile {receiptFileId} not found.");
        }

        var run = await dbContext.ProcessingRuns
            .Where(r => r.ReceiptFileId == receiptFileId)
            .OrderByDescending(r => r.StartedAt)
            .FirstOrDefaultAsync(cancellationToken);
        if (run is null)
        {
            logger.LogWarning("No ProcessingRun for ReceiptFile {ReceiptFileId} — retrying", receiptFileId);
            throw new InvalidOperationException($"No ProcessingRun for ReceiptFile {receiptFileId}.");
        }

        // Cancellation observed at boundary (D-11): CancelReceiptFileHandler commits
        // Status=Cancelled BEFORE deleting the Hangfire job (Pitfall 3 mitigation),
        // so by the time the worker observes the row it knows to bail.
        if (run.Status == ProcessingStatus.Cancelled)
        {
            logger.LogInformation("ReceiptFile {ReceiptFileId} already Cancelled — job exiting", receiptFileId);
            return;
        }

        // Idempotency: if a Receipt already exists, parsing already succeeded on a
        // prior attempt (e.g. this run being manually retried, or an automatic retry
        // firing after the barrier check below threw) — skip straight to the barrier
        // instead of re-parsing, which would violate the unique index on
        // receipts.receipt_file_id and mark an already-successful parse as Failed.
        var alreadyParsed = await dbContext.Receipts.AnyAsync(r => r.ReceiptFileId == receiptFileId, cancellationToken);
        if (alreadyParsed)
        {
            // The barrier below only counts runs with Status >= Parsing — without this,
            // a run left at an earlier status (e.g. still Pending, as after the exact
            // race this idempotency guard protects against) would never register as
            // "done" there, even though its Receipt already exists.
            if (run.Status < ProcessingStatus.Parsing)
            {
                run.Status = ProcessingStatus.Parsing;
                await dbContext.SaveChangesAsync(cancellationToken);
            }
            logger.LogInformation(
                "ReceiptFile {ReceiptFileId} already has a Receipt — skipping re-parse, running barrier only",
                receiptFileId);
        }
        else
        {
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

                // PdfPig/Tesseract can emit '\0' for glyphs their font/OCR mapping can't resolve
                // to a Unicode codepoint. PostgreSQL text columns categorically reject NUL bytes
                // (SqlState 22021), which would otherwise fail SaveChangesAsync below.
                rawText = rawText.Replace("\0", string.Empty);

                if (string.IsNullOrWhiteSpace(rawText))
                {
                    // Pitfall: vision fires ONLY on this pre-parse failure branch and
                    // ParserMissing below — never on a clean parse — so the per-receipt
                    // AI cost stays strictly fallback-only (cost containment, VIS-03).
                    if (await TryVisionFallbackAsync(run, receiptFile, ms, cancellationToken))
                    {
                        await RunBarrierAsync(uploadBatchId, receiptFile, batchSize, cancellationToken);
                        return;
                    }

                    await MarkFailedAsync(run, receiptFile, ProcessingStatus.Failed, "NoTextExtracted", "Aus dieser Datei konnte kein Text gelesen werden.");
                    return;
                }

                // Step 2: Parse
                run.Status = ProcessingStatus.Parsing;
                await dbContext.SaveChangesAsync(cancellationToken);

                var parser = parsers.FirstOrDefault(p => p.CanParse(rawText, receiptFile.SourceHint));
                if (parser is null)
                {
                    // Text extraction consumed `ms` — TryVisionFallbackAsync resets its
                    // position before reading, so re-use of the same stream is safe here.
                    if (await TryVisionFallbackAsync(run, receiptFile, ms, cancellationToken))
                    {
                        await RunBarrierAsync(uploadBatchId, receiptFile, batchSize, cancellationToken);
                        return;
                    }

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
        }

        await RunBarrierAsync(uploadBatchId, receiptFile, batchSize, cancellationToken);
    }

    /// <summary>
    /// Barrier: count completed parents in this batch. Last one enqueues the classify job.
    /// Includes Parsing (just-finished current run — parser success or vision-fallback
    /// success alike), Classifying, Completed; excludes Failed/Cancelled (those don't go
    /// through classify).
    /// Explicit equality, NOT >=: ProcessingRun.Status is persisted as a string
    /// (HasConversion&lt;string&gt;()), so a ">=" comparison against the enum compiles fine
    /// in C# but translates to a lexicographic SQL string comparison — under which
    /// "Completed" sorts BEFORE "Parsing" (C &lt; P), silently excluding already-completed
    /// siblings from the count. Never manifested in the plain happy path (every sibling
    /// is freshly at exactly "Parsing" when this barrier runs, so string equality still
    /// matched), but broke retrying one stuck file in a batch whose siblings had already
    /// reached Completed.
    /// </summary>
    private async Task RunBarrierAsync(
        Guid uploadBatchId,
        ReceiptFile receiptFile,
        int batchSize,
        CancellationToken cancellationToken)
    {
        var completedCount = await dbContext.ProcessingRuns
            .Where(r => r.ReceiptFile.UploadBatchId == uploadBatchId
                     && (r.Status == ProcessingStatus.Parsing
                         || r.Status == ProcessingStatus.Classifying
                         || r.Status == ProcessingStatus.Completed))
            .CountAsync(cancellationToken);

        if (completedCount >= batchSize)
        {
            logger.LogInformation("Last parent finished for batch {UploadBatchId}; enqueueing ClassifyBatchJob", uploadBatchId);
            await jobClient.EnqueueAsync<ClassifyBatchJob>(
                j => j.HandleAsync(uploadBatchId, receiptFile.UserId, CancellationToken.None),
                cancellationToken);
        }
    }

    /// <summary>
    /// Pitfall: vision is confined to the two pre-parse failure branches (NoTextExtracted,
    /// ParserMissing) that call this method — never on a clean parse — so per-receipt AI
    /// cost stays strictly fallback-only (cost containment, VIS-03). ClassifyBatchJob is
    /// intentionally untouched (D-02): vision never re-fires on sum-mismatch.
    /// </summary>
    /// <returns>
    /// <c>true</c> when a vision-extracted Receipt was persisted (caller should run the
    /// barrier and return); <c>false</c> when the caller should fall through to the
    /// existing terminal MarkFailedAsync path unchanged.
    /// </returns>
    private async Task<bool> TryVisionFallbackAsync(
        ProcessingRun run,
        ReceiptFile receiptFile,
        MemoryStream ms,
        CancellationToken cancellationToken)
    {
        if (!visionExtractor.IsConfigured)
            return false;

        var isPdf = !IsImageFile(receiptFile.OriginalFileName);
        var mediaType = isPdf ? "application/pdf" : GetMediaType(receiptFile.OriginalFileName);

        var cost = visionSettings.CostPerVisionExtraction;
        var ledger = new List<TokenLedgerEntry> { new(cost, "Vision extraction", receiptFile.Id) };
        var hasTokens = await tokenService.TryConsumeManyAsync(ledger, receiptFile.UserId, cancellationToken);
        if (!hasTokens)
        {
            logger.LogInformation(
                "Vision fallback skipped for ReceiptFile {ReceiptFileId} — insufficient tokens.",
                receiptFile.Id);
            return false;
        }

        // Retry-once-then-degrade (mirrors AiOnlyClassificationService.ClassifyItemsAsync):
        // genuine cancellation must propagate, never be swallowed into a retry or refund.
        VisionExtractionResult? result = null;
        var attempt = 0;
        while (true)
        {
            try
            {
                ms.Position = 0; // text extraction (or the prior failed attempt) consumed it
                result = await visionExtractor.ExtractAsync(ms, mediaType, isPdf, cancellationToken);
                break;
            }
            catch (Exception ex) when (attempt == 0 && !cancellationToken.IsCancellationRequested)
            {
                attempt++;
                logger.LogWarning(ex,
                    "Vision extraction failed on attempt 1 for ReceiptFile {ReceiptFileId} — retrying.",
                    receiptFile.Id);
                await Task.Delay(visionSettings.RetryDelay, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex,
                    "Vision extraction failed on retry for ReceiptFile {ReceiptFileId} — falling back to Failed.",
                    receiptFile.Id);
                await tokenService.RefundManyAsync(ledger, receiptFile.UserId, cancellationToken);
                return false;
            }
        }

        if (result is null || result.Items.Count == 0)
        {
            logger.LogWarning(
                "Vision extraction returned no usable items for ReceiptFile {ReceiptFileId} — falling back to Failed.",
                receiptFile.Id);
            await tokenService.RefundManyAsync(ledger, receiptFile.UserId, cancellationToken);
            return false;
        }

        var total = result.Total ?? result.Items.Sum(i => i.Price);
        var receipt = new Receipt
        {
            Id = Guid.NewGuid(),
            ReceiptFileId = receiptFile.Id,
            Vendor = result.Vendor ?? "Unbekannt",
            PurchaseDate = DateOnly.FromDateTime(receiptFile.UploadedAt),
            SubTotal = total,
            TaxAmount = 0,
            TotalAmount = total,
            RawExtractedText = string.Empty,
            ParsedAt = DateTime.UtcNow,
            ExtractionSource = ExtractionSource.Vision
        };

        foreach (var item in result.Items)
        {
            receipt.Items.Add(new ReceiptItem
            {
                Id = Guid.NewGuid(),
                ReceiptId = receipt.Id,
                Description = item.Description,
                Quantity = 1,
                UnitPrice = item.Price,
                TotalPrice = item.Price
            });
        }

        dbContext.Receipts.Add(receipt);
        run.Status = ProcessingStatus.Parsing; // same status the parser-success path leaves the run in
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Vision fallback succeeded for ReceiptFile {ReceiptFileId} — {ItemCount} items extracted.",
            receiptFile.Id, receipt.Items.Count);

        return true;
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
