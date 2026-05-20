using TaxReader.Domain.Entities;

namespace TaxReader.Application.Interfaces;

public interface IClassificationService
{
    Task<IReadOnlyList<ItemClassification>> ClassifyItemsAsync(
        IEnumerable<ReceiptItem> items,
        CancellationToken cancellationToken = default);
}
