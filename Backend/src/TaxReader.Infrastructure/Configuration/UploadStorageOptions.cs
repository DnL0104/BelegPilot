namespace TaxReader.Infrastructure.Configuration;

public class UploadStorageOptions
{
    public const string SectionName = "UploadStorage";

    /// <summary>
    /// Absolute filesystem path where upload bytes are buffered until terminal state.
    /// Default: Path.Combine(Path.GetTempPath(), "taxreader-uploads"). NEVER point this
    /// at the repo root — Phase 1 CONCERNS.md #4 cleaned up the legacy storage/ dir.
    /// </summary>
    public string Path { get; set; } = string.Empty;
}
