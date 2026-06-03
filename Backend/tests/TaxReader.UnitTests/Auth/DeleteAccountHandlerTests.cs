using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using TaxReader.Application.Commands;
using TaxReader.Application.DTOs;
using TaxReader.Application.Interfaces;
using TaxReader.Domain.Entities;
using TaxReader.Domain.Enums;
using TaxReader.Infrastructure.Data;

namespace TaxReader.UnitTests.Auth;

/// <summary>
/// AUTH-02: DeleteAccountHandler password-reauth, defense-in-depth refresh-token
/// revoke, cascade delete. In-memory EF DB + Moq for ICurrentUser, IRefreshTokenService,
/// and IAuditLogger.
/// </summary>
public class DeleteAccountHandlerTests : IDisposable
{
    private static readonly Guid TestUserId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");

    private readonly AppDbContext _dbContext;
    private readonly Mock<ICurrentUser> _currentUserMock;
    private readonly Mock<IRefreshTokenService> _refreshTokenServiceMock;
    private readonly Mock<IAuditLogger> _auditLoggerMock;
    private readonly DeleteAccountHandler _handler;

    public DeleteAccountHandlerTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContext = new AppDbContext(options);
        _currentUserMock = new Mock<ICurrentUser>();
        _currentUserMock.Setup(u => u.UserId).Returns(TestUserId);
        _refreshTokenServiceMock = new Mock<IRefreshTokenService>();
        _refreshTokenServiceMock
            .Setup(s => s.RevokeAllForUserAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        _auditLoggerMock = new Mock<IAuditLogger>();
        _auditLoggerMock
            .Setup(a => a.RecordAsync(
                It.IsAny<AuditAction>(),
                It.IsAny<Guid?>(),
                It.IsAny<Guid?>(),
                It.IsAny<Dictionary<string, object?>>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _handler = new DeleteAccountHandler(
            _dbContext,
            _currentUserMock.Object,
            _refreshTokenServiceMock.Object,
            _auditLoggerMock.Object);
    }

    [Fact]
    public async Task CorrectPassword_Returns204_AndCascadeDeletes()
    {
        var user = new User
        {
            Id = TestUserId,
            Email = "test@example.de",
            DisplayName = "Test",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("mypassword123"),
        };
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        var result = await _handler.HandleAsync(new DeleteAccountRequest("mypassword123"));

        result.IsSuccess.Should().BeTrue();
        (await _dbContext.Users.CountAsync()).Should().Be(0);
        _refreshTokenServiceMock.Verify(
            s => s.RevokeAllForUserAsync(TestUserId, It.IsAny<CancellationToken>()),
            Times.Once);
        _auditLoggerMock.Verify(
            a => a.RecordAsync(
                AuditAction.AccountDeleted,
                TestUserId,
                TestUserId,
                It.Is<Dictionary<string, object?>>(m => m.ContainsKey("email_hash")),
                It.IsAny<CancellationToken>()),
            Times.Once,
            "LEG-08: audit entry must be recorded on successful account deletion");
    }

    [Fact]
    public async Task AuditRecordedBeforeUsersRemove_OrderingInvariant()
    {
        // LEG-08: IAuditLogger.RecordAsync must fire BEFORE dbContext.Users.Remove.
        // We assert ordering by capturing the user count at the moment the audit callback fires.
        int userCountAtAuditMoment = -1;
        _auditLoggerMock
            .Setup(a => a.RecordAsync(
                AuditAction.AccountDeleted,
                It.IsAny<Guid?>(),
                It.IsAny<Guid?>(),
                It.IsAny<Dictionary<string, object?>>(),
                It.IsAny<CancellationToken>()))
            .Callback(() => userCountAtAuditMoment = _dbContext.Users.Count())
            .Returns(Task.CompletedTask);

        var user = new User
        {
            Id = TestUserId,
            Email = "order@test.de",
            DisplayName = "Order Test",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("p"),
        };
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        var result = await _handler.HandleAsync(new DeleteAccountRequest("p"));

        result.IsSuccess.Should().BeTrue();
        userCountAtAuditMoment.Should().Be(1,
            "the user row must still exist when the audit entry is recorded (record-before-delete)");
        (await _dbContext.Users.CountAsync()).Should().Be(0,
            "after the handler returns, the cascade delete has fired");
    }

    [Fact]
    public async Task WrongPassword_Returns401_GermanError()
    {
        var user = new User
        {
            Id = TestUserId,
            Email = "test@example.de",
            DisplayName = "Test",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("realpassword"),
        };
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        var result = await _handler.HandleAsync(new DeleteAccountRequest("wrongpassword"));

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Ungültiges Passwort.");
        (await _dbContext.Users.CountAsync()).Should().Be(1, "wrong password must not delete the user");
        _refreshTokenServiceMock.Verify(
            s => s.RevokeAllForUserAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "revoke must NOT be called when password verification fails");
    }

    [Fact]
    public async Task RevokesTokensBeforeDelete_DefenseInDepth()
    {
        // D-13: refreshTokenService.RevokeAllForUserAsync must run BEFORE
        // dbContext.Users.Remove. We assert ordering by capturing the user
        // count at the moment the revoke callback fires — if revoke runs
        // before the cascade delete, the user is still present.
        int userCountAtRevokeMoment = -1;
        _refreshTokenServiceMock
            .Setup(s => s.RevokeAllForUserAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Callback(() => userCountAtRevokeMoment = _dbContext.Users.Count())
            .ReturnsAsync(0);

        var user = new User
        {
            Id = TestUserId,
            Email = "test@example.de",
            DisplayName = "Test",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("p"),
        };
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        var result = await _handler.HandleAsync(new DeleteAccountRequest("p"));

        result.IsSuccess.Should().BeTrue();
        userCountAtRevokeMoment.Should().Be(1,
            "the user must still exist at the moment RevokeAllForUserAsync is invoked — proves revoke runs BEFORE Users.Remove");
        (await _dbContext.Users.CountAsync()).Should().Be(0,
            "after the handler returns, the cascade delete has fired");
        _refreshTokenServiceMock.Verify(
            s => s.RevokeAllForUserAsync(TestUserId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task MissingUser_ReturnsFailure()
    {
        // Do NOT seed any user — the current-user mock points at a non-existent id.
        var result = await _handler.HandleAsync(new DeleteAccountRequest("anything"));

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("User not found.");
        _refreshTokenServiceMock.Verify(
            s => s.RevokeAllForUserAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "no revoke should fire for a user that doesn't exist");
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        GC.SuppressFinalize(this);
    }
}
