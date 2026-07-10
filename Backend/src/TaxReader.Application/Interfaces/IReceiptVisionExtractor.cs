namespace TaxReader.Application.Interfaces;

public interface IReceiptVisionExtractor
{
    bool IsConfigured { get; }

    /// <summary>
    /// Extracts vendor, line items, and total from a single receipt image or PDF via one
    /// Haiku 4.5 vision call. Returns <c>null</c> when the model response is malformed or
    /// unparseable — never throws a parse exception and never returns partially-trusted data.
    /// A non-2xx HTTP response is surfaced as a thrown <see cref="HttpRequestException"/>.
    /// </summary>
    Task<VisionExtractionResult?> ExtractAsync(
        Stream content,
        string mediaType,
        bool isPdf,
        CancellationToken cancellationToken = default);
}

public record VisionExtractionResult(
    string? Vendor,
    IReadOnlyList<VisionExtractedItem> Items,
    decimal? Total);

public record VisionExtractedItem(string Description, decimal Price);
