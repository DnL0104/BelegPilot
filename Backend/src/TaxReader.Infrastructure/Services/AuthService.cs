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
    IOptions<JwtOptions> jwtOptions) : IAuthService
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

        // TODO Task 4: delegate refresh-token issuance to IRefreshTokenService.IssueAsync.
        throw new NotImplementedException("Awaiting Task 4: IRefreshTokenService integration.");
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

        // TODO Task 4: delegate refresh-token issuance to IRefreshTokenService.IssueAsync.
        throw new NotImplementedException("Awaiting Task 4: IRefreshTokenService integration.");
    }

    public Task<Result<AuthResponse>> RefreshAsync(
        string refreshToken,
        string? userAgent,
        string? ipAddress,
        CancellationToken cancellationToken = default)
    {
        // TODO Task 4: delegate rotation+replay to IRefreshTokenService.ValidateAndRotateAsync.
        throw new NotImplementedException("Awaiting Task 4: IRefreshTokenService integration.");
    }

    private string GenerateAccessToken(User user)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.Secret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim("name", user.DisplayName),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer: _jwt.Issuer,
            audience: _jwt.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_jwt.AccessTokenExpirationMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
