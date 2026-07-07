using TaxReader.Domain.Common;

namespace TaxReader.Application.Commands;

/// <summary>
/// Retries multiple stuck receipt files in one request. Composes
/// RetryReceiptFileHandler per file so the ownership check, retryable-state
/// validation, and standalone-batch isolation all stay defined in one place.
/// Best-effort: files that are no longer in a retryable state (e.g. finished
/// between listing and this call) are silently skipped, not treated as an
/// overall failure.
/// </summary>
public class BulkRetryReceiptFilesHandler(RetryReceiptFileHandler retryHandler)
{
    public async Task<Result<int>> HandleAsync(
        BulkRetryReceiptFilesCommand command,
        CancellationToken cancellationToken = default)
    {
        if (command.ReceiptFileIds.Count == 0)
            return Result<int>.Failure("Keine Belege zum erneuten Versuch angegeben.");

        var retriedCount = 0;
        foreach (var id in command.ReceiptFileIds)
        {
            var result = await retryHandler.HandleAsync(new RetryReceiptFileCommand(id), cancellationToken);
            if (result.IsSuccess) retriedCount++;
        }

        return Result<int>.Success(retriedCount);
    }
}
