using System.Threading.Channels;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.PixelFormats;
using TaxReader.Infrastructure.Configuration;
using TaxReader.Infrastructure.Services;
using Tesseract;

namespace TaxReader.UnitTests.Infrastructure.Tesseract;

/// <summary>
/// Layer A tests (per Plan 03-03 T1): exercise the Channel-backed pool's lifecycle / count
/// invariants without invoking the native Tesseract library. The pool exposes an
/// internal <c>EngineFactoryOverride</c> seam so tests inject engine instances built via
/// the public <see cref="TesseractEngine"/> ctor — which requires a real tessdata path —
/// OR a thrown exception to drive the failure branches. The throw-on-create path covers
/// the bulk of the algorithmic invariants without needing native libs.
///
/// Layer B (real OCR roundtrip) is deferred to manual UAT (03-HUMAN-UAT.md): the docker
/// integration test covers it once the container has the Tesseract native libs installed.
/// </summary>
public class TesseractEnginePoolTests
{
    private static IOptions<TesseractOptions> Options(int poolSize) =>
        Microsoft.Extensions.Options.Options.Create(new TesseractOptions
        {
            PoolSize = poolSize,
            // Path / language don't matter — factory is overridden in every test.
            TessDataPath = "tessdata",
            Language = "deu+eng"
        });

    [Fact]
    public void Initialize_CreatesPoolSizeEngines()
    {
        // Arrange — pool with override returning a counter, mocking 3 engines created.
        var pool = new TesseractEnginePool(Options(poolSize: 3), NullLogger<TesseractEnginePool>.Instance);
        var factoryCalls = 0;
        pool.EngineFactoryOverride = () =>
        {
            Interlocked.Increment(ref factoryCalls);
            // Return a dummy engine — TesseractEngine ctor needs real tessdata.
            // We use null! because tests only inspect pool's internal counters; the engine
            // reference is never dereferenced unless ExtractTextAsync is called.
            return null!;
        };

        // Act
        pool.Initialize();

        // Assert
        factoryCalls.Should().Be(3);
        pool.LiveEngineCount.Should().Be(3);
    }

    [Fact]
    public void Initialize_Twice_DoesNotDoubleFill()
    {
        // Arrange
        var pool = new TesseractEnginePool(Options(poolSize: 3), NullLogger<TesseractEnginePool>.Instance);
        var factoryCalls = 0;
        pool.EngineFactoryOverride = () =>
        {
            Interlocked.Increment(ref factoryCalls);
            return null!;
        };

        // Act — calling Initialize twice. Bounded channel rejects extra writes; the engines
        // are still created but immediately disposed inside Initialize (per Pitfall 6).
        pool.Initialize();
        pool.Initialize();

        // Assert — engine count stays at PoolSize (the channel is bounded).
        pool.LiveEngineCount.Should().Be(3);
    }

    [Fact]
    public async Task ExtractTextAsync_RespectsCancellation()
    {
        // Arrange — empty pool (no Initialize call → channel has 0 engines, ReadAsync blocks).
        var pool = new TesseractEnginePool(Options(poolSize: 3), NullLogger<TesseractEnginePool>.Instance);
        using var cts = new CancellationTokenSource();

        // Act — start the call, then cancel.
        var task = pool.ExtractTextAsync(new MemoryStream([1, 2, 3]), "image/png", cts.Token);
        await cts.CancelAsync();

        // Assert
        await FluentActions.Awaiting(() => task).Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task Dispose_DrainsChannelAndMarksPoolDisposed()
    {
        // Arrange — pool with null! test-seam engines. Production code null-guards
        // engine.Dispose() (only relevant to the test seam) so the drain loop runs to
        // completion and LiveEngineCount goes to zero.
        var pool = new TesseractEnginePool(Options(poolSize: 2), NullLogger<TesseractEnginePool>.Instance);
        pool.EngineFactoryOverride = () => null!;
        pool.Initialize();
        pool.LiveEngineCount.Should().Be(2);

        // Act
        pool.Dispose();

        // Assert — engine count drained AND subsequent ExtractTextAsync rejects.
        pool.LiveEngineCount.Should().Be(0);

        Func<Task> act = async () => await pool.ExtractTextAsync(new MemoryStream([1]), "image/png", CancellationToken.None);
        await act.Should().ThrowAsync<ObjectDisposedException>();
    }

    [Fact]
    public async Task FiveConcurrentAcquires_QueuesTwoWhenPoolSizeIsThree()
    {
        // This test exercises the channel queueing semantics directly — not via the
        // pool's full OCR pipeline (which needs a real native engine).
        // Bounded channel of capacity 3, with 5 concurrent readers; only 3 succeed
        // before the channel is drained, 2 wait until items are written back.

        var channel = Channel.CreateBounded<int>(new BoundedChannelOptions(3)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = false,
            SingleWriter = false
        });

        // Seed 3 items (mimicking Initialize creating 3 engines).
        channel.Writer.TryWrite(1).Should().BeTrue();
        channel.Writer.TryWrite(2).Should().BeTrue();
        channel.Writer.TryWrite(3).Should().BeTrue();

        // Spawn 5 readers — 2 will block until something is written back.
        var inFlight = 0;
        var maxConcurrent = 0;
        var locker = new object();
        var readers = Enumerable.Range(0, 5).Select(_ => Task.Run(async () =>
        {
            var item = await channel.Reader.ReadAsync();
            lock (locker)
            {
                inFlight++;
                if (inFlight > maxConcurrent) maxConcurrent = inFlight;
            }
            // Simulate brief OCR work.
            await Task.Delay(50);
            lock (locker) { inFlight--; }
            // Release back into the pool.
            channel.Writer.TryWrite(item).Should().BeTrue();
        })).ToArray();

        // Wait for all readers to finish — the 2 that queued must succeed once the
        // first 3 release. Give Task.WhenAll a generous timeout so a hang is visible.
        var allDone = Task.WhenAll(readers);
        var winner = await Task.WhenAny(allDone, Task.Delay(TimeSpan.FromSeconds(5)));
        winner.Should().BeSameAs(allDone, "all 5 acquires must complete; channel must not deadlock");

        // The pool's bounded capacity ensures we never see more than 3 concurrent in-flight.
        maxConcurrent.Should().BeLessThanOrEqualTo(3);
    }

    [Fact]
    public async Task ExtractTextAsync_AfterDispose_Throws()
    {
        // Arrange
        var pool = new TesseractEnginePool(Options(poolSize: 1), NullLogger<TesseractEnginePool>.Instance);
        pool.EngineFactoryOverride = () => null!;
        pool.Dispose();

        // Act — wrap in async lambda so the assertion sees a Func<Task>, not Func<Task<string>>.
        Func<Task> act = async () => await pool.ExtractTextAsync(new MemoryStream([1]), "image/png", CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ObjectDisposedException>();
    }

    // Regression: a phone photo of a receipt (e.g. rotated to fit a long thermal
    // strip in frame) carries an EXIF Orientation tag instead of pre-rotated pixels.
    // Leptonica's Pix.LoadFromMemory ignores EXIF entirely, so without this
    // normalization step OCR sees the raw sensor orientation and produces garbage.
    // Pure ImageSharp logic — no native Tesseract install needed.
    [Fact]
    public void NormalizeOrientation_RotatesPixelsToMatchExifOrientation()
    {
        // Arrange — a 2x4 image (taller than wide) tagged as rotated 90° CW (EXIF
        // value 6: "the top of the captured image is on its right side"), so the
        // content should actually display as 4x2 (wider than tall) once corrected.
        using var source = new Image<Rgba32>(2, 4);
        source.Metadata.ExifProfile = new ExifProfile();
        source.Metadata.ExifProfile.SetValue(ExifTag.Orientation, (ushort)6);
        using var sourceStream = new MemoryStream();
        source.SaveAsJpeg(sourceStream);

        var pool = new TesseractEnginePool(Options(poolSize: 1), NullLogger<TesseractEnginePool>.Instance);

        // Act
        var normalizedBytes = pool.NormalizeOrientation(sourceStream.ToArray());

        // Assert — dimensions swapped (2x4 -> 4x2) and the orientation tag no longer
        // demands further rotation (AutoOrient resets it to 1/"normal" on correction).
        using var normalized = Image.Load(normalizedBytes);
        normalized.Width.Should().Be(4);
        normalized.Height.Should().Be(2);
    }

    [Fact]
    public void NormalizeOrientation_InvalidImageBytes_FallsBackToOriginalBytes()
    {
        // Arrange — not a decodable image at all.
        var garbage = new byte[] { 1, 2, 3, 4, 5 };
        var pool = new TesseractEnginePool(Options(poolSize: 1), NullLogger<TesseractEnginePool>.Instance);

        // Act
        var result = pool.NormalizeOrientation(garbage);

        // Assert — best-effort preprocessing failure must not lose the original bytes.
        result.Should().BeEquivalentTo(garbage);
    }
}
