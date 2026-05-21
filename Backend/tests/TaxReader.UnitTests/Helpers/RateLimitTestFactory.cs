using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace TaxReader.UnitTests.Helpers;

/// <summary>
/// Shared WebApplicationFactory builder for Phase 2 plans (02-01 refresh-token tests,
/// 02-02 delete-account tests, 02-03 rate-limit tests). Mirrors the pattern in
/// CorsConfigurationTests.BuildFactory but adds RefreshToken:HashKey seeding (32-byte
/// zero pepper is fine for tests — only determinism matters) and accepts an arbitrary
/// settings dictionary for per-test overrides.
/// </summary>
public static class RateLimitTestFactory
{
    public static WebApplicationFactory<Program> BuildFactory(
        string environment = "Production",
        string? origins = null,
        Dictionary<string, string?>? extraSettings = null)
    {
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment(environment);
            builder.UseSetting("CORS_ALLOWED_ORIGINS", origins ?? string.Empty);
            builder.UseSetting("Jwt:Secret", "test-secret-test-secret-test-secret-1234");
            builder.UseSetting("Jwt:Issuer", "test");
            builder.UseSetting("Jwt:Audience", "test");
            // Short connection timeout so rate-limit integration tests don't wait for
            // a real Postgres handshake — the local DB isn't expected to be running.
            // Each request fails fast (Npgsql times out in ~1 second) and the test
            // can burn the per-minute policy budget well under the 60-second window.
            builder.UseSetting(
                "ConnectionStrings:DefaultConnection",
                "Host=localhost;Port=5432;Database=test;Username=test;Password=test;Timeout=1;Command Timeout=1");

            // RefreshToken pepper — 32 zero bytes Base64-encoded. Tests that need a
            // non-zero pepper override this via extraSettings.
            builder.UseSetting("RefreshToken:HashKey", Convert.ToBase64String(new byte[32]));

            // Phase 3 plan 03-01: every WAF host now boots Hangfire. Force in-memory
            // storage here so existing rate-limit tests don't try to handshake against
            // a Postgres database that isn't running. Tests that need real Postgres
            // (none today) override via extraSettings.
            builder.UseSetting("Hangfire:UseInMemoryStorage", "true");

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
