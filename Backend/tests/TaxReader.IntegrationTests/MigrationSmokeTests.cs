using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using TaxReader.Domain.Entities;
using TaxReader.Domain.Enums;
using TaxReader.Infrastructure.Data;
using TaxReader.IntegrationTests.Fixtures;
using TaxReader.UnitTests.Helpers;

namespace TaxReader.IntegrationTests;

/// <summary>
/// QA-01: fulfills the deferred MigrationTests.cs placeholder from UnitTests/Auth.
/// Verifies that EF migrations apply cleanly to a fresh Postgres 17 container (the
/// fixture already ran MigrateAsync once) and that seeded data round-trips correctly.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public sealed class MigrationSmokeTests(PostgresContainerFixture fixture) : IAsyncLifetime
{
    public Task InitializeAsync() => fixture.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    private AppDbContext CreateContext() => new(
        new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(fixture.ConnectionString)
            .UseSnakeCaseNamingConvention()
            .Options);

    [Fact]
    public async Task Migrate_FreshContainer_SchemaQueryable()
    {
        // Arrange — the fixture already applied migrations once (MigrateAsync in InitializeAsync).
        // Respawn clears data but leaves the schema intact. Seed a user + file to prove
        // the schema is fully queryable post-migration.
        var userId = Guid.NewGuid();
        var fileId = Guid.NewGuid();

        await using (var ctx = CreateContext())
        {
            ctx.Users.Add(new User
            {
                Id = userId,
                Email = $"migration-smoke-{userId:N}@test.local",
                DisplayName = "Migration Smoke",
                PasswordHash = "x"
            });

            var file = TestDataFactory.CreateReceiptFile(id: fileId, contentHash: "migration-smoke-hash");
            file.UserId = userId;
            ctx.ReceiptFiles.Add(file);

            await ctx.SaveChangesAsync();
        }

        // Assert — round-trip the seeded file via a fresh context.
        await using var verifyCtx = CreateContext();
        var retrieved = await verifyCtx.ReceiptFiles.FindAsync(fileId);

        retrieved.Should().NotBeNull("seeded ReceiptFile must be queryable after migrations are applied");
        retrieved!.ContentHash.Should().Be("migration-smoke-hash");
        retrieved.UserId.Should().Be(userId);

        // Also confirm migrations were applied by checking pending migrations = none.
        var pending = await verifyCtx.Database.GetPendingMigrationsAsync();
        pending.Should().BeEmpty("all migrations must be applied to the fresh container");
    }
}
