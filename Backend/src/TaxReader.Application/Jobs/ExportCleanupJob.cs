using Hangfire;
using Microsoft.Extensions.Logging;

namespace TaxReader.Application.Jobs;

/// <summary>
/// Daily recurring Hangfire job that purges export zip files older than 24 hours.
/// Scheduled at 02:00 UTC by RecurringJobsBootstrap.
/// LEG-07 / D-10 / T-06-43.
/// Pattern: RefreshTokenCleanupJob (logger-only ctor, [DisableConcurrentExecution], [AutomaticRetry(0)]).
/// </summary>
[DisableConcurrentExecution(timeoutInSeconds: 600)]
[AutomaticRetry(Attempts = 0)]
public class ExportCleanupJob(ILogger<ExportCleanupJob> logger)
{
    public Task HandleAsync(CancellationToken cancellationToken)
    {
        var exportsDir = Path.Combine(Path.GetTempPath(), "taxreader-exports");

        if (!Directory.Exists(exportsDir))
        {
            logger.LogInformation("Export cleanup: directory {ExportsDir} does not exist, nothing to purge",
                exportsDir);
            return Task.CompletedTask;
        }

        var cutoff = DateTime.UtcNow.AddHours(-24);
        var deleted = 0;

        foreach (var file in Directory.GetFiles(exportsDir, "*.zip"))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (File.GetCreationTimeUtc(file) < cutoff)
            {
                try
                {
                    File.Delete(file);
                    deleted++;
                }
                catch (IOException ex)
                {
                    // Log but continue — don't fail the entire cleanup run for one file
                    logger.LogWarning(ex, "Export cleanup: failed to delete {FilePath}", file);
                }
            }
        }

        logger.LogInformation("Export cleanup deleted {Count} zip file(s) older than {Cutoff:o}",
            deleted, cutoff);

        return Task.CompletedTask;
    }
}
