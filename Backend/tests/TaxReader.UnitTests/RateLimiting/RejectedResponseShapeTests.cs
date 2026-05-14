using FluentAssertions;

namespace TaxReader.UnitTests.RateLimiting;

/// <summary>
/// Plan 02-03 D-08: OnRejected callback emits application/problem+json with German
/// title + detail and Retry-After header. T-02-07 mitigation: no policy-name leakage.
/// </summary>
public class RejectedResponseShapeTests
{
    [Fact(Skip = "Pending implementation in Task 2-4")]
    public Task RateLimited_Returns429WithGermanProblemDetails_AndRetryAfter()
    {
        // Stub — un-skipped in Task 4. Triggers any limiter (easiest: 6 /auth/login
        // attempts); asserts status 429, ContentType application/problem+json,
        // Retry-After header set, body Title == "Zu viele Anfragen.", Detail contains
        // "Sekunden erneut", Status == 429.
        return Task.CompletedTask;
    }
}
