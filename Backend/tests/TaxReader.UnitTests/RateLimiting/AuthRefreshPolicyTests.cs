using FluentAssertions;

namespace TaxReader.UnitTests.RateLimiting;

/// <summary>
/// Plan 02-03 D-05: auth-refresh policy = 30 req/min fixed-window, partitioned by IP.
/// Higher than auth-strict because legitimate token rotation may burst (multiple tabs,
/// app cold-starts). Two NAT'd devices share a bucket — acceptable at 100-500 user target.
/// </summary>
public class AuthRefreshPolicyTests
{
    [Fact(Skip = "Pending implementation in Task 2-4")]
    public Task ThirtyFirstRefreshAttempt_Returns429()
    {
        // Stub — un-skipped in Task 4. Burns 30 /auth/refresh attempts with dummy
        // token (each returns 401 or 400); 31st must be 429.
        return Task.CompletedTask;
    }
}
