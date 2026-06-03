using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using TaxReader.Domain.Entities;

namespace TaxReader.Infrastructure.Data.Configurations;

public class AuditLogEntryConfiguration : IEntityTypeConfiguration<AuditLogEntry>
{
    public void Configure(EntityTypeBuilder<AuditLogEntry> builder)
    {
        builder.ToTable("audit_log");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasDefaultValueSql("gen_random_uuid()");

        builder.Property(e => e.Action).IsRequired().HasMaxLength(100);
        builder.Property(e => e.CreatedAt).IsRequired();

        // Npgsql maps Dictionary<string, object?> to PostgreSQL jsonb when HasColumnType("jsonb") is set.
        // A value converter (JSON serialization) satisfies the InMemory provider which cannot validate
        // jsonb types (RESEARCH Pitfall 1). Npgsql overrides the converter transparently in production.
        var metadataConverter = new ValueConverter<Dictionary<string, object?>, string>(
            v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
            v => JsonSerializer.Deserialize<Dictionary<string, object?>>(v, (JsonSerializerOptions?)null) ?? new());

        builder.Property(e => e.Metadata)
            .HasConversion(metadataConverter)
            .HasColumnType("jsonb")
            .IsRequired();

        // Index on subject for per-user audit log queries (DSGVO Art. 15 export).
        builder.HasIndex(e => e.SubjectUserId);
        builder.HasIndex(e => e.CreatedAt);

        // No HasOne/WithMany for actor_user_id or subject_user_id.
        // actor/subject are nullable uuid columns with no FK constraint —
        // audit rows survive user deletion (T-06-11, D-14).
    }
}
