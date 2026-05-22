using Microsoft.Extensions.Logging;
using TaxReader.Application.Interfaces;
using TaxReader.Domain.Entities;

namespace TaxReader.Infrastructure.Services;

/// <summary>
/// D-07: Rules-first, AI-for-remainder hybrid. Evaluates RuleBasedClassifier for every item,
/// collects unmatched items, then makes ONE AI batch call for all of them — preserving
/// Phase 3 D-01 single-Anthropic-call-per-upload invariant.
/// Token pre-charge applies only to AI-bound items (D-08): AiOnlyClassificationService
/// receives only unmatched items, so its pre-charge covers exactly those items.
/// </summary>
public class HybridClassificationService(
    RuleBasedClassifier ruleBasedClassifier,
    AiOnlyClassificationService aiClassifier,
    ILogger<HybridClassificationService> logger) : IClassificationService
{
    public async Task<IReadOnlyList<ItemClassification>> ClassifyItemsAsync(
        IEnumerable<ReceiptItem> items,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var itemList = items as IReadOnlyList<ReceiptItem> ?? items.ToList();
        if (itemList.Count == 0) return [];

        var results = new List<ItemClassification>(itemList.Count);
        var aiItems = new List<ReceiptItem>();

        foreach (var item in itemList)
        {
            // ClassifyBatchJob loads receipt + receiptFile navigations — safe to access here.
            var vendor = item.Receipt?.Vendor ?? string.Empty;
            var fileName = item.Receipt?.ReceiptFile?.OriginalFileName ?? string.Empty;

            var ruleMatch = await ruleBasedClassifier.ClassifyItemAsync(
                item, vendor, fileName, userId, cancellationToken);

            if (ruleMatch is not null)
                results.Add(ruleMatch);
            else
                aiItems.Add(item);
        }

        logger.LogInformation(
            "Hybrid classification: {RuleCount} rule-matched, {AiCount} AI-bound for user {UserId}",
            results.Count, aiItems.Count, userId);

        // Single AI batch call for all unmatched items — preserves Phase 3 D-01 batching invariant
        if (aiItems.Count > 0)
        {
            var aiResults = await aiClassifier.ClassifyItemsAsync(aiItems, userId, cancellationToken);
            results.AddRange(aiResults);
        }

        return results;
    }
}
