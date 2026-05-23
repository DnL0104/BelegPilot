using FluentAssertions;

namespace TaxReader.UnitTests.Pipeline;

/// <summary>
/// Source-grep regression tests for the AddQueuedAndCancelledProcessingStatuses migration.
/// 03-RESEARCH Pitfall 8 mitigation: when D-06 reorders the ProcessingStatus enum, any
/// pre-existing integer-stored status row would map to the wrong value post-migration.
/// The mitigation is descending-order UPDATE statements (5→6, 4→5, 3→4, 2→3, 1→2) BEFORE
/// the EF column changes take effect. These tests lock the SQL strings in place so a
/// future refactor that drops or reorders them fails CI loudly.
///
/// Note: ProcessingRun.Status is currently mapped via HasConversion&lt;string&gt;() so the
/// SQL operates against the string column (production rows like 'Pending', 'Extracting').
/// The numeric UPDATE statements are safe no-ops against existing string-stored rows
/// (no row matches 'status = 5'), but they MUST remain present so any future migration
/// to integer storage carries the data-safety guarantees out of the box.
/// </summary>
public class ProcessingRunStatusMigrationTests
{
    private static string MigrationsDir()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "src", "TaxReader.Infrastructure", "Migrations");
        return Path.GetFullPath(path);
    }

    private static string FindMigrationFile()
    {
        var dir = MigrationsDir();
        var matches = Directory.GetFiles(dir, "*_AddQueuedAndCancelledProcessingStatuses.cs", SearchOption.TopDirectoryOnly);
        matches.Should().NotBeEmpty($"migration file should exist in {dir}");
        return matches[0];
    }

    [Fact]
    public void Migration_File_Exists()
    {
        var file = FindMigrationFile();
        File.Exists(file).Should().BeTrue();
    }

    [Fact]
    public void Migration_Up_ContainsDescendingRenumberStatements()
    {
        var source = File.ReadAllText(FindMigrationFile());

        // Descending order: 5→6, 4→5, 3→4, 2→3, 1→2. Order matters per Pitfall 8 — process
        // from highest old value to lowest so we never collide with a row we're about to write.
        // Values are quoted strings ('6', '5', …) because the column is character varying —
        // PostgreSQL rejects varchar = integer comparison (42883 operator does not exist).
        source.Should().Contain("UPDATE processing_runs SET status = '6' WHERE status = '5'");
        source.Should().Contain("UPDATE processing_runs SET status = '5' WHERE status = '4'");
        source.Should().Contain("UPDATE processing_runs SET status = '4' WHERE status = '3'");
        source.Should().Contain("UPDATE processing_runs SET status = '3' WHERE status = '2'");
        source.Should().Contain("UPDATE processing_runs SET status = '2' WHERE status = '1'");
    }

    [Fact]
    public void Migration_Up_AddsNewColumns()
    {
        var source = File.ReadAllText(FindMigrationFile());

        // processing_runs.error_code — D-21 stable code for the frontend.
        source.Should().Contain("error_code");
        // processing_runs.hangfire_job_id — D-14 cancel target.
        source.Should().Contain("hangfire_job_id");
        // receipt_files.upload_batch_id — D-01 fan-in barrier key.
        source.Should().Contain("upload_batch_id");
    }

    [Fact]
    public void Migration_Down_ContainsReverseRenumberStatements()
    {
        var source = File.ReadAllText(FindMigrationFile());

        // Reverse order (ascending) on Down: 2→1, 3→2, 4→3, 5→4, 6→5. Same collision-
        // avoidance logic reversed (lowest first so we never collide). Quoted string literals.
        source.Should().Contain("UPDATE processing_runs SET status = '1' WHERE status = '2'");
        source.Should().Contain("UPDATE processing_runs SET status = '2' WHERE status = '3'");
        source.Should().Contain("UPDATE processing_runs SET status = '3' WHERE status = '4'");
        source.Should().Contain("UPDATE processing_runs SET status = '4' WHERE status = '5'");
        source.Should().Contain("UPDATE processing_runs SET status = '5' WHERE status = '6'");
    }
}
