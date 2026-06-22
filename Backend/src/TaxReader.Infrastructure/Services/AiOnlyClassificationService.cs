using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TaxReader.Application.Interfaces;
using TaxReader.Domain.Entities;
using TaxReader.Domain.Enums;
using TaxReader.Infrastructure.Configuration;

namespace TaxReader.Infrastructure.Services;

/// <summary>
/// Classifies every item via the AI classifier (Claude) in sequential ≤50-item chunks.
/// Tokens are pre-charged per chunk upfront, then:
///   - Refunded per-item when AI returns Unbekannt for that item.
///   - Refunded for the whole chunk if ClassifyBatchAsync throws on both the first
///     attempt and the one automatic retry; those items are marked Failed (D-04/CLS-01).
/// Partial success (D-08): a failed chunk does not roll back successfully classified chunks.
///
/// Phase 3 refactor: no longer depends on ICurrentUser — the caller (a Hangfire job
/// running outside any HTTP request) passes the owning userId explicitly.
/// </summary>
public class AiOnlyClassificationService(
    IAiClassifier aiClassifier,
    ITokenService tokenService,
    IAppDbContext dbContext,
    IOptions<AnthropicOptions> anthropicOptions,
    ILogger<AiOnlyClassificationService> logger) : IClassificationService
{
    private readonly AnthropicOptions _anthropicOptions = anthropicOptions.Value;
    private const int ChunkSize = 50;

    public async Task<IReadOnlyList<ItemClassification>> ClassifyItemsAsync(
        IEnumerable<ReceiptItem> items,
        Guid userId,
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
            .Where(u => u.Id == userId)
            .Select(u => u.AutoConfirmThreshold)
            .FirstOrDefaultAsync(cancellationToken);

        var cost = _anthropicOptions.CostPerClassification;
        var allClassifications = new List<ItemClassification>(itemList.Count);

        foreach (var chunk in itemList.Chunk(ChunkSize))
        {
            var chunkList = chunk.ToList();

            // Pre-charge this chunk only (per D-09 / D-08 partial-success model).
            var chunkLedger = chunkList
                .Select(i => new TokenLedgerEntry(cost, $"AI classification: {Truncate(i.Description, 80)}", i.Id))
                .ToList();

            var hasTokens = await tokenService.TryConsumeManyAsync(chunkLedger, cancellationToken);
            if (!hasTokens)
            {
                logger.LogInformation(
                    "AI classification skipped for chunk of {Count} items — insufficient tokens.", chunkList.Count);
                allClassifications.AddRange(chunkList
                    .Select(i => Unknown(i, "Keine Tokens verfügbar – bitte Credits aufladen.")));
                continue;
            }

            // Retry-once wrapper around the Anthropic call (D-04).
            // The cancellation guard ensures OperationCanceledException is NOT swallowed
            // into a retry — it propagates immediately (Pitfall 3).
            IReadOnlyList<AiClassificationResult>? results = null;
            var attempt = 0;
            while (true)
            {
                try
                {
                    var descriptions = chunkList.Select(i => i.Description).ToList();
                    results = await aiClassifier.ClassifyBatchAsync(descriptions, cancellationToken);
                    break;
                }
                catch (Exception ex) when (attempt == 0 && !cancellationToken.IsCancellationRequested)
                {
                    attempt++;
                    logger.LogWarning(ex, "AI call failed on attempt 1 — retrying chunk of {Count} items.", chunkList.Count);
                    await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    // Cancellation must propagate — never swallow it into a retry or Failed path.
                    // The token pre-charge for this chunk will not be refunded here; the caller
                    // (Hangfire job) is being cancelled and the whole operation is abandoned.
                    throw;
                }
                catch (Exception ex)
                {
                    // Second failure — retry budget exhausted. Mark chunk items Failed and refund.
                    logger.LogError(ex, "AI call failed on retry — marking {Count} items as Failed.", chunkList.Count);
                    await tokenService.RefundManyAsync(chunkLedger, cancellationToken);
                    allClassifications.AddRange(chunkList.Select(i => Failed(i, $"AI-Fehler: {ex.Message}")));
                    results = null;
                    break;
                }
            }

            // null signals the Failed path was taken; skip per-item mapping for this chunk.
            if (results is null) continue;

            // Per-item refunds for entries the model couldn't classify.
            // Collected in one list so we hit the DB once instead of once-per-Unknown.
            var refunds = new List<TokenLedgerEntry>();

            for (var i = 0; i < chunkList.Count; i++)
            {
                var item = chunkList[i];
                var result = results[i];

                if (result.Category == Category.Unbekannt)
                {
                    refunds.Add(new TokenLedgerEntry(
                        cost,
                        $"Refund — AI could not classify: {Truncate(item.Description, 80)}",
                        item.Id));
                    allClassifications.Add(Unknown(item, $"AI: {result.Reason}"));
                    continue;
                }

                var meetsThreshold = threshold.HasValue && result.Confidence >= threshold.Value;
                var status = meetsThreshold
                    ? ClassificationStatus.Confirmed
                    : ClassificationStatus.Suggested;

                var reasonPrefix = meetsThreshold
                    ? $"Auto-bestätigt ({result.Confidence:P0} ≥ {threshold:P0})"
                    : $"AI ({result.Confidence:P0})";

                allClassifications.Add(new ItemClassification
                {
                    Id = Guid.NewGuid(),
                    ReceiptItemId = item.Id,
                    Category = result.Category,
                    Method = ClassificationMethod.AI,
                    Status = status,
                    Reason = $"{reasonPrefix}: {result.Reason}",
                    ClassifiedAt = DateTime.UtcNow,
                    Confidence = result.Confidence   // populated for DTO confidence-tier computation (CLS-03)
                });
            }

            if (refunds.Count > 0)
                await tokenService.RefundManyAsync(refunds, cancellationToken);
        }

        return allClassifications;
    }

    private static ItemClassification Unknown(ReceiptItem item, string reason) => new()
    {
        Id = Guid.NewGuid(),
        ReceiptItemId = item.Id,
        Category = Category.Unbekannt,
        Method = ClassificationMethod.AI,
        Status = ClassificationStatus.Suggested,
        Reason = reason,
        ClassifiedAt = DateTime.UtcNow
    };

    /// <summary>
    /// Creates a Failed classification for items where the AI call exhausted its retry
    /// budget. Category stays Unbekannt because the model never produced a result.
    /// Status = Failed (not Suggested) so the UI can render "Fehler" distinctly (CLS-02).
    /// </summary>
    private static ItemClassification Failed(ReceiptItem item, string reason) => new()
    {
        Id = Guid.NewGuid(),
        ReceiptItemId = item.Id,
        Category = Category.Unbekannt,   // model never ran — Unbekannt is correct, not a classification result
        Method = ClassificationMethod.AI,
        Status = ClassificationStatus.Failed,
        Reason = reason,
        ClassifiedAt = DateTime.UtcNow
    };

    private static string Truncate(string value, int max)
        => value.Length <= max ? value : value[..max] + "…";
}
