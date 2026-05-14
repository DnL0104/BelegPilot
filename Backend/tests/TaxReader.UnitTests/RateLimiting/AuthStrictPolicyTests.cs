using FluentAssertions;

namespace TaxReader.UnitTests.RateLimiting;

/// <summary>
/// Plan 02-03 D-09 + D-12: auth-strict policy = 5 req/min fixed-window.
/// Partition key = ip:{RemoteIpAddress} for anonymous /login + /register;
/// user:{sub} for authenticated /account (plan 02-02 wires that endpoint).
/// </summary>
public class AuthStrictPolicyTests
{
    [Fact(Skip = "Pending implementation in Task 2-4")]
    public Task SixthLoginAttempt_Returns429WithGermanProblemDetails()
    {
        // Stub — un-skipped in Task 4 once Task 2 (Program.cs registration)
        // and Task 3 (.RequireRateLimiting("auth-strict") on /login) land.
        return Task.CompletedTask;
    }

    [Fact(Skip = "Pending implementation in Task 2-4")]
    public Task TwoUsersOneIp_BothGetFiveAttempts()
    {
        // Stub — partition-by-sub test for /account. Plan 02-02 owns the endpoint
        // wiring; once that lands this verifies two authenticated users from the
        // same IP each get their full 5/min budget independently.
        return Task.CompletedTask;
    }
}
