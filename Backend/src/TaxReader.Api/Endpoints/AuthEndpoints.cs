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
            CancellationToken cancellationToken) =>
        {
            var result = await authService.RegisterAsync(request, userAgent: null, ipAddress: null, cancellationToken);

            return result.IsSuccess
                ? Results.Ok(result.Value)
                : Results.BadRequest(new { error = result.Error });
        })
        .AllowAnonymous()
        .WithName("Register")
        .WithSummary("Create a new user account");

        auth.MapPost("/login", async (
            LoginRequest request,
            IAuthService authService,
            CancellationToken cancellationToken) =>
        {
            var result = await authService.LoginAsync(request, userAgent: null, ipAddress: null, cancellationToken);

            return result.IsSuccess
                ? Results.Ok(result.Value)
                : Results.Unauthorized();
        })
        .AllowAnonymous()
        .WithName("Login")
        .WithSummary("Authenticate and receive JWT tokens");

        auth.MapPost("/refresh", async (
            RefreshRequest request,
            IAuthService authService,
            CancellationToken cancellationToken) =>
        {
            var result = await authService.RefreshAsync(request.RefreshToken, userAgent: null, ipAddress: null, cancellationToken);

            return result.IsSuccess
                ? Results.Ok(result.Value)
                : Results.Unauthorized();
        })
        .AllowAnonymous()
        .WithName("RefreshToken")
        .WithSummary("Exchange a refresh token for new tokens");

        auth.MapDelete("/account", async (
            DeleteAccountHandler handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(cancellationToken);

            return result.IsSuccess
                ? Results.NoContent()
                : Results.NotFound(new { error = result.Error });
        })
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
