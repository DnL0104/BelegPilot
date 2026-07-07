using System.Globalization;
using System.Text.RegularExpressions;

namespace TaxReader.Infrastructure.Parsers;

/// <summary>
/// Primitives shared by every <see cref="IReceiptParser"/> implementation (Amazon, Eduki,
/// Generic). Each parser previously kept its own copy of these regexes, which let the same
/// fix drift out of sync across parsers — e.g. "Fällig" was added to GenericParser's total-line
/// keyword list without the identical gap being closed in AmazonParser/EdukiParser, which
/// independently maintained near-duplicate lists. Add new universal keywords/patterns here so
/// every parser benefits; keep genuinely format-specific logic (Amazon's column-based tax
/// extraction, Eduki's stricter price format, etc.) local to that parser.
/// </summary>
public static partial class ReceiptParsingHelpers
{
    /// <summary>
    /// True if the line contains any keyword indicating a total/summary/tax/metadata line
    /// rather than an actual purchased item. Format-specific parsers should OR this with
    /// their own additional keywords rather than replacing it.
    /// </summary>
    public static bool IsCommonNonItemLine(string text) => CommonNonItemKeywordRegex().IsMatch(text);

    public static decimal ExtractTaxAmount(string text)
    {
        var match = TaxRegex().Match(text);
        if (match.Success)
        {
            var priceStr = match.Groups["tax"].Value.Replace(",", ".");
            if (decimal.TryParse(priceStr, CultureInfo.InvariantCulture, out var tax))
                return tax;
        }
        return 0m;
    }

    // Union of every total/summary/metadata keyword observed across Amazon, Eduki, and
    // Generic receipts to date.
    [GeneratedRegex(@"(?:Gesamt|Total|Summe|Endbetrag|Zwischensumme|Netto|Brutto|MwSt|USt|Umsatzsteuer|Steuer|Versand|Shipping|Zahldatum|Rechnungsdatum|Bestelldatum|Referenz|Rechnung\s+\d|Pos\s+Nummer|Anzahl|Preis|Fällig)", RegexOptions.IgnoreCase)]
    public static partial Regex CommonNonItemKeywordRegex();

    [GeneratedRegex(@"\d{1,2}\.\d{1,2}\.\d{4}")]
    public static partial Regex GermanDateRegex();

    [GeneratedRegex(@"\d{4}-\d{2}-\d{2}")]
    public static partial Regex IsoDateRegex();

    // Require € to avoid matching dates. Decimal part optional — OCR from screenshots
    // often drops the comma (e.g. "3,99 €" is read as "399€").
    [GeneratedRegex(@"(?<price>\d+(?:[.,]\d{1,2})?)\s*€")]
    public static partial Regex PriceRegex();

    [GeneratedRegex(@"(?:MwSt|USt|Umsatzsteuer|Steuer)[^\d]*(?<tax>\d+[.,]\d{2})", RegexOptions.IgnoreCase)]
    private static partial Regex TaxRegex();
}
