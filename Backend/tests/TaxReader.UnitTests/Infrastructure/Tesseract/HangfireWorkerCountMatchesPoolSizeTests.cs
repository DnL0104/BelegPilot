using FluentAssertions;

namespace TaxReader.UnitTests.Infrastructure.Tesseract;

/// <summary>
/// Source-grep regression test for RESEARCH § Pitfall 7: WorkerCount > PoolSize causes
/// silent contention as Hangfire workers block indefinitely on
/// <c>Channel.Reader.ReadAsync</c>. Plan 03-01 introduced the alignment in
/// <c>DependencyInjection.cs</c>; this test locks it so future refactors cannot drift.
/// </summary>
public class HangfireWorkerCountMatchesPoolSizeTests
{
    private static string ResolveDependencyInjectionFile()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Backend", "TaxReader.sln")))
        {
            dir = dir.Parent;
        }
        if (dir is null)
        {
            throw new InvalidOperationException("Could not locate repo root from " + AppContext.BaseDirectory);
        }
        return Path.Combine(dir.FullName, "Backend", "src", "TaxReader.Infrastructure", "DependencyInjection.cs");
    }

    [Fact]
    public void DependencyInjection_ReadsTesseractPoolSizeFromConfiguration()
    {
        // Arrange
        var content = File.ReadAllText(ResolveDependencyInjectionFile());

        // Assert
        content.Should().Contain(
            "configuration.GetValue<int>(\"Tesseract:PoolSize\"",
            "DependencyInjection.cs must read Tesseract:PoolSize so Hangfire.WorkerCount can be aligned to it (RESEARCH Pitfall 7)");
    }

    [Fact]
    public void DependencyInjection_AssignsPoolSizeToHangfireWorkerCount()
    {
        // Arrange
        var content = File.ReadAllText(ResolveDependencyInjectionFile());

        // Assert — match the literal assignment shape; either `WorkerCount = poolSize`
        // (current shape) or any future renaming that keeps the variable identity.
        content.Should().Contain(
            "options.WorkerCount = poolSize",
            "Hangfire WorkerCount must be aligned with Tesseract PoolSize via a shared variable (RESEARCH Pitfall 7)");
    }
}
