namespace TaxReader.Domain.Entities;

public class AuditLogEntry
{
    public Guid Id { get; set; }
    public string Action { get; set; } = string.Empty;
    public Guid? ActorUserId { get; set; }
    public Guid? SubjectUserId { get; set; }
    public Dictionary<string, object?> Metadata { get; set; } = [];
    public DateTime CreatedAt { get; set; }
}
