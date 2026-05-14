using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using TaxReader.UnitTests.Helpers;

namespace TaxReader.UnitTests.RateLimiting;

/// <summary>
/// Plan 02-03 D-09 + D-12: auth-strict policy = 5 req/min fixed-window.
/// Partition key = ip:{RemoteIpAddress} for anonymous /login + /register;
/// user:{sub} for authenticated /account (plan 02-02 wires that endpoint).
/// </summary>
[Collection(RateLimiterTestCollection.Name)]
public class AuthStrictPolicyTests
{
    [Fact]
    public async Task SixthLoginAttempt_Returns429WithGermanProblemDetails()
    {
        using var factory = RateLimitTestFactory.BuildFactory();
        var client = factory.CreateClient();

        // Burn the 5/min budget with intentionally-bad credentials.
        // /auth/login returns 401 (Unauthorized) for missing user — does NOT
        // touch the rate-limit policy decision (each request still consumes a permit).
        for (var i = 0; i < 5; i++)
        {
            var attempt = await client.PostAsJsonAsync("/api/v1/auth/login",
                new { Email = "ratelimit-test@example.de", Password = "wrong-password" });

            attempt.StatusCode.Should().BeOneOf(
                HttpStatusCode.Unauthorized,
                HttpStatusCode.BadRequest);
        }

        // 6th attempt MUST be rate-limited.
        var response = await client.PostAsJsonAsync("/api/v1/auth/login",
            new { Email = "ratelimit-test@example.de", Password = "wrong-password" });

        response.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        response.Content.Headers.ContentType?.MediaType
            .Should().Be("application/problem+json",
                "OnRejected emits application/problem+json per D-08");
        response.Headers.RetryAfter.Should().NotBeNull(
            "Retry-After header MUST accompany 429 (D-08)");

        var body = await response.Content.ReadFromJsonAsync<ProblemResponse>();
        body.Should().NotBeNull();
        body!.Title.Should().Be("Zu viele Anfragen.",
            "German title is the canonical user-visible string per D-08");
        body.Detail.Should().Contain("Sekunden erneut",
            "German detail tells the user how long to wait");
        body.Status.Should().Be(429);
    }

    [Fact(Skip = "Requires plan 02-02 endpoint wiring — /account DELETE attached to auth-strict by plan 02-02 with sub-partitioning")]
    public Task TwoUsersOneIp_BothGetFiveAttempts()
    {
        // Partition-by-sub assertion for /account. The policy registration (auth-strict
        // with sub-or-IP partition logic) is in Program.cs as of plan 02-03, but the
        // endpoint .RequireRateLimiting("auth-strict") attachment for /account is owned
        // by plan 02-02. This test will be un-skipped when 02-02 lands.
        return Task.CompletedTask;
    }

    private sealed record ProblemResponse(string Title, string Detail, int Status);
}
