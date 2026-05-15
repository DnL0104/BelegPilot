using Microsoft.EntityFrameworkCore;
using TaxReader.Application.Commands;
using TaxReader.Application.DTOs;
using TaxReader.Application.Interfaces;

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
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var userAgent = httpContext.Request.Headers.UserAgent.ToString();
            var ipAddress = httpContext.Connection.RemoteIpAddress?.ToString();

            var result = await authService.LoginAsync(request, userAgent, ipAddress, cancellationToken);

            return result.IsSuccess
                ? Results.Ok(result.Value)
                : Results.Unauthorized();
        })
        .AllowAnonymous()
        .RequireRateLimiting("auth-strict")
        .WithName("Login")
        .WithSummary("Authenticate and receive JWT tokens");

        auth.MapPost("/refresh", async (
            RefreshRequest request,
            IAuthService authService,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var userAgent = httpContext.Request.Headers.UserAgent.ToString();
            var ipAddress = httpContext.Connection.RemoteIpAddress?.ToString();

            var result = await authService.RefreshAsync(request.RefreshToken, userAgent, ipAddress, cancellationToken);

            return result.IsSuccess
                ? Results.Ok(result.Value)
                : Results.Unauthorized();
        })
        .AllowAnonymous()
        .RequireRateLimiting("auth-refresh")
        .WithName("RefreshToken")
        .WithSummary("Exchange a refresh token for new tokens");

        auth.MapDelete("/account", async (
            DeleteAccountRequest request,
            DeleteAccountHandler handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(request, cancellationToken);

            if (result.IsSuccess)
                return Results.NoContent();

            // D-12: wrong password = 401 with German error inline so the dialog
            // can surface it without bouncing the user to /login.
            if (result.Error == "Ungültiges Passwort.")
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
}
