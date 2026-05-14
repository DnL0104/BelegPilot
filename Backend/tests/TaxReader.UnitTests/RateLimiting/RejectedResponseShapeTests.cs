using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using TaxReader.UnitTests.Helpers;

namespace TaxReader.UnitTests.RateLimiting;

/// <summary>
/// Plan 02-03 D-08: OnRejected callback emits application/problem+json with German
/// title + detail and Retry-After header. T-02-07 mitigation: no policy-name leakage.
/// </summary>
[Collection(RateLimiterTestCollection.Name)]
public class RejectedResponseShapeTests
{
    [Fact]
    public async Task RateLimited_Returns429WithGermanProblemDetails_AndRetryAfter()
    {
        using var factory = RateLimitTestFactory.BuildFactory();
        var client = factory.CreateClient();

        // Trigger the auth-strict limiter (easiest path: burn 5 /auth/login attempts).
        for (var i = 0; i < 5; i++)
        {
            await client.PostAsJsonAsync("/api/v1/auth/login",
                new { Email = "shape-test@example.de", Password = "x" });
        }

        var response = await client.PostAsJsonAsync("/api/v1/auth/login",
            new { Email = "shape-test@example.de", Password = "x" });

        // 1. Status code
        response.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);

        // 2. Content type
        response.Content.Headers.ContentType?.MediaType
            .Should().Be("application/problem+json");

        // 3. Retry-After header present and numeric
        response.Headers.RetryAfter.Should().NotBeNull();
        var retryAfterDeltaSeconds = response.Headers.RetryAfter?.Delta?.TotalSeconds;
        // Retry-After in this response is set via Headers.RetryAfter = "{N}" string
        // (NumberFormatInfo.InvariantInfo), which HttpClient parses into Delta.
        retryAfterDeltaSeconds.Should().NotBeNull(
            "Retry-After must be a numeric delta in seconds");
        retryAfterDeltaSeconds!.Value.Should().BeGreaterThan(0);

        // 4. Body matches German ProblemDetails shape
        var body = await response.Content.ReadFromJsonAsync<ProblemResponse>();
        body.Should().NotBeNull();
        body!.Title.Should().Be("Zu viele Anfragen.");
        body.Detail.Should().Contain("Sekunden erneut",
            "Detail must read like 'Bitte versuchen Sie es in {N} Sekunden erneut.'");
        body.Status.Should().Be(429);

        // Negative assertion: no policy name leakage (T-02-07)
        var raw = await response.Content.ReadAsStringAsync();
        raw.Should().NotContain("auth-strict",
            "T-02-07: the 429 body must NOT leak the internal policy name");
        raw.Should().NotContain("auth-refresh");
        raw.Should().NotContain("upload-concurrency");
    }

    private sealed record ProblemResponse(string Title, string Detail, int Status);
}
