namespace TaxReader.Application.Common;

/// <summary>
/// STUB — Plan 03-04 ships the full German catalog mapping known exception types to
/// (errorCode, germanMessage) pairs surfaced via D-13's status response. Until that
/// ships, every classification falls through to the fallback "Unknown" pair.
///
/// D-21 invariant: raw exception messages NEVER appear in HTTP body or
/// processing_runs.error_message. They go to Serilog only via
/// logger.LogError(ex, "{ErrorCode} during {Step} for ReceiptFile {Id}", ...).
/// </summary>
public static class UploadErrorCatalog
{
    public static (string ErrorCode, string GermanMessage) Classify(Exception ex)
    {
        // Plan 03-04 replaces this stub with full mappings for NoTextExtracted /
        // ParserMissing / AiUnavailable / InsufficientTokens / Cancelled / Unknown.
        // Until then every exception falls through to the user-safe German fallback.
        return ("Unknown", "Verarbeitung fehlgeschlagen — bitte erneut versuchen oder Support kontaktieren.");
    }
}
