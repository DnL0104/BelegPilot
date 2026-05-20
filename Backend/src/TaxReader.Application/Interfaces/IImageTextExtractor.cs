namespace TaxReader.Application.Interfaces;

public interface IImageTextExtractor
{
    Task<string> ExtractTextAsync(Stream imageStream, string mediaType, CancellationToken cancellationToken = default);
}
