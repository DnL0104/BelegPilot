using FluentAssertions;
using TaxReader.Application.Mapping;
using TaxReader.Domain.Enums;
using TaxReader.UnitTests.Helpers;

namespace TaxReader.UnitTests.Application.Mapping;

public class DtoMappingExtensionsTests
{
    [Fact]
    public void ToDto_ReceiptFile_MapsAllProperties()
    {
        var entity = TestDataFactory.CreateReceiptFile(
            fileName: "invoice.pdf",
            sourceHint: "Amazon",
            yearHint: 2025);

        var dto = entity.ToDto();

        dto.Id.Should().Be(entity.Id);
        dto.OriginalFileName.Should().Be("invoice.pdf");
        dto.FileSize.Should().Be(entity.FileSize);
        dto.SourceHint.Should().Be("Amazon");
        dto.YearHint.Should().Be(2025);
        dto.UploadedBy.Should().Be(entity.UploadedBy);
        dto.Status.Should().Be("Uploaded");
    }

    [Fact]
    public void ToDto_Receipt_MapsAllProperties()
    {
        var entity = TestDataFactory.CreateReceipt(vendor: "Amazon", totalAmount: 50.00m);
        entity.Items.Add(TestDataFactory.CreateReceiptItem(receiptId: entity.Id));
        entity.Items.Add(TestDataFactory.CreateReceiptItem(receiptId: entity.Id));

        var dto = entity.ToDto();

        dto.Id.Should().Be(entity.Id);
        dto.Vendor.Should().Be("Amazon");
        dto.TotalAmount.Should().Be(50.00m);
        dto.Currency.Should().Be("EUR");
        dto.ItemCount.Should().Be(2);
    }

    [Fact]
    public void ToDto_ReceiptItem_WithClassification_MapsLatest()
    {
        var item = TestDataFactory.CreateReceiptItem(description: "Tinte blau");
        var classification = TestDataFactory.CreateClassification(
            receiptItemId: item.Id,
            category: Category.WerbungskostenBueromaterial);
        item.Classifications.Add(classification);

        var dto = item.ToDto();

        dto.Description.Should().Be("Tinte blau");
        dto.LatestClassification.Should().NotBeNull();
        dto.LatestClassification!.Category.Should().Be("WerbungskostenBueromaterial");
    }

    [Fact]
    public void ToDto_ReceiptItem_WithoutClassification_LatestIsNull()
    {
        var item = TestDataFactory.CreateReceiptItem();

        var dto = item.ToDto();

        dto.LatestClassification.Should().BeNull();
    }

    [Fact]
    public void ToDto_ItemClassification_MapsEnumsAsStrings()
    {
        var classification = TestDataFactory.CreateClassification(
            category: Category.WerbungskostenFachliteratur,
            method: ClassificationMethod.Manual,
            status: ClassificationStatus.Confirmed);

        var dto = classification.ToDto();

        dto.Category.Should().Be("WerbungskostenFachliteratur");
        dto.Method.Should().Be("Manual");
        dto.Status.Should().Be("Confirmed");
    }
}
