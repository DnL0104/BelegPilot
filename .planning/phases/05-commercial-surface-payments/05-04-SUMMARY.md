---
phase: 05-commercial-surface-payments
plan: 04
subsystem: payments
tags: [stripe, hangfire, tokens, refunds, chargebacks, validation]

# Dependency graph
requires:
  - phase: 05-01
    provides: "Payment entity with StripePaymentIntentId, GrantTokensJob pattern, StripeOptionsValidator implementation, StripeWebhookHandler with charge.refunded stub"
provides:
  - "RevokeTokensJob: deducts credits from UserTokenBalance (balance can go negative per D-11)"
  - "charge.refunded webhook branch wired via StripePaymentIntentId correlation (no AmountCents ambiguity)"
  - "StripeOptionsValidatorTests: 4 tests covering D-13 guard scenarios"
  - "RevokeTokensJobTests: 4 tests covering PAY-05 behavior"
affects: [billing-page, token-display, receipt-file-upload-gate]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "RevokeTokensJob mirrors GrantTokensJob: direct IAppDbContext, no ITokenService (Pitfall 3), AutomaticRetry(3)"
    - "D-11: balance -= credits with no floor check — negative balance is intentional and triggers 402 on upload"
    - "charge.refunded correlation via StripePaymentIntentId (charge.PaymentIntentId == payment.StripePaymentIntentId) — not AmountCents"
    - "StripeOptionsValidator tested via direct instantiation with Moq IWebHostEnvironment (IsProduction() extension method)"

key-files:
  created:
    - Backend/src/TaxReader.Application/Jobs/RevokeTokensJob.cs
    - Backend/tests/TaxReader.UnitTests/Jobs/RevokeTokensJobTests.cs
    - Backend/tests/TaxReader.UnitTests/Configuration/StripeOptionsValidatorTests.cs
  modified:
    - Backend/src/TaxReader.Infrastructure/Services/StripeWebhookHandler.cs (charge.refunded stub replaced)
    - Backend/src/TaxReader.Api/Program.cs (RevokeTokensJob Scoped registration)

key-decisions:
  - "StripeWebhookHandler.cs (not PaymentEndpoints.cs) contains the charge.refunded handler — plan spec referenced the wrong file but the correct behavior was implemented in the right location"
  - "RevokeTokensJob uses IAppDbContext directly (no ITokenService) following GrantTokensJob Pitfall 3 pattern"
  - "charge.refunded correlation uses StripePaymentIntentId — NOT AmountCents — to avoid ambiguity when a user purchases the same pack twice"
  - "D-11: negative balance is by design; RevokeTokensJob never floors the balance"

requirements-completed: [PAY-05, PAY-06]

# Metrics
duration: ~20min
completed: 2026-05-28
---

# Phase 05 Plan 04: Refund Flow and Multi-Env Safety Summary

**RevokeTokensJob (PAY-05) + charge.refunded webhook via StripePaymentIntentId correlation + StripeOptionsValidator unit tests (PAY-06)**

## Performance

- **Duration:** ~20 min
- **Started:** 2026-05-28T16:15:03Z
- **Completed:** 2026-05-28
- **Tasks:** 2
- **Files modified:** 5 (3 created, 2 modified)

## Accomplishments

- `RevokeTokensJob` Hangfire fire-and-forget job that deducts credits directly via `IAppDbContext` (mirrors `GrantTokensJob` pattern)
- D-11: balance can go negative after refund — no floor check in `RevokeTokensJob`
- D-11: creates a `UserTokenBalance` with negative value when no balance record exists (user owes tokens)
- `RevokeTokensJob` updates the most recent `Granted` payment to `Revoked` and sets `RevokedAt`
- `charge.refunded` webhook branch now correlates via `StripePaymentIntentId` (not `AmountCents`) — one-to-one match even when user has multiple same-priced purchases
- `StripeWebhookHandler` warns and returns 200 when `PaymentIntentId` is missing or no `Granted` payment found (no crash, Stripe won't retry)
- `RevokeTokensJob` registered as `Scoped` in `Program.cs` alongside `GrantTokensJob`
- 4 `RevokeTokensJobTests` passing (balance deduction, negative balance, payment status transition, no-balance creation)
- 4 `StripeOptionsValidatorTests` passing (Production+sk_test_ throws, missing SecretKey fails, missing WebhookSecret fails, dev+test key succeeds)
- 252 total unit tests passing, 0 failures

## Task Commits

1. **Task 1: RevokeTokensJob + unit tests** - `58e27b0` (feat)
2. **Task 2: StripeOptionsValidator tests + charge.refunded wired + RevokeTokensJob registered** - `8b2a605` (feat)

## Files Created/Modified

**Created:**
- `Backend/src/TaxReader.Application/Jobs/RevokeTokensJob.cs` — Hangfire job: `HandleAsync(userId, credits, ct)`, `AutomaticRetry(3)`, `LogContext.PushProperty`, `balance -= credits` (no floor), `TokenTransaction(Type=Refund, Amount=-credits)`, updates most-recent Granted payment to Revoked
- `Backend/tests/TaxReader.UnitTests/Jobs/RevokeTokensJobTests.cs` — 4 tests covering PAY-05 behavior
- `Backend/tests/TaxReader.UnitTests/Configuration/StripeOptionsValidatorTests.cs` — 4 tests covering D-13 guard scenarios

**Modified:**
- `Backend/src/TaxReader.Infrastructure/Services/StripeWebhookHandler.cs` — replaced charge.refunded stub with StripePaymentIntentId correlation + `RevokeTokensJob` enqueue
- `Backend/src/TaxReader.Api/Program.cs` — added `builder.Services.AddScoped<RevokeTokensJob>()`

## Decisions Made

- **StripeWebhookHandler, not PaymentEndpoints, contains charge.refunded logic**: The plan spec referenced `PaymentEndpoints.cs` for the stub location, but the actual webhook handler was in `StripeWebhookHandler.cs` (Infrastructure). Updated the correct file. No behavior change — the plan's intent was fully realized.
- **StripePaymentIntentId correlation over AmountCents**: T-05-16 (Tampering threat) — `charge.PaymentIntentId == payment.StripePaymentIntentId` provides a one-to-one match. AmountCents matching would be ambiguous if a user buys the same pack twice (two rows with identical amounts).
- **RevokeTokensJob avoids ITokenService**: Follows Pitfall 3 from the codebase — `ITokenService` depends on `IHttpContextAccessor` which returns null in Hangfire worker context.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Plan References Wrong File] charge.refunded stub in StripeWebhookHandler.cs, not PaymentEndpoints.cs**
- **Found during:** Task 2 (reading PaymentEndpoints.cs to locate the stub)
- **Issue:** The plan's `<interfaces>` block quotes the stub as being in `Backend/src/TaxReader.Api/Endpoints/PaymentEndpoints.cs`. The actual stub was placed in `Backend/src/TaxReader.Infrastructure/Services/StripeWebhookHandler.cs` by Plan 05-01 (which consolidated webhook logic into a dedicated handler class for testability).
- **Fix:** Updated `StripeWebhookHandler.cs` with the StripePaymentIntentId correlation logic. The behavior is identical to what the plan specified — only the file location differed.
- **Files modified:** `Backend/src/TaxReader.Infrastructure/Services/StripeWebhookHandler.cs`
- **Commit:** `8b2a605`

## Known Stubs

None — `RevokeTokensJob` is fully wired. The `charge.refunded` → `RevokeTokensJob` → deduct balance pipeline is complete.

## Threat Flags

None — all STRIDE threats from the plan's threat model (T-05-14, T-05-15, T-05-16) were addressed:
- T-05-14: negative balance is intentional design (D-11), 402 gate on upload enforced in 05-01
- T-05-15: `StripeOptionsValidator` tested and proven to throw in Production + sk_test_ scenario
- T-05-16: `StripePaymentIntentId` correlation eliminates AmountCents ambiguity

## Self-Check: PASSED

- `Backend/src/TaxReader.Application/Jobs/RevokeTokensJob.cs` — FOUND
- `Backend/tests/TaxReader.UnitTests/Jobs/RevokeTokensJobTests.cs` — FOUND
- `Backend/tests/TaxReader.UnitTests/Configuration/StripeOptionsValidatorTests.cs` — FOUND
- `58e27b0` — FOUND (feat(05-04): implement RevokeTokensJob with unit tests)
- `8b2a605` — FOUND (feat(05-04): wire charge.refunded via StripePaymentIntentId)
- `dotnet build Backend` exits 0 — VERIFIED
- `dotnet test Backend --filter "RevokeTokensJob"` → 4 passed — VERIFIED
- `dotnet test Backend --filter "StripeOptionsValidator"` → 4 passed — VERIFIED
- `grep -c "Balance -= credits" RevokeTokensJob.cs` → 1 — VERIFIED (no floor check)
