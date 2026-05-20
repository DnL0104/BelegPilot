namespace TaxReader.Application.DTOs;

/// <summary>
/// Per-file result of a batch upload. Callers can succeed partially — the handler
/// never aborts the batch on a single file's failure.
/// </summary>
public record UploadReceiptFilesResponse(
    IReadOnlyList<SuccessfulUpload> Successful,
    IReadOnlyList<FailedUpload> Failed);

/// <summary>
/// Carries the original file name alongside the parsed receipt so clients can
/// reconcile results with the batch they sent. Symmetric to <see cref="FailedUpload"/>.
/// </summary>
public record SuccessfulUpload(
    string FileName,
    ReceiptDto Receipt);

public record FailedUpload(
    string FileName,
    string Reason,
    FailureKind Kind);

public enum FailureKind
{
    /// <summary>File's content hash matches an already-processed file for this user.</summary>
    Duplicate,
    /// <summary>Text extraction or parsing failed for this file.</summary>
    ProcessingError
}
