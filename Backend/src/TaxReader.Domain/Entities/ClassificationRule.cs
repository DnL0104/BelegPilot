using TaxReader.Domain.Enums;

namespace TaxReader.Domain.Entities;

public class ClassificationRule
{
    public Guid Id { get; set; }
    public string Pattern { get; set; } = string.Empty;
    public Category Category { get; set; }
    public int Priority { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
