using Microsoft.EntityFrameworkCore;
using TaxReader.Application.Interfaces;
using TaxReader.Domain.Common;

namespace TaxReader.Application.Commands;

public class BulkDeleteReceiptFilesHandler(
    IAppDbContext dbContext,
    ICurrentUser currentUser)
{
    public async Task<Result<int>> HandleAsync(
        BulkDeleteReceiptFilesCommand command,
        CancellationToken cancellationToken = default)
    {
        if (command.ReceiptFileIds.Count == 0)
            return Result<int>.Failure("Keine Dateien zum Löschen angegeben.");

        var files = await dbContext.ReceiptFiles
            .Where(f => command.ReceiptFileIds.Contains(f.Id)
                     && f.UserId == currentUser.UserId)
            .ToListAsync(cancellationToken);

        if (files.Count == 0)
            return Result<int>.Failure("Keine der angegebenen Dateien gefunden.");

        foreach (var file in files)
            dbContext.ReceiptFiles.Remove(file);

        await dbContext.SaveChangesAsync(cancellationToken);

        return Result<int>.Success(files.Count);
    }
}
