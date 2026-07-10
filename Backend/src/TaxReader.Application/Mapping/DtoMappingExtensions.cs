using TaxReader.Application.DTOs;
using TaxReader.Domain.Entities;
using TaxReader.Domain.Enums;

namespace TaxReader.Application.Mapping;

public static class DtoMappingExtensions
{
    public static ReceiptFileDto ToDto(this ReceiptFile entity) =>
        new(
            entity.Id,
            entity.OriginalFileName,
            entity.FileSize,
            entity.SourceHint,
            entity.YearHint,
            entity.UploadedBy,
            entity.UploadedAt,
            entity.Status.ToString());

    public static ReceiptDto ToDto(this Receipt entity, bool includeRawText = false)
    {
        var suggestedCount = 0;
        var unknownCount = 0;
        var failedCount = 0;

        foreach (var item in entity.Items)
        {
            // WR-01: reuse the LatestClassification computed property as the single source of
            // truth for "latest" ordering, so this count never drifts from ToDto(ReceiptItem).
            var latest = item.LatestClassification;

            // Failed items (technical failures) are counted separately — they do NOT
            // inflate unknownCount (Pitfall 2 regression guard).
            if (latest?.Status == ClassificationStatus.Failed)
                failedCount++;
            else if (latest is null || latest.Category == Category.Unbekannt)
                unknownCount++;
            else if (latest.Status == ClassificationStatus.Suggested)
                suggestedCount++;
        }

        return new(
            entity.Id,
            entity.ReceiptFileId,
            entity.Vendor,
            entity.PurchaseDate,
            entity.SubTotal,
            entity.TaxAmount,
            entity.TotalAmount,
            entity.Currency,
            entity.ParsedAt,
            entity.Items.Count,
            suggestedCount,
            unknownCount,
            failedCount,
            entity.HasSumMismatch,
            entity.ExtractionSource.ToString(),
            includeRawText ? entity.RawExtractedText : null);
    }

    public static ReceiptItemDto ToDto(this ReceiptItem entity) =>
        new(
            entity.Id,
            entity.ReceiptId,
            entity.Description,
            entity.Quantity,
            entity.UnitPrice,
            entity.TotalPrice,
            entity.LineNumber,
            entity.LatestClassification?.ToDto());

    public static ItemClassificationDto ToDto(this ItemClassification entity) =>
        new(
            entity.Id,
            entity.Category.ToString(),
            entity.Method.ToString(),
            entity.Status.ToString(),
            entity.Reason,
            entity.ClassifiedAt,
            ToConfidenceTier(entity.Confidence));

    // D-01: HIGH ≥ 85%, MEDIUM 60–84%, LOW < 60%, null for manual/rule (no AI score).
    private static string? ToConfidenceTier(double? confidence) => confidence switch
    {
        null => null,
        >= 0.85 => "HIGH",
        >= 0.60 => "MEDIUM",
        _ => "LOW"
    };

    public static ClassificationRuleDto ToDto(this ClassificationRule rule) => new(
        rule.Id,
        rule.UserId,
        rule.VendorPattern,
        rule.DescriptionPattern,
        rule.SourceFilePattern,
        rule.Category.ToString(),
        rule.Priority,
        rule.IsActive,
        rule.CreatedAt);
}
