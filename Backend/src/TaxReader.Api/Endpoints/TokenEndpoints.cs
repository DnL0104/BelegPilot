using TaxReader.Application.DTOs;
using TaxReader.Application.Interfaces;

namespace TaxReader.Api.Endpoints;

public static class TokenEndpoints
{
    public static RouteGroupBuilder MapTokenEndpoints(this RouteGroupBuilder group)
    {
        var tokens = group.MapGroup("/tokens").WithTags("Tokens");

        tokens.MapGet("/balance", async (
            ITokenService tokenService,
            CancellationToken cancellationToken) =>
        {
            var balance = await tokenService.GetOrCreateBalanceAsync(cancellationToken);
            return Results.Ok(new TokenBalanceDto(balance.Balance, balance.UpdatedAt));
        })
        .WithName("GetTokenBalance")
        .WithSummary("Returns the current AI credit balance.");

        tokens.MapGet("/transactions", async (
            ITokenService tokenService,
            CancellationToken cancellationToken,
            int take = 20) =>
        {
            var transactions = await tokenService.GetRecentTransactionsAsync(take, cancellationToken);
            var dtos = transactions.Select(t => new TokenTransactionDto(
                t.Id,
                t.Type.ToString(),
                t.Amount,
                t.BalanceAfter,
                t.Description,
                t.RelatedItemId,
                t.CreatedAt));
            return Results.Ok(dtos);
        })
        .WithName("GetTokenTransactions")
        .WithSummary("Returns the most recent token transactions.");

        // POST /tokens/purchase stub removed — replaced by real POST /payments/checkout endpoint
        // that creates a Stripe Checkout session (PAY-01, Phase 5)

        return group;
    }
}
