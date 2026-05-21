using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TaxReader.Application.Interfaces;

namespace TaxReader.Application.Jobs;

/// <summary>
/// D-23 #1: removes refresh_tokens rows whose ExpiresAt is older than now - 7 days.
/// The 7-day grace beyond expiry preserves brief audit-query visibility post-expiration
/// (Phase 2 D-16 deferred this cleanup to Phase 3). Scheduled by
/// RecurringJobsBootstrap to fire daily at 03:00 UTC.
/// </summary>
[DisableConcurrentExecution(timeoutInSeconds: 600)]
[AutomaticRetry(Attempts = 0)]
public class RefreshTokenCleanupJob(IAppDbContext dbContext, ILogger<RefreshTokenCleanupJob> logger)
{
    public async Task HandleAsync(CancellationToken cancellationToken)
    {
        var cutoff = DateTime.UtcNow.AddDays(-7);

        // Materialise expired rows then RemoveRange. We do NOT use ExecuteDeleteAsync
        // here because the unit-test suite uses the EF InMemory provider which does
        // not implement that surface; the cleanup is a daily cron run against
        // refresh_tokens, which carries well under 100k rows even at the 100–500
        // user target — the entity-tracked delete is fast enough.
        var expired = await dbContext.RefreshTokens
            .Where(t => t.ExpiresAt < cutoff)
            .ToListAsync(cancellationToken);

        if (expired.Count > 0)
        {
            dbContext.RefreshTokens.RemoveRange(expired);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        logger.LogInformation(
            "Refresh-token cleanup deleted {Count} expired rows (cutoff {Cutoff:o})",
            expired.Count,
            cutoff);
    }
}
