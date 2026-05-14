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
