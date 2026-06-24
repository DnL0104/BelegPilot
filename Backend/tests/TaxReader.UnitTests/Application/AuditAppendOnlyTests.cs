using FluentAssertions;

namespace TaxReader.UnitTests.Application;

/// <summary>
/// LEG-08 T-06-10: Structural-grep tests that assert the append-only invariant:
/// no Remove/RemoveRange/ExecuteDelete calls against AuditLogEntries anywhere
/// in Application or Infrastructure source, except the deliberate retention
/// prune in AuditLogRetentionJob (LEGAL-07). Also verifies EF config has no
/// cascade FK on actor/subject.
/// </summary>
public class AuditAppendOnlyTests
{
    private static readonly string[] SourceRoots =
    [
        Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
            "..", "..", "..", "..", "..", "..",
            "Backend", "src", "TaxReader.Application")),
        Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
            "..", "..", "..", "..", "..", "..",
            "Backend", "src", "TaxReader.Infrastructure")),
    ];

    // The append-only invariant blocks *accidental* deletion of audit rows. The single
    // deliberate, controlled pruning path is AuditLogRetentionJob (LEGAL-07 D-05/06/07):
    // GDPR data minimisation mandates removing entries older than the retention window.
    // It is exempt by exact filename so deletions from any other file still fail this test.
    private static readonly string[] ExemptFileNames = ["AuditLogRetentionJob.cs"];

    private static IEnumerable<string> AllSourceLines(string dir)
    {
        // WR-S01: fail loud, not vacuously. Returning [] here meant a source-less CI run
        // (artifacts only) would pass the append-only invariant having checked nothing.
        if (!Directory.Exists(dir))
            throw new DirectoryNotFoundException(
                $"Append-only audit scan requires source at '{dir}'. Run from a source checkout.");

        return Directory.EnumerateFiles(dir, "*.cs", SearchOption.AllDirectories)
            .Where(file => !ExemptFileNames.Contains(Path.GetFileName(file)))
            .SelectMany(file => File.ReadAllLines(file)
                .Select(line => line.Trim())
                .Where(line => !line.StartsWith("//")));
    }

    [Fact]
    public void AuditLogEntries_HasNoRemoveOrDeleteCallsInApplicationOrInfrastructure()
    {
        var forbiddenPatterns = new[]
        {
            "AuditLogEntries.Remove(",
            "AuditLogEntries.RemoveRange(",
            ".AuditLogEntries.ExecuteDelete",
        };

        var violations = SourceRoots
            .SelectMany(AllSourceLines)
            .Where(line => forbiddenPatterns.Any(p => line.Contains(p, StringComparison.Ordinal)))
            .ToList();

        violations.Should().BeEmpty(
            "audit_log is append-only — no Remove/RemoveRange/ExecuteDelete must target AuditLogEntries");
    }

    [Fact]
    public void AuditLogEntryConfiguration_HasNoOnDeleteCascadeOrHasOneForeignKey()
    {
        var configPath = Path.GetFullPath(Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "..", "..",
            "Backend", "src", "TaxReader.Infrastructure",
            "Data", "Configurations", "AuditLogEntryConfiguration.cs"));

        File.Exists(configPath).Should().BeTrue(
            "AuditLogEntryConfiguration.cs must exist");

        var source = File.ReadAllText(configPath);

        source.Should().NotContain("OnDelete(DeleteBehavior.Cascade)",
            "audit rows must not cascade-delete when the actor user is deleted (T-06-13)");
        source.Should().NotContain(".HasOne(",
            "no FK navigation to User — actor/subject ids are nullable columns only (D-14)");
    }
}
