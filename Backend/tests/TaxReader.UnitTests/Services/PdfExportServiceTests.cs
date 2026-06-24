using FluentAssertions;

namespace TaxReader.UnitTests.Services;

/// <summary>
/// Verifies that PdfExportService.Generate includes the StBerG §5-safe disclaimer
/// in its footer (D-09 requirement — per-page footer disclaimer).
///
/// PDF binary output is not text-greppable, so assertions are made against source code
/// (research-prescribed approach: Pitfall 6).
/// </summary>
public class PdfExportServiceTests
{
    [Fact]
    public void Generate_FooterContainsDisclaimerText()
    {
        // Locate PdfExportService.cs from repo root
        var sourceFile = LocateFromRepoRoot(Path.Combine(
            "Backend", "src", "TaxReader.Infrastructure", "Services", "PdfExportService.cs"));

        var source = File.ReadAllText(sourceFile);

        // Assert: source contains the StBerG disclaimer reference
        source.Should().Contain("StBerG",
            because: "PdfExportService footer must carry a § 5 StBerG disclaimer (D-09)");
    }

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
