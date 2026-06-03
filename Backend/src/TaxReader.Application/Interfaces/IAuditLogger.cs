using TaxReader.Domain.Enums;

namespace TaxReader.Application.Interfaces;

public interface IAuditLogger
{
    Task RecordAsync(
        AuditAction action,
        Guid? actorUserId,
        Guid? subjectUserId,
        Dictionary<string, object?> metadata,
        CancellationToken cancellationToken = default);
}
