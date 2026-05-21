using FluentAssertions;

namespace TaxReader.UnitTests.Infrastructure.Tesseract;

/// <summary>
/// Source-grep regression tests that prevent re-introduction of the old Singleton+lock
/// <c>TesseractImageTextExtractor</c> class. The plan removes the file outright; these
/// tests fail loudly if anyone copy-pastes it back or leaves a dangling reference.
/// </summary>
public class TesseractImageTextExtractorRemovedTests
{
    private static string ResolveBackendSrc()
    {
        // Walk up from AppContext.BaseDirectory to the repo root, then descend to Backend/src.
        // Test bin path looks like: <repo>/Backend/tests/TaxReader.UnitTests/bin/Debug/net10.0/
        // -> ../../../../../../Backend/src
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Backend", "TaxReader.sln")))
        {
            dir = dir.Parent;
        }
        if (dir is null)
        {
            throw new InvalidOperationException("Could not locate repo root (Backend/TaxReader.sln) from " + AppContext.BaseDirectory);
        }
        return Path.Combine(dir.FullName, "Backend", "src");
    }

    [Fact]
    public void OldExtractorFile_IsDeleted()
    {
        // Arrange
        var backendSrc = ResolveBackendSrc();

        // Act
        var matches = Directory.GetFiles(backendSrc, "TesseractImageTextExtractor.cs", SearchOption.AllDirectories);

        // Assert
        matches.Should().BeEmpty(
            "the Singleton+lock TesseractImageTextExtractor was replaced by TesseractEnginePool; " +
            "no file with that basename may remain under Backend/src");
    }

    [Fact]
    public void OldExtractorClassName_HasNoReferencesInNonCommentSourceLines()
    {
        // Arrange
        var backendSrc = ResolveBackendSrc();
        var allCs = Directory.GetFiles(backendSrc, "*.cs", SearchOption.AllDirectories);

        // Act — collect non-comment lines that mention the type name.
        var offendingReferences = new List<string>();
        foreach (var file in allCs)
        {
            var lines = File.ReadAllLines(file);
            for (var i = 0; i < lines.Length; i++)
            {
                var trimmed = lines[i].TrimStart();
                if (trimmed.StartsWith("//") || trimmed.StartsWith("/*") || trimmed.StartsWith("*"))
                {
                    continue;
                }
                if (trimmed.Contains("TesseractImageTextExtractor"))
                {
                    offendingReferences.Add($"{file}:{i + 1}: {lines[i]}");
                }
            }
        }

        // Assert
        offendingReferences.Should().BeEmpty(
            "all source-level references to TesseractImageTextExtractor must be removed; " +
            "TesseractEnginePool is the replacement");
    }
}
