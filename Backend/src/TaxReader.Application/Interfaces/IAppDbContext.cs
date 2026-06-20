using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using TaxReader.Domain.Entities;

namespace TaxReader.Application.Interfaces;

public interface IAppDbContext
{
    DatabaseFacade Database { get; }

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

    /// <summary>
    /// PAY-03: Atomically inserts a payment row using INSERT ... ON CONFLICT (stripe_event_id) DO NOTHING.
    /// Returns 1 when the row was inserted (first delivery); 0 on duplicate (idempotent no-op).
    /// Implemented in AppDbContext using ExecuteSqlRawAsync — requires a relational provider.
    /// Exposed here so the webhook handler can be unit-tested by mocking this method.
    /// </summary>
    Task<int> InsertPaymentAtomicAsync(
        Guid userId,
        string stripeEventId,
        string stripeSessionId,
        string? stripePaymentIntentId,
        int creditsGranted,
        int amountCents,
        string currency,
        CancellationToken cancellationToken = default);
}
