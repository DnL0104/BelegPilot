using Microsoft.EntityFrameworkCore;
using TaxReader.Application.DTOs;
using TaxReader.Application.Interfaces;
using TaxReader.Application.Mapping;
using TaxReader.Domain.Common;

namespace TaxReader.Application.Queries;

public class GetReceiptFilesHandler(IAppDbContext dbContext, ICurrentUser currentUser)
{
    public async Task<Result<List<ReceiptFileDto>>> HandleAsync(
        GetReceiptFilesQuery query,
        CancellationToken cancellationToken = default)
    {
        var files = await dbContext.ReceiptFiles
            .Where(f => f.UserId == currentUser.UserId)
            .OrderByDescending(f => f.UploadedAt)
            .ToListAsync(cancellationToken);

        return Result<List<ReceiptFileDto>>.Success(
            files.Select(f => f.ToDto()).ToList());
    }
}
