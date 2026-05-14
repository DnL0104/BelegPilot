namespace TaxReader.UnitTests.RateLimiting;

/// <summary>
/// Serializes the WebApplicationFactory&lt;Program&gt; tests in this directory.
/// The .NET 10 top-level-statement Program runs <c>await app.RunAsync()</c> once;
/// xUnit's default parallel test execution across classes triggers
/// "The entry point exited without ever building an IHost" when multiple
/// factory instances start in parallel. Serializing the rate-limit tests fixes it
/// without affecting unrelated test classes.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public class RateLimiterTestCollection
{
    public const string Name = "RateLimiter integration tests (sequential)";
}
