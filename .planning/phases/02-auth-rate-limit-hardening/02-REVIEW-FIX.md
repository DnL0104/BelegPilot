---
phase: 02-auth-rate-limit-hardening
fixed_at: 2026-05-16T00:00:00Z
fix_scope: critical_only
fix_strategy: inline_orchestrator
findings_in_scope: 2
fixed: 2
skipped: 9
iteration: 1
status: partial
review_path: 02-REVIEW.md
---

# Phase 02 Code Review Fix Report

_Fixed: 2026-05-16_
_Fix Scope: critical findings only (per orchestrator session bandwidth — user opted for inline fix path after gsd-code-fixer agent dispatch was interrupted)_

## Summary

Two CRITICAL findings from `02-REVIEW.md` were addressed; the nine WARNING findings remain open and tracked. INFO findings out of scope.

| Severity | Total | Fixed | Skipped |
|----------|-------|-------|---------|
| Critical | 2     | 2     | 0       |
| Warning  | 9     | 0     | 9       |
| Info     | 6     | 0     | 6       |
| **Total** | **17** | **2** | **15** |

## Fixed

### CR-01 — Fail-fast HMAC pepper validation
**File:** `Backend/src/TaxReader.Infrastructure/Configuration/RefreshTokenOptions.cs`, `Backend/src/TaxReader.Infrastructure/DependencyInjection.cs`
**Commit:** `28ee28e fix(02-01): CR-01 fail fast on missing/invalid RefreshToken HashKey`

**Before:** `RefreshTokenService` would silently degrade to an empty-key HMAC if `REFRESHTOKEN_HASHKEY` was unset. `Convert.FromBase64String("")` returns a zero-length array without throwing; HMAC-SHA256 accepts it. Every refresh-token hash would collapse to an unprotected SHA-256-derived value with no operational signal. Nullifies the core premise of D-01.

**After:** New `RefreshTokenOptionsValidator : IValidateOptions<RefreshTokenOptions>` rejects three failure modes at boot: (1) empty/whitespace `HashKey`, (2) non-Base64 string, (3) decoded byte length ≠ 32. DI wires it via `services.AddSingleton<IValidateOptions<RefreshTokenOptions>, RefreshTokenOptionsValidator>()` + `.AddOptions<RefreshTokenOptions>().Bind(...).ValidateOnStart()`. A missing or malformed value now fails the host build loudly with a specific error message that includes the `openssl rand -base64 32` generation hint.

### CR-02 — DeleteAccountValidator invocation
**File:** `Backend/src/TaxReader.Api/Endpoints/AuthEndpoints.cs` (+ `Backend/src/TaxReader.Application/TaxReader.Application.csproj` restore)
**Commit:** `5725721 fix(02-02): CR-02 invoke DeleteAccountValidator + restore BCrypt PackageReference`

**Before:** `DeleteAccountValidator` was registered for DI via `AddValidatorsFromAssemblyContaining<>` but Minimal APIs do not auto-run FluentValidation. The validator and its 8 tests were dead code. A null/empty `Password` reached `BCrypt.Verify` and threw `ArgumentNullException` → 500 instead of the intended 400 with `"Passwort ist erforderlich."`.

**After:** The `MapDelete("/account", ...)` handler now injects `IValidator<DeleteAccountRequest>` and calls `await validator.ValidateAsync(request, ct)` before invoking `DeleteAccountHandler.HandleAsync`. Validation failures return `Results.ValidationProblem(errors)` (HTTP 400) with grouped per-property German messages from the validator. Also restored `BCrypt.Net-Next` PackageReference in `TaxReader.Application.csproj` that commit `7de3dcc` accidentally reverted — `DeleteAccountHandler` depends on it at the Application layer.

## Skipped (out of scope this run)

These nine WARNING findings remain open and are tracked in `02-REVIEW.md`. Recommended follow-ups annotated.

| ID | File | Skip Reason |
|----|------|-------------|
| WR-01 | `DeleteAccountHandler.cs:23` | German localization of `"User not found."` — straightforward, but parked because the endpoint never returns 404 in practice (auth middleware blocks unauthenticated callers, so the user always exists). Treat as polish. |
| WR-02 | `AuthEndpoints.cs:88-91` | Discriminated error type for 401 vs 404 mapping. Sound suggestion but requires a `Result<T>` extension or sentinel pattern decision that should be made project-wide (not just this endpoint). Defer to a `/gsd-quick` polish pass. |
| WR-03 | `DeleteAccountHandler.cs:27` | BCrypt.Verify null/empty guard — superseded by CR-02 fix above (validator now rejects empty Password before it reaches BCrypt). Defensive guard would still be nice; keep tracking. |
| WR-04 | `RefreshTokenService.cs:154-155` | Provider-name string branch is intentional per 02-01 key decision (no InMemory package dependency leaking into production Infrastructure). The existing comment already explains the trade-off. Mark as `decision_documented`. |
| WR-05 | `RefreshTokenService.cs:79` | `<` vs `<=` for `ExpiresAt` boundary. One-line change but needs corresponding test for the boundary tick. Worth doing but bundled with other auth polish. |
| WR-06 | `RateLimitTestFactory.cs:32-33` | Test factory's `BeOneOf(401, 400)` is flaky under fast-fail Postgres. Either loosen to also accept 500 or rewrite the test DB strategy. Touching test infrastructure is risky mid-phase; defer to a dedicated test-hardening pass. |
| WR-07 | `AuthStrictPolicyTests.cs:55` | Un-skip the /account partition-by-sub test now that the endpoint exists. Should happen, but the existing 5/9 active tests already cover the sub-partition codepath via login burns; the /account specific test is incremental coverage. Defer. |
| WR-08 | Pipeline order (`Program.cs:269-278`) | `UseRateLimiter` before `UseAuthentication` is intentional per 02-03 RESEARCH Pitfall 2 (global IP limiter must trigger on unauthenticated traffic; per-endpoint sub-partitioned policies attach at the endpoint layer where claims are available). Comment requested by reviewer; not added in this pass. Mark as `decision_to_document`. |
| WR-09 | Various INFO items | Logging gaps, magic numbers, comment polish, frontend dialog stale-JWT 401 corner case. Six INFO findings remain. Out of scope for `critical_warning` fix mode. |

## Build & Test Verification

- `dotnet build Backend` — **0 errors**, 2 pre-existing NU1510 warnings (unrelated)
- `dotnet test Backend` — **139 passed / 5 skipped / 0 failed**

The 5 skipped tests are intentional manual-UAT deferrals documented in `02-VALIDATION.md` (concurrency limiter timing, X-Forwarded-For reverse-proxy simulation under WAF, /account partition-by-sub, MigrationTests deferred to Phase 7 QA-01).

## Status: partial

Both CRITICAL findings resolved. The phase's core security premise (HMAC pepper enforcement) and the AUTH-02 surface (DELETE /account with German validation) now behave correctly. Nine WARNING findings remain open as advisory items in `02-REVIEW.md` — none of them undermine the ROADMAP Phase 2 success criteria, but several are worth addressing in a follow-up polish pass.

## Next Steps

- ✓ Phase 2 can close out cleanly (verifier already returned `human_needed` with 4 manual UAT items; this fix pass does not change that verdict).
- Re-run `/gsd-code-review 2` after a polish pass to confirm the remaining WARNING items have been addressed.
- Track the deferred items as `/gsd-add-todo` entries or roll into Phase 7 QA-01 if they're test-side.
