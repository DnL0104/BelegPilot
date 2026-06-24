using Microsoft.EntityFrameworkCore;
using TaxReader.Application.DTOs;
using TaxReader.Application.Interfaces;
using TaxReader.Application.Mapping;
using TaxReader.Domain.Common;
using TaxReader.Domain.Enums;

namespace TaxReader.Application.Commands;

public class RetryFailedItemsHandler(
    IAppDbContext dbContext,
    IClassificationService classificationService,
    ICurrentUser currentUser)
{
    public async Task<Result<IReadOnlyList<ReceiptItemDto>>> HandleAsync(
        RetryFailedItemsCommand command,
        CancellationToken cancellationToken = default)
    {
        // IDOR ownership gate — copied verbatim from ReclassifyReceiptHandler (T-02-06).
        // Non-owned receipt id returns the same not-found shape — does not reveal existence.
        var receipt = await dbContext.Receipts
            .Include(r => r.ReceiptFile)
            .Include(r => r.Items)
                .ThenInclude(i => i.Classifications)
            .FirstOrDefaultAsync(
                r => r.Id == command.ReceiptId
                  && r.ReceiptFile.UserId == currentUser.UserId,
                cancellationToken);

        if (receipt is null)
            return Result<IReadOnlyList<ReceiptItemDto>>.Failure(
                $"Receipt with id '{command.ReceiptId}' not found.");

        var failedItems = receipt.Items
            .Where(i => i.Classifications
                .OrderByDescending(c => c.ClassifiedAt)
                .FirstOrDefault()?.Status == ClassificationStatus.Failed)
            .ToList();

        if (failedItems.Count == 0)
            return Result<IReadOnlyList<ReceiptItemDto>>.Failure(
                "Keine fehlgeschlagenen Klassifizierungen für diesen Beleg gefunden.");

        var classifications = await classificationService.ClassifyItemsAsync(
            failedItems, currentUser.UserId, cancellationToken);

        foreach (var classification in classifications)
        {
            classification.Id = Guid.NewGuid();
            dbContext.ItemClassifications.Add(classification);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        // Reload items with fresh classifications so LatestClassification reflects the new ones.
        // WR-02: AsNoTracking (read-only projection) + ownership re-filter (defense in depth —
        // the IDOR gate above already proved ownership, but the reload should not trust ReceiptId alone).
        var updatedItems = await dbContext.ReceiptItems
            .AsNoTracking()
            .Include(i => i.Classifications)
            .Where(i => i.ReceiptId == command.ReceiptId
                     && i.Receipt.ReceiptFile.UserId == currentUser.UserId)
            .OrderBy(i => i.LineNumber)
            .ToListAsync(cancellationToken);

        return Result<IReadOnlyList<ReceiptItemDto>>.Success(
            updatedItems.Select(i => i.ToDto()).ToList());
    }
}
