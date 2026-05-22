namespace TaxReader.Application.DTOs;

/// <summary>D-03: 202 Accepted response shape. One entry per uploaded file; UI computes
/// batch-level progress client-side from per-file polling.</summary>
public record UploadAcceptedResponse(IReadOnlyList<UploadAcceptedFile> Files);

public record UploadAcceptedFile(Guid ReceiptFileId, string JobId, string FileName);
