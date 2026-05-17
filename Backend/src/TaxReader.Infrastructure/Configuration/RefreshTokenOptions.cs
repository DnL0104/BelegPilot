using Microsoft.Extensions.Options;

namespace TaxReader.Infrastructure.Configuration;

public class RefreshTokenOptions
{
    public const string SectionName = "RefreshToken";

    /// <summary>
    /// Base64-encoded 32-byte HMAC-SHA256 pepper. Generate with:
    /// <c>openssl rand -base64 32</c>. Rotating this value invalidates all
    /// existing refresh tokens (forces re-login).
    /// </summary>
    public string HashKey { get; set; } = string.Empty;
}

/// <summary>
/// Fails the host build if RefreshToken:HashKey is missing, not Base64, or not
/// exactly 32 bytes after decoding. Without this guard an empty value silently
/// degrades HMAC-SHA256 to an empty-key construction (CR-01 from the Phase 2
/// review) — the API would boot, tokens would "work", but the pepper would
/// provide zero security.
/// </summary>
public sealed class RefreshTokenOptionsValidator : IValidateOptions<RefreshTokenOptions>
{
    public ValidateOptionsResult Validate(string? name, RefreshTokenOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.HashKey))
        {
            return ValidateOptionsResult.Fail(
                "RefreshToken:HashKey is not configured. Generate with " +
                "`openssl rand -base64 32` and set REFRESHTOKEN_HASHKEY.");
        }

        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(options.HashKey);
        }
        catch (FormatException)
        {
            return ValidateOptionsResult.Fail(
                "RefreshToken:HashKey is not valid Base64.");
        }

        if (bytes.Length != 32)
        {
            return ValidateOptionsResult.Fail(
                $"RefreshToken:HashKey must be exactly 32 bytes (got {bytes.Length}). " +
                "Generate with `openssl rand -base64 32`.");
        }

        return ValidateOptionsResult.Success;
    }
}
