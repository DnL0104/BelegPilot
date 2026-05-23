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

        foreach (var item in entity.Items)
        {
            var latest = item.Classifications
                .OrderByDescending(c => c.ClassifiedAt)
                .FirstOrDefault();

            if (latest is null || latest.Category == Category.Unbekannt)
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
            entity.HasSumMismatch,
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
            entity.ClassifiedAt);

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
