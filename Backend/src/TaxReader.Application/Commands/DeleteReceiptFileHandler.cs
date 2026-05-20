using Microsoft.EntityFrameworkCore;
using TaxReader.Application.Interfaces;
using TaxReader.Domain.Common;

namespace TaxReader.Application.Commands;

public class DeleteReceiptFileHandler(
    IAppDbContext dbContext,
    ICurrentUser currentUser)
{
    public async Task<Result<bool>> HandleAsync(
        DeleteReceiptFileCommand command,
        CancellationToken cancellationToken = default)
    {
        var receiptFile = await dbContext.ReceiptFiles
            .FirstOrDefaultAsync(f => f.Id == command.ReceiptFileId && f.UserId == currentUser.UserId, cancellationToken);

        if (receiptFile is null)
            return Result<bool>.Failure($"Receipt file with id '{command.ReceiptFileId}' not found.");

        dbContext.ReceiptFiles.Remove(receiptFile);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result<bool>.Success(true);
    }
}
