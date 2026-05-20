using System.Text.RegularExpressions;

namespace TaxReader.Infrastructure.Services;

/// <summary>
/// Normalises common OCR artefacts so downstream parsers can use consistent
/// patterns regardless of how Tesseract rendered the source. Currency-symbol
/// confusion is the dominant failure mode on German receipts scanned with
/// non-embedded fonts — "€" is often dropped, swapped with "E", or
/// substituted with "£".
/// </summary>
public static partial class OcrTextNormalizer
{
    public static string Normalize(string text)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        // "9,99EUR" (no space — \b won't fire between digit and letter) → "9,99 €"
        text = DigitEurAttachedRegex().Replace(text, "$1 €");
        // "9,99 EUR" (with space) → "9,99 €"
        text = DigitEurSpacedRegex().Replace(text, "$1 €");
        // Remaining standalone "EUR" (e.g. column header) → "€"
        text = StandaloneEurRegex().Replace(text, "€");
        // "£" is a common Tesseract misread of "€" on some fonts
        text = text.Replace('£', '€');
        // Trailing isolated "E" right after a decimal price → "€"
        // e.g. "29,99E" or "29,99 E " (Tesseract misread of €)
        text = TrailingEAfterPriceRegex().Replace(text, "$1 €");
        return text;
    }

    [GeneratedRegex(@"(\d)EUR\b")]
    private static partial Regex DigitEurAttachedRegex();

    [GeneratedRegex(@"(\d)\s+EUR\b")]
    private static partial Regex DigitEurSpacedRegex();

    [GeneratedRegex(@"\bEUR\b")]
    private static partial Regex StandaloneEurRegex();

    [GeneratedRegex(@"(\d{1,3}[.,]\d{2})\s*E\b")]
    private static partial Regex TrailingEAfterPriceRegex();
}
