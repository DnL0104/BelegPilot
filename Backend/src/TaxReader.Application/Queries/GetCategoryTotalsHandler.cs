using Microsoft.EntityFrameworkCore;
using TaxReader.Application.DTOs;
using TaxReader.Application.Interfaces;
using TaxReader.Domain.Common;
using TaxReader.Domain.Enums;

namespace TaxReader.Application.Queries;

public class GetCategoryTotalsHandler(IAppDbContext dbContext, ICurrentUser currentUser)
{
    public async Task<Result<List<CategoryTotalDto>>> HandleAsync(
        GetCategoryTotalsQuery query,
        CancellationToken cancellationToken = default)
    {
        var totals = await dbContext.ReceiptItems
            .Include(i => i.Classifications)
            .Include(i => i.Receipt)
                .ThenInclude(r => r.ReceiptFile)
            .Where(i => i.Receipt.ReceiptFile.UserId == currentUser.UserId)
            .Where(i => i.Receipt.PurchaseDate.Year == query.Year)
            .ToListAsync(cancellationToken);

        var categoryTotals = totals
            .Select(item =>
            {
                var latest = item.Classifications
                    .OrderByDescending(c => c.ClassifiedAt)
                    .FirstOrDefault();
                return new { Item = item, Category = latest?.Category ?? Category.Unbekannt };
            })
            .Where(x => x.Category != Category.Unbekannt)
            .GroupBy(x => x.Category)
            .Select(g => new CategoryTotalDto(
                g.Key.ToString(),
                g.Sum(x => x.Item.TotalPrice),
                g.Count()))
            .ToList();

        return Result<List<CategoryTotalDto>>.Success(categoryTotals);
    }
}
