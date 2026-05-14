using FluentAssertions;

namespace TaxReader.UnitTests.RateLimiting;

/// <summary>
/// Plan 02-03 D-09: global policy = 60 req/min fixed-window per source IP.
/// Catches generic abuse below the per-endpoint quotas. Triggers BEFORE auth so
/// even unauthenticated requests count against the bucket.
/// </summary>
public class GlobalPolicyTests
{
    [Fact(Skip = "Pending implementation in Task 2-4")]
    public Task SixtyFirstRequest_Returns429()
    {
        // Stub — un-skipped in Task 4. Burns 60 generic GETs on /api/v1/*;
        // 61st must be 429.
        return Task.CompletedTask;
    }
}
