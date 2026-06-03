using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using TaxReader.Application.Interfaces;
using TaxReader.Domain.Enums;
using TaxReader.Infrastructure.Data;
using TaxReader.Infrastructure.Services;

namespace TaxReader.UnitTests.Application;

/// <summary>
/// LEG-08: AuditLogger.RecordAsync writes an append-only row to audit_log.
/// Uses in-memory DB (jsonb is ignored by InMemory provider — metadata asserted
/// by key presence and string equality per RESEARCH.md Pitfall 1).
/// </summary>
public class AuditLoggerTests
{
    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task RecordAsync_AccountDeleted_WritesOneRowWithExpectedFields()
    {
        // Arrange
        var db = CreateDb();
        var logger = new AuditLogger(db);
        var actor = Guid.NewGuid();
        var subject = Guid.NewGuid();
        var metadata = new Dictionary<string, object?> { ["email_hash"] = "abc123" };

        var before = DateTime.UtcNow;

        // Act
        await logger.RecordAsync(
            AuditAction.AccountDeleted,
            actorUserId: actor,
            subjectUserId: subject,
            metadata: metadata);

        // Assert
        var entries = await db.AuditLogEntries.ToListAsync();
        entries.Should().HaveCount(1);

        var entry = entries[0];
        entry.Action.Should().Be("AccountDeleted");
        entry.ActorUserId.Should().Be(actor);
        entry.SubjectUserId.Should().Be(subject);
        entry.Metadata.Should().ContainKey("email_hash");
        entry.Metadata["email_hash"]?.ToString().Should().Be("abc123");
        entry.CreatedAt.Should().BeOnOrAfter(before).And.BeOnOrBefore(DateTime.UtcNow.AddSeconds(5));
    }

    [Fact]
    public async Task RecordAsync_NullActorUserId_PersistsRowWithNullActor()
    {
        // Arrange — anonymous actor (system-generated event, no HTTP context user)
        var db = CreateDb();
        var logger = new AuditLogger(db);
        var subject = Guid.NewGuid();

        // Act
        await logger.RecordAsync(
            AuditAction.TokensGranted,
            actorUserId: null,
            subjectUserId: subject,
            metadata: new Dictionary<string, object?> { ["credits"] = "50" });

        // Assert
        var entry = await db.AuditLogEntries.SingleAsync();
        entry.ActorUserId.Should().BeNull("anonymous actor must persist as null");
        entry.SubjectUserId.Should().Be(subject);
        entry.Action.Should().Be("TokensGranted");
    }
}
