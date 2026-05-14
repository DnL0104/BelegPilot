using FluentAssertions;

namespace TaxReader.UnitTests.RateLimiting;

/// <summary>
/// Plan 02-03 D-07: upload-concurrency policy = concurrency=2 + queue=4, partitioned by
/// authenticated user. Legitimate double-click queues; abuse beyond 2+4 returns 429.
/// Will be retired in Phase 3 (PIPE-02) when uploads become 202 Accepted + Hangfire jobs.
/// </summary>
public class UploadConcurrencyPolicyTests
{
    [Fact(Skip = "Pending implementation in Task 2-4")]
    public Task ThirdConcurrentUpload_QueuesUntilSlotFree()
    {
        // Stub — un-skipped in Task 4. Concurrency-limiter tests in WebApplicationFactory
        // are tricky (in-process test client); may remain skipped with manual UAT note.
        return Task.CompletedTask;
    }

    [Fact(Skip = "Pending implementation in Task 2-4")]
    public Task SeventhConcurrentUpload_Returns429()
    {
        // Stub — un-skipped in Task 4. 2 active + 4 queued = capacity 6; 7th = 429.
        return Task.CompletedTask;
    }
}
