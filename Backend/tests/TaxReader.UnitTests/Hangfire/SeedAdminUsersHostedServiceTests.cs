using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using TaxReader.Application.Interfaces;
using TaxReader.Domain.Entities;
using TaxReader.Infrastructure.Data;
using TaxReader.Infrastructure.Services.AdminBootstrap;
using TaxReader.UnitTests.Helpers;

namespace TaxReader.UnitTests.Hangfire;

/// <summary>
/// D-08: SeedAdminUsersHostedService reads Hangfire:SeedAdminEmails and flips
/// IsAdmin=true on matching users. Idempotent, case-insensitive, no-op on empty.
/// </summary>
public class SeedAdminUsersHostedServiceTests
{
    [Fact]
    public async Task StartAsync_WithMatchingEmail_PromotesUser()
    {
        var (db, sp) = BuildServices();
        db.Users.Add(TestDataFactory.CreateRegularUser("admin@test.local"));
        await db.SaveChangesAsync();

        var service = BuildService(sp, "admin@test.local");

        await service.StartAsync(CancellationToken.None);

        var promoted = await ReadFromFreshScope(sp, "admin@test.local");
        promoted.IsAdmin.Should().BeTrue(
            "D-08: a configured email in Hangfire:SeedAdminEmails MUST be promoted");
    }

    [Fact]
    public async Task StartAsync_AlreadyAdmin_IsIdempotent()
    {
        var (db, sp) = BuildServices();
        var existing = TestDataFactory.CreateAdminUser("admin@test.local");
        db.Users.Add(existing);
        await db.SaveChangesAsync();

        var service = BuildService(sp, "admin@test.local");

        await service.StartAsync(CancellationToken.None);
        await service.StartAsync(CancellationToken.None);

        var stillAdmin = await ReadFromFreshScope(sp, "admin@test.local");
        stillAdmin.IsAdmin.Should().BeTrue("re-running the seeder must not throw or demote");
        stillAdmin.Id.Should().Be(existing.Id, "idempotent run must not create duplicate users");
    }

    [Fact]
    public async Task StartAsync_EmptyConfig_DoesNothing()
    {
        var (db, sp) = BuildServices();
        db.Users.Add(TestDataFactory.CreateRegularUser("user@test.local"));
        await db.SaveChangesAsync();

        var service = BuildService(sp, configuredEmails: string.Empty);

        await service.StartAsync(CancellationToken.None);

        var untouched = await ReadFromFreshScope(sp, "user@test.local");
        untouched.IsAdmin.Should().BeFalse(
            "empty Hangfire:SeedAdminEmails MUST NOT promote any user");
    }

    [Fact]
    public async Task StartAsync_MultiEmailCsv_PromotesAllCaseInsensitive()
    {
        var (db, sp) = BuildServices();
        db.Users.Add(TestDataFactory.CreateRegularUser("a@test.local"));
        db.Users.Add(TestDataFactory.CreateRegularUser("b@test.local"));
        db.Users.Add(TestDataFactory.CreateRegularUser("c@test.local"));
        await db.SaveChangesAsync();

        // Mixed-case input — DB rows are lowercase-normalised at register time.
        var service = BuildService(sp, "A@TEST.LOCAL , B@Test.Local");

        await service.StartAsync(CancellationToken.None);

        var a = await ReadFromFreshScope(sp, "a@test.local");
        var b = await ReadFromFreshScope(sp, "b@test.local");
        var c = await ReadFromFreshScope(sp, "c@test.local");

        a.IsAdmin.Should().BeTrue();
        b.IsAdmin.Should().BeTrue();
        c.IsAdmin.Should().BeFalse("users not listed must stay non-admin");
    }

    private static async Task<User> ReadFromFreshScope(IServiceProvider sp, string email)
    {
        // The test's helper DbContext has a stale change-tracker once the service
        // runs in its own scope. A fresh scope materialises a new DbContext that
        // reads the in-memory store from scratch.
        using var scope = sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Users.SingleAsync(u => u.Email == email);
    }

    private static (AppDbContext, IServiceProvider) BuildServices()
    {
        // Capture the DB name OUTSIDE the lambda — otherwise every scope creates a
        // new DbContext with a freshly-generated UseInMemoryDatabase name, and the
        // helper db (test scope) sees different data than the SeedAdminUsersHostedService's
        // service-scope db.
        var dbName = Guid.NewGuid().ToString();
        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(options =>
            options.UseInMemoryDatabase(dbName));
        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());

        var sp = services.BuildServiceProvider();
        var db = sp.GetRequiredService<AppDbContext>();
        return (db, sp);
    }

    private static SeedAdminUsersHostedService BuildService(IServiceProvider sp, string configuredEmails)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Hangfire:SeedAdminEmails"] = configuredEmails
            })
            .Build();

        return new SeedAdminUsersHostedService(
            sp.GetRequiredService<IServiceScopeFactory>(),
            config,
            NullLogger<SeedAdminUsersHostedService>.Instance);
    }
}
