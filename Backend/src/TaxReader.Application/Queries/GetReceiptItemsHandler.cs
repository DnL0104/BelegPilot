using Microsoft.EntityFrameworkCore;
using TaxReader.Application.DTOs;
using TaxReader.Application.Interfaces;
using TaxReader.Application.Mapping;
using TaxReader.Domain.Common;

namespace TaxReader.Application.Queries;

public class GetReceiptItemsHandler(IAppDbContext dbContext, ICurrentUser currentUser)
{
    public async Task<Result<List<ReceiptItemDto>>> HandleAsync(
        GetReceiptItemsQuery query,
        CancellationToken cancellationToken = default)
    {
        var receiptExists = await dbContext.Receipts
            .AnyAsync(r => r.Id == query.ReceiptId && r.ReceiptFile.UserId == currentUser.UserId, cancellationToken);

        if (!receiptExists)
            return Result<List<ReceiptItemDto>>.Failure($"Receipt with id '{query.ReceiptId}' not found.");

        var items = await dbContext.ReceiptItems
            .Include(i => i.Classifications)
            .Where(i => i.ReceiptId == query.ReceiptId)
            .OrderBy(i => i.LineNumber)
            .ToListAsync(cancellationToken);

        return Result<List<ReceiptItemDto>>.Success(
            items.Select(i => i.ToDto()).ToList());
    }
}
