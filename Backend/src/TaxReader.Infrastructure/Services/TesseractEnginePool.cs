using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using TaxReader.Application.Interfaces;
using TaxReader.Infrastructure.Configuration;
using Tesseract;

namespace TaxReader.Infrastructure.Services;

/// <summary>
/// Bounded <see cref="Channel{T}"/> pool of <see cref="TesseractEngine"/> instances.
/// Replaces the previous Singleton-with-lock <c>TesseractImageTextExtractor</c> so concurrent
/// image uploads no longer serialise on a single engine. Pool capacity is
/// <see cref="TesseractOptions.PoolSize"/> (default 3). Hangfire's WorkerCount is aligned
/// to the same value in <c>DependencyInjection.cs</c> so workers never out-pace OCR engines
/// (RESEARCH § Pitfall 7).
///
/// Engines are eagerly warmed up by <see cref="TesseractEnginePoolWarmupService"/> at host
/// start. On <see cref="TesseractException"/> or <see cref="OutOfMemoryException"/> the
/// engine is quarantined (Disposed + dropped) and a replacement is spawned (D-19).
/// </summary>
public sealed class TesseractEnginePool : IImageTextExtractor, IDisposable
{
    private readonly TesseractOptions _options;
    private readonly ILogger<TesseractEnginePool> _logger;
    private readonly Channel<TesseractEngine> _channel;
    private int _engineCount;
    private bool _disposed;

    /// <summary>
    /// Test seam — when set, replaces the real <see cref="TesseractEngine"/> ctor so unit
    /// tests can exercise the channel mechanics without a native Tesseract install.
    /// </summary>
    internal Func<TesseractEngine>? EngineFactoryOverride { get; set; }

    /// <summary>Diagnostic counter; tests read this to assert pool invariants.</summary>
    internal int LiveEngineCount => Volatile.Read(ref _engineCount);

    public TesseractEnginePool(IOptions<TesseractOptions> options, ILogger<TesseractEnginePool> logger)
    {
        _options = options.Value;
        _logger = logger;
        _channel = Channel.CreateBounded<TesseractEngine>(new BoundedChannelOptions(_options.PoolSize)
        {
            // Wait mode is unreachable in practice — we only ever Write what we Read — but
            // it's the most defensive choice in case a future code path tries to over-fill.
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = false,
            SingleWriter = false
        });
    }

    /// <summary>
    /// Eagerly creates <see cref="TesseractOptions.PoolSize"/> engines. Called once at
    /// host start by <see cref="TesseractEnginePoolWarmupService"/>. Idempotent — calling
    /// twice does not exceed the channel's bounded capacity (extra engines are Disposed).
    /// </summary>
    public void Initialize()
    {
        for (var i = 0; i < _options.PoolSize; i++)
        {
            var engine = CreateEngine();
            if (_channel.Writer.TryWrite(engine))
            {
                Interlocked.Increment(ref _engineCount);
            }
            else
            {
                // Channel already full (likely Initialize called twice) — dispose this
                // engine, do NOT leak it. Pitfall 6. Null guard for test-seam factories.
                engine?.Dispose();
            }
        }
        _logger.LogInformation(
            "Tesseract pool warmed up with {Count} engines (configured PoolSize={PoolSize})",
            _engineCount, _options.PoolSize);
    }

    public async Task<string> ExtractTextAsync(
        Stream imageStream,
        string mediaType,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        using var ms = new MemoryStream();
        await imageStream.CopyToAsync(ms, cancellationToken);
        var imageBytes = NormalizeOrientation(ms.ToArray());

        // Acquire — Hangfire's job CancellationToken is the only thing that aborts the wait.
        var engine = await _channel.Reader.ReadAsync(cancellationToken);
        var quarantined = false;
        try
        {
            return await Task.Run(() => RunOcr(engine, imageBytes), cancellationToken);
        }
        catch (TesseractException ex)
        {
            quarantined = true;
            _logger.LogWarning(ex, "Tesseract engine threw TesseractException — quarantining and replacing");
            throw;
        }
        catch (OutOfMemoryException ex)
        {
            quarantined = true;
            _logger.LogError(ex, "Tesseract engine OOM — quarantining and replacing");
            throw;
        }
        finally
        {
            if (quarantined)
            {
                // D-19: dispose dead engine + spawn replacement. Replacement cost ~100ms.
                engine.Dispose();
                Interlocked.Decrement(ref _engineCount);
                try
                {
                    var replacement = CreateEngine();
                    if (_channel.Writer.TryWrite(replacement))
                    {
                        Interlocked.Increment(ref _engineCount);
                    }
                    else
                    {
                        // Pitfall 6: TryWrite false — dispose, don't leak. Log so monitoring
                        // can baseline this as a "should be rare" event.
                        replacement.Dispose();
                        _logger.LogWarning(
                            "Replacement engine could not enter channel (pool full); engine disposed; engine count {Count}",
                            _engineCount);
                    }
                }
                catch (Exception spawnEx)
                {
                    _logger.LogError(spawnEx,
                        "Failed to spawn replacement engine; pool count dropped to {Count}",
                        _engineCount);
                }
            }
            else
            {
                // Healthy engine — return to pool. TryWrite must succeed because we acquired
                // by reading (capacity has room for at least one Write). Defensive guard:
                // if it somehow rejects, dispose to avoid leak, log so we notice.
                if (!_channel.Writer.TryWrite(engine))
                {
                    engine.Dispose();
                    Interlocked.Decrement(ref _engineCount);
                    _logger.LogWarning(
                        "Healthy engine could not return to channel; disposed; pool count {Count}",
                        _engineCount);
                }
            }
        }
    }

    /// <summary>
    /// Phone photos routinely carry an EXIF Orientation tag instead of pre-rotated
    /// pixels (e.g. a long thermal receipt photographed sideways to fit the whole
    /// strip in frame). Leptonica's <see cref="Pix.LoadFromMemory"/> ignores EXIF
    /// entirely, so without this step Tesseract sees the raw sensor orientation and
    /// produces unusable output. Best-effort: a decode failure falls back to the
    /// original bytes rather than failing the whole OCR pass over a preprocessing
    /// step that ImageSharp doesn't need to support every format Leptonica does.
    /// </summary>
    internal byte[] NormalizeOrientation(byte[] imageBytes)
    {
        try
        {
            using var image = Image.Load(imageBytes);
            image.Mutate(x => x.AutoOrient());
            using var output = new MemoryStream();
            image.SaveAsJpeg(output);
            return output.ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "EXIF orientation normalization failed; using original image bytes");
            return imageBytes;
        }
    }

    /// <summary>
    /// Resolves a potentially relative tessdata path against the application's
    /// base directory so the engine finds the files regardless of the working
    /// directory at startup (IDE, dotnet run from repo root, published output, …).
    /// Absolute paths (e.g. Docker system path) are returned unchanged.
    /// </summary>
    private static string ResolveTessDataPath(string path) =>
        Path.IsPathRooted(path) ? path : Path.Combine(AppContext.BaseDirectory, path);

    private string RunOcr(TesseractEngine engine, byte[] imageBytes)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
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
                using var page = engine.Process(working);
                var raw = page.GetText() ?? string.Empty;
                var normalized = OcrTextNormalizer.Normalize(raw);
                sw.Stop();
                // Info-level so we can spot slow OCR runs in prod logs without needing Debug.
                // Image dims + final dims (after downsample) + elapsed are the diagnostic levers.
                _logger.LogInformation(
                    "OCR done: {Chars} chars in {Ms} ms (input {InW}×{InH}px, processed {OutW}×{OutH}px{Note})",
                    normalized.Length, sw.ElapsedMilliseconds,
                    loaded.Width, loaded.Height, working.Width, working.Height, note);
                _logger.LogDebug("OCR normalized text:\n{Text}", normalized);
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
            _logger.LogError(ex, "Tesseract could not be initialised (tessdata path: {Path})", tessDataPath);
            throw new InvalidOperationException(
                $"OCR-Engine nicht verfügbar. Tesseract ist nicht installiert oder die Sprachdaten " +
                $"wurden nicht unter '{_options.TessDataPath}' gefunden. " +
                $"Tipp: Digitale Rechnungen (z.B. Amazon) bitte als PDF hochladen, nicht als Screenshot.", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Tesseract OCR failed");
            throw;
        }
    }

    private TesseractEngine CreateEngine()
    {
        // Tests inject a fake factory so they don't need the native Tesseract install.
        if (EngineFactoryOverride is not null) return EngineFactoryOverride();

        var tessDataPath = ResolveTessDataPath(_options.TessDataPath);
        _logger.LogInformation(
            "Initialising Tesseract engine (lang={Lang}, tessdata={Path})",
            _options.Language, tessDataPath);
        // LstmOnly: skip the legacy engine — modern LSTM-only is both faster and
        // more accurate for receipt text. Default would run both engines and pick
        // the better result, which roughly doubles inference time.
        var engine = new TesseractEngine(tessDataPath, _options.Language, EngineMode.LstmOnly);
        // SingleColumn (PSM 4): run layout analysis to locate text blocks, but never
        // attempt to split the page into multiple newspaper-style columns.
        //
        // INVESTIGATION NOTE (this fix was re-examined and the PSM itself was NOT
        // changed further — see debug session ocr-price-column-not-detected.md):
        // SparseText (PSM 11) was tried next because it was the only PSM that
        // recovered every price token in raw OCR text across 5 real receipt photos.
        // It was REJECTED: SparseText finds text as scattered independent fragments
        // with no block/line coherence, so unrelated tokens from opposite corners of
        // the image get concatenated onto the same output line in effectively random
        // order. On one test photo this produced a fabricated "shop:1302 Datum:
        // 06.07.2026 €" line that GenericParser mis-parsed as a EUR 2026,00 line
        // item — i.e. SparseText trades "missing prices" for "phantom prices",
        // which is worse for a tax report (a fictional four-figure line item is more
        // dangerous than a missing one). SingleColumn's occasional column-drop
        // failure (see Evidence) is a real, unresolved gap, but the fix for it is
        // NOT a blanket PSM swap — every PSM that does full-page layout analysis
        // (3/4/6/12) is inconsistent depending on photo framing/skew, and the one
        // PSM that's consistent (11) is unsafe for this parser's line-based
        // description+price pairing. Was previously SingleBlock (treat the whole
        // image as one text block), which fed surrounding background (e.g. a table)
        // straight into OCR as if it were text on phone photos. OCR runs in an async
        // Hangfire job, so the added layout-analysis latency isn't user-facing.
        engine.DefaultPageSegMode = PageSegMode.SingleColumn;
        return engine;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _channel.Writer.Complete();
        while (_channel.Reader.TryRead(out var engine))
        {
            // Null guard: test seam factories may legitimately return null! engines for
            // Layer A unit tests; we still want LiveEngineCount to drain cleanly.
            engine?.Dispose();
            Interlocked.Decrement(ref _engineCount);
        }
    }
}
