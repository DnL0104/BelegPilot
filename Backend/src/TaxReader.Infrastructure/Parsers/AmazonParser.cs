using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using TaxReader.Application.Interfaces;
using TaxReader.Domain.Entities;

namespace TaxReader.Infrastructure.Parsers;

public partial class AmazonParser(ILogger<AmazonParser> logger) : IReceiptParser
{
    public bool CanParse(string rawText, string? sourceHint)
    {
        if (sourceHint?.Contains("amazon", StringComparison.OrdinalIgnoreCase) == true)
            return true;

        return rawText.Contains("Amazon", StringComparison.OrdinalIgnoreCase)
               || rawText.Contains("amazon.de", StringComparison.OrdinalIgnoreCase)
               || rawText.Contains("amazon.com", StringComparison.OrdinalIgnoreCase);
    }

    public Receipt Parse(string rawText, ReceiptFile receiptFile)
    {
        var receipt = new Receipt
        {
            Vendor = ExtractVendor(rawText),
            Currency = "EUR",
            PurchaseDate = ExtractDate(rawText),
            RawExtractedText = rawText
        };

        var items = ExtractItems(rawText);

        if (items.Count == 0)
            logger.LogWarning(
                "AmazonParser found 0 items. Lines with '€': {Count}. First 600 chars of text:\n{Text}",
                rawText.Split('\n').Count(l => l.Contains('€')),
                rawText[..Math.Min(600, rawText.Length)]);

        foreach (var item in items)
        {
            receipt.Items.Add(item);
        }

        receipt.TaxAmount = ExtractTax(rawText);
        receipt.TotalAmount = ExtractTotal(rawText);

        if (receipt.TotalAmount > 0)
        {
            receipt.SubTotal = receipt.TotalAmount - receipt.TaxAmount;
        }
        else
        {
            receipt.SubTotal = receipt.Items.Sum(i => i.TotalPrice);
            receipt.TotalAmount = receipt.SubTotal + receipt.TaxAmount;
        }

        // OCR from screenshots often drops decimal commas in table cells, producing
        // mangled prices like "399" instead of "3,99". When there is exactly one item
        // and Gesamtpreis was reliably extracted, use it to correct the item price.
        if (receipt.Items.Count == 1 && receipt.TotalAmount > 0)
        {
            var item = receipt.Items.First();
            item.TotalPrice = receipt.TotalAmount;
            item.UnitPrice  = receipt.TotalAmount / item.Quantity;
        }

        return receipt;
    }

    private static string ExtractVendor(string text)
    {
        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines.Take(5))
        {
            var trimmed = line.Trim();
            if (trimmed.Length is >= 3 and <= 80 &&
                !trimmed.StartsWith("Daniel", StringComparison.OrdinalIgnoreCase) &&
                !trimmed.Contains("Rechnung", StringComparison.OrdinalIgnoreCase) &&
                !ReceiptParsingHelpers.GermanDateRegex().IsMatch(trimmed) &&
                (trimmed.Contains("GmbH", StringComparison.OrdinalIgnoreCase) ||
                 trimmed.Contains("AG", StringComparison.OrdinalIgnoreCase) ||
                 trimmed.Contains("UG", StringComparison.OrdinalIgnoreCase) ||
                 trimmed.Contains("e.K.", StringComparison.OrdinalIgnoreCase)))
            {
                return trimmed;
            }
        }

        return "Amazon";
    }

    private static DateOnly ExtractDate(string text)
    {
        // Try labeled German date with dots: "Rechnungsdatum 19.03.2026"
        var labeledMatch = LabeledDateRegex().Match(text);
        if (labeledMatch.Success &&
            DateOnly.TryParseExact(labeledMatch.Groups["date"].Value,
                ["dd.MM.yyyy", "d.MM.yyyy", "dd.M.yyyy"],
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var labeledDate))
            return labeledDate;

        // Try labeled long German date: "Rechnungsdatum 22 März 2026"
        var labeledLongMatch = LabeledLongDateRegex().Match(text);
        if (labeledLongMatch.Success)
        {
            var culture = new CultureInfo("de-DE");
            var dateStr = labeledLongMatch.Groups["date"].Value.Trim();
            if (DateOnly.TryParseExact(dateStr, ["d MMMM yyyy", "dd MMMM yyyy"],
                    culture, DateTimeStyles.None, out var longLabeledDate))
                return longLabeledDate;
        }

        // Try any German date format: dd.MM.yyyy
        var germanDateMatch = ReceiptParsingHelpers.GermanDateRegex().Match(text);
        if (germanDateMatch.Success &&
            DateOnly.TryParseExact(germanDateMatch.Value, ["dd.MM.yyyy", "d.MM.yyyy", "dd.M.yyyy"],
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var germanDate))
            return germanDate;

        // Try long German date: "22 März 2026"
        var longDateMatch = LongGermanDateRegex().Match(text);
        if (longDateMatch.Success)
        {
            var culture = new CultureInfo("de-DE");
            if (DateOnly.TryParseExact(longDateMatch.Value, ["d MMMM yyyy", "dd MMMM yyyy", "d. MMMM yyyy"],
                    culture, DateTimeStyles.None, out var longDate))
                return longDate;
        }

        // Try ISO format: yyyy-MM-dd
        var isoDateMatch = ReceiptParsingHelpers.IsoDateRegex().Match(text);
        if (isoDateMatch.Success &&
            DateOnly.TryParse(isoDateMatch.Value, out var isoDate))
            return isoDate;

        return DateOnly.FromDateTime(DateTime.UtcNow);
    }

    private static List<ReceiptItem> ExtractItems(string text)
    {
        var items = new List<ReceiptItem>();
        var lineNumber = 1;
        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        foreach (var line in lines)
        {
            // Skip lines that don't contain € at all
            if (!line.Contains('€'))
                continue;

            // Skip any line that contains meta/summary keywords
            if (IsNonItemLine(line))
                continue;

            // Find the LAST price with € on the line (that's typically the line total)
            var priceMatches = ReceiptParsingHelpers.PriceRegex().Matches(line);
            if (priceMatches.Count == 0) continue;

            var lastPriceMatch = priceMatches[^1];
            var priceStr = lastPriceMatch.Groups["price"].Value.Replace(",", ".");
            if (!decimal.TryParse(priceStr, CultureInfo.InvariantCulture, out var price) || price <= 0)
                continue;

            // Everything before the first price is the description
            var firstPriceMatch = priceMatches[0];
            var description = line[..firstPriceMatch.Index].Trim();

            // Clean up: remove leading quantity like "1" or "1,00"
            var quantity = 1;
            var leadingQtyMatch = LeadingQuantityRegex().Match(description);
            if (leadingQtyMatch.Success)
            {
                description = description[leadingQtyMatch.Length..].Trim();
                if (int.TryParse(leadingQtyMatch.Groups["qty"].Value, out var q) && q > 0)
                    quantity = q;
            }

            // Clean up: remove trailing quantity like "und 1" or just "1" at end of line
            var trailingQtyMatch = TrailingQuantityRegex().Match(description);
            if (trailingQtyMatch.Success)
            {
                description = description[..trailingQtyMatch.Index].Trim();
                if (int.TryParse(trailingQtyMatch.Groups["qty"].Value, out var q) && q > 0)
                    quantity = q;
            }

            if (string.IsNullOrWhiteSpace(description) || description.Length < 3)
                continue;

            items.Add(new ReceiptItem
            {
                Description = description.Length > 200 ? description[..200] : description,
                Quantity = quantity,
                UnitPrice = price / quantity,
                TotalPrice = price,
                LineNumber = lineNumber++
            });
        }

        return items;
    }

    private static bool IsNonItemLine(string text) =>
        ReceiptParsingHelpers.IsCommonNonItemLine(text) ||
        AmazonOnlyNonItemKeywordRegex().IsMatch(text) ||
        TaxPercentageLineRegex().IsMatch(text);

    private static decimal ExtractTax(string text)
    {
        // Find the tax summary line, then take the LAST price on it
        // e.g. "USt. Gesamt 25,20 € 4,79 €" → 4,79 (the actual tax, not the net subtotal)
        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            if (!TaxLineKeywordRegex().IsMatch(line)) continue;

            var priceMatches = ReceiptParsingHelpers.PriceRegex().Matches(line);
            if (priceMatches.Count == 0) continue;

            // Take the LAST price — in Amazon's format the columns are: net subtotal | tax amount
            var lastPrice = priceMatches[^1].Groups["price"].Value.Replace(",", ".");
            if (decimal.TryParse(lastPrice, CultureInfo.InvariantCulture, out var tax))
                return tax;
        }

        return 0m;
    }

    private static decimal ExtractTotal(string text)
    {
        var match = TotalRegex().Match(text);
        if (match.Success)
        {
            var priceStr = match.Groups["total"].Value.Replace(",", ".");
            if (decimal.TryParse(priceStr, CultureInfo.InvariantCulture, out var total))
                return total;
        }
        return 0m;
    }

    [GeneratedRegex(@"\d{1,2}\.?\s+\w+\s+\d{4}")]
    private static partial Regex LongGermanDateRegex();

    [GeneratedRegex(@"(?:Rechnungsdatum|Bestelldatum|Zahldatum)\s+(?<date>\d{1,2}\.\d{1,2}\.\d{4})", RegexOptions.IgnoreCase)]
    private static partial Regex LabeledDateRegex();

    [GeneratedRegex(@"(?:Rechnungsdatum|Bestelldatum|Zahldatum|Lieferdatum)[/\s]+(?<date>\d{1,2}\s+\w+\s+\d{4})", RegexOptions.IgnoreCase)]
    private static partial Regex LabeledLongDateRegex();

    // Leading quantity at start of description: "1" or "1,00"
    [GeneratedRegex(@"^(?<qty>\d+)(?:[.,]00)?\s+")]
    private static partial Regex LeadingQuantityRegex();

    // Trailing quantity at end of description: "und 1" or just " 1"
    [GeneratedRegex(@"\s+(?<qty>\d{1,3})\s*$")]
    private static partial Regex TrailingQuantityRegex();

    // Amazon-specific lines that should NOT be treated as items, on top of the
    // universal ReceiptParsingHelpers.CommonNonItemKeywordRegex list (ORed in IsNonItemLine).
    // Kept as Amazon's original full list rather than pruning overlaps with the common
    // list — redundancy here is harmless (OR of an already-true term is a no-op) and
    // guarantees zero behavior change for this parser from the consolidation.
    [GeneratedRegex(@"(?:Gesamt|Total|Summe|Endbetrag|Zwischensumme|Netto|Brutto|MwSt|USt[\.\s]|Umsatzsteuer|Steuer|Versand|Shipping|Zahldatum|Rechnungsdatum|Bestelldatum|Referenz|Rechnung\s*(?:nummer|sdatum|\d)|Pos\s+Nummer|Anzahl|Preis|Stückpreis|Zahlbetrag|Zahlungsreferenz|Stammkapital|Menge|ASIN|Beschreibung|Verkauft\s+von|Rechnungsadresse|Lieferadresse|inkl\.\s*USt|ohne\s*USt|Versandkosten)", RegexOptions.IgnoreCase)]
    private static partial Regex AmazonOnlyNonItemKeywordRegex();

    // Lines that start with a tax percentage like "19% 25,20 € 4,79 €"
    [GeneratedRegex(@"^\s*\d{1,2}%")]
    private static partial Regex TaxPercentageLineRegex();

    // Identifies a tax summary line (not capturing the amount — we take the last € price on the line)
    // Matches: "USt. Gesamt", "Umsatzsteuer (19,0%)", "MwSt. Gesamt", "MwSt (19%)", etc.
    [GeneratedRegex(@"(?:USt[\.\s]*Gesamt|Umsatzsteuer|MwSt[\.\s]*Gesamt|MwSt\s*\()", RegexOptions.IgnoreCase)]
    private static partial Regex TaxLineKeywordRegex();

    [GeneratedRegex(@"(?:Gesamtpreis|Gesamtsumme|Gesamt\b(?!\s*netto))[^\d]*(?<total>\d+[.,]\d{2})\s*€", RegexOptions.IgnoreCase)]
    private static partial Regex TotalRegex();
}
