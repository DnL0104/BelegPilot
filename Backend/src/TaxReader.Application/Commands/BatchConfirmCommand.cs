namespace TaxReader.Application.Commands;

/// <summary>
/// Confirms the latest AI-suggested classification for multiple items at once.
/// Each item keeps its already-suggested category — no category override needed.
/// </summary>
public record BatchConfirmCommand(IReadOnlyList<Guid> ReceiptItemIds);
