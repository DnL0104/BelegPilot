namespace TaxReader.Application.DTOs;

/// <summary>D-03: 202 Accepted response shape. One entry per uploaded file; UI computes
/// batch-level progress client-side from per-file polling. Duplicates (by content hash,
/// against either a prior upload or another file in this same batch) are skipped rather
/// than failing the whole request, and reported here so the UI can say which file and why.</summary>
public record UploadAcceptedResponse(
    IReadOnlyList<UploadAcceptedFile> Files,
    IReadOnlyList<DuplicateFileInfo> Duplicates);

public record UploadAcceptedFile(Guid ReceiptFileId, string JobId, string FileName);

/// <summary>A file skipped during upload because its content already exists for this user.</summary>
public record DuplicateFileInfo(string FileName, string Reason);
