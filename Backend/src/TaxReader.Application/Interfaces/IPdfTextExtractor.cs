namespace TaxReader.Application.Interfaces;

public interface IPdfTextExtractor
{
    Task<string> ExtractTextAsync(Stream pdfStream, CancellationToken cancellationToken = default);
}
