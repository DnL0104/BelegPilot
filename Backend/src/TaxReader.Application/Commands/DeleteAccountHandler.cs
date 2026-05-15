using Microsoft.EntityFrameworkCore;
using TaxReader.Application.DTOs;
using TaxReader.Application.Interfaces;
using TaxReader.Domain.Common;

namespace TaxReader.Application.Commands;

public class DeleteAccountHandler(
    IAppDbContext dbContext,
    ICurrentUser currentUser,
    IRefreshTokenService refreshTokenService)
{
    public async Task<Result<bool>> HandleAsync(
        DeleteAccountRequest request,
        CancellationToken cancellationToken = default)
    {
        var userId = currentUser.UserId;

        var user = await dbContext.Users
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user is null)
            return Result<bool>.Failure("User not found.");

        // D-13 step 1: re-verify password (same factor that gates login).
        // Identical pattern to AuthService.cs:100 — BCrypt.Verify is constant-time.
        if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            return Result<bool>.Failure("Ungültiges Passwort.");

        // D-13 step 2: revoke ALL of the user's refresh tokens BEFORE the cascade
        // delete fires. The FK CASCADE on refresh_tokens.user_id would drop the rows
        // anyway, but the explicit revoke makes the operation auditable (Phase 6
        // LEG-08) and closes a small race window where an in-flight /auth/refresh
        // could otherwise re-issue against a user-row mid-delete.
        await refreshTokenService.RevokeAllForUserAsync(userId, cancellationToken);

        // D-13 step 3: cascade delete via FK. Drops refresh_tokens, receipt_files,
        //   ReceiptFiles -> ProcessingRuns
        //               -> Receipts -> ReceiptItems -> ItemClassifications
        //   UserTokenBalance
        //   TokenTransactions
        dbContext.Users.Remove(user);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result<bool>.Success(true);
    }
}
