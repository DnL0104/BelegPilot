using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TaxReader.Application.Interfaces;
using TaxReader.Infrastructure.Data;

namespace TaxReader.UnitTests.Helpers;

/// <summary>
/// Shared WebApplicationFactory builder for Phase 2 plans (02-01 refresh-token tests,
/// 02-02 delete-account tests, 02-03 rate-limit tests). Mirrors the pattern in
/// CorsConfigurationTests.BuildFactory but adds RefreshToken:HashKey seeding (32-byte
/// zero pepper is fine for tests — only determinism matters) and accepts an arbitrary
/// settings dictionary for per-test overrides.
///
/// EF Core is swapped to an in-memory provider so auth endpoints return 401 (user/token
/// not found) rather than 500 (Postgres connection refused) when Postgres is not running.
/// This lets rate-limit warm-up loops consume permits against clean 401s and the
/// Nth+1 request can be asserted as 429. Pattern mirrors CookieAuthIntegrationTests.
/// </summary>
public static class RateLimitTestFactory
{
    public static WebApplicationFactory<Program> BuildFactory(
        string environment = "Production",
        string? origins = null,
        Dictionary<string, string?>? extraSettings = null)
    {
        var dbName = Guid.NewGuid().ToString();
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment(environment);
            builder.UseSetting("CORS_ALLOWED_ORIGINS", origins ?? string.Empty);
            builder.UseSetting("Jwt:Secret", "test-secret-test-secret-test-secret-1234");
            builder.UseSetting("Jwt:Issuer", "test");
            builder.UseSetting("Jwt:Audience", "test");

            // RefreshToken pepper — 32 zero bytes Base64-encoded. Tests that need a
            // non-zero pepper override this via extraSettings.
            builder.UseSetting("RefreshToken:HashKey", Convert.ToBase64String(new byte[32]));

            // Phase 3 plan 03-01: every WAF host now boots Hangfire. Force in-memory
            // storage here so existing rate-limit tests don't try to handshake against
            // a Postgres database that isn't running. Tests that need real Postgres
            // (none today) override via extraSettings.
            builder.UseSetting("Hangfire:UseInMemoryStorage", "true");
            builder.UseSetting("Hangfire:SeedAdminEmails", string.Empty);

            // Phase 5 plan 05-01: StripeOptionsValidator.ValidateOnStart() requires these.
            // Tests run in Production environment so we use a sk_live_ prefix to bypass
            // the D-13 guard (Production + sk_test_ → throws). No real Stripe calls are made.
            builder.UseSetting("Stripe:SecretKey", "sk_live_test_placeholder_for_unit_tests");
            builder.UseSetting("Stripe:PublishableKey", "pk_live_test_placeholder_for_unit_tests");
            builder.UseSetting("Stripe:WebhookSecret", "whsec_placeholder_for_unit_tests");

            // Swap EF Core from Npgsql to in-memory so auth endpoints return 401 (record
            // not found) instead of 500 (Npgsql connection refused). Strip every EF/Npgsql
            // descriptor before re-registering — UseInMemoryDatabase and UseNpgsql cannot
            // share a service provider. Same pattern as CookieAuthIntegrationTests.
            builder.ConfigureServices(services =>
            {
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
                services.AddScoped<IAppDbContext>(sp =>
                    sp.GetRequiredService<AppDbContext>());
            });

            if (extraSettings is not null)
            {
                foreach (var (key, value) in extraSettings)
                {
                    builder.UseSetting(key, value);
                }
            }
        });
    }
}
