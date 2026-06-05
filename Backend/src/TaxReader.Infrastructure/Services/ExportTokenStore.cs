using System.Collections.Concurrent;
using TaxReader.Application.Interfaces;

namespace TaxReader.Infrastructure.Services;

/// <summary>
/// Singleton in-memory token registry for DSGVO Art. 20 export bundles.
/// Uses a ConcurrentDictionary for thread safety across requests and Hangfire workers.
/// LEG-07 / D-10: lost on container restart (accepted tradeoff per RESEARCH Pitfall 4).
/// T-06-40: UserId stored per token enables ownership validation in the download endpoint.
/// T-06-42: Invalidate() removes the token after download (one-time).
/// T-06-41: token value is Guid.NewGuid().ToString("N") — CSPRNG, 128-bit, not sequential.
/// </summary>
public sealed class ExportTokenStore : IExportTokenStore
{
    private readonly ConcurrentDictionary<string, ExportRecord> _tokens = new();

    public void MarkGenerating(string token, Guid userId, DateTime expiresAtUtc)
    {
        _tokens[token] = new ExportRecord(userId, expiresAtUtc, ExportStatus.Generating);
    }

    public void Register(string token, Guid userId, DateTime expiresAtUtc)
    {
        _tokens[token] = new ExportRecord(userId, expiresAtUtc, ExportStatus.Ready);
    }

    public bool TryGet(string token, out ExportRecord? record)
    {
        if (!_tokens.TryGetValue(token, out var stored))
        {
            record = null;
            return false;
        }

        // Flip any non-terminal token (Ready OR Generating) to Expired once past its TTL.
        // WR-04: a Generating token whose job died must flip too, so the UI stops spinning.
        if (stored.Status != ExportStatus.Expired && DateTime.UtcNow > stored.ExpiresAtUtc)
        {
            record = stored with { Status = ExportStatus.Expired };
            return true;
        }

        record = stored;
        return true;
    }

    public void Invalidate(string token)
    {
        _tokens.TryRemove(token, out _);
    }

    public void MarkExpired(string token)
    {
        if (_tokens.TryGetValue(token, out var stored))
            _tokens[token] = stored with { Status = ExportStatus.Expired };
    }
}
