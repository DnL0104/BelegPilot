using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using TaxReader.Infrastructure.Parsers;
using TaxReader.UnitTests.Helpers;

namespace TaxReader.UnitTests.Infrastructure.Parsers;

public class AmazonParserTests
{
    private readonly AmazonParser _parser = new(NullLogger<AmazonParser>.Instance);

    [Theory]
    [InlineData("Amazon", null)]
    [InlineData("amazon", null)]
    [InlineData(null, "Amazon.de Bestellung")]
    [InlineData(null, "Your Amazon.com order")]
    public void CanParse_AmazonIndicators_ReturnsTrue(string? sourceHint, string? rawText)
    {
        _parser.CanParse(rawText ?? "some text", sourceHint).Should().BeTrue();
    }

    [Fact]
    public void CanParse_NoAmazonIndicators_ReturnsFalse()
    {
        _parser.CanParse("Random receipt text", null).Should().BeFalse();
    }

    [Fact]
    public void Parse_WithItemLines_ExtractsItems()
    {
        var text = """
            Amazon.de
            Bestelldatum: 15.03.2025
            1 Tinte schwarz 12,99 €
            2 Papier A4 8,50 €
            Gesamt 21,49 €
            MwSt 3,43 €
            """;

        var file = TestDataFactory.CreateReceiptFile(sourceHint: "Amazon");
        var receipt = _parser.Parse(text, file);

        receipt.Vendor.Should().Be("Amazon");
        receipt.Items.Should().HaveCountGreaterOrEqualTo(1);
    }

    [Fact]
    public void Parse_AmazonMarketplaceInvoice_ExtractsOnlyRealItems()
    {
        // Simulates actual PdfPig output from a real Amazon Marketplace invoice
        var text = """
            Rechnung
            Zahlungsreferenznummer 19U4HJU7QH29NGCW
            Verkauft von Amazon EU S.à r.l., Niederlassung Deutschland
            USt-IDNr. LU20260743
            Rechnungsdatum
            /Lieferdatum 22 März 2026
            DANIEL HALLING
            Rechnungsnummer LU61K9NMWAEUI
            HANSEWEG 1 A Zahlbetrag 29,99 €
            LEMGO, 32657
            DE
            Rechnungsadresse Lieferadresse Verkauft von
            Bestellinformationen
            Bestelldatum 21 März 2026
            Bestellnummer 305-7107923-9416338
            Rechnungsdetails
            Beschreibung Menge Stückpreis USt. % Stückpreis Zwischensumme
            SteelSeries QcK Gaming Mauspad - XXL Stoff - Erstklassiges Tracking und 1 25,20 € 19% 29,99 € 29,99 €
            ASIN: B0CVN5MQ6P
            Versandkosten 0,00 € 0,00 € 0,00 €
            Gesamtpreis 29,99 €
            USt. % Zwischensumme USt.
            19% 25,20 € 4,79 €
            USt. Gesamt 25,20 € 4,79 €
            Stammkapital: 37.500 EUR
            """;

        var file = TestDataFactory.CreateReceiptFile(sourceHint: "Amazon");
        var receipt = _parser.Parse(text, file);

        // Should extract exactly 1 item - the SteelSeries mousepad
        receipt.Items.Should().HaveCount(1);
        receipt.Items.First().Description.Should().Contain("SteelSeries");
        receipt.Items.First().TotalPrice.Should().Be(29.99m);

        // Should NOT contain address lines, tax lines, or Zahlbetrag
        receipt.Items.Should().NotContain(i => i.Description.Contains("HANSEWEG"));
        receipt.Items.Should().NotContain(i => i.Description.Contains("Zahlbetrag"));
        receipt.Items.Should().NotContain(i => i.Description.Contains("19%"));

        // Tax should be 4,79 € (NOT 25,20 which is the net subtotal column)
        receipt.TaxAmount.Should().Be(4.79m);
        receipt.TotalAmount.Should().Be(29.99m);
        receipt.SubTotal.Should().Be(25.20m);
    }

    [Fact]
    public void Parse_StortebekkerInvoice_ExtractsItemAndTax()
    {
        // Simulates actual PdfPig output from a Störtebekker Amazon Marketplace invoice
        var text = """
            Störtebekker Shaving Accessories GmbH
            Daniel Halling Störtebekker Kundenbetreuung
            Hanseweg 1A Telefon: +49 (0) 6104 9536611
            32657 Lemgo E-Mail: info@stoertebekker.com
            Rechnung
            Rechnung 973844
            Rechnungsdatum 19.03.2026
            Bestelldatum 18.03.2026
            Zahldatum 18.03.2026
            Zahlart Amazon Marketplace
            Referenz 305-9080543-0109114
            Pos Nummer Artikel Anzahl Preis Summe
            1 Bartpflege Set Störtebekker® Premium Bartpflege Set für 1,00 54,99 € 54,99 €
            Sandelholz Männer - inkl. Bartöl Sandelholz, Bartkamm und
            Bartbürste - perfekt für die tägliche Bartpflege -
            hochwertige Geschenkidee für Herren - Made in
            Germany
            Farbe: Sandelholz
            Zwischensumme 54,99 €
            Versand 0,00 €
            Gesamt netto 46,21 €
            Umsatzsteuer (19,0%) 8,78 €
            Gesamtsumme 54,99 €
            """;

        var file = TestDataFactory.CreateReceiptFile(sourceHint: "Amazon");
        var receipt = _parser.Parse(text, file);

        // Should extract exactly 1 item - the Bartpflege Set
        receipt.Items.Should().HaveCount(1);
        receipt.Items.First().Description.Should().Contain("Bartpflege");
        receipt.Items.First().TotalPrice.Should().Be(54.99m);

        // Tax should be 8,78 € from "Umsatzsteuer (19,0%) 8,78 €"
        receipt.TaxAmount.Should().Be(8.78m);
        receipt.TotalAmount.Should().Be(54.99m);
        receipt.SubTotal.Should().Be(46.21m);
    }

    [Fact]
    public void Parse_FaelligerBetragLine_ExcludedAsNonItem()
    {
        // Regression test: "Fällig" previously lived only in GenericParser's keyword list.
        // After consolidating into ReceiptParsingHelpers.CommonNonItemKeywordRegex, Amazon
        // (which ORs the common list into its own IsNonItemLine check) must exclude it too.
        var text = """
            Amazon.de
            Bestelldatum: 15.03.2025
            1 Tinte schwarz 12,99 €
            Fälliger Betrag 12,99 €
            """;

        var file = TestDataFactory.CreateReceiptFile(sourceHint: "Amazon");
        var receipt = _parser.Parse(text, file);

        receipt.Items.Should().ContainSingle();
        receipt.Items.First().Description.Should().Contain("Tinte");
    }

    [Fact]
    public void Parse_ExtractsDate_GermanFormat()
    {
        var text = """
            Amazon.de
            15.03.2025
            1 Item 10,00 €
            """;

        var file = TestDataFactory.CreateReceiptFile();
        var receipt = _parser.Parse(text, file);

        receipt.PurchaseDate.Should().Be(new DateOnly(2025, 3, 15));
    }
}
