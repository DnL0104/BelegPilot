using System.Text;
using FluentAssertions;
using TaxReader.Infrastructure.Services;

namespace TaxReader.UnitTests.Services;

/// <summary>
/// Verifies that CsvExportService.Generate emits the StBerG §5-safe disclaimer
/// before the data rows (D-08 requirement — CSV leading #-comment lines).
/// </summary>
public class CsvExportServiceTests
{
    [Fact]
    public void Generate_IncludesDisclaimerCommentLines()
    {
        // Arrange: empty item list to isolate the header/disclaimer output
        var result = CsvExportService.Generate([]);

        // Act: decode as UTF-8 (strip BOM if present for string comparison)
        var text = Encoding.UTF8.GetString(result);

        // Assert: a #-prefixed disclaimer line is present
        text.Should().Contain("# ", because: "CSV must carry a #-prefixed disclaimer line (D-08)");

        // Assert: the disclaimer references § 5 StBerG
        text.Should().Contain("StBerG", because: "disclaimer must cite § 5 StBerG safe-harbour");

        // Assert: the disclaimer uses Helfer / Vorschlag framing (not Beratung)
        text.Should().ContainAny(["Vorschlag", "Hilfsmittel", "Helfer"],
            because: "disclaimer must frame output as Vorschlag/Helfer, not professional advice (StBerG §5-safe)");

        // Assert: the disclaimer line appears BEFORE the column header row
        var disclaimerIndex = text.IndexOf("# ", StringComparison.Ordinal);
        var headerIndex = text.IndexOf("Datum;", StringComparison.Ordinal);
        disclaimerIndex.Should().BeLessThan(headerIndex,
            because: "disclaimer must appear before the data header row");
    }
}
