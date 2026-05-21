namespace TaxReader.Infrastructure.Configuration;

public class TesseractOptions
{
    public const string SectionName = "Tesseract";

    /// <summary>
    /// Absolute path to the tessdata directory.
    /// Docker default: /usr/share/tesseract-ocr/5/tessdata
    /// Windows dev:    C:/Program Files/Tesseract-OCR/tessdata
    /// </summary>
    public string TessDataPath { get; set; } = "/usr/share/tesseract-ocr/5/tessdata";

    /// <summary>
    /// Tesseract language(s) to use, e.g. "deu+eng".
    /// </summary>
    public string Language { get; set; } = "deu+eng";

    /// <summary>
    /// D-16: number of TesseractEngine instances in the pool. Sized for the typical
    /// concurrent-OCR-2-or-3 at the 100–500 user target. Hangfire WorkerCount is
    /// aligned to this value in DependencyInjection.cs — never set WorkerCount higher,
    /// or jobs will queue indefinitely on Channel.Reader.ReadAsync (RESEARCH Pitfall 7).
    /// Bound from the Tesseract__PoolSize env var.
    /// </summary>
    public int PoolSize { get; set; } = 3;
}
