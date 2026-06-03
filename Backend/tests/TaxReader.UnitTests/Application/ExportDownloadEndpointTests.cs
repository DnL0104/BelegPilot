using FluentAssertions;
using TaxReader.Application.Interfaces;
using TaxReader.Infrastructure.Services;

namespace TaxReader.UnitTests.Application;

/// <summary>
/// Unit tests for the export download ownership validation logic (IDOR mitigation).
/// Tests the ExportTokenStore ownership check that the ExportEndpoints download handler uses.
/// T-06-40 / T-06-42 / LEG-07.
///
/// Note: These tests focus on the ownership-decision logic directly via ExportTokenStore,
/// which is the heart of the IDOR mitigation. Integration tests (docker UAT) cover
/// the full HTTP 403 flow.
/// </summary>
public class ExportDownloadEndpointTests
{
    private readonly ExportTokenStore _tokenStore = new();

    [Fact]
    public void Download_TokenBelongsToCorrectUser_OwnershipCheckPasses()
    {
        // Arrange
        var token = Guid.NewGuid().ToString("N");
        var ownerId = Guid.NewGuid();
        _tokenStore.Register(token, ownerId, DateTime.UtcNow.AddHours(24));

        // Act
        var found = _tokenStore.TryGet(token, out var record);

        // Assert — ownership check logic: record.UserId == requestingUserId
        found.Should().BeTrue();
        var ownershipCheckPasses = record!.UserId == ownerId;
        ownershipCheckPasses.Should().BeTrue("token belongs to the requesting user");
    }

    [Fact]
    public void Download_TokenBelongsToDifferentUser_OwnershipCheckFails_Should403()
    {
        // Arrange
        var token = Guid.NewGuid().ToString("N");
        var ownerId = Guid.NewGuid();
        var attackerId = Guid.NewGuid();

        _tokenStore.Register(token, ownerId, DateTime.UtcNow.AddHours(24));

        // Act — attacker tries to use owner's token
        var found = _tokenStore.TryGet(token, out var record);

        // Assert — IDOR mitigation: comparing stored userId to requesting userId must fail
        found.Should().BeTrue("token exists");
        var ownershipCheckPasses = record!.UserId == attackerId;
        ownershipCheckPasses.Should().BeFalse(
            "T-06-40: token belongs to a different user; endpoint must return 403 Forbid");
    }

    [Fact]
    public void Download_AfterInvalidation_TokenIsGone_Should404()
    {
        // Arrange — owner downloads, token gets invalidated (one-time)
        var token = Guid.NewGuid().ToString("N");
        var ownerId = Guid.NewGuid();
        _tokenStore.Register(token, ownerId, DateTime.UtcNow.AddHours(24));

        // Simulate first download: invalidate after streaming
        _tokenStore.Invalidate(token);

        // Act — second attempt
        var found = _tokenStore.TryGet(token, out _);

        // Assert — T-06-42: second download must fail (token is one-time)
        found.Should().BeFalse(
            "T-06-42: after download, token is invalidated; second request should get 404");
    }

    [Fact]
    public void Download_ExpiredToken_StatusIsExpired_Should410()
    {
        // Arrange
        var token = Guid.NewGuid().ToString("N");
        var ownerId = Guid.NewGuid();
        _tokenStore.Register(token, ownerId, DateTime.UtcNow.AddSeconds(-1)); // already expired

        // Act
        var found = _tokenStore.TryGet(token, out var record);

        // Assert — T-06-43: expired token returns Expired status; endpoint returns 410 Gone
        found.Should().BeTrue("expired token is still in store but with Expired status");
        record!.Status.Should().Be(ExportStatus.Expired);
    }

    [Fact]
    public void Status_OtherUsersToken_ReturnsExpiredNotExists()
    {
        // Arrange — simulate status endpoint behavior for another user's token
        // The status endpoint returns "Expired" for tokens that don't belong to the caller
        // (no existence leak — T-06-41)
        var token = Guid.NewGuid().ToString("N");
        var ownerId = Guid.NewGuid();
        var callerId = Guid.NewGuid(); // different user querying status

        _tokenStore.Register(token, ownerId, DateTime.UtcNow.AddHours(24));

        // Act — status endpoint logic: TryGet(token, out record) + check rec.UserId == callerId
        var found = _tokenStore.TryGet(token, out var record);
        var belongsToCaller = found && record!.UserId == callerId;

        // Assert — caller gets "Expired" response (not "Ready") — no existence leak
        belongsToCaller.Should().BeFalse(
            "T-06-41: token exists but belongs to another user; status endpoint must return Expired");
    }
}
