using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using TaxReader.Application.DTOs;
using TaxReader.Application.Interfaces;
using TaxReader.Domain.Common;
using TaxReader.Infrastructure.Configuration;
using TaxReader.Infrastructure.Data;
using TaxReader.Infrastructure.Services;
using TaxReader.UnitTests.Helpers;

namespace TaxReader.UnitTests.Services;

/// <summary>
/// Covers the highest-risk auth paths: register (duplicate e-mail, short password, success),
/// login (wrong password, unknown e-mail, correct password with BCrypt verify).
/// No real refresh-token storage — IRefreshTokenService is mocked to return a benign token.
/// </summary>
public class AuthServiceTests : IDisposable
{
    private static readonly Guid TestUserId = Guid.Parse("cccccccc-1111-2222-3333-444444444444");

    private readonly AppDbContext _dbContext;
    private readonly AuthService _service;
    private readonly Mock<IRefreshTokenService> _refreshTokenServiceMock;

    public AuthServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new AppDbContext(options);

        // Seed a known user for login tests
        var seededUser = TestDataFactory.CreateRegularUser("existing@test.local");
        seededUser.Id = TestUserId;
        _dbContext.Users.Add(seededUser);
        _dbContext.SaveChanges();

        _refreshTokenServiceMock = new Mock<IRefreshTokenService>();
        _refreshTokenServiceMock
            .Setup(r => r.IssueAsync(
                It.IsAny<Guid>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("mock-refresh-token");

        _service = new AuthService(
            _dbContext,
            Options.Create(new JwtOptions
            {
                Secret = "test-secret-test-secret-test-secret-12",
                Issuer = "test",
                Audience = "test",
                AccessTokenExpirationMinutes = 60,
                RefreshTokenExpirationDays = 30
            }),
            _refreshTokenServiceMock.Object);
    }

    [Fact]
    public async Task RegisterAsync_NewEmail_ReturnsSuccessWithHashedPassword()
    {
        var request = new RegisterRequest("new@test.local", "New User", "securepassword1234");

        var result = await _service.RegisterAsync(request, null, null);

        result.IsSuccess.Should().BeTrue();
        result.Value!.User.Email.Should().Be("new@test.local");

        var user = await _dbContext.Users.FirstAsync(u => u.Email == "new@test.local");
        user.PasswordHash.Should().NotBe("securepassword1234", "password must be hashed, not stored in plaintext");
        BCrypt.Net.BCrypt.Verify("securepassword1234", user.PasswordHash).Should().BeTrue();
    }

    [Fact]
    public async Task RegisterAsync_DuplicateEmail_ReturnsFailure()
    {
        var request = new RegisterRequest("existing@test.local", "Dup User", "securepassword1234");

        var result = await _service.RegisterAsync(request, null, null);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("Ein Konto mit dieser E-Mail existiert bereits.");
    }

    [Fact]
    public async Task RegisterAsync_ShortPassword_ReturnsFailure()
    {
        var request = new RegisterRequest("short@test.local", "Short Pw", "abc123");

        var result = await _service.RegisterAsync(request, null, null);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("Das Passwort muss mindestens 8 Zeichen lang sein.");
    }

    [Fact]
    public async Task LoginAsync_WrongPassword_ReturnsFailure()
    {
        var request = new LoginRequest("existing@test.local", "wrong-password");

        var result = await _service.LoginAsync(request, null, null);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("Ungültige E-Mail oder Passwort.");
    }

    [Fact]
    public async Task LoginAsync_CorrectPassword_ReturnsSuccess()
    {
        // TestDataFactory.CreateRegularUser hashes "test-password-1234"
        var request = new LoginRequest("existing@test.local", "test-password-1234");

        var result = await _service.LoginAsync(request, null, null);

        result.IsSuccess.Should().BeTrue();
        result.Value!.User.Email.Should().Be("existing@test.local");
        result.Value.AccessToken.Should().NotBeNullOrEmpty();
        result.Value.RefreshToken.Should().Be("mock-refresh-token");
    }

    [Fact]
    public async Task LoginAsync_UnknownEmail_ReturnsFailure()
    {
        // Must return the SAME message as wrong password — no user enumeration (ASVS V2)
        var request = new LoginRequest("nobody@test.local", "some-password");

        var result = await _service.LoginAsync(request, null, null);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("Ungültige E-Mail oder Passwort.");
    }

    public void Dispose() => _dbContext.Dispose();
}
