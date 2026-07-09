using FluentAssertions;
using TaxReader.Infrastructure.Parsers;
using TaxReader.UnitTests.Helpers;

namespace TaxReader.UnitTests.Infrastructure.Parsers;

public class GenericParserTests
{
    private readonly GenericParser _parser = new();

    [Fact]
    public void CanParse_AnyText_ReturnsTrue()
    {
        _parser.CanParse("anything", null).Should().BeTrue();
        _parser.CanParse("", "hint").Should().BeTrue();
        _parser.CanParse("random", "random").Should().BeTrue();
    }

    [Fact]
    public void Parse_WithPriceLines_ExtractsItems()
    {
        var text = """
            Local Store
            10.01.2025
            Kugelschreiber 3,99
            Radiergummi 1,50
            Gesamt 5,49
            """;

        var file = TestDataFactory.CreateReceiptFile(sourceHint: "LocalStore");
        var receipt = _parser.Parse(text, file);

        receipt.Vendor.Should().Be("LocalStore");
        receipt.Items.Should().HaveCountGreaterOrEqualTo(1);
    }

    [Fact]
    public void Parse_WithSourceHint_UsesAsVendor()
    {
        var file = TestDataFactory.CreateReceiptFile(sourceHint: "MyShop");
        var receipt = _parser.Parse("some text with 10,00 total", file);

        receipt.Vendor.Should().Be("MyShop");
    }

    [Fact]
    public void Parse_NoSourceHint_UsesFirstLine()
    {
        var file = TestDataFactory.CreateReceiptFile(sourceHint: null);
        var receipt = _parser.Parse("StoreNameHere\nSome item 5,00", file);

        receipt.Vendor.Should().Be("StoreNameHere");
    }

    [Fact]
    public void Parse_ExtractsDate_GermanFormat()
    {
        var text = "Store\n15.06.2025\nItem 10,00";
        var file = TestDataFactory.CreateReceiptFile();
        var receipt = _parser.Parse(text, file);

        receipt.PurchaseDate.Should().Be(new DateOnly(2025, 6, 15));
    }

    [Fact]
    public void Parse_StripeStyleInvoice_DoesNotDuplicateItemAsFaelligerBetrag()
    {
        // Regression test: a Velrion/Stripe invoice with a "Fälliger Betrag" summary line
        // (same amount as the real line item) was previously mis-parsed as a second,
        // bogus ReceiptItem because "Fälliger" wasn't in the total-line keyword filter.
        var text = """
            Velrion Sandbox
            Velrion 1500 Credits 1 49,99 € 49,99 €
            Zwischensumme 49,99 €
            Summe 49,99 €
            Fälliger Betrag 49,99 €
            """;

        var file = TestDataFactory.CreateReceiptFile(sourceHint: "Velrion");
        var receipt = _parser.Parse(text, file);

        receipt.Items.Should().ContainSingle();
        receipt.Items.First().Description.Should().Contain("Velrion 1500 Credits");
        receipt.TotalAmount.Should().Be(49.99m);
    }

    [Fact]
    public void Parse_RewePaymentSlipWithCashbackSection_ExtractsCorrectTotalAndExcludesSummaryLines()
    {
        // Regression test: real Tesseract OCR output (raw_extracted_text) from a phone-photo
        // REWE receipt. "Betrag € 11,06" (payment-confirmation section) is the real, correct
        // total; "Gesantbetrag 1,36" further down is an OCR-corrupted "Gesamtbetrag" (misread
        // 'm' -> 'n', so the old "Gesamt"-only TotalValueRegex never matched it); "A= 19,0% ..."
        // and "7,0% ..." are per-row VAT breakdown lines with no "Steuer" keyword on the row
        // itself (only on a preceding header line). Before this fix, all 4 of these
        // summary/tax lines were mis-parsed as fake items and TotalAmount silently fell back
        // to their wrong sum (15.78) instead of the real total (11.06).
        var text = """
            Betrag € 11,06
            Cashback €
            Gesamt j
            Steuer % Netto fi Steuer
            A= 19,0% 5,71) 1,08
            7,0% 3,99 0,28
            Gesantbetrag 1,36
            BFAND 2,00 EURO
            """;

        var file = TestDataFactory.CreateReceiptFile(sourceHint: null);
        var receipt = _parser.Parse(text, file);

        // Only BFAND (a real deposit charge) survives filtering; the single-item safety
        // net then corrects its price to the reliably-extracted total (same mechanism as
        // AmazonParser), since 2,00 differs substantially from 11,06.
        receipt.Items.Should().ContainSingle();
        receipt.Items.First().Description.Should().Be("BFAND");
        receipt.TotalAmount.Should().Be(11.06m);
    }

    [Fact]
    public void Parse_TimestampWithStrayEuroSign_DoesNotCreatePhantomPriceItem()
    {
        // Regression test: real Tesseract OCR output misread a receipt timestamp
        // ("17:48:55") with a stray "€" landing right after it. Before this fix,
        // PriceRegex matched the seconds field "55" as a price (immediately followed by
        // " €"), producing a fake item "ma 17:48:" priced at €55,00 — the dominant wrong
        // value in a real bug report (total_amount 65,00 instead of a plausible ~10,00).
        var text = """
            selene + 323066
            ma 17:48:55 €
            Kasse: Bon
            Partyartikel 3,00
            """;

        var file = TestDataFactory.CreateReceiptFile(sourceHint: null);
        var receipt = _parser.Parse(text, file);

        receipt.Items.Should().NotContain(i => i.TotalPrice == 55.00m);
        receipt.Items.Should().NotContain(i => i.Description.Contains("17:48"));
    }
}
