using FluentAssertions;

namespace TaxReader.UnitTests.RateLimiting;

/// <summary>
/// Plan 02-03 D-07: upload-concurrency policy = concurrency=2 + queue=4, partitioned by
/// authenticated user. Legitimate double-click queues; abuse beyond 2+4 returns 429.
/// Will be retired in Phase 3 (PIPE-02) when uploads become 202 Accepted + Hangfire jobs.
/// </summary>
public class UploadConcurrencyPolicyTests
{
    [Fact(Skip = "Concurrency limiter behavior verified manually via `docker compose up` — see VALIDATION.md Manual-Only Verifications. WebApplicationFactory in-process test client makes the timing assertions for 'queues vs rejects' unreliable.")]
    public Task ThirdConcurrentUpload_QueuesUntilSlotFree()
    {
        // Manual-only: see VALIDATION.md.
        // Behavior contract: with concurrency=2, three concurrent uploads from the same
        // authenticated user => 2 active, 3rd queues until one completes.
        return Task.CompletedTask;
    }

    [Fact(Skip = "Concurrency limiter behavior verified manually via `docker compose up` — see VALIDATION.md Manual-Only Verifications.")]
    public Task SeventhConcurrentUpload_Returns429()
    {
        // Manual-only: see VALIDATION.md.
        // Behavior contract: with concurrency=2 + QueueLimit=4, 7 concurrent uploads =>
        // 2 active + 4 queued + 7th rejected with 429 + German body.
        return Task.CompletedTask;
    }
}
