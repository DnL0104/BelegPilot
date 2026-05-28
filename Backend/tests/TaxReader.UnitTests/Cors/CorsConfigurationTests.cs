using FluentAssertions;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace TaxReader.UnitTests.Cors;

public class CorsConfigurationTests
{
    [Fact]
    public void Production_NoOrigins_DeniesAll()
    {
        using var factory = BuildFactory(environment: "Production", origins: null);
        var policy = ResolveDefaultPolicy(factory);

        policy.Origins.Should().BeEmpty(
            "D-07: non-Development without CORS_ALLOWED_ORIGINS must deny all cross-origin requests");
    }

    [Fact]
    public void Development_NoOrigins_AllowsLocalhost3000()
    {
        using var factory = BuildFactory(environment: "Development", origins: null);
        var policy = ResolveDefaultPolicy(factory);

        policy.Origins.Should().ContainSingle()
            .Which.Should().Be("http://localhost:3000");
    }

    [Fact]
    public void Production_OriginsConfigured_AllowsConfigured()
    {
        using var factory = BuildFactory(environment: "Production", origins: "https://example.com");
        var policy = ResolveDefaultPolicy(factory);

        policy.Origins.Should().Contain("https://example.com");
    }

    private static WebApplicationFactory<Program> BuildFactory(string environment, string? origins)
    {
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment(environment);
            builder.UseSetting("CORS_ALLOWED_ORIGINS", origins ?? string.Empty);
            // The factory still needs a JWT secret + connection string to start;
            // provide harmless test values so DI resolution doesn't blow up.
            builder.UseSetting("Jwt:Secret", "test-secret-test-secret-test-secret-1234");
            builder.UseSetting("Jwt:Issuer", "test");
            builder.UseSetting("Jwt:Audience", "test");
            builder.UseSetting(
                "ConnectionStrings:DefaultConnection",
                "Host=localhost;Port=5432;Database=test;Username=test;Password=test");
            // RefreshToken pepper — 32 zero bytes Base64-encoded. Required since CR-01
            // wired ValidateOnStart() on RefreshTokenOptions in DependencyInjection.cs.
            builder.UseSetting("RefreshToken:HashKey", Convert.ToBase64String(new byte[32]));
            // Phase 3 plan 03-01: Hangfire boots on every WAF host. Use in-memory
            // storage here so this CORS-only test doesn't try to connect to Postgres.
            builder.UseSetting("Hangfire:UseInMemoryStorage", "true");
            builder.UseSetting("Hangfire:SeedAdminEmails", string.Empty);
            // Phase 5 plan 05-01: StripeOptionsValidator.ValidateOnStart() requires these.
            // Production environment uses sk_live_ prefix to bypass D-13 guard.
            // Development environment is unaffected (sk_test_ is allowed there).
            builder.UseSetting("Stripe:SecretKey", "sk_live_test_placeholder_for_unit_tests");
            builder.UseSetting("Stripe:PublishableKey", "pk_live_test_placeholder_for_unit_tests");
            builder.UseSetting("Stripe:WebhookSecret", "whsec_placeholder_for_unit_tests");
            builder.UseSetting("RUN_MIGRATIONS", "false");
        });
    }

    private static CorsPolicy ResolveDefaultPolicy(WebApplicationFactory<Program> factory)
    {
        using var scope = factory.Services.CreateScope();
        var corsOptions = scope.ServiceProvider
            .GetRequiredService<IOptions<CorsOptions>>().Value;
        var policy = corsOptions.GetPolicy(corsOptions.DefaultPolicyName!);
        policy.Should().NotBeNull("the default CORS policy must be registered");
        return policy!;
    }
}
