using TaxReader.Domain.Enums;

namespace TaxReader.Domain.Entities;

public class ProcessingRun
{
    public Guid Id { get; set; }
    public Guid ReceiptFileId { get; set; }
    public ProcessingStatus Status { get; set; } = ProcessingStatus.Pending;
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public string? ErrorMessage { get; set; }
    public string StepDetails { get; set; } = "[]";

    /// <summary>D-21: stable enum value the frontend switches on. Pairs with German ErrorMessage.</summary>
    public string? ErrorCode { get; set; }

    /// <summary>D-14: Hangfire job ID for cancel target. The latest ProcessingRun for a ReceiptFile carries the cancel target.</summary>
    public string? HangfireJobId { get; set; }

    public ReceiptFile ReceiptFile { get; set; } = null!;
}
