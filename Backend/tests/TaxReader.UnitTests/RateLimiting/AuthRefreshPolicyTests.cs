using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using TaxReader.UnitTests.Helpers;

namespace TaxReader.UnitTests.RateLimiting;

/// <summary>
/// Plan 02-03 D-05: auth-refresh policy = 30 req/min fixed-window, partitioned by IP.
/// Higher than auth-strict because legitimate token rotation may burst (multiple tabs,
/// app cold-starts). Two NAT'd devices share a bucket — acceptable at 100-500 user target.
/// </summary>
[Collection(RateLimiterTestCollection.Name)]
public class AuthRefreshPolicyTests
{
    [Fact]
    public async Task ThirtyFirstRefreshAttempt_Returns429()
    {
        using var factory = RateLimitTestFactory.BuildFactory();
        var client = factory.CreateClient();

        // Burn the 30/min budget with a dummy refresh token. /auth/refresh returns
        // 401 (Unauthorized) for unknown tokens — the rate-limit policy still
        // consumes a permit on each attempt.
        for (var i = 0; i < 30; i++)
        {
            var attempt = await client.PostAsJsonAsync("/api/v1/auth/refresh",
                new { RefreshToken = $"dummy-token-{i}" });

            attempt.StatusCode.Should().BeOneOf(
                HttpStatusCode.Unauthorized,
                HttpStatusCode.BadRequest);
        }

        // 31st attempt MUST be rate-limited.
        var response = await client.PostAsJsonAsync("/api/v1/auth/refresh",
            new { RefreshToken = "dummy-token-31" });

        response.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        response.Content.Headers.ContentType?.MediaType
            .Should().Be("application/problem+json");
        response.Headers.RetryAfter.Should().NotBeNull();
    }
}
