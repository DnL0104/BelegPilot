using System.Globalization;
using System.Text.RegularExpressions;
using TaxReader.Application.Interfaces;
using TaxReader.Domain.Entities;

namespace TaxReader.Infrastructure.Parsers;

public partial class EdukiParser : IReceiptParser
{
    public bool CanParse(string rawText, string? sourceHint)
    {
        if (sourceHint?.Contains("eduki", StringComparison.OrdinalIgnoreCase) == true)
            return true;

        return rawText.Contains("eduki", StringComparison.OrdinalIgnoreCase)
               || rawText.Contains("lehrermarktplatz", StringComparison.OrdinalIgnoreCase);
    }

    public Receipt Parse(string rawText, ReceiptFile receiptFile)
    {
        var receipt = new Receipt
        {
            Vendor = "Eduki",
            Currency = "EUR",
            PurchaseDate = ExtractDate(rawText),
            RawExtractedText = rawText
        };

        var items = ExtractItems(rawText);
        foreach (var item in items)
        {
            receipt.Items.Add(item);
        }

        receipt.SubTotal = receipt.Items.Sum(i => i.TotalPrice);
        receipt.TaxAmount = ReceiptParsingHelpers.ExtractTaxAmount(rawText);
        receipt.TotalAmount = receipt.SubTotal + receipt.TaxAmount;

        return receipt;
    }

    private static DateOnly ExtractDate(string text)
    {
        var dateMatch = ReceiptParsingHelpers.GermanDateRegex().Match(text);
        if (dateMatch.Success &&
            DateOnly.TryParseExact(dateMatch.Value, ["dd.MM.yyyy", "d.MM.yyyy", "dd.M.yyyy"],
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            return date;

        var isoMatch = ReceiptParsingHelpers.IsoDateRegex().Match(text);
        if (isoMatch.Success && DateOnly.TryParse(isoMatch.Value, out var isoDate))
            return isoDate;

        return DateOnly.FromDateTime(DateTime.UtcNow);
    }

    private static List<ReceiptItem> ExtractItems(string text)
    {
        var items = new List<ReceiptItem>();
        var lineNumber = 1;

        // Eduki items are typically digital downloads with a price
        var matches = EdukiItemRegex().Matches(text);
        foreach (Match match in matches)
        {
            var description = match.Groups["desc"].Value.Trim();
            var priceStr = match.Groups["price"].Value.Replace(",", ".");

            if (decimal.TryParse(priceStr, CultureInfo.InvariantCulture, out var price) && price > 0)
            {
                items.Add(new ReceiptItem
                {
                    Description = description,
                    Quantity = 1,
                    UnitPrice = price,
                    TotalPrice = price,
                    LineNumber = lineNumber++
                });
            }
        }

        // Fallback: find price patterns on individual lines (require € to avoid matching dates)
        if (items.Count == 0)
        {
            var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                var priceMatch = PricePatternRegex().Match(line);
                if (priceMatch.Success)
                {
                    var priceStr = priceMatch.Groups["price"].Value.Replace(",", ".");
                    if (decimal.TryParse(priceStr, CultureInfo.InvariantCulture, out var price) && price > 0)
                    {
                        var desc = line[..priceMatch.Index].Trim();
                        if (!string.IsNullOrWhiteSpace(desc) && desc.Length > 2 &&
                            !ReceiptParsingHelpers.IsCommonNonItemLine(desc))
                        {
                            items.Add(new ReceiptItem
                            {
                                Description = desc.Length > 200 ? desc[..200] : desc,
                                Quantity = 1,
                                UnitPrice = price,
                                TotalPrice = price,
                                LineNumber = lineNumber++
                            });
                        }
                    }
                }
            }
        }

        return items;
    }

    [GeneratedRegex(@"(?<desc>[^\n]{3,?})\s+(?<price>\d+[.,]\d{2})\s*€", RegexOptions.IgnoreCase)]
    private static partial Regex EdukiItemRegex();

    // Require € to avoid matching dates like 18.03.2026. Mandatory 2 decimal digits —
    // intentionally stricter than ReceiptParsingHelpers.PriceRegex's optional-decimal
    // variant, so not shared with it.
    [GeneratedRegex(@"(?<price>\d+[.,]\d{2})\s*€")]
    private static partial Regex PricePatternRegex();
}
