using Hangfire;
using Microsoft.Extensions.Logging;

namespace TaxReader.Application.Jobs;

/// <summary>
/// D-23 #2: prunes Hangfire's internal Failed-state job metadata older than 30 days.
/// ProcessingRun rows are untouched (DB audit); only Hangfire's job table is pruned.
/// Scheduled by RecurringJobsBootstrap to fire weekly on Sunday at 04:00 UTC.
/// </summary>
[DisableConcurrentExecution(timeoutInSeconds: 600)]
[AutomaticRetry(Attempts = 0)]
public class HangfireFailedJobCleanupJob(
    IBackgroundJobClient backgroundJobClient,
    ILogger<HangfireFailedJobCleanupJob> logger)
{
    public Task HandleAsync(CancellationToken cancellationToken)
    {
        var api = JobStorage.Current.GetMonitoringApi();
        var cutoff = DateTime.UtcNow.AddDays(-30);
        var failed = api.FailedJobs(0, int.MaxValue);
        var pruned = 0;
        foreach (var entry in failed)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            if (entry.Value?.FailedAt is { } failedAt && failedAt < cutoff)
            {
                backgroundJobClient.Delete(entry.Key);
                pruned++;
            }
        }
        logger.LogInformation(
            "Hangfire failed-job cleanup pruned {Count} jobs older than {Cutoff:o}",
            pruned,
            cutoff);
        return Task.CompletedTask;
    }
}
