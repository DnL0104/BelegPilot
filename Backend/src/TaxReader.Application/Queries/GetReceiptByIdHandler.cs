using Microsoft.EntityFrameworkCore;
using TaxReader.Application.DTOs;
using TaxReader.Application.Interfaces;
using TaxReader.Application.Mapping;
using TaxReader.Domain.Common;

namespace TaxReader.Application.Queries;

public class GetReceiptByIdHandler(IAppDbContext dbContext, ICurrentUser currentUser)
{
    public async Task<Result<ReceiptDto>> HandleAsync(
        GetReceiptByIdQuery query,
        CancellationToken cancellationToken = default)
    {
        var receipt = await dbContext.Receipts
            .Include(r => r.Items)
                .ThenInclude(i => i.Classifications)
            .Where(r => r.ReceiptFile.UserId == currentUser.UserId)
            .FirstOrDefaultAsync(r => r.Id == query.Id, cancellationToken);

        if (receipt is null)
            return Result<ReceiptDto>.Failure($"Receipt with id '{query.Id}' not found.");

        return Result<ReceiptDto>.Success(receipt.ToDto(includeRawText: true));
    }
}
