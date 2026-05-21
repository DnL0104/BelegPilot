using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Text;
using FluentAssertions;
using Microsoft.IdentityModel.Tokens;
using TaxReader.UnitTests.Helpers;
using TaxReader.UnitTests.Pipeline;

namespace TaxReader.UnitTests.Hangfire;

/// <summary>
/// D-07 + D-10 + threat T-03-01..T-03-03: /hangfire is gated by the tr_access
/// cookie carrying a JWT with role=admin. Anything else → 401. We hit the
/// dashboard with crafted cookies to prove the four authorisation outcomes
/// the HangfireAdminAuthFilter promises.
/// </summary>
[Collection(PipelineTestCollection.Name)]
public class HangfireDashboardAuthTests
{
    private const string TestSecret = "test-secret-test-secret-test-secret-1234";

    [Fact]
    public async Task HangfireDashboard_AnonymousRequest_Returns401()
    {
        await using var factory = HangfireTestFactory.BuildFactory();
        var client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var response = await client.GetAsync("/hangfire");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "T-03-01: no tr_access cookie ⇒ filter returns false ⇒ Hangfire emits 401");
    }

    [Fact]
    public async Task HangfireDashboard_ForgedCookie_Returns401()
    {
        await using var factory = HangfireTestFactory.BuildFactory();
        var client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        // Sign the JWT with a DIFFERENT key so signature validation fails.
        var forged = CreateJwt(
            secret: "different-secret-different-different-1234",
            isAdmin: true);

        client.DefaultRequestHeaders.Add("Cookie", $"tr_access={forged}");

        var response = await client.GetAsync("/hangfire");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "T-03-02: ValidateIssuerSigningKey=true ⇒ bad signature ⇒ filter returns false");
    }

    [Fact]
    public async Task HangfireDashboard_NonAdminToken_Returns401()
    {
        await using var factory = HangfireTestFactory.BuildFactory();
        var client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var nonAdmin = CreateJwt(TestSecret, isAdmin: false);
        client.DefaultRequestHeaders.Add("Cookie", $"tr_access={nonAdmin}");

        var response = await client.GetAsync("/hangfire");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "T-03-03: valid signature but missing role=admin claim ⇒ filter returns false");
    }

    [Fact]
    public async Task HangfireDashboard_AdminToken_Returns200()
    {
        await using var factory = HangfireTestFactory.BuildFactory();
        var client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var admin = CreateJwt(TestSecret, isAdmin: true);
        client.DefaultRequestHeaders.Add("Cookie", $"tr_access={admin}");

        var response = await client.GetAsync("/hangfire");

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "valid admin JWT in tr_access cookie should pass the dashboard filter");
    }

    private static string CreateJwt(string secret, bool isAdmin)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, Guid.NewGuid().ToString()),
            new(JwtRegisteredClaimNames.Email, "tester@test.local"),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };
        if (isAdmin)
        {
            claims.Add(new Claim("role", "admin"));
        }

        var token = new JwtSecurityToken(
            issuer: "test",
            audience: "test",
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(60),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
