using System.Globalization;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TaxReader.Application.DTOs;
using TaxReader.Application.Interfaces;
using TaxReader.Application.Jobs;
using TaxReader.Domain.Common;
using TaxReader.Domain.Entities;
using TaxReader.Domain.Enums;

namespace TaxReader.Application.Commands;

public class UploadReceiptFilesHandler(
    IAppDbContext dbContext,
    IUploadBlobStore blobStore,
    IBackgroundJobClient jobClient,
    ICurrentUser currentUser,
    ILogger<UploadReceiptFilesHandler> logger)
{
    public async Task<Result<UploadAcceptedResponse>> HandleAsync(
        UploadReceiptFilesCommand command,
        CancellationToken cancellationToken = default)
    {
        var userId = currentUser.UserId;

        // Hash every file up front so duplicates (against a prior upload, or another file
        // in this same batch) can be identified and skipped BEFORE anything is written —
        // previously the unique (user_id, content_hash) index only caught this at
        // SaveChangesAsync, after blobs were written and Hangfire jobs for the WHOLE batch
        // (including non-duplicate siblings) had already been enqueued, so one duplicate
        // corrupted the entire upload with a generic error instead of a per-file answer.
        var hashedFiles = new List<(FileUploadItem File, string Hash)>(command.Files.Count);
        foreach (var file in command.Files)
            hashedFiles.Add((file, await ComputeHashAsync(file.Stream, cancellationToken)));

        var candidateHashes = hashedFiles.Select(h => h.Hash).Distinct().ToList();
        var existingByHash = await dbContext.ReceiptFiles
            .Where(f => f.UserId == userId && candidateHashes.Contains(f.ContentHash))
            .Select(f => new { f.ContentHash, f.OriginalFileName, f.UploadedAt })
            .ToDictionaryAsync(f => f.ContentHash, cancellationToken);

        var duplicates = new List<DuplicateFileInfo>();
        var toProcess = new List<(FileUploadItem File, string Hash)>();
        var seenInBatch = new Dictionary<string, string>();

        foreach (var (file, hash) in hashedFiles)
        {
            if (existingByHash.TryGetValue(hash, out var existing))
            {
                duplicates.Add(new DuplicateFileInfo(
                    file.FileName,
                    $"Bereits hochgeladen am {existing.UploadedAt.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture)} als \"{existing.OriginalFileName}\"."));
                continue;
            }

            if (seenInBatch.TryGetValue(hash, out var firstInBatchName))
            {
                duplicates.Add(new DuplicateFileInfo(
                    file.FileName,
                    $"Identisch mit \"{firstInBatchName}\" in diesem Upload."));
                continue;
            }

            seenInBatch[hash] = file.FileName;
            toProcess.Add((file, hash));
        }

        // Generate one batch ID for the whole upload; every ReceiptFile carries it.
        var uploadBatchId = Guid.NewGuid();
        var acceptedFiles = new List<UploadAcceptedFile>(toProcess.Count);
        var blobsWritten = new List<Guid>();
        var receiptFilesAdded = new List<ReceiptFile>();
        var runsAdded = new List<ProcessingRun>();
        var batchSize = toProcess.Count;

        try
        {
            // Pass 1: create every row and write every blob, then commit — BEFORE
            // enqueueing any Hangfire job. Previously rows were created and jobs
            // enqueued in the same loop iteration, with the actual SaveChangesAsync
            // deferred to after the whole loop; a worker could dequeue and start
            // ProcessReceiptFileJob before that commit landed, hit "row not found",
            // and return without throwing — which Hangfire counts as Succeeded, so
            // the file was silently stuck forever with zero error surfaced.
            foreach (var (file, hash) in toProcess)
            {
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
                    UploadBatchId = uploadBatchId,
                    Status = FileStatus.Processing,
                    UploadedAt = DateTime.UtcNow
                };
                dbContext.ReceiptFiles.Add(receiptFile);
                receiptFilesAdded.Add(receiptFile);

                var run = new ProcessingRun
                {
                    Id = Guid.NewGuid(),
                    ReceiptFileId = receiptFile.Id,
                    Status = ProcessingStatus.Pending,
                    StartedAt = DateTime.UtcNow
                };
                dbContext.ProcessingRuns.Add(run);
                runsAdded.Add(run);

                // Persist file bytes BEFORE enqueueing so the job can always read them
                // across container restarts and retries (D-15 addendum).
                file.Stream.Position = 0;
                await blobStore.SaveAsync(receiptFile.Id, file.Stream, cancellationToken);
                blobsWritten.Add(receiptFile.Id);
            }

            if (toProcess.Count > 0)
                await dbContext.SaveChangesAsync(cancellationToken);

            // Pass 2: every row is now durably committed and visible to any Hangfire
            // worker, however fast it picks up the job — safe to enqueue.
            // D-01: enqueue ProcessReceiptFileJob per file. ClassifyBatchJob is
            // enqueued by the LAST parent from inside ProcessReceiptFileJob (barrier).
            // batchSize is the accepted-file count, NOT command.Files.Count — a
            // duplicate skipped above never gets enqueued, so the barrier must not
            // wait for it.
            for (var i = 0; i < toProcess.Count; i++)
            {
                var receiptFile = receiptFilesAdded[i];
                var run = runsAdded[i];
                var fileName = toProcess[i].File.FileName;

                var jobId = await jobClient.EnqueueAsync<ProcessReceiptFileJob>(
                    j => j.HandleAsync(receiptFile.Id, uploadBatchId, batchSize, CancellationToken.None),
                    cancellationToken);

                run.HangfireJobId = jobId;
                acceptedFiles.Add(new UploadAcceptedFile(receiptFile.Id, jobId, fileName));
            }

            if (toProcess.Count > 0)
                await dbContext.SaveChangesAsync(cancellationToken);

            return Result<UploadAcceptedResponse>.Success(new UploadAcceptedResponse(acceptedFiles, duplicates));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Upload failed before enqueue completed for batch {UploadBatchId}", uploadBatchId);

            // Remove any EF-tracked entities that haven't been saved
            foreach (var rf in receiptFilesAdded)
                dbContext.ReceiptFiles.Remove(rf);
            foreach (var r in runsAdded)
                dbContext.ProcessingRuns.Remove(r);
            try { await dbContext.SaveChangesAsync(CancellationToken.None); } catch { /* best-effort */ }

            // Best-effort blob cleanup
            foreach (var id in blobsWritten)
            {
                try { await blobStore.DeleteAsync(id, CancellationToken.None); }
                catch (Exception deleteEx)
                {
                    logger.LogWarning(deleteEx, "Could not clean up blob {ReceiptFileId} after failed upload", id);
                }
            }
            return Result<UploadAcceptedResponse>.Failure("Upload fehlgeschlagen — bitte erneut versuchen.");
        }
    }

    private static async Task<string> ComputeHashAsync(Stream stream, CancellationToken ct)
    {
        stream.Position = 0;
        using var sha = SHA256.Create();
        var bytes = await sha.ComputeHashAsync(stream, ct);
        return Convert.ToHexString(bytes);
    }
}
