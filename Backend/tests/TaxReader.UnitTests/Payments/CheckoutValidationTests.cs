using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using FluentAssertions;
using Microsoft.IdentityModel.Tokens;
using TaxReader.UnitTests.Helpers;
using TaxReader.UnitTests.RateLimiting;

namespace TaxReader.UnitTests.Payments;

/// <summary>
/// PAY-05: POST /payments/checkout must be rejected in German when WaiverAccepted or AgbAccepted is false.
/// PAY-01: Only the three D-10 credit values {100, 500, 1500} are accepted; others return a German error.
///
/// Uses WebApplicationFactory with Stripe:DemoMode=true so no real Stripe call is made (D-14).
/// The endpoint lives in an inline lambda gated by RequireAuthorization — tests supply a
/// valid HS256 JWT signed with the same secret used by RateLimitTestFactory.
/// </summary>
[Collection(RateLimiterTestCollection.Name)]
public class CheckoutValidationTests
{
    private const string TestJwtSecret = "test-secret-test-secret-test-secret-1234";
    private const string TestIssuer = "test";
    private const string TestAudience = "test";

    // DemoMode settings used for every test in this class.
    // Uses sk_live_ prefix because we run in Production environment (matching RateLimitTestFactory).
    // DemoMode=true means no real Stripe call is made despite having a live-prefixed key (D-14).
    private static readonly Dictionary<string, string?> DemoModeSettings = new()
    {
        ["Stripe:DemoMode"] = "true",
        ["Stripe:AppBaseUrl"] = "http://localhost:3000",
        // Production environment enforces the D-13 guard (sk_test_ in Production throws).
        // DemoMode bypasses the guard at runtime so sk_live_ placeholder is safe here.
        ["Stripe:SecretKey"] = "sk_live_test_placeholder_for_unit_tests",
        ["Stripe:PublishableKey"] = "pk_live_test_placeholder_for_unit_tests",
        ["Stripe:WebhookSecret"] = "whsec_placeholder_for_unit_tests",
    };

    /// <summary>
    /// Generates a minimal HS256 JWT accepted by the WAF host configured in RateLimitTestFactory.
    /// The factory sets Jwt:Secret, Jwt:Issuer, and Jwt:Audience to the same values.
    /// </summary>
    private static string MakeJwt(Guid userId)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestJwtSecret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: TestIssuer,
            audience: TestAudience,
            claims: [new Claim(JwtRegisteredClaimNames.Sub, userId.ToString())],
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: creds);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static HttpClient CreateAuthenticatedClient(
        Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> factory)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue(
                "Bearer", MakeJwt(Guid.NewGuid()));
        return client;
    }

    // --- PAY-05 ---

    /// <summary>
    /// PAY-05: WaiverAccepted=false (AGB true) must be rejected with the German error message.
    /// T-01-15: checkout hard-gated on WaiverAccepted; checkboxes are not pre-ticked.
    /// </summary>
    [Fact]
    public async Task Checkout_WaiverNotAccepted_ReturnsBadRequestWithGermanError()
    {
        using var factory = RateLimitTestFactory.BuildFactory(extraSettings: DemoModeSettings);
        var client = CreateAuthenticatedClient(factory);

        var response = await client.PostAsJsonAsync("/api/v1/payments/checkout", new
        {
            credits = 500,
            waiverAccepted = false,
            agbAccepted = true,
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "PAY-05: checkout without Widerrufsrecht waiver must return 400");
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("AGB", "PAY-05: German error must reference AGB or Widerrufsverzicht");
    }

    /// <summary>
    /// PAY-05: AgbAccepted=false (Waiver true) must be rejected with the same German error.
    /// T-01-15: checkout hard-gated on AgbAccepted.
    /// </summary>
    [Fact]
    public async Task Checkout_AgbNotAccepted_ReturnsBadRequestWithGermanError()
    {
        using var factory = RateLimitTestFactory.BuildFactory(
            extraSettings: DemoModeSettings);
        var client = CreateAuthenticatedClient(factory);

        var response = await client.PostAsJsonAsync("/api/v1/payments/checkout", new
        {
            credits = 500,
            waiverAccepted = true,
            agbAccepted = false,
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "PAY-05: checkout without AGB acceptance must return 400");
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("AGB", "PAY-05: German rejection body must mention AGB");
    }

    // --- PAY-01 ---

    /// <summary>
    /// PAY-01: An invalid credits value (999) must be rejected with a German error.
    /// T-01-14: backend validCredits allow-list {100,500,1500} rejects any other value.
    /// </summary>
    [Fact]
    public async Task Checkout_InvalidCredits_ReturnsBadRequestWithGermanError()
    {
        using var factory = RateLimitTestFactory.BuildFactory(
            extraSettings: DemoModeSettings);
        var client = CreateAuthenticatedClient(factory);

        var response = await client.PostAsJsonAsync("/api/v1/payments/checkout", new
        {
            credits = 999,
            waiverAccepted = true,
            agbAccepted = true,
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "PAY-01: credits value 999 is not in the D-10 allow-list {100,500,1500}");
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("Credits", "PAY-01: German rejection body must mention Credits-Anzahl");
    }

    /// <summary>
    /// PAY-01: A valid D-10 credits value (500) with both acknowledgments succeeds in DemoMode.
    /// </summary>
    [Fact]
    public async Task Checkout_ValidD10Credits500_BothAcknowledged_Succeeds()
    {
        using var factory = RateLimitTestFactory.BuildFactory(
            extraSettings: DemoModeSettings);
        var client = CreateAuthenticatedClient(factory);

        var response = await client.PostAsJsonAsync("/api/v1/payments/checkout", new
        {
            credits = 500,
            waiverAccepted = true,
            agbAccepted = true,
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "PAY-01: 500 credits is a D-10 valid value; DemoMode must return 200 OK");
    }

    /// <summary>
    /// PAY-01: A valid D-10 credits value (100) with both acknowledgments succeeds in DemoMode.
    /// </summary>
    [Fact]
    public async Task Checkout_ValidD10Credits100_BothAcknowledged_Succeeds()
    {
        using var factory = RateLimitTestFactory.BuildFactory(
            extraSettings: DemoModeSettings);
        var client = CreateAuthenticatedClient(factory);

        var response = await client.PostAsJsonAsync("/api/v1/payments/checkout", new
        {
            credits = 100,
            waiverAccepted = true,
            agbAccepted = true,
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "PAY-01: 100 credits is a D-10 valid value; DemoMode must return 200 OK");
    }

    /// <summary>
    /// PAY-01: A valid D-10 credits value (1500) with both acknowledgments succeeds in DemoMode.
    /// </summary>
    [Fact]
    public async Task Checkout_ValidD10Credits1500_BothAcknowledged_Succeeds()
    {
        using var factory = RateLimitTestFactory.BuildFactory(
            extraSettings: DemoModeSettings);
        var client = CreateAuthenticatedClient(factory);

        var response = await client.PostAsJsonAsync("/api/v1/payments/checkout", new
        {
            credits = 1500,
            waiverAccepted = true,
            agbAccepted = true,
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "PAY-01: 1500 credits is a D-10 valid value; DemoMode must return 200 OK");
    }

    /// <summary>
    /// PAY-01: The old pre-D-10 value (50) must be rejected after Task 2 updates the allow-list.
    /// This test is RED before Task 2 (50 was previously valid) and turns GREEN after.
    /// </summary>
    [Fact]
    public async Task Checkout_OldCredits50_NotInD10List_ReturnsBadRequest()
    {
        using var factory = RateLimitTestFactory.BuildFactory(
            extraSettings: DemoModeSettings);
        var client = CreateAuthenticatedClient(factory);

        var response = await client.PostAsJsonAsync("/api/v1/payments/checkout", new
        {
            credits = 50,
            waiverAccepted = true,
            agbAccepted = true,
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "PAY-01: 50 credits was pre-D10 tier; after D-10 update it must be rejected");
    }
}
