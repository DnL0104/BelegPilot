using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Serilog.Context;
using TaxReader.Application.Interfaces;
using TaxReader.Domain.Entities;
using TaxReader.Domain.Enums;

namespace TaxReader.Application.Jobs;

public class GrantTokensJob(IAppDbContext dbContext, ILogger<GrantTokensJob> logger)
{
    [AutomaticRetry(Attempts = 3, DelaysInSeconds = new[] { 30, 120, 300 })]
    public async Task HandleAsync(Guid userId, int credits, CancellationToken cancellationToken)
    {
        using var _scope = LogContext.PushProperty("JobId", $"Grant_{userId}_{credits}");

        // Pitfall 3: ITokenService depends on ICurrentUser (HTTP context) — not injectable in Hangfire jobs.
        // Access IAppDbContext directly to update balance + write transaction row.
        var balance = await dbContext.UserTokenBalances
            .FirstOrDefaultAsync(b => b.UserId == userId, cancellationToken);

        if (balance is null)
        {
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

        balance.Balance += credits;
        balance.UpdatedAt = DateTime.UtcNow;

        dbContext.TokenTransactions.Add(new TokenTransaction
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            UserKey = userId.ToString(),
            Type = TokenTransactionType.Purchase,
            Amount = credits,
            BalanceAfter = balance.Balance,
            Description = $"Zahlung bestätigt: {credits} Credits",
            CreatedAt = DateTime.UtcNow
        });

        // Update the most recent Pending payment for this user+credits to Granted
        var payment = await dbContext.Payments
            .Where(p => p.UserId == userId && p.Status == PaymentStatus.Pending && p.CreditsGranted == credits)
            .OrderByDescending(p => p.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
        if (payment is not null)
        {
            payment.Status = PaymentStatus.Granted;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Granted {Credits} tokens to User {UserId} — new balance: {Balance}",
            credits, userId, balance.Balance);
    }
}
