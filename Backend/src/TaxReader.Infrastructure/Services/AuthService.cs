using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using TaxReader.Application.DTOs;
using TaxReader.Application.Interfaces;
using TaxReader.Domain.Common;
using TaxReader.Domain.Entities;
using TaxReader.Domain.Enums;
using TaxReader.Infrastructure.Configuration;

namespace TaxReader.Infrastructure.Services;

public class AuthService(
    IAppDbContext dbContext,
    IOptions<JwtOptions> jwtOptions,
    IRefreshTokenService refreshTokenService) : IAuthService
{
    private readonly JwtOptions _jwt = jwtOptions.Value;
    private const int InitialFreeTokens = 10;

    public async Task<Result<AuthResponse>> RegisterAsync(
        RegisterRequest request,
        string? userAgent,
        string? ipAddress,
        CancellationToken cancellationToken = default)
    {
        var emailNormalized = request.Email.Trim().ToLowerInvariant();

        var exists = await dbContext.Users
            .AnyAsync(u => u.Email == emailNormalized, cancellationToken);

        if (exists)
            return Result<AuthResponse>.Failure("Ein Konto mit dieser E-Mail existiert bereits.");

        if (request.Password.Length < 8)
            return Result<AuthResponse>.Failure("Das Passwort muss mindestens 8 Zeichen lang sein.");

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = emailNormalized,
            DisplayName = request.DisplayName.Trim(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            CreatedAt = DateTime.UtcNow
        };

        dbContext.Users.Add(user);

        // Welcome tokens
        var tokenBalance = new UserTokenBalance
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            UserKey = user.Id.ToString(),
            Balance = InitialFreeTokens,
            UpdatedAt = DateTime.UtcNow
        };
        dbContext.UserTokenBalances.Add(tokenBalance);

        dbContext.TokenTransactions.Add(new TokenTransaction
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            UserKey = user.Id.ToString(),
            Type = TokenTransactionType.Adjustment,
            Amount = InitialFreeTokens,
            BalanceAfter = InitialFreeTokens,
            Description = "Willkommensbonus",
            CreatedAt = DateTime.UtcNow
        });

        // Persist the user + welcome-tokens before issuing the refresh-token row —
        // the FK on refresh_tokens.user_id requires the user to exist first.
        await dbContext.SaveChangesAsync(cancellationToken);

        var accessToken = GenerateAccessToken(user);
        var refreshToken = await refreshTokenService.IssueAsync(
            user.Id, userAgent, ipAddress, cancellationToken);

        return Result<AuthResponse>.Success(new AuthResponse(
            accessToken,
            refreshToken,
            new UserDto(user.Id, user.Email, user.DisplayName)));
    }

    public async Task<Result<AuthResponse>> LoginAsync(
        LoginRequest request,
        string? userAgent,
        string? ipAddress,
        CancellationToken cancellationToken = default)
    {
        var emailNormalized = request.Email.Trim().ToLowerInvariant();

        var user = await dbContext.Users
            .FirstOrDefaultAsync(u => u.Email == emailNormalized, cancellationToken);

        if (user is null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            return Result<AuthResponse>.Failure("Ungültige E-Mail oder Passwort.");

        var accessToken = GenerateAccessToken(user);
        var refreshToken = await refreshTokenService.IssueAsync(
            user.Id, userAgent, ipAddress, cancellationToken);

        return Result<AuthResponse>.Success(new AuthResponse(
            accessToken,
            refreshToken,
            new UserDto(user.Id, user.Email, user.DisplayName)));
    }

    public async Task<Result<AuthResponse>> RefreshAsync(
        string refreshToken,
        string? userAgent,
        string? ipAddress,
        CancellationToken cancellationToken = default)
    {
        var rotationResult = await refreshTokenService.ValidateAndRotateAsync(
            refreshToken, userAgent, ipAddress, cancellationToken);

        if (rotationResult.IsFailure)
            return Result<AuthResponse>.Failure(rotationResult.Error!);

        var user = await dbContext.Users
            .FirstOrDefaultAsync(u => u.Id == rotationResult.Value.UserId, cancellationToken);

        if (user is null)
            return Result<AuthResponse>.Failure("Ungültiges oder abgelaufenes Refresh-Token.");

        var accessToken = GenerateAccessToken(user);

        return Result<AuthResponse>.Success(new AuthResponse(
            accessToken,
            rotationResult.Value.PlaintextToken,
            new UserDto(user.Id, user.Email, user.DisplayName)));
    }

    private string GenerateAccessToken(User user)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.Secret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        // D-07: emit role=admin only when User.IsAdmin is true. The Hangfire dashboard
        // filter (HangfireAdminAuthFilter) checks principal.FindFirst("role")?.Value
        // against "admin". Demotion takes effect within the access-token TTL (60 min).
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new("name", user.DisplayName),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        if (user.IsAdmin)
        {
            claims.Add(new Claim("role", "admin"));
        }

        var token = new JwtSecurityToken(
            issuer: _jwt.Issuer,
            audience: _jwt.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_jwt.AccessTokenExpirationMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
