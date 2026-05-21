using FluentAssertions;

namespace TaxReader.UnitTests.Hangfire;

/// <summary>
/// RESEARCH Pitfall 1: MigrateAsync MUST run BEFORE UseHangfireDashboard.
/// If a future refactor reorders Program.cs and reintroduces the bug, this
/// source-level wiring test fails — surfacing the regression without
/// needing a live Postgres instance. Pattern from Phase 1 plan 01-04.
/// </summary>
public class HangfireWiringTests
{
    [Fact]
    public void Program_RegistersHangfireDashboard_AfterAuthorizationAndMigration()
    {
        var programPath = LocateProgramCs();
        var source = File.ReadAllText(programPath);

        var authIdx = source.IndexOf("app.UseAuthorization()", StringComparison.Ordinal);
        var migrateIdx = source.IndexOf("dbContext.Database.MigrateAsync", StringComparison.Ordinal);
        var dashboardIdx = source.IndexOf("app.UseHangfireDashboard", StringComparison.Ordinal);

        authIdx.Should().BeGreaterThan(0, "UseAuthorization must be present in Program.cs");
        migrateIdx.Should().BeGreaterThan(0, "MigrateAsync must be present in Program.cs");
        dashboardIdx.Should().BeGreaterThan(0, "UseHangfireDashboard must be present in Program.cs");

        authIdx.Should().BeLessThan(dashboardIdx,
            "Pitfall 1: dashboard mounts after UseAuthorization so the filter sees claims");
        migrateIdx.Should().BeLessThan(dashboardIdx,
            "Pitfall 1: EF MigrateAsync must finish before Hangfire prepares its schema "
          + "on the same connection pool");
    }

    private static string LocateProgramCs()
    {
        var dir = AppContext.BaseDirectory;
        for (var i = 0; i < 10; i++)
        {
            var candidate = Path.Combine(dir, "Backend", "src", "TaxReader.Api", "Program.cs");
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
        throw new FileNotFoundException("Could not locate Backend/src/TaxReader.Api/Program.cs");
    }
}
