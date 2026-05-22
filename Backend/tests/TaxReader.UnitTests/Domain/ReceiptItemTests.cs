using FluentAssertions;
using TaxReader.Domain.Enums;
using TaxReader.UnitTests.Helpers;

namespace TaxReader.UnitTests.Domain;

public class ReceiptItemTests
{
    [Fact]
    public void LatestClassification_NoClassifications_ReturnsNull()
    {
        var item = TestDataFactory.CreateReceiptItem();

        item.LatestClassification.Should().BeNull();
    }

    [Fact]
    public void LatestClassification_SingleClassification_ReturnsThatClassification()
    {
        var item = TestDataFactory.CreateReceiptItem();
        var classification = TestDataFactory.CreateClassification(receiptItemId: item.Id);
        item.Classifications.Add(classification);

        item.LatestClassification.Should().Be(classification);
    }

    [Fact]
    public void LatestClassification_MultipleClassifications_ReturnsMostRecent()
    {
        var item = TestDataFactory.CreateReceiptItem();

        var older = TestDataFactory.CreateClassification(
            receiptItemId: item.Id,
            category: Category.Unbekannt);
        older.ClassifiedAt = DateTime.UtcNow.AddHours(-2);

        var newer = TestDataFactory.CreateClassification(
            receiptItemId: item.Id,
            category: Category.WerbungskostenFachliteratur);
        newer.ClassifiedAt = DateTime.UtcNow;

        item.Classifications.Add(older);
        item.Classifications.Add(newer);

        item.LatestClassification.Should().Be(newer);
        item.LatestClassification!.Category.Should().Be(Category.WerbungskostenFachliteratur);
    }

    [Fact]
    public void CreateReceiptItem_DefaultQuantity_IsOne()
    {
        var item = new TaxReader.Domain.Entities.ReceiptItem();

        item.Quantity.Should().Be(1);
    }
}
