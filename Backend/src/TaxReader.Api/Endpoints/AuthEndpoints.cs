using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TaxReader.Application.Commands;
using TaxReader.Application.DTOs;
using TaxReader.Application.Interfaces;
using TaxReader.Infrastructure.Configuration;

namespace TaxReader.Api.Endpoints;

public static class AuthEndpoints
{
    public static RouteGroupBuilder MapAuthEndpoints(this RouteGroupBuilder group)
    {
        var auth = group.MapGroup("/auth").WithTags("Auth");

        auth.MapPost("/register", async (
            RegisterRequest request,
            IAuthService authService,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var userAgent = httpContext.Request.Headers.UserAgent.ToString();
            // After UseForwardedHeaders runs (plan 02-03), RemoteIpAddress is the real client IP.
            var ipAddress = httpContext.Connection.RemoteIpAddress?.ToString();

            var result = await authService.RegisterAsync(request, userAgent, ipAddress, cancellationToken);

            return result.IsSuccess
                ? Results.Ok(result.Value)
                : Results.BadRequest(new { error = result.Error });
        })
        .AllowAnonymous()
        .RequireRateLimiting("auth-strict")
        .WithName("Register")
        .WithSummary("Create a new user account");

        auth.MapPost("/login", async (
            LoginRequest request,
            IAuthService authService,
            IOptions<JwtOptions> jwtOptions,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var userAgent = httpContext.Request.Headers.UserAgent.ToString();
            var ipAddress = httpContext.Connection.RemoteIpAddress?.ToString();

            var result = await authService.LoginAsync(request, userAgent, ipAddress, cancellationToken);

            if (result.IsFailure)
                return Results.Unauthorized();

            // D-10: HttpOnly cookie scoped to /hangfire for dashboard browser auth.
            // Same JWT as the SPA holds in localStorage — two transports, one token.
            SetTrAccessCookie(httpContext, result.Value!.AccessToken, jwtOptions.Value);

            return Results.Ok(result.Value);
        })
        .AllowAnonymous()
        .RequireRateLimiting("auth-strict")
        .WithName("Login")
        .WithSummary("Authenticate and receive JWT tokens");

        auth.MapPost("/refresh", async (
            RefreshRequest request,
            IAuthService authService,
            IOptions<JwtOptions> jwtOptions,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var userAgent = httpContext.Request.Headers.UserAgent.ToString();
            var ipAddress = httpContext.Connection.RemoteIpAddress?.ToString();

            var result = await authService.RefreshAsync(request.RefreshToken, userAgent, ipAddress, cancellationToken);

            if (result.IsFailure)
                return Results.Unauthorized();

            // D-10: refresh re-issues the cookie with the new access JWT and a fresh TTL.
            SetTrAccessCookie(httpContext, result.Value!.AccessToken, jwtOptions.Value);

            return Results.Ok(result.Value);
        })
        .AllowAnonymous()
        .RequireRateLimiting("auth-refresh")
        .WithName("RefreshToken")
        .WithSummary("Exchange a refresh token for new tokens");

        auth.MapPost("/logout", (HttpContext httpContext) =>
        {
            // D-10: clear the dashboard cookie. Path MUST match the original Set-Cookie
            // (Path=/hangfire) or the browser silently keeps it. SPA still calls its
            // local clearAuthStorage() for the localStorage half.
            httpContext.Response.Cookies.Delete("tr_access", new CookieOptions
            {
                Path = "/hangfire"
            });
            return Results.NoContent();
        })
        .AllowAnonymous()
        .WithName("Logout")
        .WithSummary("Clear the tr_access cookie used for Hangfire dashboard access");

        auth.MapDelete("/account", async (
            [FromBody] DeleteAccountRequest request,
            IValidator<DeleteAccountRequest> validator,
            DeleteAccountHandler handler,
            CancellationToken cancellationToken) =>
        {
            // CR-02 fix: Minimal APIs do NOT auto-invoke FluentValidation; the validator
            // is registered for DI but never fires unless we call it explicitly. Without
            // this, a null/empty Password reaches BCrypt.Verify and throws → 500 instead
            // of the intended 400 with the German "Passwort ist erforderlich." message.
            var validation = await validator.ValidateAsync(request, cancellationToken);
            if (!validation.IsValid)
            {
                var errors = validation.Errors
                    .GroupBy(e => e.PropertyName)
                    .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
                return Results.ValidationProblem(errors);
            }

            var result = await handler.HandleAsync(request, cancellationToken);

            if (result.IsSuccess)
                return Results.NoContent();

            // D-12: wrong password = 401 with German error inline so the dialog
            // can surface it without bouncing the user to /login.
            // WR-02: compare against the handler's const, not a duplicated literal, so a
            // wording change can't silently downgrade this 401 to a 404.
            if (result.Error == DeleteAccountHandler.InvalidPasswordError)
                return Results.Json(new { error = result.Error }, statusCode: 401);

            return Results.NotFound(new { error = result.Error });
        })
        .RequireRateLimiting("auth-strict")
        .WithName("DeleteAccount")
        .WithSummary("Permanently delete the authenticated user account and all associated data");

        auth.MapGet("/me", async (
            ICurrentUser currentUser,
            IAppDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var user = await dbContext.Users
                .Where(u => u.Id == currentUser.UserId)
                .Select(u => new UserDto(u.Id, u.Email, u.DisplayName))
                .FirstOrDefaultAsync(cancellationToken);

            return user is not null
                ? Results.Ok(user)
                : Results.NotFound();
        })
        .WithName("GetCurrentUser")
        .WithSummary("Get the currently authenticated user");

        return group;
    }

    // D-10: identical attributes on /auth/login and /auth/refresh keep cookie
    // semantics in lock-step (HttpOnly + Secure + SameSite=Strict + Path=/hangfire
    // + expiry tied to JWT TTL). The helper exists so a future tweak only happens
    // once. AuthService stays HTTP-context-free (cookie work lives in the endpoint
    // layer per the Phase 2 02-01 invariant).
    private static void SetTrAccessCookie(HttpContext httpContext, string accessToken, JwtOptions jwt)
    {
        httpContext.Response.Cookies.Append("tr_access", accessToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Path = "/hangfire",
            Expires = DateTimeOffset.UtcNow.AddMinutes(jwt.AccessTokenExpirationMinutes)
        });
    }
}
