using Microsoft.EntityFrameworkCore;
using TaxReader.Application.DTOs;
using TaxReader.Application.Interfaces;
using TaxReader.Application.Mapping;
using TaxReader.Domain.Common;
using TaxReader.Domain.Entities;
using TaxReader.Domain.Enums;

namespace TaxReader.Application.Commands;

public class ConfirmClassificationHandler(IAppDbContext dbContext, ICurrentUser currentUser)
{
    public async Task<Result<ItemClassificationDto>> HandleAsync(
        ConfirmClassificationCommand command,
        CancellationToken cancellationToken = default)
    {
        var item = await dbContext.ReceiptItems
            .Include(i => i.Receipt)
                .ThenInclude(r => r.ReceiptFile)
            .FirstOrDefaultAsync(i => i.Id == command.ReceiptItemId
                && i.Receipt.ReceiptFile.UserId == currentUser.UserId, cancellationToken);

        if (item is null)
            return Result<ItemClassificationDto>.Failure(
                $"Artikel mit id '{command.ReceiptItemId}' nicht gefunden.");

        var classification = new ItemClassification
        {
            Id = Guid.NewGuid(),
            ReceiptItemId = command.ReceiptItemId,
            Category = command.Category,
            Method = ClassificationMethod.Manual,
            Status = ClassificationStatus.Confirmed,
            Reason = $"Manuell bestätigt als {command.Category}",
            ClassifiedAt = DateTime.UtcNow
        };

        dbContext.ItemClassifications.Add(classification);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result<ItemClassificationDto>.Success(classification.ToDto());
    }
}
