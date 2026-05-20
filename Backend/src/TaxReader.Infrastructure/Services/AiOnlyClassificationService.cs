using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TaxReader.Application.Interfaces;
using TaxReader.Domain.Entities;
using TaxReader.Domain.Enums;
using TaxReader.Infrastructure.Configuration;

namespace TaxReader.Infrastructure.Services;

/// <summary>
/// Classifies every item via the AI classifier (Claude) in a single batched API call.
/// Tokens are pre-charged for the whole batch upfront, then refunded per-item if AI
/// returns Unknown for that item or the batch fails entirely. If the user has configured
/// an auto-confirm threshold, classifications at or above that confidence are
/// automatically confirmed; otherwise they stay as Suggested for manual review.
/// </summary>
public class AiOnlyClassificationService(
    IAiClassifier aiClassifier,
    ITokenService tokenService,
    ICurrentUser currentUser,
    IAppDbContext dbContext,
    IOptions<AnthropicOptions> anthropicOptions,
    ILogger<AiOnlyClassificationService> logger) : IClassificationService
{
    private readonly AnthropicOptions _anthropicOptions = anthropicOptions.Value;

    public async Task<IReadOnlyList<ItemClassification>> ClassifyItemsAsync(
        IEnumerable<ReceiptItem> items,
        CancellationToken cancellationToken = default)
    {
        var itemList = items as IReadOnlyList<ReceiptItem> ?? items.ToList();
        if (itemList.Count == 0) return [];

        if (!aiClassifier.IsConfigured)
        {
            logger.LogWarning("AI classifier not configured — {Count} items left as Unknown.", itemList.Count);
            return itemList.Select(i => Unknown(i, "AI-Klassifizierung nicht konfiguriert.")).ToList();
        }

        var threshold = await dbContext.Users
            .Where(u => u.Id == currentUser.UserId)
            .Select(u => u.AutoConfirmThreshold)
            .FirstOrDefaultAsync(cancellationToken);

        // Pre-charge the whole batch in a single DB roundtrip. If we can't cover all
        // items, fail closed and mark every item as Unknown (no partial AI runs —
        // simpler than rationing across a batch and easier for the user to understand).
        var cost = _anthropicOptions.CostPerClassification;
        var ledgerEntries = itemList
            .Select(i => new TokenLedgerEntry(cost, $"AI classification: {Truncate(i.Description, 80)}", i.Id))
            .ToList();

        var hasTokens = await tokenService.TryConsumeManyAsync(ledgerEntries, cancellationToken);
        if (!hasTokens)
        {
            logger.LogInformation(
                "AI classification skipped for {Count} items — insufficient tokens.", itemList.Count);
            return itemList
                .Select(i => Unknown(i, "Keine Tokens verfügbar – bitte Credits aufladen."))
                .ToList();
        }

        IReadOnlyList<AiClassificationResult> results;
        try
        {
            var descriptions = itemList.Select(i => i.Description).ToList();
            results = await aiClassifier.ClassifyBatchAsync(descriptions, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "AI batch classification failed — refunding {Count} items.", itemList.Count);
            await tokenService.RefundManyAsync(ledgerEntries, cancellationToken);
            return itemList.Select(i => Unknown(i, $"AI-Fehler: {ex.Message}")).ToList();
        }

        // Per-item refunds for entries the model couldn't classify. Collected in a
        // single batch so we hit the DB once instead of once-per-Unknown.
        var refunds = new List<TokenLedgerEntry>();
        var classifications = new List<ItemClassification>(itemList.Count);

        for (var i = 0; i < itemList.Count; i++)
        {
            var item = itemList[i];
            var result = results[i];

            if (result.Category == Category.Unknown)
            {
                refunds.Add(new TokenLedgerEntry(
                    cost,
                    $"Refund — AI could not classify: {Truncate(item.Description, 80)}",
                    item.Id));
                classifications.Add(Unknown(item, $"AI: {result.Reason}"));
                continue;
            }

            var meetsThreshold = threshold.HasValue && result.Confidence >= threshold.Value;
            var status = meetsThreshold
                ? ClassificationStatus.Confirmed
                : ClassificationStatus.Suggested;

            var reasonPrefix = meetsThreshold
                ? $"Auto-bestätigt ({result.Confidence:P0} ≥ {threshold:P0})"
                : $"AI ({result.Confidence:P0})";

            classifications.Add(new ItemClassification
            {
                Id = Guid.NewGuid(),
                ReceiptItemId = item.Id,
                Category = result.Category,
                Method = ClassificationMethod.AI,
                Status = status,
                Reason = $"{reasonPrefix}: {result.Reason}",
                ClassifiedAt = DateTime.UtcNow
            });
        }

        if (refunds.Count > 0)
            await tokenService.RefundManyAsync(refunds, cancellationToken);

        return classifications;
    }

    private static ItemClassification Unknown(ReceiptItem item, string reason) => new()
    {
        Id = Guid.NewGuid(),
        ReceiptItemId = item.Id,
        Category = Category.Unknown,
        Method = ClassificationMethod.AI,
        Status = ClassificationStatus.Suggested,
        Reason = reason,
        ClassifiedAt = DateTime.UtcNow
    };

    private static string Truncate(string value, int max)
        => value.Length <= max ? value : value[..max] + "…";
}
