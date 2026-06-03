using TaxReader.Application.Interfaces;
using TaxReader.Domain.Entities;
using TaxReader.Domain.Enums;

namespace TaxReader.Infrastructure.Services;

public class AuditLogger(IAppDbContext dbContext) : IAuditLogger
{
    public async Task RecordAsync(
        AuditAction action,
        Guid? actorUserId,
        Guid? subjectUserId,
        Dictionary<string, object?> metadata,
        CancellationToken cancellationToken = default)
    {
        dbContext.AuditLogEntries.Add(new AuditLogEntry
        {
            Id = Guid.NewGuid(),
            Action = action.ToString(),
            ActorUserId = actorUserId,
            SubjectUserId = subjectUserId,
            Metadata = metadata,
            CreatedAt = DateTime.UtcNow
        });
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
