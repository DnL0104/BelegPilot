using Microsoft.EntityFrameworkCore;
using TaxReader.Application.DTOs;
using TaxReader.Application.Interfaces;
using TaxReader.Application.Mapping;
using TaxReader.Domain.Common;
using TaxReader.Domain.Enums;

namespace TaxReader.Application.Commands;

public record CorrectReceiptItemCommand(
    Guid ReceiptItemId,
    string Description,
    decimal UnitPrice,
    decimal TotalPrice);

public class CorrectReceiptItemHandler(IAppDbContext dbContext, ICurrentUser currentUser, IAuditLogger auditLogger)
{
    public async Task<Result<ReceiptItemDto>> HandleAsync(
        CorrectReceiptItemCommand command,
        CancellationToken cancellationToken = default)
    {
        var item = await dbContext.ReceiptItems
            .Include(i => i.Receipt)
                .ThenInclude(r => r.ReceiptFile)
            .FirstOrDefaultAsync(i => i.Id == command.ReceiptItemId
                && i.Receipt.ReceiptFile.UserId == currentUser.UserId, cancellationToken);

        if (item is null)
            return Result<ReceiptItemDto>.Failure(
                $"Artikel mit id '{command.ReceiptItemId}' nicht gefunden.");

        var oldDescription = item.Description;
        var oldUnitPrice = item.UnitPrice;
        var oldTotalPrice = item.TotalPrice;

        item.Description = command.Description;
        item.UnitPrice = command.UnitPrice;
        item.TotalPrice = command.TotalPrice;

        await dbContext.SaveChangesAsync(cancellationToken);

        // D-01: a correction that updates ReceiptItem without a corresponding audit
        // entry is a bug — this call must follow every write path, no early return.
        await auditLogger.RecordAsync(
            AuditAction.ItemCorrected,
            actorUserId: currentUser.UserId,
            subjectUserId: currentUser.UserId,
            metadata: new Dictionary<string, object?>
            {
                ["receipt_item_id"] = item.Id,
                ["old_description"] = oldDescription,
                ["new_description"] = command.Description,
                ["old_unit_price"] = oldUnitPrice,
                ["new_unit_price"] = command.UnitPrice,
                ["old_total_price"] = oldTotalPrice,
                ["new_total_price"] = command.TotalPrice
            },
            cancellationToken);

        return Result<ReceiptItemDto>.Success(item.ToDto());
    }
}
