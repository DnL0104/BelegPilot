using FluentAssertions;
using TaxReader.Application.Mapping;
using TaxReader.Domain.Enums;
using TaxReader.UnitTests.Helpers;

namespace TaxReader.UnitTests.Application.Mapping;

public class DtoMappingExtensionsTests
{
    // ── ConfidenceTier mapping (D-01 thresholds: HIGH ≥0.85, MEDIUM ≥0.60, LOW <0.60) ──

    [Fact]
    public void ToDto_ItemClassification_WithHighConfidence_ReturnsHighTier()
    {
        var classification = TestDataFactory.CreateClassification(
            category: Category.WerbungskostenBueromaterial);
        classification.Confidence = 0.92;

        var dto = classification.ToDto();

        dto.ConfidenceTier.Should().Be("HIGH");
    }

    [Fact]
    public void ToDto_ItemClassification_WithMediumConfidence_ReturnsMediumTier()
    {
        var classification = TestDataFactory.CreateClassification(
            category: Category.WerbungskostenBueromaterial);
        classification.Confidence = 0.70;

        var dto = classification.ToDto();

        dto.ConfidenceTier.Should().Be("MEDIUM");
    }

    [Fact]
    public void ToDto_ItemClassification_WithLowConfidence_ReturnsLowTier()
    {
        var classification = TestDataFactory.CreateClassification(
            category: Category.WerbungskostenBueromaterial);
        classification.Confidence = 0.40;

        var dto = classification.ToDto();

        dto.ConfidenceTier.Should().Be("LOW");
    }

    [Fact]
    public void ToDto_ItemClassification_WithNullConfidence_ReturnsNullTier()
    {
        // Manual/rule-based classifications have no AI confidence score.
        var classification = TestDataFactory.CreateClassification(
            method: ClassificationMethod.Manual,
            status: ClassificationStatus.Confirmed);
        // Confidence defaults to null (not set in TestDataFactory)

        var dto = classification.ToDto();

        dto.ConfidenceTier.Should().BeNull();
    }

    [Fact]
    public void ToDto_Receipt_WithFailedItems_FailedCountSeparateFromUnknown()
    {
        // Pitfall 2 regression: a Failed item must NOT inflate UnknownCount.
        var receipt = TestDataFactory.CreateReceipt();

        var unknownItem = TestDataFactory.CreateReceiptItem(receiptId: receipt.Id, lineNumber: 1);
        var unknownClassification = TestDataFactory.CreateClassification(
            receiptItemId: unknownItem.Id,
            category: Category.Unbekannt,
            status: ClassificationStatus.Suggested);
        unknownItem.Classifications.Add(unknownClassification);

        var failedItem = TestDataFactory.CreateReceiptItem(receiptId: receipt.Id, lineNumber: 2);
        var failedClassification = TestDataFactory.CreateClassification(
            receiptItemId: failedItem.Id,
            category: Category.Unbekannt,
            status: ClassificationStatus.Failed);
        failedItem.Classifications.Add(failedClassification);

        receipt.Items.Add(unknownItem);
        receipt.Items.Add(failedItem);

        var dto = receipt.ToDto();

        dto.UnknownCount.Should().Be(1, "only the genuine Unbekannt item counts as unknown");
        dto.FailedCount.Should().Be(1, "the Failed item is counted separately");
    }


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

    [Fact]
    public void ToDto_Receipt_HasSumMismatch_False_MapsToDto()
    {
        var entity = TestDataFactory.CreateReceipt(totalAmount: 10.00m);
        entity.HasSumMismatch = false;

        var dto = entity.ToDto();

        dto.HasSumMismatch.Should().BeFalse();
    }

    [Fact]
    public void ToDto_Receipt_HasSumMismatch_True_MapsToDto()
    {
        var entity = TestDataFactory.CreateReceipt(totalAmount: 10.00m);
        entity.HasSumMismatch = true;

        var dto = entity.ToDto();

        dto.HasSumMismatch.Should().BeTrue();
    }
}
