using TaxReader.Domain.Enums;

namespace TaxReader.Application.Interfaces;

public interface IAiClassifier
{
    bool IsConfigured { get; }

    /// <summary>
    /// Classifies a batch of items in a single API call. Returns one result per
    /// input description, in the same order. The returned list always has the same
    /// length as the input — items the model couldn't classify come back as
    /// <see cref="Category.Unknown"/> rather than being omitted.
    /// </summary>
    Task<IReadOnlyList<AiClassificationResult>> ClassifyBatchAsync(
        IReadOnlyList<string> itemDescriptions,
        CancellationToken cancellationToken = default);
}

public record AiClassificationResult(Category Category, string Reason, double Confidence);
