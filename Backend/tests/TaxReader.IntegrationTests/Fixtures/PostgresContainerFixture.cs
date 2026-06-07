using Microsoft.EntityFrameworkCore;
using Npgsql;
using Respawn;
using Testcontainers.PostgreSql;
using TaxReader.Infrastructure.Data;

namespace TaxReader.IntegrationTests.Fixtures;

/// <summary>
/// Shared postgres:17-alpine container for the integration test collection.
/// Migrations run once at startup; Respawn resets data between tests.
/// </summary>
public sealed class PostgresContainerFixture : IAsyncLifetime
{
    public PostgreSqlContainer Container { get; } = new PostgreSqlBuilder("postgres:17-alpine")
        .Build();

    private Respawner _respawner = default!;

    public string ConnectionString => Container.GetConnectionString();

    public async Task InitializeAsync()
    {
        await Container.StartAsync();

        // Apply EF migrations once so all schema + constraints exist before Respawn snapshots.
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(ConnectionString)
            .UseSnakeCaseNamingConvention()
            .Options;
        await using var dbContext = new AppDbContext(options);
        await dbContext.Database.MigrateAsync();

        await using var conn = new NpgsqlConnection(ConnectionString);
        await conn.OpenAsync();
        _respawner = await Respawner.CreateAsync(conn, new RespawnerOptions
        {
            DbAdapter = DbAdapter.Postgres,
            SchemasToInclude = ["public"],
            // Preserve EF migration history so the schema stays intact across resets.
            TablesToIgnore = ["__EFMigrationsHistory"],
        });
    }

    public async Task ResetAsync()
    {
        await using var conn = new NpgsqlConnection(ConnectionString);
        await conn.OpenAsync();
        await _respawner.ResetAsync(conn);
    }

    public Task DisposeAsync() => Container.DisposeAsync().AsTask();
}
