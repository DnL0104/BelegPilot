using TaxReader.Domain.Enums;

namespace TaxReader.Domain.Entities;

public class ClassificationRule
{
    public Guid Id { get; set; }
    public Guid? UserId { get; set; }              // null = system rule; non-null = user-private
    public string? VendorPattern { get; set; }     // substring match, case-insensitive
    public string? SourceFilePattern { get; set; } // regex match, case-insensitive
    public string? DescriptionPattern { get; set; }// regex match, case-insensitive (was Pattern)
    public Category Category { get; set; }
    public int Priority { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
