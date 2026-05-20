using TaxReader.Application.DTOs;
using TaxReader.Application.Interfaces;
using TaxReader.Domain.Enums;

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

        tokens.MapPost("/purchase", async (
            PurchaseTokensRequest request,
            ITokenService tokenService,
            CancellationToken cancellationToken) =>
        {
            if (request.Amount <= 0 || request.Amount > 10_000)
                return Results.BadRequest(new { error = "Amount must be between 1 and 10000." });

            var balance = await tokenService.AddTokensAsync(
                request.Amount,
                TokenTransactionType.Purchase,
                $"Token purchase ({request.Amount} credits)",
                cancellationToken);

            return Results.Ok(new TokenBalanceDto(balance.Balance, balance.UpdatedAt));
        })
        .WithName("PurchaseTokens")
        .WithSummary("Simulated token top-up. No payment processing yet.");

        return group;
    }
}

public record PurchaseTokensRequest(int Amount);
