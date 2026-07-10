namespace TaxReader.Application.Interfaces;

/// <summary>
/// Thin Application-side port over Infrastructure's <c>AnthropicOptions</c> (vision-fallback
/// slice only). <c>ProcessReceiptFileJob</c> lives in TaxReader.Application, which per CLAUDE.md
/// must never reference TaxReader.Infrastructure — this interface lets the job read the two
/// vision-fallback settings it needs (cost, retry delay) without that reference. Infrastructure
/// implements it by reading <c>IOptions&lt;AnthropicOptions&gt;</c>.
/// </summary>
public interface IVisionFallbackSettings
{
    /// <summary>Tokens (credits) consumed per vision fallback extraction call.</summary>
    int CostPerVisionExtraction { get; }

    /// <summary>Delay between the first vision-extraction attempt and the single automatic retry.</summary>
    TimeSpan RetryDelay { get; }
}
