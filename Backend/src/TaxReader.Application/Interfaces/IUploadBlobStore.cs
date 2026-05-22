namespace TaxReader.Application.Interfaces;

/// <summary>
/// Application port for persisting upload bytes across the HTTP→Hangfire boundary.
/// Backed by a filesystem implementation in Phase 3 (FileSystemUploadBlobStore); a
/// cloud-storage adapter is a Phase 6+ candidate. See 03-02-PLAN Task T3 for the
/// D-15 addendum rationale (BYTEA-in-DB rejected for backup/retention cost).
/// </summary>
public interface IUploadBlobStore
{
    Task SaveAsync(Guid receiptFileId, Stream content, CancellationToken cancellationToken = default);
    Task<Stream?> OpenReadAsync(Guid receiptFileId, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid receiptFileId, CancellationToken cancellationToken = default);
}
