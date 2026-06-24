using System.Text;
using FluentAssertions;
using TaxReader.Application.DTOs;
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

    [Fact]
    public void Generate_EmitsUtf8BomAsFirstBytes()
    {
        // CR-01: the UTF-8 BOM must be the very first 3 bytes, otherwise Excel/LibreOffice
        // mis-detect the encoding and render the disclaimer/data umlauts as mojibake.
        var result = CsvExportService.Generate([]);

        result.Length.Should().BeGreaterThanOrEqualTo(3);
        result[0].Should().Be(0xEF);
        result[1].Should().Be(0xBB);
        result[2].Should().Be(0xBF);
    }

    [Theory]
    [InlineData("=1+1")]
    [InlineData("+CMD")]
    [InlineData("-2+3")]
    [InlineData("@SUM(A1:A9)")]
    public void Generate_NeutralizesFormulaInjectionInTextFields(string maliciousVendor)
    {
        // CR-02: vendor/description/reason originate from OCR'd user uploads. A value
        // beginning with a formula trigger must be prefixed with a single quote so it is
        // not executed when the export is opened in a spreadsheet.
        var item = new ExportItemDto(
            PurchaseDate: new DateOnly(2026, 1, 15),
            Vendor: maliciousVendor,
            Description: "Test",
            Quantity: 1,
            UnitPrice: 1.00m,
            TotalPrice: 1.00m,
            Category: "Privat",
            ClassificationStatus: "Confirmed",
            ClassificationMethod: "Manual",
            Reason: null);

        var text = Encoding.UTF8.GetString(CsvExportService.Generate([item]));

        text.Should().Contain("'" + maliciousVendor,
            because: "a formula-triggering value must be neutralized with a leading single quote");
    }
}
