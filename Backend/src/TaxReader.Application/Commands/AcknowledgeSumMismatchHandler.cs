using Microsoft.EntityFrameworkCore;
using TaxReader.Application.Interfaces;
using TaxReader.Domain.Common;

namespace TaxReader.Application.Commands;

public record AcknowledgeSumMismatchCommand(Guid ReceiptId);

public class AcknowledgeSumMismatchHandler(IAppDbContext dbContext, ICurrentUser currentUser)
{
    public async Task<Result<bool>> HandleAsync(
        AcknowledgeSumMismatchCommand command,
        CancellationToken cancellationToken = default)
    {
        // Scope: receipt belongs to current user (via ReceiptFile.UserId)
        var receipt = await dbContext.Receipts
            .Include(r => r.ReceiptFile)
            .FirstOrDefaultAsync(r => r.Id == command.ReceiptId
                && r.ReceiptFile.UserId == currentUser.UserId, cancellationToken);

        if (receipt is null)
            return Result<bool>.Failure($"Beleg mit id '{command.ReceiptId}' nicht gefunden.");

        receipt.HasSumMismatch = false;
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result<bool>.Success(true);
    }
}
