namespace TaxReader.Infrastructure.Configuration;

public class AnthropicOptions
{
    public const string SectionName = "Anthropic";

    public string? ApiKey { get; set; }
    // Haiku is plenty for the 13-category DE tax classification choice — ~3-5× faster
    // and ~10× cheaper than Sonnet for this task. Override in appsettings for higher accuracy.
    public string Model { get; set; } = "claude-haiku-4-5";
    /// <summary>Tokens (credits) consumed per AI classification call.</summary>
    public int CostPerClassification { get; set; } = 1;

    /// <summary>
    /// Tokens (credits) consumed per vision fallback extraction call. Default reflects the
    /// spike's real measured cost of ~0.26-0.3 Cent/receipt (claude-haiku-4-5) — tunable per
    /// environment.
    /// </summary>
    public int CostPerVisionExtraction { get; set; } = 1;

    /// <summary>
    /// Delay between the first AI attempt and the single automatic retry (CLS-01).
    /// Configurable so unit tests can set <see cref="System.TimeSpan.Zero"/> instead of
    /// paying the real wait on every retry-path test.
    /// </summary>
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(2);
}
