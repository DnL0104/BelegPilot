using FluentAssertions;
using TaxReader.Infrastructure.Services;

namespace TaxReader.UnitTests.Infrastructure.Services;

public class OcrTextNormalizerTests
{
    [Theory]
    [InlineData("", "")]
    [InlineData("   ", "   ")]
    public void Normalize_EmptyOrWhitespace_ReturnsUnchanged(string input, string expected)
    {
        OcrTextNormalizer.Normalize(input).Should().Be(expected);
    }

    [Theory]
    [InlineData("9,99EUR",          "9,99 €")]          // digit glued to EUR (no \b between digit and letter)
    [InlineData("Total: 12,50EUR",  "Total: 12,50 €")]
    [InlineData("9.99EUR",          "9.99 €")]
    public void Normalize_DigitAttachedToEur_InsertsEuroSign(string input, string expected)
    {
        OcrTextNormalizer.Normalize(input).Should().Be(expected);
    }

    [Theory]
    [InlineData("9,99 EUR",   "9,99 €")]
    [InlineData("9,99  EUR",  "9,99 €")]    // multiple spaces collapse
    [InlineData("54,99 EUR",  "54,99 €")]
    public void Normalize_DigitSpacedFromEur_InsertsEuroSign(string input, string expected)
    {
        OcrTextNormalizer.Normalize(input).Should().Be(expected);
    }

    [Theory]
    [InlineData("Preis EUR",          "Preis €")]       // standalone EUR (column header)
    [InlineData("Summe EUR gesamt",   "Summe € gesamt")]
    public void Normalize_StandaloneEur_ReplacesWithEuroSign(string input, string expected)
    {
        OcrTextNormalizer.Normalize(input).Should().Be(expected);
    }

    [Theory]
    [InlineData("9,99£",        "9,99€")]
    [InlineData("Total: 12£",   "Total: 12€")]
    public void Normalize_PoundSign_ReplacedWithEuroSign(string input, string expected)
    {
        OcrTextNormalizer.Normalize(input).Should().Be(expected);
    }

    [Theory]
    [InlineData("29,99E",        "29,99 €")]
    [InlineData("29,99 E",       "29,99 €")]
    [InlineData("29,99 E ",      "29,99 € ")]
    [InlineData("1,00E",         "1,00 €")]
    [InlineData("1.50E",         "1.50 €")]
    public void Normalize_TrailingIsolatedEAfterPrice_ReplacedWithEuroSign(string input, string expected)
    {
        OcrTextNormalizer.Normalize(input).Should().Be(expected);
    }

    [Theory]
    [InlineData("Gesamt 25,20 € 4,79 €")]  // proper euro signs untouched
    [InlineData("1 Tinte schwarz 12,99 €")]
    public void Normalize_CorrectEuroSigns_PreservesText(string input)
    {
        OcrTextNormalizer.Normalize(input).Should().Be(input);
    }

    [Fact]
    public void Normalize_MixedArtefactsInSameText_AppliesAllFixes()
    {
        var input = """
            Rechnung
            1 Tinte 9,99EUR
            2 Papier 8,50 EUR
            Zwischensumme EUR
            Versand 0,00£
            Gesamt 29,99E
            """;
        var expected = """
            Rechnung
            1 Tinte 9,99 €
            2 Papier 8,50 €
            Zwischensumme €
            Versand 0,00€
            Gesamt 29,99 €
            """;

        OcrTextNormalizer.Normalize(input).Should().Be(expected);
    }

    [Fact]
    public void Normalize_WordEndingInEAfterSpace_IsNotRewritten()
    {
        // "Bestelldatum: Montag" — the "E" at end of "Montage" shouldn't match,
        // only an isolated E right after a price. Sanity check that context matters.
        const string input = "Montage 9,99 €";
        OcrTextNormalizer.Normalize(input).Should().Be(input);
    }

    [Fact]
    public void Normalize_EurInsideWord_IsNotRewritten()
    {
        // "EURATOM" contains EUR but \b requires a word boundary — the word is a single
        // token so the right-side \b never matches. Behaviour we want: leave it alone.
        const string input = "EURATOM";
        OcrTextNormalizer.Normalize(input).Should().Be(input);
    }
}
