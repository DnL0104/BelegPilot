using Hangfire;
using TaxReader.Application.Jobs;

namespace TaxReader.Api.Hangfire;

/// <summary>
/// D-23: registers the two recurring cleanup jobs at startup via IRecurringJobManager.
/// Idempotent — re-registration on every boot is safe (Hangfire keys jobs by the
/// recurringJobId string, so AddOrUpdate replaces existing metadata in place).
/// </summary>
public static class RecurringJobsBootstrap
{
    public static void Register(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<IRecurringJobManager>();

        // D-23 #1: daily at 03:00 UTC. Refresh tokens are pruned 7 days past expiry
        // (RefreshTokenCleanupJob handles the cutoff math).
        manager.AddOrUpdate<RefreshTokenCleanupJob>(
            recurringJobId: "refresh-tokens-cleanup",
            methodCall: job => job.HandleAsync(CancellationToken.None),
            cronExpression: "0 3 * * *",
            options: new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });

        // D-23 #2: weekly Sunday at 04:00 UTC. Prunes Hangfire's internal Failed-state
        // job metadata older than 30 days (HangfireFailedJobCleanupJob picks the cutoff).
        manager.AddOrUpdate<HangfireFailedJobCleanupJob>(
            recurringJobId: "hangfire-failed-cleanup",
            methodCall: job => job.HandleAsync(CancellationToken.None),
            cronExpression: "0 4 * * 0",
            options: new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });
    }
}
