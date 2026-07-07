using FluentAssertions;
using TaxReader.Infrastructure.Parsers;
using TaxReader.UnitTests.Helpers;

namespace TaxReader.UnitTests.Infrastructure.Parsers;

public class EdukiParserTests
{
    private readonly EdukiParser _parser = new();

    [Theory]
    [InlineData("Eduki", null)]
    [InlineData("eduki", null)]
    [InlineData(null, "eduki.com Rechnung")]
    [InlineData(null, "lehrermarktplatz download")]
    public void CanParse_EdukiIndicators_ReturnsTrue(string? sourceHint, string? rawText)
    {
        _parser.CanParse(rawText ?? "some text", sourceHint).Should().BeTrue();
    }

    [Fact]
    public void CanParse_NoEdukiIndicators_ReturnsFalse()
    {
        _parser.CanParse("Random receipt text", null).Should().BeFalse();
    }

    [Fact]
    public void Parse_WithPriceLines_ExtractsItems()
    {
        var text = """
            eduki.com
            Rechnung 01.02.2025
            Unterrichtsmaterial Mathe 4,99 €
            Arbeitsblatt Deutsch 3,50 €
            """;

        var file = TestDataFactory.CreateReceiptFile(sourceHint: "Eduki");
        var receipt = _parser.Parse(text, file);

        receipt.Vendor.Should().Be("Eduki");
        receipt.Items.Should().HaveCountGreaterOrEqualTo(1);
    }

    [Fact]
    public void Parse_FallbackPath_ExcludesFaelligerBetragLine()
    {
        // Regression test: "Fällig" previously lived only in GenericParser's keyword list.
        // After consolidating into ReceiptParsingHelpers.CommonNonItemKeywordRegex, Eduki's
        // fallback item loop (which now calls IsCommonNonItemLine directly) must exclude it
        // too — its own old TotalKeywordRegex list never had "Fällig" or several other terms
        // GenericParser had (Zahldatum, Rechnungsdatum, Referenz, Anzahl, Preis, ...).
        // EdukiItemRegex requires >=3 chars before the price, so a lone "eduki" line won't
        // satisfy CanParse's own indicator check inside Parse — use a description that forces
        // the fallback path by having no EdukiItemRegex match on the total line alone.
        var text = """
            eduki.com
            Unterrichtsmaterial Mathe 4,99 €
            Fälliger Betrag 4,99 €
            """;

        var file = TestDataFactory.CreateReceiptFile(sourceHint: "Eduki");
        var receipt = _parser.Parse(text, file);

        receipt.Items.Should().NotContain(i => i.Description.Contains("Fällig"));
    }
}
