using TaxReader.Application.DTOs;
using TaxReader.Domain.Common;

namespace TaxReader.Application.Interfaces;

public interface IAuthService
{
    Task<Result<AuthResponse>> RegisterAsync(
        RegisterRequest request,
        string? userAgent,
        string? ipAddress,
        CancellationToken cancellationToken = default);

    Task<Result<AuthResponse>> LoginAsync(
        LoginRequest request,
        string? userAgent,
        string? ipAddress,
        CancellationToken cancellationToken = default);

    Task<Result<AuthResponse>> RefreshAsync(
        string refreshToken,
        string? userAgent,
        string? ipAddress,
        CancellationToken cancellationToken = default);
}
