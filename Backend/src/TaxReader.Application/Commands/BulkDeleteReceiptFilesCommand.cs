namespace TaxReader.Application.Commands;

public record BulkDeleteReceiptFilesCommand(IReadOnlyList<Guid> ReceiptFileIds);
