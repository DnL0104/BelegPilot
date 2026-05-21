using System.Diagnostics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TaxReader.Application.Interfaces;

namespace TaxReader.Infrastructure.Services;

/// <summary>
/// D-18: eagerly creates all <see cref="Configuration.TesseractOptions.PoolSize"/>
/// <c>TesseractEngine</c> instances at host start so the first OCR call doesn't pay the
/// ~100ms-per-engine init cost. Adds ~<c>PoolSize × 100ms</c> (~300ms at default 3) to
/// container boot — acceptable for a Docker Compose deploy that restarts rarely.
///
/// Warmup failure is logged at Error and swallowed: the host stays up and the pool will
/// degrade to first-call init via the engine-spawn path inside
/// <see cref="TesseractEnginePool.ExtractTextAsync"/>.
/// </summary>
public class TesseractEnginePoolWarmupService(
    IImageTextExtractor pool,
    ILogger<TesseractEnginePoolWarmupService> logger) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (pool is TesseractEnginePool concretePool)
        {
            var sw = Stopwatch.StartNew();
            try
            {
                concretePool.Initialize();
                logger.LogInformation("Tesseract pool warmup complete in {Ms}ms", sw.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                // Do NOT take the host down on warmup failure — the pool will fall back to
                // first-call init. Container logs surface the failure; Sentry captures it
                // via the global handler.
                logger.LogError(ex, "Tesseract pool warmup failed after {Ms}ms — pool will initialise lazily on first OCR call", sw.ElapsedMilliseconds);
            }
        }
        else
        {
            logger.LogDebug("TesseractEnginePoolWarmupService skipped — bound IImageTextExtractor is not a TesseractEnginePool (test/mock environment)");
        }
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
