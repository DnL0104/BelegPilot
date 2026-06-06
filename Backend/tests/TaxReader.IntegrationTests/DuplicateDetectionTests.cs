using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using TaxReader.Domain.Entities;
using TaxReader.Domain.Enums;
using TaxReader.Infrastructure.Data;
using TaxReader.IntegrationTests.Fixtures;

namespace TaxReader.IntegrationTests;

/// <summary>
/// QA-01: asserts that the composite UNIQUE index on receipt_files(user_id, content_hash)
/// rejects a duplicate upload at the DB level. The in-memory provider ignores this constraint.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public sealed class DuplicateDetectionTests(PostgresContainerFixture fixture) : IAsyncLifetime
{
    public Task InitializeAsync() => fixture.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    private AppDbContext CreateContext() => new(
        new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(fixture.ConnectionString)
            .UseSnakeCaseNamingConvention()
            .Options);

    [Fact]
    public async Task SecondInsert_SameUserIdAndContentHash_ViolatesUniqueConstraint()
    {
        // Arrange — a user and the first receipt file.
        var userId = Guid.NewGuid();
        const string contentHash = "sha256-abc123-duplicate-detection-test";

        await using (var ctx = CreateContext())
        {
            ctx.Users.Add(new User
            {
                Id = userId,
                Email = $"dup-{userId:N}@test.local",
                DisplayName = "Dup Tester",
                PasswordHash = "x"
            });
            ctx.ReceiptFiles.Add(new ReceiptFile
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                ContentHash = contentHash,
                OriginalFileName = "first.pdf",
                FileSize = 1024,
                UploadedAt = DateTime.UtcNow,
                Status = FileStatus.Uploaded
            });
            await ctx.SaveChangesAsync();
        }

        // Act — attempt to insert a second file with the same (UserId, ContentHash).
        var act = async () =>
        {
            await using var ctx = CreateContext();
            ctx.ReceiptFiles.Add(new ReceiptFile
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                ContentHash = contentHash,
                OriginalFileName = "duplicate.pdf",
                FileSize = 2048,
                UploadedAt = DateTime.UtcNow,
                Status = FileStatus.Uploaded
            });
            await ctx.SaveChangesAsync();
        };

        // Assert — UNIQUE(user_id, content_hash) rejects the duplicate at the Postgres layer.
        await act.Should().ThrowAsync<DbUpdateException>(
            "receipt_files has a composite UNIQUE index on (user_id, content_hash)");
    }
}
