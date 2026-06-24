using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TaxReader.Application.Interfaces;

namespace TaxReader.Application.Jobs;

/// <summary>
/// D-05/LEGAL-07: hard-deletes AuditLogEntry rows older than 90 days.
/// Scheduled daily at 01:00 UTC by RecurringJobsBootstrap (D-07).
/// Uniform retention: all AuditAction types are pruned at the same 90-day
/// boundary — no differential handling needed at 100–500 user scale.
/// </summary>
[DisableConcurrentExecution(timeoutInSeconds: 600)]
[AutomaticRetry(Attempts = 0)]
public class AuditLogRetentionJob(IAppDbContext dbContext, ILogger<AuditLogRetentionJob> logger)
{
    // D-06: hardcoded, not read from appsettings/IOptions. The 90-day figure is
    // a business/legal constant, not an operator-tunable parameter.
    private const int RetentionDays = 90;

    public async Task HandleAsync(CancellationToken cancellationToken)
    {
        var cutoff = DateTime.UtcNow.AddDays(-RetentionDays);

        // Materialise expired rows then RemoveRange. We do NOT use ExecuteDeleteAsync
        // here because the unit-test suite uses the EF InMemory provider which does
        // not implement that surface (Pitfall 1). The audit_log table stays well under
        // 100k rows at the 100–500 user target — entity-tracked delete is fast enough.
        var expired = await dbContext.AuditLogEntries
            .Where(e => e.CreatedAt < cutoff)
            .ToListAsync(cancellationToken);

        if (expired.Count > 0)
        {
            dbContext.AuditLogEntries.RemoveRange(expired);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        // T-03-02: log only Count and Cutoff — no row content, no user GUID, no email hash.
        logger.LogInformation(
            "Audit-log retention deleted {Count} entries older than {Cutoff:o}",
            expired.Count,
            cutoff);
    }
}
