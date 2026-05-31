---
phase: 05-commercial-surface-payments
reviewed: 2026-05-31T00:00:00Z
depth: standard
files_reviewed: 31
files_reviewed_list:
  - Backend/src/TaxReader.Domain/Entities/Payment.cs
  - Backend/src/TaxReader.Domain/Enums/PaymentStatus.cs
  - Backend/src/TaxReader.Application/Interfaces/IStripePaymentProvider.cs
  - Backend/src/TaxReader.Application/DTOs/PaymentDtos.cs
  - Backend/src/TaxReader.Application/Jobs/GrantTokensJob.cs
  - Backend/src/TaxReader.Application/Jobs/RevokeTokensJob.cs
  - Backend/src/TaxReader.Infrastructure/Configuration/StripeOptions.cs
  - Backend/src/TaxReader.Infrastructure/Data/Configurations/PaymentConfiguration.cs
  - Backend/src/TaxReader.Infrastructure/Services/StripePaymentProvider.cs
  - Backend/src/TaxReader.Infrastructure/Services/StripeWebhookHandler.cs
  - Backend/src/TaxReader.Api/Endpoints/PaymentEndpoints.cs
  - Backend/src/TaxReader.Api/Endpoints/ReceiptFileEndpoints.cs
  - Backend/src/TaxReader.Api/Endpoints/TokenEndpoints.cs
  - Backend/src/TaxReader.Api/Program.cs
  - Backend/src/TaxReader.Domain/Entities/User.cs
  - Backend/src/TaxReader.Application/Interfaces/IAppDbContext.cs
  - Backend/src/TaxReader.Infrastructure/Data/AppDbContext.cs
  - Backend/src/TaxReader.Infrastructure/DependencyInjection.cs
  - Backend/tests/TaxReader.UnitTests/Jobs/GrantTokensJobTests.cs
  - Backend/tests/TaxReader.UnitTests/Jobs/RevokeTokensJobTests.cs
  - Backend/tests/TaxReader.UnitTests/Webhooks/StripeWebhookHandlerTests.cs
  - Backend/tests/TaxReader.UnitTests/Services/StripePaymentProviderTests.cs
  - Backend/tests/TaxReader.UnitTests/Configuration/StripeOptionsValidatorTests.cs
  - Frontend/src/hooks/use-billing.ts
  - Frontend/src/components/ui/checkbox.tsx
  - Frontend/src/types/api.ts
  - Frontend/src/lib/api-client.ts
  - Frontend/src/components/tokens/top-up-dialog.tsx
  - Frontend/src/components/tokens/token-balance-badge.tsx
  - Frontend/src/components/layout/app-sidebar.tsx
  - Frontend/src/app/(authenticated)/billing/page.tsx
findings:
  critical: 3
  warning: 5
  info: 3
  total: 11
status: issues_found
---

# Phase 05: Code Review Report

**Reviewed:** 2026-05-31T00:00:00Z
**Depth:** standard
**Files Reviewed:** 31
**Status:** issues_found

## Summary

This phase introduces the Stripe payment integration: checkout sessions, webhook handling, token grant/revoke jobs, a legal gate in the top-up dialog, and a new billing page. The overall architecture is sound — idempotency on `stripe_event_id`, typed options with startup validation, and direct DB access in Hangfire jobs to avoid the HTTP-context dependency are all correct decisions.

Three blockers were found: (1) a double-division of invoice amounts that makes all invoice prices display as 100× too small, (2) the legal-gate checkbox values are hardcoded `true` in the API call regardless of what the user actually checked — a browser bypass of a legally-required consent gate, and (3) the core webhook business-logic test is not actually calling the handler and is therefore a no-op assertion. Five warnings cover a race window between Payment insert and job enqueue, missing `StripeCustomerId` DB column configuration, the `charge.refunded` handler silently swallowing partial refunds, a stale dead-code API function, and a missing negative-balance guard in upload for the exact-zero case.

---

## Critical Issues

### CR-01: Invoice amount double-divided by 100 — amounts display as 1/100 of actual value

**File:** `Frontend/src/app/(authenticated)/billing/page.tsx:298`

**Issue:** `StripePaymentProvider.GetInvoicesAsync` already converts Stripe's integer-cents value to a decimal euro amount by dividing by `100m` (line 102 of `StripePaymentProvider.cs`). The `InvoiceDto.AmountPaid` is therefore already in euros when it reaches the frontend. The billing page then divides by `100` a second time: `formatCurrency(invoice.amountPaid / 100)`. A 14.99 EUR invoice will render as 0.1499 EUR.

**Fix:** Remove the `/100` from the billing page — the backend DTO already carries the final euro value:
```tsx
// Before (wrong)
{formatCurrency(invoice.amountPaid / 100)}

// After (correct)
{formatCurrency(invoice.amountPaid)}
```

---

### CR-02: Legal-gate checkboxes are never submitted — hardcoded `true` bypasses consent requirement

**File:** `Frontend/src/components/tokens/top-up-dialog.tsx:49-53`

**Issue:** The dialog collects two legal consent checkboxes (`agbChecked`, `widerrufsrechtChecked`) and gates the button's `disabled` state on them. However, `handleKaufen` always sends `waiverAccepted: true, agbAccepted: true` regardless of what the user actually checked. Any user who can programmatically submit the form (e.g., via the browser console calling the underlying mutation directly, or by triggering a click via automated means that bypasses the `disabled` check) will bypass the consent gate. More importantly, the API server validates `WaiverAccepted` and `AgbAccepted` from the request body — but because the frontend always sends `true`, the server-side check passes even if the user never ticked the boxes.

The fix has two parts: the frontend should submit the actual checkbox state, and the server-side endpoint should be the authoritative enforcement point (which it already is structurally, but it's fed wrong values).

**Fix:**
```tsx
// In handleKaufen, use the actual state values:
const data = await checkout.mutateAsync({
  credits: selected,
  waiverAccepted: widerrufsrechtChecked,   // the waiver checkbox
  agbAccepted: agbChecked,
});
```

---

### CR-03: `HandleAsync_ValidCheckoutSessionCompleted_InsertsPaymentAndEnqueuesJob` is a no-op test — the handler is never invoked

**File:** `Backend/tests/TaxReader.UnitTests/Webhooks/StripeWebhookHandlerTests.cs:78-113`

**Issue:** The test named `HandleAsync_ValidCheckoutSessionCompleted_InsertsPaymentAndEnqueuesJob` never calls `_handler.HandleAsync(...)`. It manually inserts a `Payment` row into the in-memory DB, then asserts `AnyAsync(p => p.StripeEventId == stripeEventId)` is `true` — a tautology. The `_jobClientMock` is never verified. The test comment on lines 85-88 acknowledges the HMAC difficulty but the workaround was never implemented: it tests neither the handler's payment insertion path nor the `GrantTokensJob` enqueue. If the handler's main `checkout.session.completed` branch is deleted entirely, this test still passes.

**Fix:** Either construct a HMAC-signed payload using `EventUtility.ParseEvent`/`ConstructEvent` with the test secret (Stripe's SDK supports this in test mode with a fixed timestamp), or refactor the handler to accept a pre-parsed `Event` to make it directly testable. At minimum, the test must call `_handler.HandleAsync(...)` and verify `_jobClientMock.Verify(c => c.EnqueueAsync<GrantTokensJob>(...))`.

---

## Warnings

### WR-01: Race window between Payment insert and GrantTokensJob enqueue — partial failure leaves Payment stuck in Pending forever

**File:** `Backend/src/TaxReader.Infrastructure/Services/StripeWebhookHandler.cs:85-103`

**Issue:** The handler calls `SaveChangesAsync` (line 99) to persist the `Payment` row, then immediately calls `jobClient.EnqueueAsync` (line 101). If `EnqueueAsync` throws (Hangfire Postgres storage unavailable, network blip), the `Payment` row is already committed with `Status = Pending` and there is no compensation path. Stripe will retry the webhook (standard Stripe retry policy), but the idempotency check on `stripe_event_id` (line 52) will find the existing row and return `200 Ok` immediately, skipping the job enqueue on every subsequent retry. The user's tokens are never granted and the payment is stuck in `Pending` permanently with no alert.

**Fix:** Enqueue the Hangfire job before committing to the DB (Hangfire's PostgreSQL storage is transactionally durable), or use Hangfire's `IBackgroundJobClient` inside a database transaction so both succeed or both roll back. Alternatively, add a periodic reconciliation job that finds `Pending` payments older than N minutes with no corresponding `Granted` status and re-enqueues the grant.

---

### WR-02: `UserConfiguration` does not configure `StripeCustomerId` — column is unbounded `text` in Postgres

**File:** `Backend/src/TaxReader.Infrastructure/Data/Configurations/UserConfiguration.cs` (not in scope) / `Backend/src/TaxReader.Domain/Entities/User.cs:27`

**Issue:** The `User` entity has a `string? StripeCustomerId` property. The migration (`20260528160059_AddPaymentsTableAndStripeCustomerId.cs:14-18`) correctly creates the column as `type: "text"` and nullable — but `UserConfiguration.cs` has no `builder.Property(e => e.StripeCustomerId)` call, so there is no `HasMaxLength` constraint. Stripe customer IDs follow the format `cus_XXXXXX` (max ~255 chars); without a bounded column the property silently accepts any length. More importantly, there is no index on `stripe_customer_id`. `StripeWebhookHandler` queries `Users` by `Id` (not customer ID) which is fine, but any future query filtering by customer ID will do a full table scan.

**Fix:** Add to `UserConfiguration.Configure`:
```csharp
builder.Property(e => e.StripeCustomerId).HasMaxLength(255);
// Optional but recommended for portal/invoice lookups:
builder.HasIndex(e => e.StripeCustomerId).IsUnique().HasFilter("stripe_customer_id IS NOT NULL");
```

---

### WR-03: `charge.refunded` handler silently ignores partial refunds — full credits always revoked regardless of refund amount

**File:** `Backend/src/TaxReader.Infrastructure/Services/StripeWebhookHandler.cs:109-143`

**Issue:** When a `charge.refunded` event arrives, the handler revokes `matchingPayment.CreditsGranted` (the full pack). However, Stripe supports partial refunds: a user who paid 14.99 EUR for 200 credits can receive a 7.50 EUR partial refund. In that case `charge.Amount` and `charge.AmountRefunded` would differ. The handler does not inspect `charge.AmountRefunded` vs `charge.Amount`, so a partial refund triggers full credit revocation.

**Fix:** Compare `charge.AmountRefunded` to `charge.Amount`:
```csharp
var isFullRefund = charge.AmountRefunded >= charge.Amount;
var creditsToRevoke = isFullRefund
    ? matchingPayment.CreditsGranted
    : (int)Math.Round(matchingPayment.CreditsGranted * ((decimal)charge.AmountRefunded / charge.Amount));
```
Or, at minimum, log a warning when `AmountRefunded < Amount` and document that partial refunds are treated as full revocations as a deliberate policy decision.

---

### WR-04: `GrantTokensJob` matches Pending payment by `(userId, credits)` — wrong payment can be marked Granted when user buys the same pack twice

**File:** `Backend/src/TaxReader.Application/Jobs/GrantTokensJob.cs:52-58`

**Issue:** The job finds the payment to mark `Granted` by:
```csharp
.Where(p => p.UserId == userId && p.Status == PaymentStatus.Pending && p.CreditsGranted == credits)
.OrderByDescending(p => p.CreatedAt)
.FirstOrDefaultAsync(cancellationToken);
```
If a user purchases the same 200-credit pack twice in quick succession, there are two `Pending` rows with the same `(userId, CreditsGranted)`. The job for the first checkout event will mark the most-recent `Pending` row `Granted`, and the job for the second event will mark the other row `Granted`. This can produce an incorrect cross-correlation (second event marks first purchase's row). In a retry scenario (WR-01), this mismatch could also cause the wrong payment record to be updated.

The root cause is that `GrantTokensJob` receives only `(userId, credits)` but not the Stripe event ID or session ID, which are the only reliable correlation keys available at job-enqueue time.

**Fix:** Pass the `stripeEventId` (or `stripeSessionId`) to `GrantTokensJob.HandleAsync` and use it as the correlation key:
```csharp
var payment = await dbContext.Payments
    .Where(p => p.StripeEventId == stripeEventId)
    .FirstOrDefaultAsync(cancellationToken);
```
Update the `RevokeTokensJob` in the same way — or keep the `PaymentIntentId` correlation already used by the webhook handler.

---

### WR-05: Dead-code `purchaseTokens` function in `api-client.ts` hits a removed endpoint

**File:** `Frontend/src/lib/api-client.ts:302-305`

**Issue:** `purchaseTokens` posts to `/tokens/purchase`. The corresponding `TokenEndpoints.cs` contains a comment on line 41: "POST /tokens/purchase stub removed — replaced by real POST /payments/checkout endpoint". The function is dead code that will produce a 404 or 405 if called. It is not imported by any billing component (confirmed by search), but it remains exported and visible to future developers as an apparently valid API function.

**Fix:** Delete `purchaseTokens` from `api-client.ts`:
```typescript
// Remove these lines (302-305):
export async function purchaseTokens(amount: number): Promise<TokenBalance> {
  const { data } = await api.post<TokenBalance>("/tokens/purchase", { amount });
  return data;
}
```

---

## Info

### IN-01: `StripeOptionsValidator` throws `InvalidOperationException` instead of returning `ValidateOptionsResult.Fail` — inconsistent with the `IValidateOptions<T>` contract

**File:** `Backend/src/TaxReader.Infrastructure/Configuration/StripeOptions.cs:41-43`

**Issue:** The `IValidateOptions<T>` pattern's contract is to return `ValidateOptionsResult.Fail(message)` for validation failures; the framework then handles propagation. Throwing directly instead works and fails fast, but it short-circuits the `ValidateOnStart` pipeline in a way that produces a raw uncaught exception in the startup log rather than a structured validation failure message. The `MissingSecretKey` and `MissingWebhookSecret` cases both return `Fail` correctly — only the production-with-test-key case throws. This inconsistency makes the test in `StripeOptionsValidatorTests.cs:27-40` have to `.Should().Throw<InvalidOperationException>()` while the other tests use `.Failed.Should().BeTrue()`.

**Fix:** Return `ValidateOptionsResult.Fail(...)` consistently:
```csharp
if (env.IsProduction() && options.SecretKey.StartsWith("sk_test_", StringComparison.Ordinal))
    return ValidateOptionsResult.Fail(
        "Stripe SecretKey ist ein Testschlüssel in einer Production-Umgebung.");
```
Update the test expectation to match.

---

### IN-02: `TokenBalanceBadge` treats balance of exactly 0 as "low" and shows amber warning

**File:** `Frontend/src/components/tokens/token-balance-badge.tsx:14`

**Issue:** `const isLow = balance <= 3` — a balance of exactly 0 (no credits remaining, but no debt) is styled amber-warning rather than destructive, and `isNegative` is `false`. A balance of 0 means the user has no credits and cannot process any new receipts. Showing amber instead of red may cause the user to miss that they are effectively blocked, since the upload guard checks `< 0` (negative), not `<= 0`. However the amber at 0 is misleading — the user can't do anything useful at 0 credits.

**Note:** This is also a minor consistency gap with the upload endpoint guard (`balance < 0` in `ReceiptFileEndpoints.cs:26`) — 0 credits will not block upload but will fail at AI classification time. The visual state and the functional state are not aligned, but fixing the badge alone won't fix the UX fully.

**Fix:** Change `isLow` to `balance > 0 && balance <= 3` so a zero balance falls into neither "low" nor "negative" but a distinct state, or adjust the threshold:
```tsx
const isLow = balance > 0 && balance <= 3;
// And add: const isEmpty = balance === 0; styled similarly to isNegative
```

---

### IN-03: `UserAvatar` in `app-sidebar.tsx` can produce empty initials for single-word display names with no spaces

**File:** `Frontend/src/components/layout/app-sidebar.tsx:44-50`

**Issue:**
```typescript
const initials = name
  .split(" ")
  .map((n) => n[0])
  .join("")
  .slice(0, 2)
  .toUpperCase();
```
If `user.displayName` is an empty string (e.g., not yet populated), `n[0]` is `undefined` for an array of `[""]`, and `undefined` joined becomes the string `"undefined"` — actually `.map((n) => n[0])` on `[""]` gives `[undefined]`, and `.join("")` on `[undefined]` gives `"undefined"`. The avatar would display `"UN"` for an empty display name.

**Fix:** Guard against empty segments:
```typescript
const initials = name
  .split(" ")
  .filter(Boolean)
  .map((n) => n[0])
  .join("")
  .slice(0, 2)
  .toUpperCase() || "?";
```

---

_Reviewed: 2026-05-31T00:00:00Z_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_
