using Microsoft.EntityFrameworkCore;
using TaxReader.Application.Interfaces;
using TaxReader.Domain.Common;
using TaxReader.Domain.Entities;
using TaxReader.Domain.Enums;

namespace TaxReader.Application.Commands;

public class BatchConfirmHandler(IAppDbContext dbContext, ICurrentUser currentUser)
{
    public async Task<Result<int>> HandleAsync(
        BatchConfirmCommand command,
        CancellationToken cancellationToken = default)
    {
        if (command.ReceiptItemIds.Count == 0)
            return Result<int>.Failure("Keine Artikel angegeben.");

        var items = await dbContext.ReceiptItems
            .Include(i => i.Classifications)
            .Include(i => i.Receipt)
                .ThenInclude(r => r.ReceiptFile)
            .Where(i => command.ReceiptItemIds.Contains(i.Id)
                     && i.Receipt.ReceiptFile.UserId == currentUser.UserId)
            .ToListAsync(cancellationToken);

        var confirmed = 0;

        foreach (var item in items)
        {
            var latest = item.Classifications
                .OrderByDescending(c => c.ClassifiedAt)
                .FirstOrDefault();

            // Only confirm items that have a Suggested classification with a real category.
            if (latest is null
                || latest.Status != ClassificationStatus.Suggested
                || latest.Category == Category.Unknown)
                continue;

            var confirmation = new ItemClassification
            {
                Id = Guid.NewGuid(),
                ReceiptItemId = item.Id,
                Category = latest.Category,
                Method = ClassificationMethod.Manual,
                Status = ClassificationStatus.Confirmed,
                Reason = $"Batch-bestätigt (ursprünglich: {latest.Reason})",
                ClassifiedAt = DateTime.UtcNow
            };

            dbContext.ItemClassifications.Add(confirmation);
            confirmed++;
        }

        if (confirmed > 0)
            await dbContext.SaveChangesAsync(cancellationToken);

        return Result<int>.Success(confirmed);
    }
}
