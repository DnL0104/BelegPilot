namespace TaxReader.Application.DTOs;

public record ClassificationRuleDto(
    Guid Id,
    Guid? UserId,
    string? VendorPattern,
    string? DescriptionPattern,
    string? SourceFilePattern,
    string Category,
    int Priority,
    bool IsActive,
    DateTime CreatedAt);
