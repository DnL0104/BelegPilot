using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Hangfire.Dashboard;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using TaxReader.Infrastructure.Configuration;

namespace TaxReader.Api.Hangfire;

/// <summary>
/// D-07 + D-10: gates the /hangfire dashboard. Validates the tr_access HttpOnly
/// cookie's JWT (same signing key as the API bearer auth) and requires a
/// role=admin claim. Any validation failure → false (Hangfire returns 401).
/// Mirrors the TokenValidationParameters set in Program.cs JWT bearer setup so
/// the dashboard cannot be reached with tokens that the API itself would reject.
/// </summary>
public class HangfireAdminAuthFilter(IOptions<JwtOptions> jwtOptions) : IDashboardAuthorizationFilter
{
    private readonly JwtOptions _jwt = jwtOptions.Value;

    public bool Authorize(DashboardContext context)
    {
        var httpContext = context.GetHttpContext();
        if (!httpContext.Request.Cookies.TryGetValue("tr_access", out var token)
            || string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        try
        {
            // Disable the default short-name → URI claim remapping that
            // JwtSecurityTokenHandler applies on read; without this, the literal
            // "role" claim is rewritten to the long ClaimTypes.Role URI and
            // principal.FindFirst("role") returns null even on valid admin JWTs.
            var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
            var parameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = _jwt.Issuer,
                ValidAudience = _jwt.Audience,
                IssuerSigningKey = new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes(_jwt.Secret)),
                ClockSkew = TimeSpan.FromSeconds(30)
            };

            var principal = handler.ValidateToken(token, parameters, out _);
            return principal.FindFirst("role")?.Value == "admin";
        }
        catch (Exception)
        {
            return false;
        }
    }
}
