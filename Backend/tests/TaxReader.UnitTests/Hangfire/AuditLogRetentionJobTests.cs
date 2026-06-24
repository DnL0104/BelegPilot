using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using TaxReader.Application.Jobs;
using TaxReader.Domain.Entities;
using TaxReader.Infrastructure.Data;

namespace TaxReader.UnitTests.Hangfire;

/// <summary>
/// D-06: Seeded-date unit tests for AuditLogRetentionJob. Proves the 90-day
/// hard-delete boundary (D-05). Uses the EF InMemory provider — no ExecuteDeleteAsync.
/// </summary>
public class AuditLogRetentionJobTests
{
    [Fact]
    public async Task HandleAsync_DeletesOnlyEntriesOlderThan90Days()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var db = new AppDbContext(options);

        // Seed three audit entries at known CreatedAt offsets.
        var now = DateTime.UtcNow;

        // Must be deleted (91 days old — older than the 90-day boundary).
        db.AuditLogEntries.Add(new AuditLogEntry
        {
            Id = Guid.NewGuid(),
            Action = "AccountDeleted",
            CreatedAt = now.AddDays(-91)
        });

        // Must survive (89 days old — within the retention window).
        db.AuditLogEntries.Add(new AuditLogEntry
        {
            Id = Guid.NewGuid(),
            Action = "TokensGranted",
            CreatedAt = now.AddDays(-89)
        });

        // Must survive (45 days old — well within the retention window).
        db.AuditLogEntries.Add(new AuditLogEntry
        {
            Id = Guid.NewGuid(),
            Action = "DataExportRequested",
            CreatedAt = now.AddDays(-45)
        });

        await db.SaveChangesAsync();

        var job = new AuditLogRetentionJob(db, NullLogger<AuditLogRetentionJob>.Instance);
        await job.HandleAsync(CancellationToken.None);

        var survivors = await db.AuditLogEntries.ToListAsync();

        survivors.Should().HaveCount(2,
            "only the entry older than 90 days must be deleted");
        survivors.Should().NotContain(e => e.CreatedAt < now.AddDays(-90),
            "no rows older than 90 days should survive the retention run");
    }

    [Fact]
    public void AuditLogRetentionJob_CarriesHangfireAttributes()
    {
        var path = LocateApplicationFile(Path.Combine("Jobs", "AuditLogRetentionJob.cs"));
        var source = File.ReadAllText(path);

        source.Should().Contain("[DisableConcurrentExecution(timeoutInSeconds: 600)]",
            "T-03-01: concurrent invocations of the retention job must be blocked");
        source.Should().Contain("[AutomaticRetry(Attempts = 0)]",
            "D-06: retention retries are pointless; the next daily schedule picks up the slack");
    }

    private static string LocateApplicationFile(string relative) =>
        LocateFromRepoRoot(Path.Combine("Backend", "src", "TaxReader.Application", relative));

    private static string LocateFromRepoRoot(string relative)
    {
        var dir = AppContext.BaseDirectory;
        for (var i = 0; i < 10; i++)
        {
            var candidate = Path.Combine(dir, relative);
            if (File.Exists(candidate))
            {
                return candidate;
            }
            var parent = Directory.GetParent(dir);
            if (parent is null)
            {
                break;
            }
            dir = parent.FullName;
        }
        throw new FileNotFoundException("Could not locate " + relative);
    }
}
