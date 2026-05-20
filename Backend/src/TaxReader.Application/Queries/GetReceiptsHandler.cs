using Microsoft.EntityFrameworkCore;
using TaxReader.Application.DTOs;
using TaxReader.Application.Interfaces;
using TaxReader.Application.Mapping;
using TaxReader.Domain.Common;

namespace TaxReader.Application.Queries;

public class GetReceiptsHandler(IAppDbContext dbContext, ICurrentUser currentUser)
{
    public async Task<Result<List<ReceiptDto>>> HandleAsync(
        GetReceiptsQuery query,
        CancellationToken cancellationToken = default)
    {
        var receiptsQuery = dbContext.Receipts
            .Include(r => r.Items)
                .ThenInclude(i => i.Classifications)
            .Where(r => r.ReceiptFile.UserId == currentUser.UserId);

        if (query.Year.HasValue)
            receiptsQuery = receiptsQuery.Where(r => r.PurchaseDate.Year == query.Year.Value);

        var receipts = await receiptsQuery
            .OrderByDescending(r => r.PurchaseDate)
            .ToListAsync(cancellationToken);

        return Result<List<ReceiptDto>>.Success(
            receipts.Select(r => r.ToDto()).ToList());
    }
}
