using TaxReader.Domain.Entities;

namespace TaxReader.Application.Interfaces;

public interface IReceiptParser
{
    bool CanParse(string rawText, string? sourceHint);
    Receipt Parse(string rawText, ReceiptFile receiptFile);
}
