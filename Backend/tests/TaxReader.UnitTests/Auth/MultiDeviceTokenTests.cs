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

namespace TaxReader.UnitTests.Auth;

/// <summary>
/// AUTH-01: The same user can hold two active refresh tokens simultaneously
/// (phone + laptop) and rotate each independently. Rotation of token A must
/// not affect token B's chain.
/// </summary>
public class MultiDeviceTokenTests : IDisposable
{
    private static readonly Guid TestUserId = Guid.Parse("cccccccc-1111-2222-3333-444444444444");

    private readonly AppDbContext _dbContext;
    private readonly RefreshTokenService _service;

    public MultiDeviceTokenTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new AppDbContext(options);

        _dbContext.Users.Add(new User
        {
            Id = TestUserId,
            Email = "multi@test.local",
            DisplayName = "Multi Tester",
            PasswordHash = "x"
        });
        _dbContext.SaveChanges();

        var auditLoggerMock = new Mock<IAuditLogger>();
        auditLoggerMock
            .Setup(a => a.RecordAsync(
                It.IsAny<AuditAction>(),
                It.IsAny<Guid?>(),
                It.IsAny<Guid?>(),
                It.IsAny<Dictionary<string, object?>>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _service = new RefreshTokenService(
            _dbContext,
            Options.Create(new RefreshTokenOptions { HashKey = Convert.ToBase64String(new byte[32]) }),
            Options.Create(new JwtOptions
            {
                Secret = "test-secret-test-secret-test-secret-12",
                Issuer = "test",
                Audience = "test",
                RefreshTokenExpirationDays = 30
            }),
            NullLogger<RefreshTokenService>.Instance,
            auditLoggerMock.Object);
    }

    [Fact]
    public async Task TwoActiveTokens_BothValidate()
    {
        var plaintextA = await _service.IssueAsync(TestUserId, "phone", null);
        var plaintextB = await _service.IssueAsync(TestUserId, "laptop", null);

        // Rotate A — B must still validate independently afterwards.
        var rotationA = await _service.ValidateAndRotateAsync(plaintextA, "phone-2", null);
        rotationA.IsSuccess.Should().BeTrue("first device should rotate cleanly");

        // The laptop's still-active plaintext must also rotate.
        var rotationB = await _service.ValidateAndRotateAsync(plaintextB, "laptop-2", null);
        rotationB.IsSuccess.Should().BeTrue(
            "second device's token must remain valid after first device rotates");

        // Final state: both old plaintexts revoked, both new plaintexts active.
        var active = await _dbContext.RefreshTokens
            .Where(t => t.UserId == TestUserId && t.RevokedAt == null)
            .ToListAsync();

        active.Should().HaveCount(2, "both devices should each have one fresh active token");
    }

    public void Dispose() => _dbContext.Dispose();
}
