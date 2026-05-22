using TaxReader.Application.Exceptions;

namespace TaxReader.Application.Common;

/// <summary>
/// D-21: static catalog mapping known exception types to (ErrorCode, GermanMessage) pairs
/// surfaced via the D-13 status endpoint. Raw exception messages NEVER appear in HTTP body
/// or processing_runs.error_message — only these catalog entries do.
///
/// Every German string uses Sie-form per CONVENTIONS.md.
/// </summary>
public readonly record struct UploadError(string Code, string GermanMessage);

public static class UploadErrorCatalog
{
    public const string CodeNoTextExtracted = "NoTextExtracted";
    public const string CodeParserMissing = "ParserMissing";
    public const string CodeAiUnavailable = "AiUnavailable";
    public const string CodeInsufficientTokens = "InsufficientTokens";
    public const string CodeCancelled = "Cancelled";
    public const string CodeUnknown = "Unknown";

    private static readonly UploadError Unknown = new(
        CodeUnknown,
        "Verarbeitung fehlgeschlagen — bitte erneut versuchen oder Support kontaktieren.");

    /// <summary>
    /// Returns the stable (ErrorCode, GermanMessage) pair for the given exception.
    /// Pass <paramref name="cancellationRequested"/> = true when the job's CancellationToken
    /// was signalled — distinguishes user-initiated cancel from AI timeout.
    /// </summary>
    public static UploadError Classify(Exception exception, bool cancellationRequested = false)
    {
        // Cancellation takes precedence — caller passes the job's CancellationToken.IsCancellationRequested.
        // TaskCanceledException extends OperationCanceledException; check token flag first.
        if (cancellationRequested && exception is OperationCanceledException)
            return new(CodeCancelled, "Vorgang abgebrochen.");

        return exception switch
        {
            NoTextExtractedException =>
                new(CodeNoTextExtracted,
                    "Aus diesem Dokument konnte kein Text extrahiert werden. " +
                    "Bitte laden Sie eine PDF-Datei mit Textinhalt hoch oder versuchen Sie ein klares Foto."),

            ParserNotFoundException =>
                new(CodeParserMissing,
                    "Das Belegformat wird derzeit nicht erkannt. " +
                    "Bitte versuchen Sie es mit einer Amazon- oder Eduki-Rechnung oder kontaktieren Sie den Support."),

            InsufficientTokensException =>
                new(CodeInsufficientTokens,
                    "Ihr Token-Guthaben reicht für diesen Beleg nicht aus. " +
                    "Bitte laden Sie Credits auf, um die Verarbeitung fortzusetzen."),

            // OperationCanceledException without cancellation token signal = general cancel (e.g. job cancellation)
            OperationCanceledException =>
                new(CodeCancelled, "Vorgang abgebrochen."),

            HttpRequestException =>
                new(CodeAiUnavailable,
                    "Die Klassifizierung ist vorübergehend nicht verfügbar. " +
                    "Wir versuchen es automatisch erneut — bitte laden Sie die Seite in einer Minute neu."),

            _ => Unknown
        };
    }
}
