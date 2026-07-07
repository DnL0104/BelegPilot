using System.Net.Http;
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
            .Setup(t => t.TryConsumeManyAsync(It.IsAny<IReadOnlyList<TokenLedgerEntry>>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _tokenServiceMock
            .Setup(t => t.RefundManyAsync(It.IsAny<IReadOnlyList<TokenLedgerEntry>>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserTokenBalance { Id = Guid.NewGuid(), UserId = TestUserId, UserKey = TestUserId.ToString(), Balance = 10 });
    }

    private AiOnlyClassificationService BuildService(AnthropicOptions? options = null)
    {
        // RetryDelay = Zero: retry-path tests must not pay the real 2s wait (IN-01).
        var opts = options ?? new AnthropicOptions { ApiKey = "test-key", CostPerClassification = 1, RetryDelay = TimeSpan.Zero };
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
            t => t.TryConsumeManyAsync(It.IsAny<IReadOnlyList<TokenLedgerEntry>>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ClassifyItemsAsync_InsufficientTokens_ReturnsUnknownAndDoesNotCallAi()
    {
        _aiClassifierMock.Setup(a => a.IsConfigured).Returns(true);
        _tokenServiceMock
            .Setup(t => t.TryConsumeManyAsync(It.IsAny<IReadOnlyList<TokenLedgerEntry>>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
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
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()),
            Times.Once,
            "a refund must be issued for the item the AI returned Unbekannt for");
    }

    [Fact]
    public async Task ClassifyItemsAsync_AiThrows_RefundsAllAndReturnsFailedClassification()
    {
        // After retry-once hardening (Task 2): AI throws on both attempts → Failed, not Suggested.
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
        // CLS-01: technical failure must produce Failed, not Suggested + Unbekannt
        results[0].Status.Should().Be(ClassificationStatus.Failed);

        _tokenServiceMock.Verify(
            t => t.RefundManyAsync(
                It.Is<IReadOnlyList<TokenLedgerEntry>>(entries => entries.Count == 1),
                It.IsAny<Guid>(),
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

    // ── NEW TESTS (Task 1 / Wave 0 – RED) ──────────────────────────────────────
    // These tests exercise retry/Failed, chunking, partial success, and
    // cancellation-no-retry behaviour that does NOT yet exist in the single-batch
    // implementation. They are expected to FAIL (RED) against the current code.
    // Task 2 makes them GREEN by replacing the single-batch body with chunked +
    // retry-once logic.

    [Fact]
    public async Task ClassifyItemsAsync_AiFailsTwice_ReturnsFailedClassifications()
    {
        // Arrange: AI always throws → retry-once path exhausted → items must be Failed
        _aiClassifierMock.Setup(a => a.IsConfigured).Returns(true);
        _aiClassifierMock
            .Setup(a => a.ClassifyBatchAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("timeout"));

        var service = BuildService();
        var item = TestDataFactory.CreateReceiptItem(description: "Failsafe item");

        // Act
        var results = await service.ClassifyItemsAsync([item], TestUserId);

        // Assert: status MUST be Failed, not Suggested
        results.Should().HaveCount(1);
        results[0].Status.Should().Be(ClassificationStatus.Failed,
            "a technical AI failure after retry must produce Failed, never Suggested + Unbekannt");
        results[0].Category.Should().Be(Category.Unbekannt,
            "model never ran — Unbekannt is correct on the Failed item");

        // Refund must be issued for the failed chunk
        _tokenServiceMock.Verify(
            t => t.RefundManyAsync(
                It.Is<IReadOnlyList<TokenLedgerEntry>>(entries => entries.Count == 1),
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()),
            Times.Once,
            "tokens pre-charged for the failed chunk must be fully refunded");
    }

    [Fact]
    public async Task ClassifyItemsAsync_AiFailsThenSucceeds_RetriesOnceAndSucceeds()
    {
        // Arrange: first call throws, second call returns a valid result
        // Uses SetupSequence so Moq alternates throw → return across consecutive calls.
        _aiClassifierMock.Setup(a => a.IsConfigured).Returns(true);
        _aiClassifierMock
            .SetupSequence(a => a.ClassifyBatchAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("transient error"))
            .ReturnsAsync(new List<AiClassificationResult>
            {
                new AiClassificationResult(Category.WerbungskostenBueromaterial, "USB cable", 0.75)
            });

        var service = BuildService();
        var item = TestDataFactory.CreateReceiptItem(description: "USB cable");

        // Act
        var results = await service.ClassifyItemsAsync([item], TestUserId);

        // Assert: retry succeeded — items are classified (not Failed)
        results.Should().HaveCount(1);
        results[0].Status.Should().NotBe(ClassificationStatus.Failed,
            "a successful retry must not produce a Failed classification");
        results[0].Category.Should().Be(Category.WerbungskostenBueromaterial);

        // AI must have been called exactly twice (first attempt + one retry)
        _aiClassifierMock.Verify(
            a => a.ClassifyBatchAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2),
            "exactly one retry should be attempted before giving up");

        // No full-chunk refund because the chunk ultimately succeeded
        _tokenServiceMock.Verify(
            t => t.RefundManyAsync(
                It.Is<IReadOnlyList<TokenLedgerEntry>>(entries => entries.Count == 1 && entries[0].RelatedItemId == item.Id),
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()),
            Times.Never,
            "successful chunk should not trigger a full chunk-level refund");
    }

    [Fact]
    public async Task ClassifyItemsAsync_60Items_Makes2AiCalls()
    {
        // Arrange: 60 items → 2 chunks (50 + 10) → 2 ClassifyBatchAsync calls.
        // The mock returns a fixed-size response; the actual per-call chunk size is
        // verified through the call-count assertions below.
        _aiClassifierMock.Setup(a => a.IsConfigured).Returns(true);

        // Return a 50-item response for the first chunk and a 10-item response for the second.
        var chunk1Response = Enumerable.Range(1, 50)
            .Select(_ => new AiClassificationResult(Category.WerbungskostenBueromaterial, "office supply", 0.80))
            .ToList<AiClassificationResult>();
        var chunk2Response = Enumerable.Range(1, 10)
            .Select(_ => new AiClassificationResult(Category.WerbungskostenBueromaterial, "office supply", 0.80))
            .ToList<AiClassificationResult>();

        _aiClassifierMock
            .SetupSequence(a => a.ClassifyBatchAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(chunk1Response)
            .ReturnsAsync(chunk2Response);

        var service = BuildService();
        var items = Enumerable.Range(1, 60)
            .Select(i => TestDataFactory.CreateReceiptItem(description: $"Item {i}", lineNumber: i))
            .ToList();

        // Act
        var results = await service.ClassifyItemsAsync(items, TestUserId);

        // Assert: all 60 items classified
        results.Should().HaveCount(60);

        // 2 ClassifyBatchAsync calls (chunks 50 + 10)
        _aiClassifierMock.Verify(
            a => a.ClassifyBatchAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2),
            "60 items must be split into 2 chunks (50 + 10), producing exactly 2 AI calls");

        // 2 TryConsumeManyAsync calls (per-chunk pre-charge)
        _tokenServiceMock.Verify(
            t => t.TryConsumeManyAsync(It.IsAny<IReadOnlyList<TokenLedgerEntry>>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2),
            "token consumption must be pre-charged once per chunk, not once per receipt");
    }

    [Fact]
    public async Task ClassifyItemsAsync_Chunk2Fails_Chunk1ItemsNotFailed()
    {
        // Arrange: 51 items → chunk1=50 succeeds, chunk2=1 throws on both attempts.
        // Uses SetupSequence: call 1 (chunk1) returns; calls 2+3 (chunk2 attempt + retry) throw.
        _aiClassifierMock.Setup(a => a.IsConfigured).Returns(true);

        // Build a 50-item success response (chunk 1)
        var chunk1Response = Enumerable.Range(1, 50)
            .Select(_ => new AiClassificationResult(Category.WerbungskostenBueromaterial, "success", 0.80))
            .ToList<AiClassificationResult>();

        _aiClassifierMock
            .SetupSequence(a => a.ClassifyBatchAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(chunk1Response)                       // call 1 — chunk 1 first attempt succeeds
            .ThrowsAsync(new HttpRequestException("chunk 2 attempt 1"))  // call 2 — chunk 2 first attempt
            .ThrowsAsync(new HttpRequestException("chunk 2 attempt 2")); // call 3 — chunk 2 retry

        var service = BuildService();
        var items = Enumerable.Range(1, 51)
            .Select(i => TestDataFactory.CreateReceiptItem(description: $"Item {i}", lineNumber: i))
            .ToList();

        // Act
        var results = await service.ClassifyItemsAsync(items, TestUserId);

        // Assert: all 51 items returned
        results.Should().HaveCount(51);

        // Chunk 1 (first 50) should be Suggested/Confirmed — NOT Failed (partial success D-08)
        var chunk1Results = results.Take(50).ToList();
        chunk1Results.Should().AllSatisfy(r =>
            r.Status.Should().NotBe(ClassificationStatus.Failed,
                "chunk 1 succeeded — its items must not be marked Failed"));

        // Chunk 2 (item 51) must be Failed
        var chunk2Item = results.Skip(50).Single();
        chunk2Item.Status.Should().Be(ClassificationStatus.Failed,
            "chunk 2 failed on both attempts — its items must be Failed (D-08 partial success)");
    }

    [Fact]
    public async Task ClassifyItemsAsync_CancellationRequested_DoesNotRetry()
    {
        // Arrange: cancellation is triggered — OperationCanceledException must NOT be
        // swallowed into a retry (Pitfall 3). The test cancels immediately so the first
        // attempt's OperationCanceledException propagates rather than triggering a retry.
        _aiClassifierMock.Setup(a => a.IsConfigured).Returns(true);
        using var cts = new CancellationTokenSource();

        _aiClassifierMock
            .Setup(a => a.ClassifyBatchAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .Callback(() => cts.Cancel())   // cancel on first call
            .ThrowsAsync(new OperationCanceledException());

        var service = BuildService();
        var item = TestDataFactory.CreateReceiptItem(description: "Cancelled item");

        // Act + Assert: OperationCanceledException propagates (is not swallowed into retry)
        await service.Invoking(s => s.ClassifyItemsAsync([item], TestUserId, cts.Token))
            .Should().ThrowAsync<OperationCanceledException>(
                "cancellation must propagate — do not retry a cancelled request (Pitfall 3)");

        // AI was called exactly once — no retry on cancellation
        _aiClassifierMock.Verify(
            a => a.ClassifyBatchAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()),
            Times.Once,
            "the retry path must be skipped when cancellation is requested");
    }

    public void Dispose() => _dbContext.Dispose();
}
