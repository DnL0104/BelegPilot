using Microsoft.EntityFrameworkCore;
using TaxReader.Application.DTOs;
using TaxReader.Application.Interfaces;
using TaxReader.Domain.Common;
using TaxReader.Domain.Enums;

namespace TaxReader.Application.Queries;

public record GetExportDataQuery(int Year, bool ConfirmedOnly = false);

public class GetExportDataHandler(IAppDbContext dbContext, ICurrentUser currentUser)
{
    public async Task<Result<List<ExportItemDto>>> HandleAsync(
        GetExportDataQuery query,
        CancellationToken cancellationToken = default)
    {
        var items = await dbContext.ReceiptItems
            .Include(i => i.Classifications)
            .Include(i => i.Receipt)
                .ThenInclude(r => r.ReceiptFile)
            .Where(i => i.Receipt.ReceiptFile.UserId == currentUser.UserId)
            .Where(i => i.Receipt.PurchaseDate.Year == query.Year)
            .OrderBy(i => i.Receipt.PurchaseDate)
            .ThenBy(i => i.LineNumber)
            .ToListAsync(cancellationToken);

        var exportItems = items
            .Select(item =>
            {
                var latest = item.Classifications
                    .OrderByDescending(c => c.ClassifiedAt)
                    .FirstOrDefault();

                var category = latest?.Category ?? Category.Unknown;
                var status = latest?.Status.ToString() ?? "None";
                var method = latest?.Method.ToString() ?? "None";
                var reason = latest?.Reason;

                return new ExportItemDto(
                    item.Receipt.PurchaseDate,
                    item.Receipt.Vendor,
                    item.Description,
                    item.Quantity,
                    item.UnitPrice,
                    item.TotalPrice,
                    category.ToString(),
                    status,
                    method,
                    reason);
            })
            .Where(e => !query.ConfirmedOnly
                || e.ClassificationStatus == ClassificationStatus.Confirmed.ToString())
            .ToList();

        return Result<List<ExportItemDto>>.Success(exportItems);
    }
}
