using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using TaxReader.Application.DTOs;
using TaxReader.Application.Interfaces;
using TaxReader.Domain.Common;
using TaxReader.Domain.Enums;

namespace TaxReader.Application.Commands;

public class DeleteAccountHandler(
    IAppDbContext dbContext,
    ICurrentUser currentUser,
    IRefreshTokenService refreshTokenService,
    IAuditLogger auditLogger)
{
    // WR-02: exposed so the endpoint maps this failure to HTTP 401 without duplicating
    // the literal — a future wording change here can't silently break the status mapping.
    public const string InvalidPasswordError = "Ungültiges Passwort.";
    public const string UserNotFoundError = "Benutzer nicht gefunden.";

    public async Task<Result<bool>> HandleAsync(
        DeleteAccountRequest request,
        CancellationToken cancellationToken = default)
    {
        var userId = currentUser.UserId;

        var user = await dbContext.Users
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user is null)
            return Result<bool>.Failure(UserNotFoundError);

        // WR-03: defensive guard. The endpoint's FluentValidation pre-check (CR-02) already
        // rejects an empty password, but a null/empty value reaching BCrypt.Verify throws
        // ArgumentNullException → 500. Guard so this handler degrades to a clean 401
        // regardless of how it is called.
        if (string.IsNullOrEmpty(request.Password))
            return Result<bool>.Failure(InvalidPasswordError);

        // D-13 step 1: re-verify password (same factor that gates login).
        // Identical pattern to AuthService.cs:100 — BCrypt.Verify is constant-time.
        if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            return Result<bool>.Failure(InvalidPasswordError);

        // D-13 step 2: revoke ALL of the user's refresh tokens BEFORE the cascade
        // delete fires. The FK CASCADE on refresh_tokens.user_id would drop the rows
        // anyway, but the explicit revoke makes the operation auditable (Phase 6
        // LEG-08) and closes a small race window where an in-flight /auth/refresh
        // could otherwise re-issue against a user-row mid-delete.
        await refreshTokenService.RevokeAllForUserAsync(userId, cancellationToken);

        // LEG-08: record account deletion BEFORE cascade delete — user row must still exist
        // when the audit entry is written (T-06-11). Store email hash, not raw email (T-06-12).
        await auditLogger.RecordAsync(
            AuditAction.AccountDeleted,
            actorUserId: userId,
            subjectUserId: userId,
            metadata: new Dictionary<string, object?> { ["email_hash"] = HashEmail(user.Email) },
            cancellationToken);

        // D-13 step 3: removing the user row triggers the FK cascade chain. The cascades
        // are declared per-entity in Infrastructure/Data/Configurations/ (IN-02: not here,
        // and not all on UserConfiguration):
        //   off User:        refresh_tokens, receipt_files, UserTokenBalance, TokenTransactions
        //   off ReceiptFile: ProcessingRuns; Receipts -> ReceiptItems -> ItemClassifications
        dbContext.Users.Remove(user);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result<bool>.Success(true);
    }

    // V9 DSGVO Art. 5(1)(c): store SHA-256 hex of email rather than the raw address.
    private static string HashEmail(string email)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(email.ToLowerInvariant()));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
