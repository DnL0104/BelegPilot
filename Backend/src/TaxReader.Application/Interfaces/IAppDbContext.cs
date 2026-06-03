using Microsoft.EntityFrameworkCore;
using TaxReader.Domain.Entities;

namespace TaxReader.Application.Interfaces;

public interface IAppDbContext
{
    DbSet<User> Users { get; }
    DbSet<RefreshToken> RefreshTokens { get; }
    DbSet<ReceiptFile> ReceiptFiles { get; }
    DbSet<Receipt> Receipts { get; }
    DbSet<ReceiptItem> ReceiptItems { get; }
    DbSet<ItemClassification> ItemClassifications { get; }
    DbSet<ClassificationRule> ClassificationRules { get; }
    DbSet<ProcessingRun> ProcessingRuns { get; }
    DbSet<UserTokenBalance> UserTokenBalances { get; }
    DbSet<TokenTransaction> TokenTransactions { get; }
    DbSet<Payment> Payments { get; }
    DbSet<AuditLogEntry> AuditLogEntries { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
