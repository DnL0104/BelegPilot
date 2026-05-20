namespace TaxReader.Domain.Enums;

public enum ProcessingStatus
{
    Pending = 0,
    Extracting = 1,
    Parsing = 2,
    Classifying = 3,
    Completed = 4,
    Failed = 5
}
