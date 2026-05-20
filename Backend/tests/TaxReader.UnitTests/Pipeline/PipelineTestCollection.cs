namespace TaxReader.UnitTests.Pipeline;

/// <summary>
/// Serializes the WebApplicationFactory&lt;Program&gt; tests in this directory.
/// The .NET 10 top-level-statement Program runs <c>await app.RunAsync()</c> once;
/// xUnit's default parallel test execution across classes triggers
/// "The entry point exited without ever building an IHost" when multiple
/// factory instances start in parallel. Serializing the pipeline tests fixes it
/// without affecting unrelated test classes. Mirrors RateLimiterTestCollection.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public class PipelineTestCollection
{
    public const string Name = "Pipeline integration tests (sequential)";
}
