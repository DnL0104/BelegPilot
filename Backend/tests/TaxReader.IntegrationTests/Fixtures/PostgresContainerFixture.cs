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
    private NpgsqlConnection _respawnConnection = default!;

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

        // Open a single connection for Respawn and reuse it across every ResetAsync call,
        // avoiding a fresh cold connection per integration test class.
        _respawnConnection = new NpgsqlConnection(ConnectionString);
        await _respawnConnection.OpenAsync();
        _respawner = await Respawner.CreateAsync(_respawnConnection, new RespawnerOptions
        {
            DbAdapter = DbAdapter.Postgres,
            SchemasToInclude = ["public"],
            // Preserve EF migration history so the schema stays intact across resets.
            TablesToIgnore = ["__EFMigrationsHistory"],
        });
    }

    public async Task ResetAsync() => await _respawner.ResetAsync(_respawnConnection);

    public async Task DisposeAsync()
    {
        await _respawnConnection.DisposeAsync();
        await Container.DisposeAsync();
    }
}
