using FluentAssertions;

namespace TaxReader.UnitTests.Pipeline;

/// <summary>
/// Source-grep regression tests locking D-04 retry policy and D-05 JobId LogContext
/// onto the two pipeline jobs. Any future refactor that drops the attributes or
/// removes the LogContext scope fails CI loudly.
/// </summary>
public class RetryAttributeWiringTests
{
    private static string AppSourceFile(string relative)
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "src", "TaxReader.Application", relative);
        return Path.GetFullPath(path);
    }

    [Fact]
    public void ProcessReceiptFileJob_Carries_AutomaticRetryWith3Attempts()
    {
        var src = File.ReadAllText(AppSourceFile("Jobs/ProcessReceiptFileJob.cs"));
        // D-04: 3 retries with backoff 30s / 2m / 5m.
        src.Should().Contain("[AutomaticRetry(Attempts = 3, DelaysInSeconds = new[] { 30, 120, 300 })]");
    }

    [Fact]
    public void ProcessReceiptFileJob_Wraps_BodyWithJobIdLogContext()
    {
        var src = File.ReadAllText(AppSourceFile("Jobs/ProcessReceiptFileJob.cs"));
        // D-05 + Phase 1 D-18: every log line carries the job ID.
        src.Should().Contain("LogContext.PushProperty(\"JobId\"");
    }

    [Fact]
    public void ClassifyBatchJob_Carries_AutomaticRetryWithZeroAttempts()
    {
        var src = File.ReadAllText(AppSourceFile("Jobs/ClassifyBatchJob.cs"));
        // D-04: no Hangfire retries — the AI-failure refund branch inside
        // AiOnlyClassificationService handles failures gracefully.
        src.Should().Contain("[AutomaticRetry(Attempts = 0)]");
    }

    [Fact]
    public void ClassifyBatchJob_Wraps_BodyWithJobIdLogContext()
    {
        var src = File.ReadAllText(AppSourceFile("Jobs/ClassifyBatchJob.cs"));
        src.Should().Contain("LogContext.PushProperty(\"JobId\"");
    }

    [Fact]
    public void IBackgroundJobClient_Port_DoesNotReference_HangfireNamespace()
    {
        var src = File.ReadAllText(AppSourceFile("Interfaces/IBackgroundJobClient.cs"));
        // Architecture rule: Application defines interfaces only; Hangfire types
        // belong in Infrastructure. The Application port must NOT import Hangfire
        // via a using directive (the only way it could pull in Hangfire types).
        // We exclude inline references like "Hangfire's IBackgroundJobClient" in XML
        // doc comments — those are doc-only mentions, not bindings.
        src.Should().NotContain("using Hangfire;");
    }

    [Fact]
    public void ClassifyBatchJob_Contains_IdempotentEntryCheck()
    {
        // RESEARCH Pitfall 2 mitigation: ClassifyBatchJob ENTRY checks whether
        // any run for the batch is already Classifying or beyond, and exits early.
        var src = File.ReadAllText(AppSourceFile("Jobs/ClassifyBatchJob.cs"));
        src.Should().Contain("alreadyClassifying");
    }
}
