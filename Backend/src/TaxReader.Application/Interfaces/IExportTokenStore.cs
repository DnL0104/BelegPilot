namespace TaxReader.Application.Interfaces;

/// <summary>
/// Status of an export bundle tracked in the IExportTokenStore.
/// </summary>
public enum ExportStatus
{
    Generating,
    Ready,
    Expired
}

/// <summary>
/// Immutable record held per export token in the IExportTokenStore.
/// </summary>
public sealed record ExportRecord(Guid UserId, DateTime ExpiresAtUtc, ExportStatus Status);

/// <summary>
/// Singleton in-memory registry mapping one-time export tokens to user IDs and status.
/// LEG-07 / D-10: tokens are ephemeral; lost on container restart (accepted tradeoff — see RESEARCH Pitfall 4).
/// </summary>
public interface IExportTokenStore
{
    /// <summary>Marks the token as Generating (called by the request endpoint before enqueuing).</summary>
    void MarkGenerating(string token, Guid userId, DateTime expiresAtUtc);

    /// <summary>Marks the token as Ready (called by ExportUserDataJob after writing the zip).</summary>
    void Register(string token, Guid userId, DateTime expiresAtUtc);

    /// <summary>
    /// Returns true if the token is known. If past ExpiresAtUtc the record's Status is Expired.
    /// Returns false if the token was never registered or was invalidated.
    /// </summary>
    bool TryGet(string token, out ExportRecord? record);

    /// <summary>One-time invalidation — called after a successful download (T-06-42).</summary>
    void Invalidate(string token);

    /// <summary>Flips a token to a terminal Expired state (job failure recovery — WR-02). Idempotent; no-op if unknown.</summary>
    void MarkExpired(string token);
}
