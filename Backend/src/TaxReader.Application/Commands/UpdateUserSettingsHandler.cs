using Microsoft.EntityFrameworkCore;
using TaxReader.Application.DTOs;
using TaxReader.Application.Interfaces;
using TaxReader.Domain.Common;

namespace TaxReader.Application.Commands;

public class UpdateUserSettingsHandler(IAppDbContext dbContext, ICurrentUser currentUser)
{
    public async Task<Result<UserSettingsDto>> HandleAsync(
        UpdateUserSettingsCommand command,
        CancellationToken cancellationToken = default)
    {
        if (command.AutoConfirmThreshold is < 0.0 or > 1.0)
            return Result<UserSettingsDto>.Failure(
                "AutoConfirmThreshold muss zwischen 0.0 und 1.0 liegen.");

        var user = await dbContext.Users
            .FirstOrDefaultAsync(u => u.Id == currentUser.UserId, cancellationToken);

        if (user is null)
            return Result<UserSettingsDto>.Failure("User nicht gefunden.");

        user.AutoConfirmThreshold = command.AutoConfirmThreshold;
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result<UserSettingsDto>.Success(
            new UserSettingsDto(user.AutoConfirmThreshold));
    }
}
