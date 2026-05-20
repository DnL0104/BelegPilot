using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace TaxReader.UnitTests.Helpers;

/// <summary>
/// Shared WebApplicationFactory builder for Phase 3 pipeline plans (03-01 Hangfire
/// dashboard + cookie auth, 03-02 background-job enqueue, 03-04 polling). Mirrors
/// <see cref="RateLimitTestFactory.BuildFactory"/> but seeds Hangfire test settings
/// so WAF tests don't require a real Postgres instance — the Infrastructure DI
/// block swaps PostgreSqlStorage → MemoryStorage when
/// <c>Hangfire:UseInMemoryStorage = "true"</c>.
/// </summary>
public static class HangfireTestFactory
{
    public static WebApplicationFactory<Program> BuildFactory(
        string environment = "Production",
        Dictionary<string, string?>? extraSettings = null)
    {
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment(environment);
            builder.UseSetting("CORS_ALLOWED_ORIGINS", string.Empty);
            builder.UseSetting("Jwt:Secret", "test-secret-test-secret-test-secret-1234");
            builder.UseSetting("Jwt:Issuer", "test");
            builder.UseSetting("Jwt:Audience", "test");
            // Short connection timeout so Hangfire integration tests don't wait for
            // a real Postgres handshake. The MemoryStorage branch below means
            // Hangfire itself never touches the connection string.
            builder.UseSetting(
                "ConnectionStrings:DefaultConnection",
                "Host=localhost;Port=5432;Database=test;Username=test;Password=test;Timeout=1;Command Timeout=1");

            // RefreshToken pepper — 32 zero bytes Base64-encoded. Required since CR-01
            // wired ValidateOnStart() on RefreshTokenOptions in DependencyInjection.cs.
            builder.UseSetting("RefreshToken:HashKey", Convert.ToBase64String(new byte[32]));

            // Hangfire test mode — swap Postgres storage for the in-memory provider so
            // the WAF host boots without a database. The infra DI block reads this flag.
            // Misconfiguration is loud: if a test forgets this setting AND no Postgres is
            // reachable, AddHangfireServer will block the host at startup.
            builder.UseSetting("Hangfire:UseInMemoryStorage", "true");
            builder.UseSetting("Hangfire:SeedAdminEmails", string.Empty);

            // Skip auto-migration in tests — MemoryStorage doesn't need a DB.
            // Without this, the test host would try to MigrateAsync() against the
            // unreachable test Postgres and fail at startup.
            builder.UseSetting("RUN_MIGRATIONS", "false");

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
