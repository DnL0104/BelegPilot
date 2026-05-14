using System.IO;
using FluentAssertions;

namespace TaxReader.UnitTests.RateLimiting;

/// <summary>
/// Plan 02-03 D-06 pipeline-order guard. Source-level structural-grep test using
/// the same pattern as <c>SerilogEnrichmentTests.UploadReceiptFilesHandler_Source_...</c>
/// — defends against future PRs that reorder middleware.
/// </summary>
public class ForwardedHeadersWiringTests
{
    [Fact]
    public void Program_UsesForwardedHeaders_BeforeRequestLogging()
    {
        var source = ReadProgramCs();

        var fwdIdx = source.IndexOf("app.UseForwardedHeaders", StringComparison.Ordinal);
        var logIdx = source.IndexOf("app.UseSerilogRequestLogging", StringComparison.Ordinal);

        fwdIdx.Should().BeGreaterThan(-1, "UseForwardedHeaders must be registered in the middleware pipeline");
        logIdx.Should().BeGreaterThan(-1, "UseSerilogRequestLogging must be registered in the middleware pipeline");
        fwdIdx.Should().BeLessThan(logIdx,
            "UseForwardedHeaders must come BEFORE UseSerilogRequestLogging so the real client IP " +
            "is resolved before request logging captures it (D-06).");
    }

    [Fact]
    public void Program_UsesRateLimiter_BetweenSerilogAndAuthentication()
    {
        var source = ReadProgramCs();

        var logIdx = source.IndexOf("app.UseSerilogRequestLogging", StringComparison.Ordinal);
        var rateIdx = source.IndexOf("app.UseRateLimiter", StringComparison.Ordinal);
        var authIdx = source.IndexOf("app.UseAuthentication", StringComparison.Ordinal);

        logIdx.Should().BeGreaterThan(-1);
        rateIdx.Should().BeGreaterThan(-1, "UseRateLimiter must be registered in the middleware pipeline");
        authIdx.Should().BeGreaterThan(-1);

        rateIdx.Should().BeGreaterThan(logIdx,
            "UseRateLimiter must come AFTER UseSerilogRequestLogging so 429 responses are logged.");
        rateIdx.Should().BeLessThan(authIdx,
            "UseRateLimiter must come BEFORE UseAuthentication so the global IP-partitioned limiter " +
            "triggers on unauthenticated requests too (RESEARCH Pitfall 2).");
    }

    [Fact]
    public void Program_UsesForwardedHeaders_BeforeExceptionHandling()
    {
        var source = ReadProgramCs();

        var fwdIdx = source.IndexOf("app.UseForwardedHeaders", StringComparison.Ordinal);
        var excIdx = source.IndexOf("app.UseMiddleware<ExceptionHandlingMiddleware>", StringComparison.Ordinal);

        fwdIdx.Should().BeGreaterThan(-1);
        excIdx.Should().BeGreaterThan(-1);
        fwdIdx.Should().BeLessThan(excIdx,
            "UseForwardedHeaders must come BEFORE ExceptionHandlingMiddleware so any error path " +
            "still sees the resolved real client IP for logging / Sentry attribution.");
    }

    private static string ReadProgramCs()
    {
        // Match the path-walk pattern from SerilogEnrichmentTests:
        // bin/Debug/net10.0/ → up 5 → repo Backend root → src/TaxReader.Api/Program.cs
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "src", "TaxReader.Api", "Program.cs");

        File.Exists(path).Should().BeTrue(
            $"Program.cs not found at {Path.GetFullPath(path)}");

        return File.ReadAllText(path);
    }
}
