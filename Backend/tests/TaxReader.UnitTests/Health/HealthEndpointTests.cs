using System.Net;
using FluentAssertions;
using TaxReader.UnitTests.Helpers;
using TaxReader.UnitTests.RateLimiting;

namespace TaxReader.UnitTests.Health;

/// <summary>
/// Plan 07-03 OBS-03: anonymous health probes for BetterStack.
/// T-07-09 mitigation: response body must contain no secrets, connection strings, or stack traces.
/// T-07-11 mitigation: endpoints must be anonymous (200 with no auth header, not 401).
/// In-memory provider's CanConnectAsync returns true → DB reports "up" and status is "healthy".
/// </summary>
[Collection(RateLimiterTestCollection.Name)]
public class HealthEndpointTests
{
    [Fact]
    public async Task GetHealth_DbReachable_Returns200WithHealthyJson()
    {
        using var factory = RateLimitTestFactory.BuildFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("healthy");
    }

    [Fact]
    public async Task GetHealth_NoAuthHeader_Returns200()
    {
        using var factory = RateLimitTestFactory.BuildFactory();
        var client = factory.CreateClient();

        // No Authorization header — must succeed as anonymous, not return 401.
        var response = await client.GetAsync("/health");

        response.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized,
            "T-07-11: /health must be anonymous — 401 means it regressed behind RequireAuthorization");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetApiV1Health_NoAuthHeader_ReturnsHealthyJsonWithAnthropicField()
    {
        using var factory = RateLimitTestFactory.BuildFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/health");

        response.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized,
            "T-07-11: /api/v1/health must be anonymous — 401 means it regressed behind RequireAuthorization");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("healthy");
        body.Should().Contain("anthropic",
            "/api/v1/health must include an 'anthropic' field per OBS-03");
    }

    [Fact]
    public async Task HealthBody_DoesNotContainSecretsOrConnectionString()
    {
        using var factory = RateLimitTestFactory.BuildFactory();
        var client = factory.CreateClient();

        var livenessBody = await (await client.GetAsync("/health")).Content.ReadAsStringAsync();
        var readinessBody = await (await client.GetAsync("/api/v1/health")).Content.ReadAsStringAsync();

        // T-07-09: coarse status only — no secrets, connection strings, or stack traces.
        foreach (var body in new[] { livenessBody, readinessBody })
        {
            body.Should().NotContainEquivalentOf("connectionstring",
                "T-07-09: DB connection string must never appear in the health response body");
            body.Should().NotContainEquivalentOf("host=",
                "T-07-09: raw Postgres host parameter must not appear");
            body.Should().NotContainEquivalentOf("password",
                "T-07-09: credentials must not appear in the health body");
            body.Should().NotContainEquivalentOf("sk_live",
                "T-07-09: Stripe secret key prefix must not appear");
            body.Should().NotContainEquivalentOf("whsec",
                "T-07-09: Stripe webhook secret prefix must not appear");
            body.Should().NotContainEquivalentOf("secret",
                "T-07-09: JWT/other secrets must not appear");
        }
    }
}
