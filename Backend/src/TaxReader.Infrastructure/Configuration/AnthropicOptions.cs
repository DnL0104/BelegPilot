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
}
