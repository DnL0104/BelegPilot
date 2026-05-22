using Microsoft.EntityFrameworkCore;
using TaxReader.Application.DTOs;
using TaxReader.Application.Interfaces;
using TaxReader.Application.Mapping;
using TaxReader.Domain.Common;

namespace TaxReader.Application.Commands;

public class ReclassifyReceiptHandler(
    IAppDbContext dbContext,
    IClassificationService classificationService,
    ICurrentUser currentUser)
{
    public async Task<Result<IReadOnlyList<ReceiptItemDto>>> HandleAsync(
        ReclassifyReceiptCommand command,
        CancellationToken cancellationToken = default)
    {
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

        if (receipt.Items.Count == 0)
            return Result<IReadOnlyList<ReceiptItemDto>>.Failure(
                "Receipt has no items to classify.");

        // Run AI classification on all items (creates new classification records,
        // preserving the historical chain). Phase 3: ClassifyItemsAsync now takes an
        // explicit userId since Hangfire jobs are HTTP-context-free; the synchronous
        // reclassify handler still uses ICurrentUser.UserId.
        var classifications = await classificationService.ClassifyItemsAsync(
            receipt.Items, currentUser.UserId, cancellationToken);

        foreach (var classification in classifications)
        {
            classification.Id = Guid.NewGuid();
            dbContext.ItemClassifications.Add(classification);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        // Reload items with the fresh classifications so LatestClassification reflects the new ones.
        var updatedItems = await dbContext.ReceiptItems
            .Include(i => i.Classifications)
            .Where(i => i.ReceiptId == command.ReceiptId)
            .OrderBy(i => i.LineNumber)
            .ToListAsync(cancellationToken);

        var dtos = updatedItems.Select(i => i.ToDto()).ToList();
        return Result<IReadOnlyList<ReceiptItemDto>>.Success(dtos);
    }
}
