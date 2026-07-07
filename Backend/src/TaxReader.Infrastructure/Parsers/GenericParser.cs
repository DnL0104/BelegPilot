using System.Globalization;
using System.Text.RegularExpressions;
using TaxReader.Application.Interfaces;
using TaxReader.Domain.Entities;

namespace TaxReader.Infrastructure.Parsers;

public partial class GenericParser : IReceiptParser
{
    public bool CanParse(string rawText, string? sourceHint) => true;

    public Receipt Parse(string rawText, ReceiptFile receiptFile)
    {
        var receipt = new Receipt
        {
            Vendor = ExtractVendor(rawText, receiptFile.SourceHint),
            Currency = "EUR",
            PurchaseDate = ExtractDate(rawText),
            RawExtractedText = rawText
        };

        var items = ExtractItems(rawText);
        foreach (var item in items)
        {
            receipt.Items.Add(item);
        }

        // If no items were found, create a single item with the total
        if (receipt.Items.Count == 0)
        {
            var total = ExtractTotal(rawText);
            if (total > 0)
            {
                receipt.Items.Add(new ReceiptItem
                {
                    Description = "Unspecified item",
                    Quantity = 1,
                    UnitPrice = total,
                    TotalPrice = total,
                    LineNumber = 1
                });
            }
        }

        receipt.SubTotal = receipt.Items.Sum(i => i.TotalPrice);
        receipt.TaxAmount = ReceiptParsingHelpers.ExtractTaxAmount(rawText);
        receipt.TotalAmount = ExtractTotal(rawText) > 0
            ? ExtractTotal(rawText)
            : receipt.SubTotal + receipt.TaxAmount;

        // Safety net: if exactly one item was found but its price differs substantially
        // from the extracted total, the parser grabbed the wrong line (e.g. a quantity "1 €"
        // instead of the actual price). Override with the reliably-extracted total so the
        // item and subtotal are at least consistent. Same logic as AmazonParser.
        if (receipt.Items.Count == 1 && receipt.TotalAmount > 0)
        {
            var singleItem = receipt.Items.First();
            if (Math.Abs(singleItem.TotalPrice - receipt.TotalAmount) > 0.50m)
            {
                singleItem.TotalPrice = receipt.TotalAmount;
                singleItem.UnitPrice  = receipt.TotalAmount / singleItem.Quantity;
                receipt.SubTotal = receipt.TotalAmount;
            }
        }

        return receipt;
    }

    private static string ExtractVendor(string text, string? sourceHint)
    {
        if (!string.IsNullOrWhiteSpace(sourceHint))
            return sourceHint;

        // Use the first non-empty line as the vendor name
        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed.Length is >= 2 and <= 100)
                return trimmed;
        }

        return "Unknown";
    }

    private static DateOnly ExtractDate(string text)
    {
        // Look for labeled dates first
        var labeledMatch = LabeledDateRegex().Match(text);
        if (labeledMatch.Success &&
            DateOnly.TryParseExact(labeledMatch.Groups["date"].Value,
                ["dd.MM.yyyy", "d.MM.yyyy", "dd.M.yyyy"],
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var labeledDate))
            return labeledDate;

        // German format
        var germanMatch = ReceiptParsingHelpers.GermanDateRegex().Match(text);
        if (germanMatch.Success &&
            DateOnly.TryParseExact(germanMatch.Value, ["dd.MM.yyyy", "d.MM.yyyy", "dd.M.yyyy"],
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var germanDate))
            return germanDate;

        // ISO format
        var isoMatch = ReceiptParsingHelpers.IsoDateRegex().Match(text);
        if (isoMatch.Success && DateOnly.TryParse(isoMatch.Value, out var isoDate))
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
            // Only match prices with € symbol to avoid matching dates like 18.03.2026
            var priceMatches = ReceiptParsingHelpers.PriceRegex().Matches(line);
            if (priceMatches.Count == 0) continue;

            // Take the LAST price on the line — that's the line total (column layout
            // puts unit price first, then quantity × unit = line total at the end).
            // Taking the first price would grab "1 €" quantity markers or unit prices.
            var lastMatch = priceMatches[^1];
            var priceStr = lastMatch.Groups["price"].Value.Replace(",", ".");
            if (!decimal.TryParse(priceStr, CultureInfo.InvariantCulture, out var price) || price <= 0)
                continue;

            // Description = everything before the FIRST price (avoids carrying intermediate
            // prices like unit price into the description string).
            var description = line[..priceMatches[0].Index].Trim();

            // Skip total/sum lines and metadata lines
            if (string.IsNullOrWhiteSpace(description) || description.Length < 3)
                continue;
            if (IsTotalLine(description))
                continue;

            items.Add(new ReceiptItem
            {
                Description = description.Length > 200 ? description[..200] : description,
                Quantity = 1,
                UnitPrice = price,
                TotalPrice = price,
                LineNumber = lineNumber++
            });
        }

        return items;
    }

    private static bool IsTotalLine(string text) =>
        ReceiptParsingHelpers.IsCommonNonItemLine(text);

    private static decimal ExtractTotal(string text)
    {
        var match = TotalValueRegex().Match(text);
        if (match.Success)
        {
            var priceStr = match.Groups["total"].Value.Replace(",", ".");
            if (decimal.TryParse(priceStr, CultureInfo.InvariantCulture, out var total))
                return total;
        }
        return 0m;
    }

    [GeneratedRegex(@"(?:Rechnungsdatum|Bestelldatum|Zahldatum|Datum)\s+(?<date>\d{1,2}\.\d{1,2}\.\d{4})", RegexOptions.IgnoreCase)]
    private static partial Regex LabeledDateRegex();

    [GeneratedRegex(@"(?:Gesamtsumme|Gesamt|Total|Summe|Endbetrag|Brutto)[^\d]*(?<total>\d+[.,]\d{2})", RegexOptions.IgnoreCase)]
    private static partial Regex TotalValueRegex();
}
