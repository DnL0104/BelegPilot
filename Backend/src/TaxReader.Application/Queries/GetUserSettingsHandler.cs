using Microsoft.EntityFrameworkCore;
using TaxReader.Application.DTOs;
using TaxReader.Application.Interfaces;
using TaxReader.Domain.Common;

namespace TaxReader.Application.Queries;

public class GetUserSettingsHandler(IAppDbContext dbContext, ICurrentUser currentUser)
{
    public async Task<Result<UserSettingsDto>> HandleAsync(
        CancellationToken cancellationToken = default)
    {
        var settings = await dbContext.Users
            .Where(u => u.Id == currentUser.UserId)
            .Select(u => new UserSettingsDto(u.AutoConfirmThreshold))
            .FirstOrDefaultAsync(cancellationToken);

        return settings is not null
            ? Result<UserSettingsDto>.Success(settings)
            : Result<UserSettingsDto>.Failure("User nicht gefunden.");
    }
}
