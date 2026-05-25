using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;
using TaxReader.Application.Interfaces;
using TaxReader.Domain.Entities;
using TaxReader.Domain.Enums;

namespace TaxReader.Infrastructure.Services;

public class RuleBasedClassifier(
    IAppDbContext dbContext,
    ILogger<RuleBasedClassifier> logger)
{
    public async Task<ItemClassification?> ClassifyItemAsync(
        ReceiptItem item,
        string vendor,
        string sourceFileName,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        // D-06: user rules first (faster exit for most classifications after rules are established)
        var userRules = await dbContext.ClassificationRules
            .Where(r => r.UserId == userId && r.IsActive)
            .OrderByDescending(r => r.Priority)
            .ToListAsync(cancellationToken);

        var matched = userRules.FirstOrDefault(r => Matches(r, item.Description, vendor, sourceFileName));
        if (matched is not null)
        {
            logger.LogInformation(
                "User rule {RuleId} matched item {ItemId} for user {UserId}",
                matched.Id, item.Id, userId);
            return BuildClassification(item, matched);
        }

        // System rules fallback (UserId == null)
        var systemRules = await dbContext.ClassificationRules
            .Where(r => r.UserId == null && r.IsActive)
            .OrderByDescending(r => r.Priority)
            .ToListAsync(cancellationToken);

        matched = systemRules.FirstOrDefault(r => Matches(r, item.Description, vendor, sourceFileName));
        if (matched is not null)
        {
            logger.LogInformation(
                "System rule {RuleId} matched item {ItemId}",
                matched.Id, item.Id);
        }
        return matched is null ? null : BuildClassification(item, matched);
    }

    // D-05: VendorPattern = substring OrdinalIgnoreCase; Description/SourceFile = regex IgnoreCase
    // ALL non-null fields must match for the rule to fire.
    private static bool Matches(ClassificationRule rule, string description, string vendor, string fileName)
    {
        if (rule.VendorPattern is not null
            && !vendor.Contains(rule.VendorPattern, StringComparison.OrdinalIgnoreCase))
            return false;

        if (rule.DescriptionPattern is not null
            && !Regex.IsMatch(description, rule.DescriptionPattern,
                              RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(200)))
            return false;

        if (rule.SourceFilePattern is not null
            && !Regex.IsMatch(fileName, rule.SourceFilePattern,
                              RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(200)))
            return false;

        return true;
    }

    private static ItemClassification BuildClassification(ReceiptItem item, ClassificationRule rule)
    {
        // Use the most specific pattern as the reason description
        var patternDesc = rule.DescriptionPattern
            ?? rule.VendorPattern
            ?? rule.SourceFilePattern
            ?? "?";

        return new ItemClassification
        {
            Id = Guid.NewGuid(),
            ReceiptItemId = item.Id,
            Category = rule.Category,
            Method = ClassificationMethod.Rule,
            Status = ClassificationStatus.Confirmed,   // Rules are deterministic — always Confirmed
            Reason = $"Regel angewendet: {patternDesc} → {rule.Category}",
            ClassifiedAt = DateTime.UtcNow
        };
    }
}
