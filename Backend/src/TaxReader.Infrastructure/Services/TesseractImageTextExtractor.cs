using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TaxReader.Application.Interfaces;
using TaxReader.Infrastructure.Configuration;
using Tesseract;

namespace TaxReader.Infrastructure.Services;

/// <summary>
/// Singleton-scoped because <see cref="TesseractEngine"/> is expensive to construct
/// (loads ~10 MB of language data and initialises the LSTM model on every call).
/// We create one engine lazily and reuse it for the process lifetime.
/// Tesseract itself is NOT thread-safe — concurrent calls are serialised via <see cref="_gate"/>.
/// </summary>
public class TesseractImageTextExtractor(
    IOptions<TesseractOptions> options,
    ILogger<TesseractImageTextExtractor> logger) : IImageTextExtractor, IDisposable
{
    private readonly TesseractOptions _options = options.Value;
    private readonly Lock _gate = new();
    private TesseractEngine? _engine;
    private bool _disposed;

    public async Task<string> ExtractTextAsync(
        Stream imageStream,
        string mediaType,
        CancellationToken cancellationToken = default)
    {
        using var ms = new MemoryStream();
        await imageStream.CopyToAsync(ms, cancellationToken);
        var imageBytes = ms.ToArray();

        return await Task.Run(() => RunOcr(imageBytes), cancellationToken);
    }

    /// <summary>
    /// Resolves a potentially relative tessdata path against the application's
    /// base directory so the engine finds the files regardless of the working
    /// directory at startup (IDE, dotnet run from repo root, published output, …).
    /// Absolute paths (e.g. Docker system path) are returned unchanged.
    /// </summary>
    private static string ResolveTessDataPath(string path) =>
        Path.IsPathRooted(path) ? path : Path.Combine(AppContext.BaseDirectory, path);

    private string RunOcr(byte[] imageBytes)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            var sw = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                _engine ??= CreateEngine();
                using var loaded = Pix.LoadFromMemory(imageBytes);

                // Tesseract's recognition cost grows ~quadratically with pixel count, but
                // accuracy plateaus around 300 DPI / ~2400 px on the long edge for receipts.
                // Downsampling oversized phone-camera shots (often 4000–6000 px) typically
                // halves OCR time without measurable accuracy loss.
                const int maxEdge = 2400;
                var longEdge = Math.Max(loaded.Width, loaded.Height);
                Pix? scaled = null;
                var working = loaded;
                var note = string.Empty;
                if (longEdge > maxEdge)
                {
                    var scale = (float)maxEdge / longEdge;
                    scaled = loaded.Scale(scale, scale);
                    working = scaled;
                    note = $", downsampled ×{scale:F2}";
                }

                try
                {
                    using var page = _engine.Process(working);
                    var raw = page.GetText() ?? string.Empty;
                    var normalized = OcrTextNormalizer.Normalize(raw);
                    sw.Stop();
                    // Info-level so we can spot slow OCR runs in prod logs without needing Debug.
                    // Image dims + final dims (after downsample) + elapsed are the diagnostic levers.
                    logger.LogInformation(
                        "OCR done: {Chars} chars in {Ms} ms (input {InW}×{InH}px, processed {OutW}×{OutH}px{Note})",
                        normalized.Length, sw.ElapsedMilliseconds,
                        loaded.Width, loaded.Height, working.Width, working.Height, note);
                    logger.LogDebug("OCR normalized text:\n{Text}", normalized);
                    return normalized;
                }
                finally
                {
                    scaled?.Dispose();
                }
            }
            catch (TesseractException ex) when (ex.Message.Contains("Failed to initialise"))
            {
                var tessDataPath = ResolveTessDataPath(_options.TessDataPath);
                logger.LogError(ex, "Tesseract could not be initialised (tessdata path: {Path})", tessDataPath);
                throw new InvalidOperationException(
                    $"OCR-Engine nicht verfügbar. Tesseract ist nicht installiert oder die Sprachdaten " +
                    $"wurden nicht unter '{_options.TessDataPath}' gefunden. " +
                    $"Tipp: Digitale Rechnungen (z.B. Amazon) bitte als PDF hochladen, nicht als Screenshot.", ex);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Tesseract OCR failed");
                throw;
            }
        }
    }

    private TesseractEngine CreateEngine()
    {
        var tessDataPath = ResolveTessDataPath(_options.TessDataPath);
        logger.LogInformation("Initialising Tesseract engine (lang={Lang}, tessdata={Path})",
            _options.Language, tessDataPath);
        // LstmOnly: skip the legacy engine — modern LSTM-only is both faster and
        // more accurate for receipt text. Default would run both engines and pick
        // the better result, which roughly doubles inference time.
        var engine = new TesseractEngine(tessDataPath, _options.Language, EngineMode.LstmOnly);
        // SingleBlock (PSM 6): treat the receipt as one uniform text block. The
        // default Auto mode runs layout analysis (~hundreds of ms) which is
        // wasteful for receipts that are already a single column of text.
        engine.DefaultPageSegMode = PageSegMode.SingleBlock;
        return engine;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        lock (_gate)
        {
            _engine?.Dispose();
            _engine = null;
        }
    }
}
