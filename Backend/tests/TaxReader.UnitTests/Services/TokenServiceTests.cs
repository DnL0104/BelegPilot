using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using TaxReader.Application.Interfaces;
using TaxReader.Domain.Enums;
using TaxReader.Infrastructure.Data;
using TaxReader.Infrastructure.Services;

namespace TaxReader.UnitTests.Services;

/// <summary>
/// Covers TokenService ledger operations: balance seeding, consume (sufficient/insufficient),
/// refund, and the AddTokensAsync guard on non-positive amounts.
/// ICurrentUser is mocked to return a fixed UserId; DB is in-memory.
/// </summary>
public class TokenServiceTests : IDisposable
{
    private static readonly Guid TestUserId = Guid.Parse("dddddddd-1111-2222-3333-444444444444");

    private readonly AppDbContext _dbContext;
    private readonly TokenService _service;

    public TokenServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new AppDbContext(options);

        var currentUserMock = new Mock<ICurrentUser>();
        currentUserMock.Setup(c => c.UserId).Returns(TestUserId);

        _service = new TokenService(_dbContext, currentUserMock.Object);
    }

    [Fact]
    public async Task GetOrCreateBalanceAsync_NewUser_SeedsTenFreeTokensAndWelcomeBonus()
    {
        var balance = await _service.GetOrCreateBalanceAsync();

        balance.Balance.Should().Be(10, "a new user receives 10 initial free tokens");
        balance.UserId.Should().Be(TestUserId);

        var welcomeTx = await _dbContext.TokenTransactions
            .FirstOrDefaultAsync(t => t.UserId == TestUserId && t.Description == "Welcome bonus");
        welcomeTx.Should().NotBeNull("a Welcome bonus transaction must be written when seeding the balance");
        welcomeTx?.Amount.Should().Be(10);
        welcomeTx?.BalanceAfter.Should().Be(10);
    }

    [Fact]
    public async Task TryConsumeManyAsync_InsufficientBalance_ReturnsFalse()
    {
        // Seed a balance of 1 token
        await _service.GetOrCreateBalanceAsync();
        var balance = await _dbContext.UserTokenBalances.FirstAsync(b => b.UserId == TestUserId);
        balance.Balance = 1;
        await _dbContext.SaveChangesAsync();

        var entries = new List<TokenLedgerEntry>
        {
            new(3, "item A", Guid.NewGuid())
        };

        var result = await _service.TryConsumeManyAsync(entries);

        result.Should().BeFalse("balance (1) is less than total cost (3)");
    }

    [Fact]
    public async Task TryConsumeManyAsync_SufficientBalance_WritesConsumptionRows()
    {
        // Default seed gives 10 tokens
        await _service.GetOrCreateBalanceAsync();

        var itemId = Guid.NewGuid();
        var entries = new List<TokenLedgerEntry>
        {
            new(2, "AI classification: Item A", itemId)
        };

        var result = await _service.TryConsumeManyAsync(entries);

        result.Should().BeTrue();

        var balanceAfter = (await _dbContext.UserTokenBalances.FirstAsync(b => b.UserId == TestUserId)).Balance;
        balanceAfter.Should().Be(8, "10 initial − 2 consumed = 8");

        var consumptionTx = await _dbContext.TokenTransactions
            .FirstOrDefaultAsync(t => t.UserId == TestUserId && t.Type == TokenTransactionType.Consumption);
        consumptionTx.Should().NotBeNull();
        consumptionTx!.Amount.Should().Be(-2, "consume writes a negative Amount equal to the entry cost");
        consumptionTx.RelatedItemId.Should().Be(itemId);
    }

    [Fact]
    public async Task RefundManyAsync_WritesRefundRows()
    {
        // Seed then consume so we have a non-trivial balance to refund
        await _service.GetOrCreateBalanceAsync();
        var consumeEntries = new List<TokenLedgerEntry> { new(3, "charge", Guid.NewGuid()) };
        await _service.TryConsumeManyAsync(consumeEntries);

        var refundItemId = Guid.NewGuid();
        var refundEntries = new List<TokenLedgerEntry>
        {
            new(3, "Refund — AI could not classify: Item X", refundItemId)
        };

        var balanceResult = await _service.RefundManyAsync(refundEntries);

        balanceResult.Balance.Should().Be(10, "consumed 3 then refunded 3 → back to 10");

        var refundTx = await _dbContext.TokenTransactions
            .FirstOrDefaultAsync(t => t.UserId == TestUserId && t.Type == TokenTransactionType.Refund);
        refundTx.Should().NotBeNull();
        refundTx!.Amount.Should().Be(3, "refund writes a positive Amount");
        refundTx.RelatedItemId.Should().Be(refundItemId);
    }

    [Fact]
    public async Task AddTokensAsync_NonPositiveAmount_ThrowsArgumentOutOfRange()
    {
        await _service.GetOrCreateBalanceAsync();

        var actZero = () => _service.AddTokensAsync(0, TokenTransactionType.Adjustment, "zero");
        var actNegative = () => _service.AddTokensAsync(-1, TokenTransactionType.Adjustment, "negative");

        await actZero.Should().ThrowAsync<ArgumentOutOfRangeException>();
        await actNegative.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    public void Dispose() => _dbContext.Dispose();
}
