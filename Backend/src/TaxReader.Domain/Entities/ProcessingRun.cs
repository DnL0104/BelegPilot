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

    public ReceiptFile ReceiptFile { get; set; } = null!;
}
