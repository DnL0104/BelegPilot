namespace TaxReader.IntegrationTests.Fixtures;

/// <summary>
/// Serializes all QA-01 integration tests and shares one Postgres container via ICollectionFixture.
/// DisableParallelization = true is mandatory: Program.cs top-level-statement <c>await app.RunAsync()</c>
/// breaks when multiple WebApplicationFactory instances start in parallel (RESEARCH Pitfall 2).
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class IntegrationTestCollection : ICollectionFixture<PostgresContainerFixture>
{
    public const string Name = "Postgres integration (shared container, sequential)";
}
