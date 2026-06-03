using FluentAssertions;
using TaxReader.Infrastructure.Services;

namespace TaxReader.UnitTests.Application;

/// <summary>
/// Unit tests for ExportTokenStore (in-memory singleton token registry for DSGVO Art. 20 exports).
/// LEG-07 / T-06-40 / T-06-42.
/// </summary>
public class ExportTokenStoreTests
{
    private readonly ExportTokenStore _store = new();

    [Fact]
    public void Register_ValidToken_TryGetReturnsReadyRecord()
    {
        // Arrange
        var token = Guid.NewGuid().ToString("N");
        var userId = Guid.NewGuid();
        var expiry = DateTime.UtcNow.AddHours(24);

        // Act
        _store.Register(token, userId, expiry);

        // Assert
        var found = _store.TryGet(token, out var record);
        found.Should().BeTrue();
        record.Should().NotBeNull();
        record!.UserId.Should().Be(userId);
        record.Status.Should().Be(ExportStatus.Ready);
        record.ExpiresAtUtc.Should().Be(expiry);
    }

    [Fact]
    public void Invalidate_ExistingToken_TryGetReturnsFalse()
    {
        // Arrange
        var token = Guid.NewGuid().ToString("N");
        var userId = Guid.NewGuid();
        _store.Register(token, userId, DateTime.UtcNow.AddHours(24));

        // Act
        _store.Invalidate(token);

        // Assert
        var found = _store.TryGet(token, out _);
        found.Should().BeFalse("token was invalidated and should not be retrievable");
    }

    [Fact]
    public void TryGet_ExpiredToken_ReturnsExpiredStatus()
    {
        // Arrange — register with expiry in the past
        var token = Guid.NewGuid().ToString("N");
        var userId = Guid.NewGuid();
        _store.Register(token, userId, DateTime.UtcNow.AddSeconds(-1));

        // Act
        var found = _store.TryGet(token, out var record);

        // Assert — found=true but status is Expired
        found.Should().BeTrue();
        record!.Status.Should().Be(ExportStatus.Expired);
    }

    [Fact]
    public void MarkGenerating_Token_TryGetReturnsGeneratingStatus()
    {
        // Arrange
        var token = Guid.NewGuid().ToString("N");
        var userId = Guid.NewGuid();

        // Act
        _store.MarkGenerating(token, userId, DateTime.UtcNow.AddHours(24));

        // Assert
        var found = _store.TryGet(token, out var record);
        found.Should().BeTrue();
        record!.Status.Should().Be(ExportStatus.Generating);
        record.UserId.Should().Be(userId);
    }

    [Fact]
    public void TryGet_UnknownToken_ReturnsFalse()
    {
        // Act
        var found = _store.TryGet("nonexistent_token", out var record);

        // Assert
        found.Should().BeFalse();
        record.Should().BeNull();
    }

    [Fact]
    public void Invalidate_UnknownToken_DoesNotThrow()
    {
        // Act
        var act = () => _store.Invalidate("doesnotexist");

        // Assert
        act.Should().NotThrow();
    }
}
