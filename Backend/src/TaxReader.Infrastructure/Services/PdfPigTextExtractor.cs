using TaxReader.Application.Interfaces;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace TaxReader.Infrastructure.Services;

public class PdfPigTextExtractor : IPdfTextExtractor
{
    public Task<string> ExtractTextAsync(Stream pdfStream, CancellationToken cancellationToken = default)
    {
        using var document = PdfDocument.Open(pdfStream);
        var pageTexts = new List<string>();

        foreach (var page in document.GetPages())
        {
            var pageText = ExtractPageText(page);
            if (!string.IsNullOrWhiteSpace(pageText))
                pageTexts.Add(pageText);
        }

        return Task.FromResult(string.Join(Environment.NewLine, pageTexts));
    }

    private static string ExtractPageText(Page page)
    {
        var words = page.GetWords().ToList();
        if (words.Count == 0)
            return page.Text;

        // Group words into lines by Y-coordinate proximity
        // Words on the same line have similar Y values (within a tolerance)
        var lines = new List<(double Y, List<Word> Words)>();

        foreach (var word in words.OrderByDescending(w => w.BoundingBox.Bottom))
        {
            var matched = false;
            foreach (var line in lines)
            {
                // Words within 3 points vertically are on the same line
                if (Math.Abs(line.Y - word.BoundingBox.Bottom) < 3)
                {
                    line.Words.Add(word);
                    matched = true;
                    break;
                }
            }

            if (!matched)
            {
                lines.Add((word.BoundingBox.Bottom, [word]));
            }
        }

        // Sort lines top-to-bottom, words left-to-right within each line
        var sortedLines = lines
            .OrderByDescending(l => l.Y)
            .Select(l =>
            {
                var sortedWords = l.Words.OrderBy(w => w.BoundingBox.Left);
                return string.Join(" ", sortedWords.Select(w => w.Text));
            });

        return string.Join(Environment.NewLine, sortedLines);
    }
}
