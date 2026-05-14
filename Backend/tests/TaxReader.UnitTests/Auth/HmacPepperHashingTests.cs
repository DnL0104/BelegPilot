using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TaxReader.Domain.Entities;
using TaxReader.Infrastructure.Configuration;
using TaxReader.Infrastructure.Data;
using TaxReader.Infrastructure.Services;

namespace TaxReader.UnitTests.Auth;

/// <summary>
/// AUTH-01 D-01: HMAC-SHA256 pepper hashing. Verifies determinism (same plaintext +
/// same pepper → identical persisted hash) and key-sensitivity (different peppers
/// → different hashes). Implementation observable through IssueAsync's persisted
/// TokenHash column: ValidateAndRotate's lookup proves the hash is reproducible.
/// </summary>
public class HmacPepperHashingTests : IDisposable
{
    private static readonly Guid TestUserId = Guid.Parse("11111111-2222-3333-4444-555555555555");

    private readonly AppDbContext _dbContext;

    public HmacPepperHashingTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new AppDbContext(options);
        _dbContext.Users.Add(new User
        {
            Id = TestUserId,
            Email = "pepper@test.local",
            DisplayName = "Pepper Tester",
            PasswordHash = "x"
        });
        _dbContext.SaveChanges();
    }

    [Fact]
    public async Task ComputeHash_SameInputSamePepper_ReturnsIdenticalHash()
    {
        // Two service instances with the SAME pepper. We can't directly call the
        // private ComputeHash, so we verify reproducibility through the round trip:
        // hash a plaintext via the rotation lookup. The fact that ValidateAndRotate
        // FINDS the row issued by the first service proves the hash is deterministic.
        var pepper = Convert.ToBase64String(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32 });

        var svcA = BuildService(pepper);
        var svcB = BuildService(pepper);

        var plaintext = await svcA.IssueAsync(TestUserId, null, null);
        var rotation = await svcB.ValidateAndRotateAsync(plaintext, null, null);

        rotation.IsSuccess.Should().BeTrue(
            "service B (same pepper) must hash the plaintext to the same value persisted by service A");
    }

    [Fact]
    public async Task ComputeHash_SameInputDifferentPeppers_ReturnsDifferentHashes()
    {
        var pepperA = Convert.ToBase64String(new byte[32]); // all zeros
        var pepperB = Convert.ToBase64String(Enumerable.Repeat((byte)0xFF, 32).ToArray());

        var svcA = BuildService(pepperA);
        var svcB = BuildService(pepperB);

        var plaintext = await svcA.IssueAsync(TestUserId, null, null);
        var rotation = await svcB.ValidateAndRotateAsync(plaintext, null, null);

        rotation.IsFailure.Should().BeTrue(
            "service B (different pepper) must hash the plaintext to a different value, so the lookup misses");
    }

    [Fact]
    public async Task ComputeHash_DifferentInputs_ReturnsDifferentHashes()
    {
        var pepper = Convert.ToBase64String(new byte[32]);
        var svc = BuildService(pepper);

        var plaintextA = await svc.IssueAsync(TestUserId, null, null);
        var plaintextB = await svc.IssueAsync(TestUserId, null, null);

        plaintextA.Should().NotBe(plaintextB,
            "RandomNumberGenerator.GetBytes(64) collision is astronomically unlikely");

        var stored = await _dbContext.RefreshTokens
            .Where(t => t.UserId == TestUserId)
            .Select(t => t.TokenHash)
            .ToListAsync();

        stored.Should().HaveCount(2);
        stored[0].Should().NotBe(stored[1],
            "two distinct plaintexts must hash to two distinct Base64 values");

        stored[0].Length.Should().Be(44, "Base64 of 32-byte HMAC-SHA256 is exactly 44 chars");
        stored[1].Length.Should().Be(44);
    }

    private RefreshTokenService BuildService(string base64Pepper)
    {
        return new RefreshTokenService(
            _dbContext,
            Options.Create(new RefreshTokenOptions { HashKey = base64Pepper }),
            Options.Create(new JwtOptions
            {
                Secret = "test-secret-test-secret-test-secret-12",
                Issuer = "test",
                Audience = "test",
                RefreshTokenExpirationDays = 30
            }),
            NullLogger<RefreshTokenService>.Instance);
    }

    public void Dispose() => _dbContext.Dispose();
}
