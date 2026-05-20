using TaxReader.Domain.Enums;

namespace TaxReader.Application.Commands;

public record ConfirmClassificationCommand(Guid ReceiptItemId, Category Category);
