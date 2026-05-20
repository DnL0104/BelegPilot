using Microsoft.EntityFrameworkCore;
using TaxReader.Application.DTOs;
using TaxReader.Application.Interfaces;
using TaxReader.Domain.Common;
using TaxReader.Domain.Enums;

namespace TaxReader.Application.Queries;

public class GetAnnualSummaryHandler(IAppDbContext dbContext, ICurrentUser currentUser)
{
    public async Task<Result<AnnualSummaryDto>> HandleAsync(
        GetAnnualSummaryQuery query,
        CancellationToken cancellationToken = default)
    {
        var receipts = await dbContext.Receipts
            .Where(r => r.ReceiptFile.UserId == currentUser.UserId)
            .Where(r => r.PurchaseDate.Year == query.Year)
            .ToListAsync(cancellationToken);

        var items = await dbContext.ReceiptItems
            .Include(i => i.Classifications)
            .Include(i => i.Receipt)
                .ThenInclude(r => r.ReceiptFile)
            .Where(i => i.Receipt.ReceiptFile.UserId == currentUser.UserId)
            .Where(i => i.Receipt.PurchaseDate.Year == query.Year)
            .ToListAsync(cancellationToken);

        var classifiedItems = items
            .Select(item =>
            {
                var latest = item.Classifications
                    .OrderByDescending(c => c.ClassifiedAt)
                    .FirstOrDefault();
                return new { Item = item, Category = latest?.Category ?? Category.Unknown };
            })
            .ToList();

        var categoryBreakdown = classifiedItems
            .Where(x => x.Category != Category.Unknown)
            .GroupBy(x => x.Category)
            .Select(g => new CategoryTotalDto(
                g.Key.ToString(),
                g.Sum(x => x.Item.TotalPrice),
                g.Count()))
            .ToList();

        var unclassifiedCount = classifiedItems.Count(x => x.Category == Category.Unknown);

        var summary = new AnnualSummaryDto(
            query.Year,
            receipts.Count,
            receipts.Sum(r => r.TotalAmount),
            categoryBreakdown,
            unclassifiedCount);

        return Result<AnnualSummaryDto>.Success(summary);
    }
}
