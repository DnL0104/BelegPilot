using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using TaxReader.Application.Interfaces;
using TaxReader.Domain.Entities;
using TaxReader.Domain.Enums;
using TaxReader.Infrastructure.Configuration;
using TaxReader.Infrastructure.Data;
using TaxReader.Infrastructure.Services;
using TaxReader.UnitTests.Helpers;

namespace TaxReader.UnitTests.Services;

/// <summary>
/// Covers AiOnlyClassificationService branches: not-configured, insufficient tokens,
/// refund-on-Unknown, refund-on-failure, and auto-confirm threshold (above/below).
/// IAiClassifier and ITokenService are both mocked — no real Anthropic call or DB ledger.
/// </summary>
public class AiOnlyClassificationServiceTests : IDisposable
{
    private static readonly Guid TestUserId = Guid.Parse("eeeeeeee-1111-2222-3333-444444444444");

    private readonly AppDbContext _dbContext;
    private readonly Mock<IAiClassifier> _aiClassifierMock;
    private readonly Mock<ITokenService> _tokenServiceMock;

    public AiOnlyClassificationServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new AppDbContext(options);

        // Seed a user with an auto-confirm threshold for threshold tests
        var user = TestDataFactory.CreateRegularUser("ai-test@test.local");
        user.Id = TestUserId;
        user.AutoConfirmThreshold = 0.80;
        _dbContext.Users.Add(user);
        _dbContext.SaveChanges();

        _aiClassifierMock = new Mock<IAiClassifier>();
        _tokenServiceMock = new Mock<ITokenService>();

        // Default: tokens available
        _tokenServiceMock
            .Setup(t => t.TryConsumeManyAsync(It.IsAny<IReadOnlyList<TokenLedgerEntry>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _tokenServiceMock
            .Setup(t => t.RefundManyAsync(It.IsAny<IReadOnlyList<TokenLedgerEntry>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserTokenBalance { Id = Guid.NewGuid(), UserId = TestUserId, UserKey = TestUserId.ToString(), Balance = 10 });
    }

    private AiOnlyClassificationService BuildService(AnthropicOptions? options = null)
    {
        var opts = options ?? new AnthropicOptions { ApiKey = "test-key", CostPerClassification = 1 };
        return new AiOnlyClassificationService(
            _aiClassifierMock.Object,
            _tokenServiceMock.Object,
            _dbContext,
            Options.Create(opts),
            NullLogger<AiOnlyClassificationService>.Instance);
    }

    [Fact]
    public async Task ClassifyItemsAsync_AiNotConfigured_ReturnsUnknownWithGermanReason()
    {
        _aiClassifierMock.Setup(a => a.IsConfigured).Returns(false);
        var service = BuildService();

        var item = TestDataFactory.CreateReceiptItem();
        var results = await service.ClassifyItemsAsync([item], TestUserId);

        results.Should().HaveCount(1);
        results[0].Category.Should().Be(Category.Unbekannt);
        results[0].Reason.Should().Be("AI-Klassifizierung nicht konfiguriert.");

        // No token consumption when AI is not configured
        _tokenServiceMock.Verify(
            t => t.TryConsumeManyAsync(It.IsAny<IReadOnlyList<TokenLedgerEntry>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ClassifyItemsAsync_InsufficientTokens_ReturnsUnknownAndDoesNotCallAi()
    {
        _aiClassifierMock.Setup(a => a.IsConfigured).Returns(true);
        _tokenServiceMock
            .Setup(t => t.TryConsumeManyAsync(It.IsAny<IReadOnlyList<TokenLedgerEntry>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var service = BuildService();

        var item = TestDataFactory.CreateReceiptItem();
        var results = await service.ClassifyItemsAsync([item], TestUserId);

        results.Should().HaveCount(1);
        results[0].Category.Should().Be(Category.Unbekannt);
        results[0].Reason.Should().Be("Keine Tokens verfügbar – bitte Credits aufladen.");

        _aiClassifierMock.Verify(
            a => a.ClassifyBatchAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "the AI must not be called when there are insufficient tokens");
    }

    [Fact]
    public async Task ClassifyItemsAsync_AiReturnsUnbekannt_RefundsThatItem()
    {
        _aiClassifierMock.Setup(a => a.IsConfigured).Returns(true);
        _aiClassifierMock
            .Setup(a => a.ClassifyBatchAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new AiClassificationResult(Category.Unbekannt, "nicht erkannt", 0.0)]);
        var service = BuildService();

        var item = TestDataFactory.CreateReceiptItem();
        var results = await service.ClassifyItemsAsync([item], TestUserId);

        results.Should().HaveCount(1);
        results[0].Category.Should().Be(Category.Unbekannt);

        _tokenServiceMock.Verify(
            t => t.RefundManyAsync(
                It.Is<IReadOnlyList<TokenLedgerEntry>>(entries => entries.Count == 1 && entries[0].RelatedItemId == item.Id),
                It.IsAny<CancellationToken>()),
            Times.Once,
            "a refund must be issued for the item the AI returned Unbekannt for");
    }

    [Fact]
    public async Task ClassifyItemsAsync_AiThrows_RefundsAllAndReturnsUnknown()
    {
        _aiClassifierMock.Setup(a => a.IsConfigured).Returns(true);
        _aiClassifierMock
            .Setup(a => a.ClassifyBatchAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Anthropic API unavailable"));
        var service = BuildService();

        var item = TestDataFactory.CreateReceiptItem(description: "Crashy item");
        var results = await service.ClassifyItemsAsync([item], TestUserId);

        results.Should().HaveCount(1);
        results[0].Category.Should().Be(Category.Unbekannt);
        results[0].Reason.Should().StartWith("AI-Fehler:");

        _tokenServiceMock.Verify(
            t => t.RefundManyAsync(
                It.Is<IReadOnlyList<TokenLedgerEntry>>(entries => entries.Count == 1),
                It.IsAny<CancellationToken>()),
            Times.Once,
            "all pre-charged tokens must be refunded when ClassifyBatchAsync throws");
    }

    [Fact]
    public async Task ClassifyItemsAsync_ConfidenceAboveThreshold_MarksConfirmed()
    {
        // AutoConfirmThreshold = 0.80; confidence = 0.90 → should be Confirmed
        _aiClassifierMock.Setup(a => a.IsConfigured).Returns(true);
        _aiClassifierMock
            .Setup(a => a.ClassifyBatchAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new AiClassificationResult(Category.WerbungskostenBueromaterial, "clearly office supplies", 0.90)]);
        var service = BuildService();

        var item = TestDataFactory.CreateReceiptItem();
        var results = await service.ClassifyItemsAsync([item], TestUserId);

        results.Should().HaveCount(1);
        results[0].Status.Should().Be(ClassificationStatus.Confirmed);
        results[0].Reason.Should().StartWith("Auto-bestätigt");
    }

    [Fact]
    public async Task ClassifyItemsAsync_ConfidenceBelowThreshold_MarksSuggested()
    {
        // AutoConfirmThreshold = 0.80; confidence = 0.60 → should be Suggested
        _aiClassifierMock.Setup(a => a.IsConfigured).Returns(true);
        _aiClassifierMock
            .Setup(a => a.ClassifyBatchAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new AiClassificationResult(Category.WerbungskostenBueromaterial, "possibly office supplies", 0.60)]);
        var service = BuildService();

        var item = TestDataFactory.CreateReceiptItem();
        var results = await service.ClassifyItemsAsync([item], TestUserId);

        results.Should().HaveCount(1);
        results[0].Status.Should().Be(ClassificationStatus.Suggested);
        results[0].Reason.Should().NotStartWith("Auto-bestätigt");
    }

    public void Dispose() => _dbContext.Dispose();
}
