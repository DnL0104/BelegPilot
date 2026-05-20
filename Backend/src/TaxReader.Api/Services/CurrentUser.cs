using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using TaxReader.Application.Interfaces;

namespace TaxReader.Api.Services;

public class CurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    public Guid UserId
    {
        get
        {
            var sub = httpContextAccessor.HttpContext?.User
                .FindFirstValue(JwtRegisteredClaimNames.Sub)
                ?? httpContextAccessor.HttpContext?.User
                    .FindFirstValue(ClaimTypes.NameIdentifier);

            return sub is not null && Guid.TryParse(sub, out var id)
                ? id
                : throw new UnauthorizedAccessException("No authenticated user.");
        }
    }
}
