using System.Security.Cryptography;
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
        // Generate one batch ID for the whole upload; every ReceiptFile carries it.
        var uploadBatchId = Guid.NewGuid();
        var acceptedFiles = new List<UploadAcceptedFile>(command.Files.Count);
        var blobsWritten = new List<Guid>();
        var receiptFilesAdded = new List<ReceiptFile>();
        var runsAdded = new List<ProcessingRun>();

        try
        {
            foreach (var file in command.Files)
            {
                var receiptFile = new ReceiptFile
                {
                    Id = Guid.NewGuid(),
                    UserId = currentUser.UserId,
                    OriginalFileName = file.FileName,
                    ContentHash = await ComputeHashAsync(file.Stream, cancellationToken),
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

                // D-01: enqueue ProcessReceiptFileJob per file. ClassifyBatchJob is
                // enqueued by the LAST parent from inside ProcessReceiptFileJob (barrier).
                var batchSize = command.Files.Count;
                var jobId = await jobClient.EnqueueAsync<ProcessReceiptFileJob>(
                    j => j.HandleAsync(receiptFile.Id, uploadBatchId, batchSize, CancellationToken.None),
                    cancellationToken);

                run.HangfireJobId = jobId;
                acceptedFiles.Add(new UploadAcceptedFile(receiptFile.Id, jobId, file.FileName));
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            return Result<UploadAcceptedResponse>.Success(new UploadAcceptedResponse(acceptedFiles));
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
