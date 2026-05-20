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
}
