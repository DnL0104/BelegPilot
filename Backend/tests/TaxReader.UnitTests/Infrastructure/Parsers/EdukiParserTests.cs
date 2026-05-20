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
}
