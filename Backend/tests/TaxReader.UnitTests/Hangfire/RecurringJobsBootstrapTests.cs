using System.Net;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using TaxReader.Application.Interfaces;
using TaxReader.Application.Jobs;
using TaxReader.Domain.Entities;
using TaxReader.Infrastructure.Data;

namespace TaxReader.UnitTests.Hangfire;

/// <summary>
/// D-23: RecurringJobsBootstrap registers the four recurring jobs. The cron strings
/// and recurring-job IDs are source-grepped here so a future refactor of the
/// schedule must update this guard before passing the suite. The 7-day grace
/// (D-16) is verified by running RefreshTokenCleanupJob against three seeded
/// rows.
/// </summary>
public class RecurringJobsBootstrapTests
{
    [Fact]
    public void RecurringJobsBootstrap_RegistersBothJobsWithExpectedCronStrings()
    {
        var path = LocateApiFile(Path.Combine("Hangfire", "RecurringJobsBootstrap.cs"));
        var source = File.ReadAllText(path);

        source.Should().Contain("\"refresh-tokens-cleanup\"",
            "the refresh-token recurring job MUST keep its stable id");
        source.Should().Contain("\"hangfire-failed-cleanup\"",
            "the Hangfire failed-job recurring job MUST keep its stable id");
        source.Should().Contain("\"0 3 * * *\"",
            "D-23: refresh-token cleanup runs daily at 03:00 UTC");
        source.Should().Contain("\"0 4 * * 0\"",
            "D-23: Hangfire failed-job cleanup runs Sunday at 04:00 UTC");
    }

    [Fact]
    public void RecurringJobsBootstrap_RegistersAuditLogRetentionJobWithExpectedIdAndCron()
    {
        var path = LocateApiFile(Path.Combine("Hangfire", "RecurringJobsBootstrap.cs"));
        var source = File.ReadAllText(path);

        source.Should().Contain("\"audit-log-retention\"",
            "D-05/D-07: the audit-log retention recurring job MUST keep its stable id");
        source.Should().Contain("\"0 1 * * *\"",
            "D-07: audit-log retention runs daily at 01:00 UTC (before export-cleanup at 02:00)");
    }

    [Fact]
    public void RecurringJobsBootstrap_RegistersExportCleanupJobWithExpectedIdAndCron()
    {
        var path = LocateApiFile(Path.Combine("Hangfire", "RecurringJobsBootstrap.cs"));
        var source = File.ReadAllText(path);

        source.Should().Contain("\"export-cleanup\"",
            "LEG-07: the export-cleanup recurring job MUST keep its stable id");
        source.Should().Contain("\"0 2 * * *\"",
            "LEG-07: export-cleanup runs daily at 02:00 UTC (after audit-log retention at 01:00)");
    }

    [Fact]
    public void RefreshTokenCleanupJob_CarriesHangfireAttributes()
    {
        var path = LocateApplicationFile(Path.Combine("Jobs", "RefreshTokenCleanupJob.cs"));
        var source = File.ReadAllText(path);

        source.Should().Contain("[DisableConcurrentExecution(timeoutInSeconds: 600)]",
            "T-03-09: concurrent invocations must be blocked");
        source.Should().Contain("[AutomaticRetry(Attempts = 0)]",
            "D-04: cleanup retries are pointless; let the next schedule pick up the slack");
    }

    [Fact]
    public async Task RefreshTokenCleanupJob_DeletesOnlyExpiredBeyond7DayGrace()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var db = new AppDbContext(options);

        // Seed a user so the FK on refresh_tokens.user_id is valid.
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "cleanup@test.local",
            DisplayName = "Cleanup",
            PasswordHash = "x"
        };
        db.Users.Add(user);

        var now = DateTime.UtcNow;
        db.RefreshTokens.Add(BuildRow(user.Id, expiresAt: now.AddDays(-15), id: "old"));
        db.RefreshTokens.Add(BuildRow(user.Id, expiresAt: now.AddDays(-2),  id: "recent"));
        db.RefreshTokens.Add(BuildRow(user.Id, expiresAt: now.AddDays(+30), id: "future"));
        await db.SaveChangesAsync();

        var job = new RefreshTokenCleanupJob(db, NullLogger<RefreshTokenCleanupJob>.Instance);
        await job.HandleAsync(CancellationToken.None);

        var survivors = await db.RefreshTokens.ToListAsync();
        survivors.Should().HaveCount(2);
        survivors.Should().NotContain(t => t.TokenHash == "old",
            "tokens expired more than 7 days ago must be deleted");
        survivors.Should().Contain(t => t.TokenHash == "recent",
            "tokens expired within the 7-day grace window must survive");
        survivors.Should().Contain(t => t.TokenHash == "future",
            "tokens not yet expired must survive");
    }

    private static RefreshToken BuildRow(Guid userId, DateTime expiresAt, string id)
    {
        return new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = id, // re-purpose as a label for test assertions
            CreatedAt = expiresAt.AddDays(-30),
            ExpiresAt = expiresAt,
            IpAddress = IPAddress.Loopback
        };
    }

    private static string LocateApiFile(string relative) =>
        LocateFromRepoRoot(Path.Combine("Backend", "src", "TaxReader.Api", relative));

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
