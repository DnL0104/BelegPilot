using System.IdentityModel.Tokens.Jwt;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;
using TaxReader.Application.DTOs;
using TaxReader.Application.Interfaces;
using TaxReader.Domain.Entities;
using TaxReader.Infrastructure.Configuration;
using TaxReader.Infrastructure.Data;
using TaxReader.Infrastructure.Services;

namespace TaxReader.UnitTests.Auth;

/// <summary>
/// D-07: AuthService.GenerateAccessToken emits role=admin claim only when User.IsAdmin is true.
/// The Hangfire dashboard filter reads principal.FindFirst("role")?.Value to gate /hangfire access.
///
/// Migration claim (Test 3) is asserted via reflection on the AddIsAdminToUsers migration
/// class so the test does not require a real Postgres instance.
/// </summary>
public class IsAdminClaimTests : IDisposable
{
    private const string TestSecret = "test-secret-test-secret-test-secret-1234";
    private readonly AppDbContext _dbContext;
    private readonly AuthService _authService;
    private readonly Mock<IRefreshTokenService> _refreshTokenServiceMock;

    public IsAdminClaimTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new AppDbContext(options);

        _refreshTokenServiceMock = new Mock<IRefreshTokenService>();
        _refreshTokenServiceMock
            .Setup(s => s.IssueAsync(It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("test-refresh-token");

        _authService = new AuthService(
            _dbContext,
            Options.Create(new JwtOptions
            {
                Secret = TestSecret,
                Issuer = "test",
                Audience = "test",
                AccessTokenExpirationMinutes = 60
            }),
            _refreshTokenServiceMock.Object);
    }

    [Fact]
    public async Task AdminUser_Login_ProducesJwtWithRoleAdminClaim()
    {
        // Arrange: BCrypt hash for the literal password we'll send on login.
        const string password = "test-password-1234";
        var admin = new User
        {
            Id = Guid.NewGuid(),
            Email = "admin@test.local",
            DisplayName = "Admin",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            CreatedAt = DateTime.UtcNow,
            IsAdmin = true
        };
        _dbContext.Users.Add(admin);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _authService.LoginAsync(
            new LoginRequest("admin@test.local", password),
            userAgent: null,
            ipAddress: null);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var token = new JwtSecurityTokenHandler().ReadJwtToken(result.Value!.AccessToken);
        token.Claims.Should().Contain(c => c.Type == "role" && c.Value == "admin");
    }

    [Fact]
    public async Task NonAdminUser_Login_ProducesJwtWithoutRoleClaim()
    {
        // Arrange
        const string password = "test-password-1234";
        var regular = new User
        {
            Id = Guid.NewGuid(),
            Email = "user@test.local",
            DisplayName = "User",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            CreatedAt = DateTime.UtcNow,
            IsAdmin = false
        };
        _dbContext.Users.Add(regular);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _authService.LoginAsync(
            new LoginRequest("user@test.local", password),
            userAgent: null,
            ipAddress: null);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var token = new JwtSecurityTokenHandler().ReadJwtToken(result.Value!.AccessToken);
        token.Claims.Should().NotContain(c => c.Type == "role",
            "non-admin users must never receive the role claim — its presence implies elevation");
    }

    [Fact]
    public void AddIsAdminToUsers_Migration_EmitsBooleanColumnWithDefaultFalse()
    {
        // Source-level reflection: locate the AddIsAdminToUsers migration class in
        // the Infrastructure assembly and read its source text from the on-disk file
        // so the assertion does not depend on an actual Postgres migration runner.
        //
        // EF InMemory cannot run real Postgres DDL (MigrationTests.cs comments this).
        var migrationDir = LocateMigrationsDirectory();
        var migrationFile = Directory.GetFiles(migrationDir, "*_AddIsAdminToUsers.cs")
            .FirstOrDefault(f => !f.EndsWith(".Designer.cs", StringComparison.Ordinal));

        migrationFile.Should().NotBeNull(
            "the AddIsAdminToUsers migration must exist before this plan ships");

        var content = File.ReadAllText(migrationFile!);
        content.Should().Contain("is_admin",
            "the migration must add the snake-cased is_admin column");
        content.Should().Contain("type: \"boolean\"",
            "the column must be a Postgres boolean, not nullable int or text");
        content.Should().Contain("nullable: false",
            "the column must be NOT NULL so existing rows pick up the default");
        content.Should().Contain("defaultValue: false",
            "the migration must default existing rows to non-admin");
    }

    private static string LocateMigrationsDirectory()
    {
        var dir = AppContext.BaseDirectory;
        for (var i = 0; i < 10; i++)
        {
            var candidate = Path.Combine(dir, "Backend", "src", "TaxReader.Infrastructure", "Migrations");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }
            var parent = Directory.GetParent(dir);
            if (parent is null)
            {
                break;
            }
            dir = parent.FullName;
        }
        throw new DirectoryNotFoundException(
            "Could not locate Backend/src/TaxReader.Infrastructure/Migrations from " + AppContext.BaseDirectory);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }
}
