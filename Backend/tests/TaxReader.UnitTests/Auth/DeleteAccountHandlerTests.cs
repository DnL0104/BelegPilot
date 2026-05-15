using Microsoft.EntityFrameworkCore;
using Moq;
using TaxReader.Application.Interfaces;
using TaxReader.Infrastructure.Data;

namespace TaxReader.UnitTests.Auth;

/// <summary>
/// AUTH-02: Stub scaffolding for the DeleteAccountHandler password-reauth flow.
/// Tests are Skipped until Tasks 2-4 land the handler refactor, validator, and
/// endpoint binding; Task 5 unskips them and fills in the assertion bodies.
/// </summary>
public class DeleteAccountHandlerTests : IDisposable
{
    private static readonly Guid TestUserId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");

    private readonly AppDbContext _dbContext;
    private readonly Mock<ICurrentUser> _currentUserMock;
    private readonly Mock<IRefreshTokenService> _refreshTokenServiceMock;

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
    }

    [Fact(Skip = "Pending implementation in Task 2-4")]
    public Task CorrectPassword_Returns204_AndCascadeDeletes()
    {
        // Will: seed User with BCrypt-hashed password; call HandleAsync(new DeleteAccountRequest("mypassword123"));
        // assert IsSuccess + dbContext.Users.Count() == 0 + refreshTokenServiceMock.Verify(RevokeAllForUserAsync, Times.Once)
        return Task.CompletedTask;
    }

    [Fact(Skip = "Pending implementation in Task 2-4")]
    public Task WrongPassword_Returns401_GermanError()
    {
        // Will: seed User; HandleAsync(new DeleteAccountRequest("wrong"));
        // assert IsFailure + Error == "Ungültiges Passwort." + User still present
        return Task.CompletedTask;
    }

    [Fact(Skip = "Pending implementation in Task 2-4")]
    public Task RevokesTokensBeforeDelete_DefenseInDepth()
    {
        // Will: spy on the mock invocation; assert RevokeAllForUserAsync was called
        // before the Users.Remove cascade fires.
        return Task.CompletedTask;
    }

    [Fact(Skip = "Pending implementation in Task 2-4")]
    public Task MissingUser_ReturnsFailure()
    {
        // Will: call HandleAsync with a userId that doesn't exist; assert IsFailure + "User not found."
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        GC.SuppressFinalize(this);
    }
}
