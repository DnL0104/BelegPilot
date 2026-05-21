using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TaxReader.Application.DTOs;
using TaxReader.Application.Interfaces;
using TaxReader.Infrastructure.Data;
using TaxReader.UnitTests.Helpers;
using TaxReader.UnitTests.Pipeline;

namespace TaxReader.UnitTests.Hangfire;

/// <summary>
/// D-10: /auth/login + /auth/refresh set the tr_access HttpOnly cookie scoped to
/// Path=/hangfire with HttpOnly+Secure+SameSite=Strict and an expiry matching the
/// access-token TTL. /auth/logout clears the cookie (Path=/hangfire) so the browser
/// drops it on next dashboard request.
///
/// EF DbContext is swapped to in-memory inside ConfigureWebHost so the WAF can boot
/// without Postgres and we can seed an admin user before the login call.
/// </summary>
[Collection(PipelineTestCollection.Name)]
public class CookieAuthIntegrationTests
{
    private const string AdminEmail = "cookie-admin@test.local";
    private const string AdminPassword = "test-password-1234";

    [Fact]
    public async Task Login_SuccessfulAdmin_SetsTrAccessCookieWithSecureAttributes()
    {
        await using var factory = CreateFactoryWithInMemoryDb();
        SeedAdmin(factory);
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new LoginRequest(AdminEmail, AdminPassword));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        response.Headers.TryGetValues("Set-Cookie", out var setCookies).Should().BeTrue(
            "/auth/login MUST emit Set-Cookie for tr_access on success");

        var cookie = setCookies!.FirstOrDefault(s =>
            s.StartsWith("tr_access=", StringComparison.OrdinalIgnoreCase));
        cookie.Should().NotBeNull();

        cookie!.Should().Contain("httponly", "T-03-04: HttpOnly blocks JS access");
        cookie.Should().Contain("secure", "T-03-04: Secure forces HTTPS-only transport");
        cookie.Should().Contain("samesite=strict",
            "T-03-04: SameSite=Strict blocks cross-site send");
        cookie.Should().Contain("path=/hangfire",
            "D-10: cookie scope MUST be /hangfire so the SPA cookie space stays clean");
        cookie.Should().Contain("expires=",
            "expiry MUST be present so the browser drops the cookie at access-token TTL");
    }

    [Fact]
    public async Task Refresh_SuccessfulRotation_ResetsTrAccessCookie()
    {
        await using var factory = CreateFactoryWithInMemoryDb();
        SeedAdmin(factory);
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        // Login first to obtain a refresh token.
        var loginResponse = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new LoginRequest(AdminEmail, AdminPassword));
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var auth = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>();
        auth.Should().NotBeNull();
        auth!.RefreshToken.Should().NotBeNullOrEmpty();

        // Refresh
        var refreshResponse = await client.PostAsJsonAsync(
            "/api/v1/auth/refresh",
            new RefreshRequest(auth.RefreshToken));

        refreshResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        refreshResponse.Headers.TryGetValues("Set-Cookie", out var setCookies)
            .Should().BeTrue("/auth/refresh MUST re-issue tr_access on rotation");

        var cookie = setCookies!.FirstOrDefault(s =>
            s.StartsWith("tr_access=", StringComparison.OrdinalIgnoreCase));
        cookie.Should().NotBeNull();
        cookie!.Should().Contain("path=/hangfire");
        cookie.Should().Contain("httponly");
    }

    [Fact]
    public async Task Logout_DeletesTrAccessCookieWithMatchingPath()
    {
        await using var factory = CreateFactoryWithInMemoryDb();
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var response = await client.PostAsync("/api/v1/auth/logout", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        response.Headers.TryGetValues("Set-Cookie", out var setCookies).Should().BeTrue();
        var cookie = setCookies!.FirstOrDefault(s =>
            s.StartsWith("tr_access=", StringComparison.OrdinalIgnoreCase));
        cookie.Should().NotBeNull();

        cookie!.Should().Contain("path=/hangfire",
            "Path attribute MUST match the original Set-Cookie or the browser silently keeps it");
        cookie.Should().Contain("expires=Thu, 01 Jan 1970",
            "Cookies.Delete sets an epoch-zero expiry so the browser drops the entry");
    }

    /// <summary>
    /// Swaps the EF AppDbContext over to a per-test in-memory provider so the WAF
    /// host boots without Postgres. The Hangfire in-memory storage flag is set by
    /// HangfireTestFactory itself. Database name is shared by name across one
    /// factory instance so the SeedAdmin call and the request handler see the
    /// same data.
    /// </summary>
    private static WebApplicationFactory<Program> CreateFactoryWithInMemoryDb()
    {
        var dbName = Guid.NewGuid().ToString();
        return HangfireTestFactory.BuildFactory()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    // Strip every EF Core service registration (DbContextOptions,
                    // AppDbContext, IDbContextOptionsConfiguration, internal services,
                    // and the IAppDbContext port) before re-registering against the
                    // in-memory provider. UseInMemoryDatabase and UseNpgsql cannot
                    // coexist on a shared service provider.
                    var efDescriptors = services
                        .Where(d => d.ServiceType.FullName is { } name
                                    && (name.Contains("Microsoft.EntityFrameworkCore", StringComparison.Ordinal)
                                     || name.Contains("Npgsql", StringComparison.Ordinal)
                                     || d.ServiceType == typeof(AppDbContext)
                                     || d.ServiceType == typeof(IAppDbContext)))
                        .ToList();
                    foreach (var descriptor in efDescriptors)
                    {
                        services.Remove(descriptor);
                    }

                    services.AddDbContext<AppDbContext>(options =>
                        options.UseInMemoryDatabase(dbName));
                    services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());
                });
            });
    }

    private static void SeedAdmin(WebApplicationFactory<Program> factory)
    {
        // factory.Services materialises the same DI root the HTTP pipeline uses,
        // so the seeded user is visible to /auth/login on the next call.
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        if (!db.Users.Any(u => u.Email == AdminEmail))
        {
            db.Users.Add(TestDataFactory.CreateAdminUser(AdminEmail));
            db.SaveChanges();
        }
    }
}
