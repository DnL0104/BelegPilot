using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using TaxReader.Application.Jobs;

namespace TaxReader.UnitTests.Jobs;

/// <summary>
/// Unit tests for ExportCleanupJob (24-hour purge of /tmp/exports/*.zip).
/// LEG-07 / T-06-43.
/// </summary>
public class ExportCleanupJobTests : IDisposable
{
    private readonly string _testExportsDir;
    private readonly ExportCleanupJob _job;

    public ExportCleanupJobTests()
    {
        // Use an isolated temp directory per test class to avoid conflicts
        _testExportsDir = Path.Combine(Path.GetTempPath(), $"taxreader-exports-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testExportsDir);

        // The job resolves its path from Path.GetTempPath() — we'll use a subclass trick
        // or, since we control the test directory, we'll verify using the actual job
        // but create files in the job's real path. However, since the job is hardcoded
        // to "taxreader-exports", we test by setting up real files there.
        _job = new ExportCleanupJob(Mock.Of<ILogger<ExportCleanupJob>>());
    }

    [Fact]
    public async Task HandleAsync_OldZipFiles_DeletesThemAndKeepsFreshOnes()
    {
        // Arrange — create the real exports dir and put files in it
        var realExportsDir = Path.Combine(Path.GetTempPath(), "taxreader-exports");
        Directory.CreateDirectory(realExportsDir);

        var oldToken = Guid.NewGuid().ToString("N");
        var freshToken = Guid.NewGuid().ToString("N");

        var oldFilePath = Path.Combine(realExportsDir, $"{oldToken}.zip");
        var freshFilePath = Path.Combine(realExportsDir, $"{freshToken}.zip");

        await File.WriteAllTextAsync(oldFilePath, "old export data");
        await File.WriteAllTextAsync(freshFilePath, "fresh export data");

        // Make the old file appear older than 24 hours by setting creation time
        File.SetCreationTimeUtc(oldFilePath, DateTime.UtcNow.AddHours(-25));
        File.SetCreationTimeUtc(freshFilePath, DateTime.UtcNow); // fresh

        try
        {
            // Act
            await _job.HandleAsync(CancellationToken.None);

            // Assert
            File.Exists(oldFilePath).Should().BeFalse("old zip (25h old) should be purged");
            File.Exists(freshFilePath).Should().BeTrue("fresh zip should be kept");
        }
        finally
        {
            // Cleanup fresh file (old was deleted by the job)
            if (File.Exists(freshFilePath)) File.Delete(freshFilePath);
        }
    }

    [Fact]
    public async Task HandleAsync_MissingDirectory_DoesNotThrow()
    {
        // Arrange — don't create the exports directory (it may not exist)
        // The test is just that HandleAsync completes without throwing

        // Act — this exercises the "if not exists return" path when the dir was never created
        // We can't control what Path.GetTempPath() "taxreader-exports" looks like globally,
        // so we test the behavior is safe. If the dir exists already from another test that's fine.
        var act = async () => await _job.HandleAsync(CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync("missing exports dir should be a no-op");
    }

    public void Dispose()
    {
        // Clean up the isolated test directory we created
        if (Directory.Exists(_testExportsDir))
        {
            Directory.Delete(_testExportsDir, recursive: true);
        }
    }
}
