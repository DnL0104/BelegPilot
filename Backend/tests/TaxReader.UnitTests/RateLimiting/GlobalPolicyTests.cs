using System.Net;
using FluentAssertions;
using TaxReader.UnitTests.Helpers;

namespace TaxReader.UnitTests.RateLimiting;

/// <summary>
/// Plan 02-03 D-09: global policy = 60 req/min fixed-window per source IP.
/// Catches generic abuse below the per-endpoint quotas. Triggers BEFORE auth so
/// even unauthenticated requests count against the bucket.
/// </summary>
[Collection(RateLimiterTestCollection.Name)]
public class GlobalPolicyTests
{
    [Fact]
    public async Task SixtyFirstRequest_Returns429()
    {
        using var factory = RateLimitTestFactory.BuildFactory();
        var client = factory.CreateClient();

        // Hit an endpoint with NO per-endpoint policy attached (so only the global
        // 60/min limiter counts). GET /api/v1/receipts returns 401 (Unauthorized)
        // unauthenticated, but the rate limiter still consumes a permit per request.
        for (var i = 0; i < 60; i++)
        {
            var attempt = await client.GetAsync("/api/v1/receipts");
            attempt.StatusCode.Should().BeOneOf(
                HttpStatusCode.Unauthorized,
                HttpStatusCode.NotFound,
                HttpStatusCode.OK);
        }

        var response = await client.GetAsync("/api/v1/receipts");

        response.StatusCode.Should().Be(HttpStatusCode.TooManyRequests,
            "61st request from one IP MUST hit the global 60/min limiter (D-09)");
        response.Content.Headers.ContentType?.MediaType
            .Should().Be("application/problem+json");
    }
}
