using FluentAssertions;
using TaxReader.Domain.Entities;
using TaxReader.Domain.Enums;

namespace TaxReader.UnitTests.Domain;

/// <summary>
/// D-06 enforces a strict numeric ordering for ProcessingStatus values matching the
/// pipeline-flow sequence: Pending=0, Queued=1, Extracting=2, Parsing=3, Classifying=4,
/// Completed=5, Failed=6, Cancelled=7. Any deviation breaks the EF migration's
/// descending-renumber strategy outlined in 03-RESEARCH Pitfall 8.
/// </summary>
public class ProcessingStatusEnumTests
{
    [Fact]
    public void Enum_Contains_AllEightValues()
    {
        var values = Enum.GetValues<ProcessingStatus>();
        values.Should().HaveCount(8);
    }

    [Theory]
    [InlineData(ProcessingStatus.Pending, 0)]
    [InlineData(ProcessingStatus.Queued, 1)]
    [InlineData(ProcessingStatus.Extracting, 2)]
    [InlineData(ProcessingStatus.Parsing, 3)]
    [InlineData(ProcessingStatus.Classifying, 4)]
    [InlineData(ProcessingStatus.Completed, 5)]
    [InlineData(ProcessingStatus.Failed, 6)]
    [InlineData(ProcessingStatus.Cancelled, 7)]
    public void Enum_Value_HasExpectedNumericPosition(ProcessingStatus status, int expected)
    {
        ((int)status).Should().Be(expected);
    }

    [Fact]
    public void ProcessingRun_HasErrorCodeProperty()
    {
        // D-21: stable enum value the frontend switches on. Pairs with German ErrorMessage.
        var run = new ProcessingRun { ErrorCode = "NoTextExtracted" };
        run.ErrorCode.Should().Be("NoTextExtracted");
    }

    [Fact]
    public void ProcessingRun_HasHangfireJobIdProperty()
    {
        // D-14: Hangfire job ID for cancel target.
        var run = new ProcessingRun { HangfireJobId = "abc123" };
        run.HangfireJobId.Should().Be("abc123");
    }

    [Fact]
    public void ReceiptFile_HasUploadBatchIdProperty()
    {
        // D-01: groups every ReceiptFile uploaded together in a single 202-Accepted batch.
        var batchId = Guid.NewGuid();
        var file = new ReceiptFile { UploadBatchId = batchId };
        file.UploadBatchId.Should().Be(batchId);
    }
}
