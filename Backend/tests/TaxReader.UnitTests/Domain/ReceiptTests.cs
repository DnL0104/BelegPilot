using FluentAssertions;
using TaxReader.Domain.Entities;
using TaxReader.UnitTests.Helpers;

namespace TaxReader.UnitTests.Domain;

public class ReceiptTests
{
    [Fact]
    public void CreateReceipt_DefaultCurrency_IsEur()
    {
        var receipt = new Receipt();

        receipt.Currency.Should().Be("EUR");
    }

    [Fact]
    public void CreateReceipt_ItemsCollection_IsInitializedEmpty()
    {
        var receipt = new Receipt();

        receipt.Items.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public void CreateReceipt_WithFactory_HasCorrectValues()
    {
        var receipt = TestDataFactory.CreateReceipt(vendor: "Amazon", totalAmount: 50.00m);

        receipt.Vendor.Should().Be("Amazon");
        receipt.TotalAmount.Should().Be(50.00m);
        receipt.Currency.Should().Be("EUR");
    }
}
