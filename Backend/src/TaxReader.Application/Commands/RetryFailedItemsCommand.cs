namespace TaxReader.Application.Commands;

public record RetryFailedItemsCommand(Guid ReceiptId);
