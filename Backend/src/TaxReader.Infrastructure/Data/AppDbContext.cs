using Microsoft.EntityFrameworkCore;
using TaxReader.Application.Interfaces;
using TaxReader.Domain.Entities;

namespace TaxReader.Infrastructure.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options)
    : DbContext(options), IAppDbContext
{
    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<ReceiptFile> ReceiptFiles => Set<ReceiptFile>();
    public DbSet<Receipt> Receipts => Set<Receipt>();
    public DbSet<ReceiptItem> ReceiptItems => Set<ReceiptItem>();
    public DbSet<ItemClassification> ItemClassifications => Set<ItemClassification>();
    public DbSet<ClassificationRule> ClassificationRules => Set<ClassificationRule>();
    public DbSet<ProcessingRun> ProcessingRuns => Set<ProcessingRun>();
    public DbSet<UserTokenBalance> UserTokenBalances => Set<UserTokenBalance>();
    public DbSet<TokenTransaction> TokenTransactions => Set<TokenTransaction>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<AuditLogEntry> AuditLogEntries => Set<AuditLogEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }

    /// <inheritdoc/>
    public Task<int> InsertPaymentAtomicAsync(
        Guid userId,
        string stripeEventId,
        string stripeSessionId,
        string? stripePaymentIntentId,
        int creditsGranted,
        int amountCents,
        string currency,
        CancellationToken cancellationToken = default)
    {
        // PAY-03: atomic idempotent insert — collapses check-then-insert to a single DB round-trip.
        // ON CONFLICT (stripe_event_id) DO NOTHING returns 0 rows on duplicate; 1 on first insert.
        // Column names are snake_case — EF UseSnakeCaseNamingConvention does not apply to raw SQL.
        return Database.ExecuteSqlRawAsync(
            """
            INSERT INTO payments (
                id, user_id, stripe_event_id, stripe_session_id,
                stripe_payment_intent_id, credits_granted, amount_cents,
                currency, status, created_at
            ) VALUES (
                gen_random_uuid(), {0}, {1}, {2}, {3}, {4}, {5}, {6}, 0, now()
            )
            ON CONFLICT (stripe_event_id) DO NOTHING
            """,
            new object[]
            {
                userId, stripeEventId, stripeSessionId, stripePaymentIntentId ?? string.Empty,
                creditsGranted, amountCents, currency
            },
            cancellationToken);
    }
}
