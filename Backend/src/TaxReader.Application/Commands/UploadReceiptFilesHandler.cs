using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Serilog.Context;
using TaxReader.Application.DTOs;
using TaxReader.Application.Interfaces;
using TaxReader.Application.Mapping;
using TaxReader.Domain.Common;
using TaxReader.Domain.Entities;
using TaxReader.Domain.Enums;

namespace TaxReader.Application.Commands;

public class UploadReceiptFilesHandler(
    IAppDbContext dbContext,
    IPdfTextExtractor pdfExtractor,
    IImageTextExtractor imageExtractor,
    IEnumerable<IReceiptParser> parsers,
    IClassificationService classificationService,
    ICurrentUser currentUser)
{
    private static readonly HashSet<string> ImageExtensions =
        [".jpg", ".jpeg", ".png", ".webp"];

    private static bool IsImageFile(string fileName) =>
        ImageExtensions.Contains(Path.GetExtension(fileName).ToLowerInvariant());

    private static string GetMediaType(string fileName) =>
        Path.GetExtension(fileName).ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png"            => "image/png",
            ".webp"           => "image/webp",
            _                 => throw new InvalidOperationException($"Unsupported image format: {Path.GetExtension(fileName)}")
        };

    public async Task<Result<UploadReceiptFilesResponse>> HandleAsync(
        UploadReceiptFilesCommand command,
        CancellationToken cancellationToken = default)
    {
        var userId = currentUser.UserId;
        var successful = new List<SuccessfulUpload>();
        var failed = new List<FailedUpload>();

        // Cross-receipt batching: parse every file first, queue successes, then run
        // a single AI classification call across all items. The Anthropic roundtrip
        // (~1s with Haiku) dominates total wall time, so collapsing N sequential
        // calls into 1 is the biggest win available without touching the model.
        var pending = new List<PendingReceipt>();

        foreach (var file in command.Files)
        {
            using var ms = new MemoryStream();
            await file.Stream.CopyToAsync(ms, cancellationToken);
            ms.Position = 0;

            // Duplicate detection
            var hash = Convert.ToHexString(await SHA256.HashDataAsync(ms, cancellationToken));

            var existingFile = await dbContext.ReceiptFiles
                .FirstOrDefaultAsync(f => f.UserId == userId && f.ContentHash == hash, cancellationToken);

            if (existingFile is not null)
            {
                if (existingFile.Status == FileStatus.Processed)
                {
                    failed.Add(new FailedUpload(
                        file.FileName,
                        $"Already uploaded (matches '{existingFile.OriginalFileName}').",
                        FailureKind.Duplicate));
                    continue;
                }

                // Any other state (Failed, Processing, Uploaded) means a previous
                // attempt didn't complete successfully — remove it and retry.
                // Cascade removes associated ProcessingRuns automatically.
                dbContext.ReceiptFiles.Remove(existingFile);
                await dbContext.SaveChangesAsync(cancellationToken);
            }

            var receiptFile = new ReceiptFile
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                OriginalFileName = file.FileName,
                ContentHash = hash,
                FileSize = file.Length,
                SourceHint = command.SourceHint,
                YearHint = command.YearHint,
                UploadedBy = command.UploadedBy,
                UploadedAt = DateTime.UtcNow,
                Status = FileStatus.Processing
            };
            dbContext.ReceiptFiles.Add(receiptFile);

            var run = new ProcessingRun
            {
                Id = Guid.NewGuid(),
                ReceiptFileId = receiptFile.Id,
                Status = ProcessingStatus.Pending,
                StartedAt = DateTime.UtcNow
            };
            dbContext.ProcessingRuns.Add(run);
            await dbContext.SaveChangesAsync(cancellationToken);

            // D-18: every log line emitted while processing this receipt file carries
            // the ID. Phase 3 will add LogContext.PushProperty("JobId", jobId) at the
            // Hangfire job boundary; explicitly NOT pre-wired now. Push only IDs
            // (non-PII) — never vendor names, item descriptions, or user emails.
            using (LogContext.PushProperty("ReceiptFileId", receiptFile.Id))
            {
                try
                {
                    // Step 1: Extract text from stream in memory
                    run.Status = ProcessingStatus.Extracting;
                    await dbContext.SaveChangesAsync(cancellationToken);

                    ms.Position = 0;
                    var rawText = IsImageFile(file.FileName)
                        ? await imageExtractor.ExtractTextAsync(ms, GetMediaType(file.FileName), cancellationToken)
                        : await pdfExtractor.ExtractTextAsync(ms, cancellationToken);

                    if (string.IsNullOrWhiteSpace(rawText))
                    {
                        await MarkFailedAsync(run, receiptFile, "Could not extract any text from the file.");
                        failed.Add(new FailedUpload(file.FileName, "Could not extract any text from the file.", FailureKind.ProcessingError));
                        continue;
                    }

                    // Step 2: Parse
                    run.Status = ProcessingStatus.Parsing;
                    await dbContext.SaveChangesAsync(cancellationToken);

                    var parser = parsers.FirstOrDefault(p => p.CanParse(rawText, receiptFile.SourceHint));
                    if (parser is null)
                    {
                        await MarkFailedAsync(run, receiptFile, "No suitable parser found for this receipt.");
                        failed.Add(new FailedUpload(file.FileName, "No suitable parser found for this receipt.", FailureKind.ProcessingError));
                        continue;
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

                    // Defer classification to the cross-receipt batch below.
                    pending.Add(new PendingReceipt(receipt, run, receiptFile, file.FileName));
                }
                catch (Exception ex)
                {
                    await MarkFailedAsync(run, receiptFile, $"Processing failed: {ex.Message}");
                    failed.Add(new FailedUpload(file.FileName, $"Processing failed: {ex.Message}", FailureKind.ProcessingError));
                }
            }
        }

        // Step 3: Classify every parsed item across every parsed receipt in a single
        // AI call. Items already carry their ReceiptItemId, so the returned
        // classifications map back automatically — we don't need to track which
        // item belonged to which receipt.
        if (pending.Count > 0)
        {
            foreach (var p in pending)
                p.Run.Status = ProcessingStatus.Classifying;
            await dbContext.SaveChangesAsync(cancellationToken);

            var allItems = pending.SelectMany(p => p.Receipt.Items).ToList();
            if (allItems.Count > 0)
            {
                var classifications = await classificationService.ClassifyItemsAsync(
                    allItems, cancellationToken);

                foreach (var classification in classifications)
                {
                    classification.Id = Guid.NewGuid();
                    dbContext.ItemClassifications.Add(classification);
                }
            }

            // Finalize: mark every successfully-parsed receipt as Processed in one save.
            var now = DateTime.UtcNow;
            foreach (var p in pending)
            {
                p.Run.Status = ProcessingStatus.Completed;
                p.Run.CompletedAt = now;
                p.File.Status = FileStatus.Processed;
                successful.Add(new SuccessfulUpload(p.FileName, p.Receipt.ToDto()));
            }
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return Result<UploadReceiptFilesResponse>.Success(
            new UploadReceiptFilesResponse(successful, failed));
    }

    private async Task MarkFailedAsync(ProcessingRun run, ReceiptFile file, string error)
    {
        run.Status = ProcessingStatus.Failed;
        run.CompletedAt = DateTime.UtcNow;
        run.ErrorMessage = error;
        file.Status = FileStatus.Failed;
        // Use CancellationToken.None — we must persist the failure status even if
        // the original request token was already cancelled (e.g. client disconnected).
        try { await dbContext.SaveChangesAsync(CancellationToken.None); } catch { /* best-effort */ }
    }

    private sealed record PendingReceipt(
        Receipt Receipt,
        ProcessingRun Run,
        ReceiptFile File,
        string FileName);
}
