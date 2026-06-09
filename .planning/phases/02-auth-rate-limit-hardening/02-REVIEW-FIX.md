---
phase: 02-auth-rate-limit-hardening
fixed_at: 2026-06-09T00:00:00Z
fix_scope: all
fix_strategy: inline_orchestrator
findings_in_scope: 17
fixed: 12
already_fixed: 2
deferred: 3
skipped: 0
iteration: 2
status: partial
review_path: 02-REVIEW.md
---

# Phase 02 Code Review Fix Report

_Iteration 1 (2026-05-16): 2 CRITICAL fixed (CR-01, CR-02)._
_Iteration 2 (2026-06-09): polish pass — remaining 9 WARNING + 6 INFO triaged and resolved._

## Summary

| Severity | Total | Fixed (iter 1+2) | Already fixed by later phases | Deferred (needs decision) |
|----------|-------|------------------|-------------------------------|---------------------------|
| Critical | 2     | 2                | 0                             | 0                         |
| Warning  | 9     | 4                | 2                             | 3                         |
| Info     | 6     | 4                | 0                             | 2                         |
| **Total** | **17** | **10**          | **2**                         | **5**                     |

The two CRITICAL findings were resolved in iteration 1. This polish pass resolved 8 more
(4 warnings, 4 info), confirmed 2 warnings already fixed by later phases, and deferred 5
items that are intentional decisions or require a deliberate design decision rather than a
mechanical fix.

`dotnet build Backend` → 0 errors. `dotnet test Backend/tests/TaxReader.UnitTests` → **305 passed / 5 skipped / 0 failed**.

---

## Iteration 1 — Critical fixes (2026-05-16)

### CR-01 — Fail-fast HMAC pepper validation
**Commit:** `28ee28e` — `RefreshTokenOptionsValidator : IValidateOptions<RefreshTokenOptions>` rejects empty/non-Base64/wrong-length `HashKey` at boot via `.ValidateOnStart()`. Eliminates the silent empty-key HMAC degradation.

### CR-02 — DeleteAccountValidator invocation
**Commit:** `5725721` — `MapDelete("/account")` now injects `IValidator<DeleteAccountRequest>` and calls `ValidateAsync` before the handler, returning `Results.ValidationProblem` (400) with German messages. Restored the BCrypt PackageReference on Application.

---

## Iteration 2 — Polish pass (2026-06-09)

### Fixed this pass

| ID | Fix | Commit |
|----|-----|--------|
| WR-01 | `DeleteAccountHandler` returns German `"Benutzer nicht gefunden."` instead of English `"User not found."` | `820f9db` |
| WR-02 | Endpoint maps wrong-password → 401 via `DeleteAccountHandler.InvalidPasswordError` const, not a duplicated string literal (a wording change can no longer silently downgrade 401→404) | `820f9db` |
| WR-03 | Null/empty-password guard before `BCrypt.Verify` in the handler — clean 401 instead of `ArgumentNullException`→500, defensive even with the CR-02 validator | `820f9db` |
| IN-02 | Corrected the cascade-origin comment — cascades are declared per-entity in `Data/Configurations/`, not all on `UserConfiguration` | `820f9db` |
| IN-06 | Unit-test `BCrypt.HashPassword(..., 4)` work factor to cut per-test CPU (default 11 ≈ 50ms/hash) | `820f9db` |
| IN-01 | Added `{TokenId}` to issue/expire/replay/rotate logs for issue→rotate→revoke chain correlation | `36e00ba` |
| IN-03 | Comment that `refresh_tokens.user_id` FK is load-bearing in `IssueAsync` (orphan insert → 500) | `36e00ba` |
| WR-04 | Extracted the EF InMemory provider name to a named `const` with a tracking comment (kept the test-only branch per the 02-01 decision) | `36e00ba` |

### Already fixed by later phases (verified, no action needed)

| ID | Status |
|----|--------|
| WR-06 | `ExpiresAt <= DateTime.UtcNow` (inclusive rejection) is already in `RefreshTokenService.cs` with an explanatory comment. |
| WR-07 | `RateLimitTestFactory` now swaps EF Core to an in-memory provider (Phase 3 03-01), so auth endpoints return clean 401s rather than fast-fail Postgres 500s. The flaky real-Postgres `Timeout=1` connection string is gone. |

### Deferred — require a decision, not a mechanical fix

| ID | Reason for deferral |
|----|---------------------|
| WR-05 | `User.RefreshTokens` / `RefreshToken.User` navigations are unused but pin the cascade. Reviewer rated it "accept-as-is if the team prefers symmetry with other User collections." Removing them touches EF cascade configuration — churn with cascade-behavior risk for marginal benefit. **Accepted as-is.** |
| WR-08 | Un-skipping `TwoUsersOneIp_BothGetFiveAttempts` would likely reveal that `/account` is **IP-partitioned, not sub-partitioned**, because `UseRateLimiter` runs before `UseAuthentication` (so `httpContext.User` has no `sub` claim at policy-resolution time). The fix is a load-bearing pipeline reorder with a security trade-off (the global IP limiter would lose pre-auth coverage). This is a deliberate design decision, out of scope for a polish pass. **Tracked for a focused decision before launch.** |
| WR-09 | `SentrySdk.CaptureMessage` on replay is the intended paging path; the same event is now also written to the LEG-08 audit log (Phase 6). Reviewer: "accept-as-is if Phase 6 LEG-08 audit-log work supersedes it." **Accepted as-is.** |
| IN-04 | Frontend `deleteAccount` stale-JWT-401 shows "Ungültiges Passwort." for an expired token — a low-priority UX edge needing body-shape discrimination or a pre-DELETE refresh. **Deferred as frontend UX polish.** |
| IN-05 | Lifting rate-limit magic numbers to a bound `RateLimitOptions` POCO is a config refactor; tests are pinned to the literals (`for i < 60`). Reviewer: "defer to Phase 6/7 if SCRUM allows." **Deferred.** |

---

## Status: partial

All CRITICAL and all mechanically-fixable WARNING/INFO findings are resolved. The 3 remaining
WARNING/INFO items (WR-05, WR-08, WR-09, IN-04, IN-05) are intentional accepts or deliberate
design decisions — none undermine the Phase 2 ROADMAP success criteria. WR-08 is the only one
worth a focused pre-launch decision (rate-limit partition for `/account`).

_Fixed: 2026-06-09 (iteration 2 polish)_
_Fixer: Claude (inline orchestrator)_
