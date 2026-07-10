using Microsoft.Extensions.Options;
using TaxReader.Application.Interfaces;

namespace TaxReader.Infrastructure.Configuration;

/// <summary>
/// Infrastructure-side implementation of <see cref="IVisionFallbackSettings"/> — reads the
/// vision-fallback slice of <see cref="AnthropicOptions"/> so Application (which must not
/// reference Infrastructure per CLAUDE.md) can consume it through the port.
/// </summary>
public class AnthropicVisionFallbackSettings(IOptions<AnthropicOptions> options) : IVisionFallbackSettings
{
    public int CostPerVisionExtraction => options.Value.CostPerVisionExtraction;
    public TimeSpan RetryDelay => options.Value.RetryDelay;
}
