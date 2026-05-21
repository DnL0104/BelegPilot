using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TaxReader.Application.Interfaces;

namespace TaxReader.Infrastructure.Services.AdminBootstrap;

/// <summary>
/// D-08: reads the Hangfire:SeedAdminEmails CSV at startup and flips User.IsAdmin
/// to true for every matching row. Idempotent — already-admin users stay admin,
/// missing users are quietly ignored. Failures during the seed are logged and
/// swallowed: an inaccessible DB during startup must not crash the host (the
/// dashboard will simply remain ungated until the operator promotes someone
/// manually). Runs once at boot via IHostedService.StartAsync.
/// </summary>
public class SeedAdminUsersHostedService(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<SeedAdminUsersHostedService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var raw = configuration["Hangfire:SeedAdminEmails"];
        if (string.IsNullOrWhiteSpace(raw))
        {
            // Empty config is a valid steady-state — no DB query fired.
            logger.LogDebug("Hangfire:SeedAdminEmails is empty; admin seeding skipped");
            return;
        }

        var emails = raw
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(e => e.ToLowerInvariant())
            .ToHashSet();

        if (emails.Count == 0)
        {
            return;
        }

        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();

            var matched = await db.Users
                .Where(u => emails.Contains(u.Email))
                .ToListAsync(cancellationToken);

            foreach (var user in matched)
            {
                user.IsAdmin = true;
            }
            await db.SaveChangesAsync(cancellationToken);

            logger.LogInformation(
                "Seeded admin role for {Count} user(s) from Hangfire:SeedAdminEmails (requested {Requested})",
                matched.Count,
                emails.Count);
        }
        catch (Exception ex)
        {
            // Swallow + log: a failed seed leaves the dashboard ungated but does NOT
            // crash the host. The operator can promote manually via SQL if needed.
            logger.LogWarning(ex, "Admin user seeding failed; /hangfire dashboard may be inaccessible until promoted manually");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
