namespace TaxReader.Domain.Enums;

public enum ClassificationStatus
{
    Suggested = 0,
    Confirmed = 1,
    Failed    = 2   // Technical failure — never confused with Unbekannt (genuine unknown)
}
