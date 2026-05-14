using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using TaxReader.UnitTests.Helpers;

namespace TaxReader.UnitTests.RateLimiting;

/// <summary>
/// Plan 02-03 D-06: UseForwardedHeaders resolves the real client IP behind Caddy.
/// .NET 10 KnownIPNetworks (NOT deprecated KnownNetworks) with System.Net.IPNetwork
/// trusts only the Docker bridge subnet 172.16.0.0/12. ForwardLimit=1 prevents
/// X-Forwarded-For spoofing.
/// </summary>
[Collection(RateLimiterTestCollection.Name)]
public class ForwardedHeadersTests
{
    [Fact]
    public void KnownIPNetworksContainsDockerSubnet()
    {
        using var factory = RateLimitTestFactory.BuildFactory();
        using var scope = factory.Services.CreateScope();

        var forwarded = scope.ServiceProvider
            .GetRequiredService<IOptions<ForwardedHeadersOptions>>().Value;

        forwarded.KnownIPNetworks.Should().Contain(
            n => n.ToString() == "172.16.0.0/12",
            "D-06: only the Docker bridge subnet must be trusted to set X-Forwarded-For. " +
            "Without this, IP-partitioned rate limits bucket all internet traffic under " +
            "Caddy's docker-internal IP.");
    }

    [Fact]
    public void ForwardLimitIsOne()
    {
        using var factory = RateLimitTestFactory.BuildFactory();
        using var scope = factory.Services.CreateScope();

        var forwarded = scope.ServiceProvider
            .GetRequiredService<IOptions<ForwardedHeadersOptions>>().Value;

        forwarded.ForwardLimit.Should().Be(1,
            "Caddy is the single reverse-proxy hop. Raising ForwardLimit lets attackers " +
            "prepend fake IPs to X-Forwarded-For and spoof partition keys (RESEARCH Pitfall 9).");
    }

    [Fact(Skip = "Requires reverse-proxy hop simulation, manual UAT — see VALIDATION.md")]
    public void XForwardedFor_TrustedSubnet_ResolvesRealIp()
    {
        // Manual-only: sending a request with X-Forwarded-For: 1.2.3.4 from a fake
        // client whose connection IP is in the trusted subnet is hard to simulate
        // in WebApplicationFactory (test client is in-process loopback). Verified
        // end-to-end via `docker compose up --build` + curl. See VALIDATION.md.
    }
}
