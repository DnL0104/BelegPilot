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
}
