using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using TaxReader.Application.DTOs;
using TaxReader.Application.Interfaces;
using TaxReader.Domain.Common;
using TaxReader.Domain.Enums;

namespace TaxReader.Application.Queries;

public partial class GetPendingSuggestionsHandler(IAppDbContext dbContext, ICurrentUser currentUser)
{
    [GeneratedRegex(@"(\d+)\s*%")]
    private static partial Regex ConfidenceRegex();

    public async Task<Result<List<PendingSuggestionDto>>> HandleAsync(
        GetPendingSuggestionsQuery query,
        CancellationToken cancellationToken = default)
    {
        var items = await dbContext.ReceiptItems
            .Include(i => i.Classifications)
            .Include(i => i.Receipt)
                .ThenInclude(r => r.ReceiptFile)
            .Where(i => i.Receipt.ReceiptFile.UserId == currentUser.UserId)
            .ToListAsync(cancellationToken);

        var suggestions = items
            .Select(item =>
            {
                var latest = item.Classifications
                    .OrderByDescending(c => c.ClassifiedAt)
                    .FirstOrDefault();
                return new { Item = item, Latest = latest };
            })
            .Where(x => x.Latest is not null
                     && x.Latest.Status == ClassificationStatus.Suggested
                     && x.Latest.Category != Category.Unbekannt)
            .Select(x => new PendingSuggestionDto(
                x.Item.Id,
                x.Item.ReceiptId,
                x.Item.Description,
                x.Item.TotalPrice,
                x.Item.Receipt.Vendor,
                x.Item.Receipt.PurchaseDate,
                x.Latest!.Category.ToString(),
                x.Latest.Reason,
                ExtractConfidence(x.Latest.Reason),
                x.Latest.ClassifiedAt))
            .OrderByDescending(s => s.ClassifiedAt)
            .ToList();

        return Result<List<PendingSuggestionDto>>.Success(suggestions);
    }

    private static double? ExtractConfidence(string reason)
    {
        var match = ConfidenceRegex().Match(reason);
        return match.Success && int.TryParse(match.Groups[1].Value, out var pct)
            ? pct / 100.0
            : null;
    }
}
