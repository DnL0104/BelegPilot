using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Serilog.Context;
using TaxReader.Application.Interfaces;
using TaxReader.Domain.Entities;
using TaxReader.Domain.Enums;

namespace TaxReader.Application.Jobs;

public class RevokeTokensJob(IAppDbContext dbContext, ILogger<RevokeTokensJob> logger)
{
    [AutomaticRetry(Attempts = 3, DelaysInSeconds = new[] { 30, 120, 300 })]
    public async Task HandleAsync(Guid userId, int credits, CancellationToken cancellationToken)
    {
        using var _scope = LogContext.PushProperty("JobId", $"Revoke_{userId}_{credits}");

        // Pitfall 3: ITokenService depends on ICurrentUser (HTTP context) — not injectable in Hangfire jobs.
        // Access IAppDbContext directly to update balance + write transaction row.
        var balance = await dbContext.UserTokenBalances
            .FirstOrDefaultAsync(b => b.UserId == userId, cancellationToken);

        if (balance is null)
        {
            // D-11: create a negative balance record — user owes tokens
            balance = new UserTokenBalance
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                UserKey = userId.ToString(),
                Balance = 0,
                UpdatedAt = DateTime.UtcNow
            };
            dbContext.UserTokenBalances.Add(balance);
        }

        // D-11: balance can go negative — no floor check
        balance.Balance -= credits;
        balance.UpdatedAt = DateTime.UtcNow;

        dbContext.TokenTransactions.Add(new TokenTransaction
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            UserKey = userId.ToString(),
            Type = TokenTransactionType.Refund,
            Amount = -credits,
            BalanceAfter = balance.Balance,
            Description = $"Rückerstattung: {credits} Credits zurückgebucht",
            CreatedAt = DateTime.UtcNow
        });

        // Update the most recent Granted payment for this user+credits to Revoked
        var payment = await dbContext.Payments
            .Where(p => p.UserId == userId && p.Status == PaymentStatus.Granted && p.CreditsGranted == credits)
            .OrderByDescending(p => p.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
        if (payment is not null)
        {
            payment.Status = PaymentStatus.Revoked;
            payment.RevokedAt = DateTime.UtcNow;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation(
            "Revoked {Credits} tokens from User {UserId} — new balance: {Balance}",
            credits, userId, balance.Balance);
    }
}
