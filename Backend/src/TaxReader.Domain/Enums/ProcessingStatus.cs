namespace TaxReader.Domain.Enums;

/// <summary>
/// D-06: enum numbered in pipeline-flow order. The EF migration AddQueuedAndCancelledProcessingStatuses
/// renumbers existing processing_runs.status rows in descending order to avoid the data corruption
/// outlined in 03-RESEARCH.md Pitfall 8. Do NOT re-order without a coordinated migration.
/// </summary>
public enum ProcessingStatus
{
    Pending = 0,
    Queued = 1,
    Extracting = 2,
    Parsing = 3,
    Classifying = 4,
    Completed = 5,
    Failed = 6,
    Cancelled = 7
}
