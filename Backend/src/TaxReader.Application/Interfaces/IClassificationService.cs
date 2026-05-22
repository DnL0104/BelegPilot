using TaxReader.Domain.Entities;

namespace TaxReader.Application.Interfaces;

public interface IClassificationService
{
    /// <summary>
    /// Classifies a batch of receipt items via the AI. Hangfire jobs are HTTP-context-free
    /// so the caller MUST pass the owning userId explicitly (ICurrentUser returns Guid.Empty
    /// inside a job).
    /// </summary>
    Task<IReadOnlyList<ItemClassification>> ClassifyItemsAsync(
        IEnumerable<ReceiptItem> items,
        Guid userId,
        CancellationToken cancellationToken = default);
}
