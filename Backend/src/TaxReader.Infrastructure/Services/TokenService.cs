using Microsoft.EntityFrameworkCore;
using TaxReader.Application.Interfaces;
using TaxReader.Domain.Entities;
using TaxReader.Domain.Enums;

namespace TaxReader.Infrastructure.Services;

public class TokenService(IAppDbContext dbContext, ICurrentUser currentUser) : ITokenService
{
    private const int InitialFreeTokens = 10;

    public async Task<int> GetBalanceAsync(CancellationToken cancellationToken = default)
    {
        var balance = await GetOrCreateBalanceAsync(cancellationToken);
        return balance.Balance;
    }

    public Task<UserTokenBalance> GetOrCreateBalanceAsync(CancellationToken cancellationToken = default) =>
        GetOrCreateBalanceAsync(currentUser.UserId, cancellationToken);

    private async Task<UserTokenBalance> GetOrCreateBalanceAsync(Guid userId, CancellationToken cancellationToken)
    {
        var existing = await dbContext.UserTokenBalances
            .FirstOrDefaultAsync(b => b.UserId == userId, cancellationToken);

        if (existing is not null) return existing;

        var created = new UserTokenBalance
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            UserKey = userId.ToString(),
            Balance = InitialFreeTokens,
            UpdatedAt = DateTime.UtcNow
        };
        dbContext.UserTokenBalances.Add(created);

        dbContext.TokenTransactions.Add(new TokenTransaction
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            UserKey = userId.ToString(),
            Type = TokenTransactionType.Adjustment,
            Amount = InitialFreeTokens,
            BalanceAfter = InitialFreeTokens,
            Description = "Welcome bonus",
            CreatedAt = DateTime.UtcNow
        });

        await dbContext.SaveChangesAsync(cancellationToken);
        return created;
    }

    public async Task<bool> TryConsumeManyAsync(
        IReadOnlyList<TokenLedgerEntry> entries,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        if (entries.Count == 0) return true;

        var total = entries.Sum(e => e.Amount);
        if (total <= 0) return true;

        var balance = await GetOrCreateBalanceAsync(userId, cancellationToken);
        if (balance.Balance < total) return false;

        balance.Balance -= total;
        balance.UpdatedAt = DateTime.UtcNow;

        // Per-entry transaction records preserve traceability (the user sees exactly
        // what they paid for in their transaction history). BalanceAfter is computed
        // running so each row reflects the state at that point.
        var running = balance.Balance + total;
        var now = DateTime.UtcNow;
        foreach (var entry in entries)
        {
            running -= entry.Amount;
            dbContext.TokenTransactions.Add(new TokenTransaction
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                UserKey = userId.ToString(),
                Type = TokenTransactionType.Consumption,
                Amount = -entry.Amount,
                BalanceAfter = running,
                Description = entry.Description,
                RelatedItemId = entry.RelatedItemId,
                CreatedAt = now
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<UserTokenBalance> RefundManyAsync(
        IReadOnlyList<TokenLedgerEntry> entries,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        if (entries.Count == 0) return await GetOrCreateBalanceAsync(userId, cancellationToken);

        var total = entries.Sum(e => e.Amount);
        var balance = await GetOrCreateBalanceAsync(userId, cancellationToken);

        if (total <= 0) return balance;

        balance.Balance += total;
        balance.UpdatedAt = DateTime.UtcNow;

        var running = balance.Balance - total;
        var now = DateTime.UtcNow;
        foreach (var entry in entries)
        {
            running += entry.Amount;
            dbContext.TokenTransactions.Add(new TokenTransaction
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                UserKey = userId.ToString(),
                Type = TokenTransactionType.Refund,
                Amount = entry.Amount,
                BalanceAfter = running,
                Description = entry.Description,
                RelatedItemId = entry.RelatedItemId,
                CreatedAt = now
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return balance;
    }

    public async Task<UserTokenBalance> AddTokensAsync(
        int amount,
        TokenTransactionType type,
        string description,
        CancellationToken cancellationToken = default)
    {
        if (amount <= 0)
            throw new ArgumentOutOfRangeException(nameof(amount), "Amount must be positive.");

        var balance = await GetOrCreateBalanceAsync(cancellationToken);
        balance.Balance += amount;
        balance.UpdatedAt = DateTime.UtcNow;

        dbContext.TokenTransactions.Add(new TokenTransaction
        {
            Id = Guid.NewGuid(),
            UserId = currentUser.UserId,
            UserKey = currentUser.UserId.ToString(),
            Type = type,
            Amount = amount,
            BalanceAfter = balance.Balance,
            Description = description,
            CreatedAt = DateTime.UtcNow
        });

        await dbContext.SaveChangesAsync(cancellationToken);
        return balance;
    }

    public async Task<IReadOnlyList<TokenTransaction>> GetRecentTransactionsAsync(
        int take = 20,
        CancellationToken cancellationToken = default)
    {
        var userId = currentUser.UserId;

        return await dbContext.TokenTransactions
            .Where(t => t.UserId == userId)
            .OrderByDescending(t => t.CreatedAt)
            .Take(take)
            .ToListAsync(cancellationToken);
    }
}
