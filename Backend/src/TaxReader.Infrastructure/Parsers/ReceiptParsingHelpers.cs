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
    public static bool IsCommonNonItemLine(string text) =>
        CommonNonItemKeywordRegex().IsMatch(text) || TaxRateRowRegex().IsMatch(text);

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
    // Generic receipts to date. Mastercard/Rückgeld are payment-method/change-due lines,
    // not items — added alongside PriceRegex's €-less alternative below, since without €
    // as a gate these lines would otherwise be picked up as false-positive items.
    // Betrag/Cashback/Auszahlung added for German till receipts (e.g. REWE) whose payment
    // section prints "Betrag € 11,06" (the payable total, not caught by "Gesamt") and, on
    // receipts with a cashback/cash-withdrawal-at-checkout, "Cashback €"/"Auszahlungsbetrag"
    // lines — none of these are ever real purchased items, so broad substring matching here
    // is safe (this only ever excludes a line from being an item, never picks a total value).
    [GeneratedRegex(@"(?:Gesamt|Total|Summe|Endbetrag|Zwischensumme|Netto|Brutto|MwSt|USt|Umsatzsteuer|Steuer|Versand|Shipping|Zahldatum|Rechnungsdatum|Bestelldatum|Referenz|Rechnung\s+\d|Pos\s+Nummer|Anzahl|Preis|Fällig|Mastercard|Rückgeld|Betrag|Cashback|Auszahlung)", RegexOptions.IgnoreCase)]
    public static partial Regex CommonNonItemKeywordRegex();

    // Tax-rate breakdown rows on German till receipts print as "<letter>= <rate>% <net> <tax>"
    // or just "<rate>% <net> <tax>" (e.g. "A= 19,0% 5,71 1,08"), with the "Steuer"/"MwSt"
    // keyword only on a preceding header line, never repeated per row — so
    // CommonNonItemKeywordRegex alone doesn't catch these rows. Anchored at the start of the
    // line (allowing only an optional single-letter tax-category prefix like "A=") so it
    // doesn't reject a real item description that merely mentions a percentage mid-line
    // (e.g. an ABV like "Wodka 40% 0,7l"). AmazonParser already has an integer-only,
    // Amazon-scoped version of this (TaxPercentageLineRegex) that was never generalized here —
    // this is the intended shared home per this file's own header comment.
    [GeneratedRegex(@"^\s*[A-Za-z]?\s*[=:]?\s*\d{1,2}(?:[.,]\d+)?\s*%")]
    private static partial Regex TaxRateRowRegex();

    [GeneratedRegex(@"\d{1,2}\.\d{1,2}\.\d{4}")]
    public static partial Regex GermanDateRegex();

    [GeneratedRegex(@"\d{4}-\d{2}-\d{2}")]
    public static partial Regex IsoDateRegex();

    // Two alternatives:
    // 1) A number followed by € — decimal part optional, since OCR from screenshots
    //    often drops the comma (e.g. "3,99 €" is read as "399€"). Requiring € here
    //    disambiguates from dates like "18.03.2026". Guarded with a negative lookbehind
    //    for a directly-preceding ":" or digit so a timestamp's seconds field isn't
    //    mistaken for a price when OCR noise places a stray "€" right after it (e.g.
    //    "17:48:55 €" would otherwise match "55" as €55.00 — observed on a real
    //    phone-photo receipt). The digit half of the lookbehind matters too: without it,
    //    once ":" blocks a match starting at the run's true first digit, the regex engine
    //    just retries starting at the run's SECOND digit (now preceded by a digit, not a
    //    colon) and matches a truncated "5" instead of "55" — worse than the original bug.
    //    Requiring the match to start at a genuine digit-run boundary closes that gap.
    // 2) A comma-decimal number with NO € at all. Many German till receipts (e.g.
    //    thermal Kassenbons) print "EUR"/"€" once as a column header instead of per
    //    line, so requiring € on every line silently drops every item on those
    //    receipts. Safe without €-anchoring because German dates always use "."
    //    between segments and never contain a comma, so a comma-decimal number can't
    //    be a mis-parsed date. Exactly 2 decimal digits required (not 1-2) — stricter
    //    than alternative 1 — to avoid loosely matching stray single-decimal-digit
    //    OCR noise (e.g. a garbled "MwSt" line read as "... 12,0 ...").
    [GeneratedRegex(@"(?<![:\d])(?<price>\d+(?:[.,]\d{1,2})?)\s*€|(?<price>\d+,\d{2})(?!\d)")]
    public static partial Regex PriceRegex();

    [GeneratedRegex(@"(?:MwSt|USt|Umsatzsteuer|Steuer)[^\d]*(?<tax>\d+[.,]\d{2})", RegexOptions.IgnoreCase)]
    private static partial Regex TaxRegex();
}
