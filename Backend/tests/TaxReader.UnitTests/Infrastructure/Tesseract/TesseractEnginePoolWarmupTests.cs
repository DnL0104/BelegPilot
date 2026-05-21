using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TaxReader.Infrastructure.Configuration;
using TaxReader.Infrastructure.Services;

namespace TaxReader.UnitTests.Infrastructure.Tesseract;

/// <summary>
/// Layer A tests for the warmup hosted service. Uses the pool's
/// <c>EngineFactoryOverride</c> seam so the warmup runs without native Tesseract libs.
/// </summary>
public class TesseractEnginePoolWarmupTests
{
    private static IOptions<TesseractOptions> Options(int poolSize) =>
        Microsoft.Extensions.Options.Options.Create(new TesseractOptions
        {
            PoolSize = poolSize,
            TessDataPath = "tessdata",
            Language = "deu+eng"
        });

    [Fact]
    public async Task StartAsync_InvokesPoolInitialize_WhenInjectedPoolIsConcrete()
    {
        // Arrange — real TesseractEnginePool with overridden factory (null! engines).
        var pool = new TesseractEnginePool(Options(poolSize: 3), NullLogger<TesseractEnginePool>.Instance);
        var factoryCalls = 0;
        pool.EngineFactoryOverride = () =>
        {
            Interlocked.Increment(ref factoryCalls);
            return null!;
        };
        var sut = new TesseractEnginePoolWarmupService(pool, NullLogger<TesseractEnginePoolWarmupService>.Instance);

        // Act
        await sut.StartAsync(CancellationToken.None);

        // Assert — pool got initialised by the warmup service.
        factoryCalls.Should().Be(3);
        pool.LiveEngineCount.Should().Be(3);
    }

    [Fact]
    public async Task StartAsync_ReturnsCompletedTask_DoesNotBlock()
    {
        // Arrange
        var pool = new TesseractEnginePool(Options(poolSize: 1), NullLogger<TesseractEnginePool>.Instance);
        pool.EngineFactoryOverride = () => null!;
        var sut = new TesseractEnginePoolWarmupService(pool, NullLogger<TesseractEnginePoolWarmupService>.Instance);

        // Act
        var task = sut.StartAsync(CancellationToken.None);

        // Assert — the implementation returns Task.CompletedTask after sync warmup.
        task.IsCompletedSuccessfully.Should().BeTrue();
    }

    [Fact]
    public async Task StopAsync_ReturnsCompletedTask()
    {
        // Arrange
        var pool = new TesseractEnginePool(Options(poolSize: 1), NullLogger<TesseractEnginePool>.Instance);
        pool.EngineFactoryOverride = () => null!;
        var sut = new TesseractEnginePoolWarmupService(pool, NullLogger<TesseractEnginePoolWarmupService>.Instance);

        // Act
        var task = sut.StopAsync(CancellationToken.None);
        await task;

        // Assert
        task.IsCompletedSuccessfully.Should().BeTrue();
    }

    [Fact]
    public async Task StartAsync_SwallowsFactoryFailure_DoesNotCrashHost()
    {
        // Arrange — factory that throws. Per the plan, warmup failure must not take the
        // host down; the pool will degrade to first-call init via the engine-spawn path.
        var pool = new TesseractEnginePool(Options(poolSize: 1), NullLogger<TesseractEnginePool>.Instance);
        pool.EngineFactoryOverride = () => throw new InvalidOperationException("native tesseract missing");
        var sut = new TesseractEnginePoolWarmupService(pool, NullLogger<TesseractEnginePoolWarmupService>.Instance);

        // Act
        Func<Task> act = async () => await sut.StartAsync(CancellationToken.None);

        // Assert — exception is logged and swallowed.
        await act.Should().NotThrowAsync();
    }
}
