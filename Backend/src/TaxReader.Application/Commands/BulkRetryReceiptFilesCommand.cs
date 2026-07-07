namespace TaxReader.Application.Commands;

public record BulkRetryReceiptFilesCommand(IReadOnlyList<Guid> ReceiptFileIds);
