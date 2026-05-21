using TaxReader.Domain.Enums;

namespace TaxReader.Domain.Entities;

public class ReceiptFile
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string OriginalFileName { get; set; } = string.Empty;
    public string ContentHash { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string? SourceHint { get; set; }
    public int? YearHint { get; set; }
    public string? UploadedBy { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    public FileStatus Status { get; set; } = FileStatus.Uploaded;

    /// <summary>D-01: groups every ReceiptFile uploaded together in a single 202-Accepted batch. The fan-in barrier in ProcessReceiptFileJob counts completed parents per UploadBatchId. Nullable for pre-Phase-3 legacy rows.</summary>
    public Guid? UploadBatchId { get; set; }

    public User User { get; set; } = null!;
    public Receipt? Receipt { get; set; }
    public ICollection<ProcessingRun> ProcessingRuns { get; set; } = [];
}
