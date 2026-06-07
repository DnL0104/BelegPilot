using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace TaxReader.IntegrationTests;

/// <summary>
/// WebApplicationFactory that boots the real API against the shared Postgres test container.
/// Carries all required boot settings so the host starts cleanly in Production environment.
/// Hangfire is forced to in-memory so its schema does NOT land in the container —
/// AppDbContext still uses the real Postgres connection (RESEARCH Pitfall 1 mitigation).
/// </summary>
public sealed class IntegrationTestWebAppFactory(string connectionString)
    : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Production");

        // Redirect AppDbContext to the test container.
        builder.UseSetting("ConnectionStrings:DefaultConnection", connectionString);

        // JWT settings — valid but non-secret values for the test host.
        builder.UseSetting("Jwt:Secret", "test-secret-test-secret-test-secret-1234");
        builder.UseSetting("Jwt:Issuer", "test");
        builder.UseSetting("Jwt:Audience", "test");

        // Refresh-token HMAC pepper — 32 zero bytes, Base64-encoded. ValidateOnStart
        // (02-CR-01) rejects an empty or malformed value, so this must be a valid 44-char string.
        builder.UseSetting("RefreshToken:HashKey", Convert.ToBase64String(new byte[32]));

        // Force Hangfire to in-memory storage so it does NOT try to create its schema in
        // the test container. AppDbContext is still on real Postgres (RESEARCH Pitfall 1).
        builder.UseSetting("Hangfire:UseInMemoryStorage", "true");
        builder.UseSetting("Hangfire:SeedAdminEmails", string.Empty);

        // Stripe options required by StripeOptionsValidator.ValidateOnStart().
        // sk_live_ prefix satisfies the Production-environment guard (D-13).
        builder.UseSetting("Stripe:SecretKey", "sk_live_test_placeholder_for_unit_tests");
        builder.UseSetting("Stripe:PublishableKey", "pk_live_test_placeholder_for_unit_tests");
        builder.UseSetting("Stripe:WebhookSecret", "whsec_placeholder_for_unit_tests");
    }
}
