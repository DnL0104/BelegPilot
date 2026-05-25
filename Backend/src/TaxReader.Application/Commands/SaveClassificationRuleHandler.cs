using Microsoft.EntityFrameworkCore;
using TaxReader.Application.DTOs;
using TaxReader.Application.Interfaces;
using TaxReader.Application.Mapping;
using TaxReader.Domain.Common;
using TaxReader.Domain.Entities;
using TaxReader.Domain.Enums;

namespace TaxReader.Application.Commands;

public record SaveClassificationRuleCommand(
    Guid ReceiptItemId,
    string? VendorPattern,
    string? DescriptionPattern,
    string? SourceFilePattern,
    Category Category);

public class SaveClassificationRuleHandler(IAppDbContext dbContext, ICurrentUser currentUser)
{
    public async Task<Result<ClassificationRuleDto>> HandleAsync(
        SaveClassificationRuleCommand command,
        CancellationToken cancellationToken = default)
    {
        // Per-user ownership guard — item must belong to current user
        var item = await dbContext.ReceiptItems
            .Include(i => i.Receipt)
                .ThenInclude(r => r.ReceiptFile)
            .FirstOrDefaultAsync(i => i.Id == command.ReceiptItemId
                && i.Receipt.ReceiptFile.UserId == currentUser.UserId, cancellationToken);

        if (item is null)
            return Result<ClassificationRuleDto>.Failure(
                $"Artikel mit id '{command.ReceiptItemId}' nicht gefunden.");

        // D-12: 409 Conflict if an identical user rule already exists (same patterns AND same category)
        var duplicate = await dbContext.ClassificationRules
            .AnyAsync(r => r.UserId == currentUser.UserId
                && r.DescriptionPattern == command.DescriptionPattern
                && r.VendorPattern == command.VendorPattern
                && r.SourceFilePattern == command.SourceFilePattern
                && r.Category == command.Category, cancellationToken);

        if (duplicate)
            return Result<ClassificationRuleDto>.Failure("Eine identische Regel existiert bereits.");

        var rule = new ClassificationRule
        {
            Id = Guid.NewGuid(),
            UserId = currentUser.UserId,
            VendorPattern = command.VendorPattern,
            DescriptionPattern = command.DescriptionPattern,
            SourceFilePattern = command.SourceFilePattern,
            Category = command.Category,
            Priority = 10,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        dbContext.ClassificationRules.Add(rule);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result<ClassificationRuleDto>.Success(rule.ToDto());
    }
}
