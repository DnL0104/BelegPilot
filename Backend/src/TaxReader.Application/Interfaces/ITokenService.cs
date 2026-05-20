using TaxReader.Domain.Entities;
using TaxReader.Domain.Enums;

namespace TaxReader.Application.Interfaces;

public interface ITokenService
{
    Task<int> GetBalanceAsync(CancellationToken cancellationToken = default);

    Task<UserTokenBalance> GetOrCreateBalanceAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Attempts to deduct the sum of all entry amounts from the balance in a single
    /// DB roundtrip. If the balance is insufficient, no entries are recorded and
    /// the method returns false.
    /// </summary>
    Task<bool> TryConsumeManyAsync(
        IReadOnlyList<TokenLedgerEntry> entries,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Refunds the given entries (e.g. for AI calls that came back as Unknown or failed).
    /// Sums all amounts into a single balance update, with one transaction record per entry
    /// for traceability. All persisted in a single DB roundtrip.
    /// </summary>
    Task<UserTokenBalance> RefundManyAsync(
        IReadOnlyList<TokenLedgerEntry> entries,
        CancellationToken cancellationToken = default);

    Task<UserTokenBalance> AddTokensAsync(
        int amount,
        TokenTransactionType type,
        string description,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TokenTransaction>> GetRecentTransactionsAsync(
        int take = 20,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// One token-ledger entry — used for batched consume/refund operations.
/// </summary>
public record TokenLedgerEntry(int Amount, string Description, Guid? RelatedItemId = null);
