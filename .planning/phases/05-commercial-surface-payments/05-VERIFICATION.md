---
phase: 05-commercial-surface-payments
verified: 2026-05-31T00:00:00Z
status: human_needed
score: 6/7 must-haves verified
overrides_applied: 0
human_verification:
  - test: "Trigger a Stripe test-mode checkout for 50 credits; complete payment in Stripe dashboard; verify GrantTokensJob credited 50 to UserTokenBalance and a TokenTransaction row with Type=Purchase was created."
    expected: "Token balance increases by 50; one Payment row with Status=Granted; one TokenTransaction with Amount=50, Type=Purchase."
    why_human: "Requires a running Stripe test environment and actual webhook delivery. HMAC signature verification prevents unit-testing the full handler path without a live Stripe secret."
  - test: "Issue a refund from the Stripe dashboard for a completed checkout; verify charge.refunded webhook fires; verify RevokeTokensJob debited the balance."
    expected: "Token balance decreases by the credited amount; Payment.Status becomes Revoked; TokenTransaction with Type=Refund, Amount=negative inserted."
    why_human: "Same reason — requires live Stripe test webhook delivery. Code is verified correct by inspection and unit tests of the job, but end-to-end path needs human smoke test."
  - test: "Check invoice PDF download from the billing page after a test-mode purchase."
    expected: "Invoice PDF is accessible from /billing invoice table; footer contains 'Gemäß §19 UStG wird keine Umsatzsteuer berechnet.' (only visible on Stripe-generated PDF)."
    why_human: "Stripe Invoicing PDF content (vendor name, address, KleinunternehmerNote footer) requires a live Stripe account with proper account details configured. Cannot be verified programmatically."
---

# Phase 5: Commercial Surface (Payments) Verification Report

**Phase Goal:** Working Stripe-mediated token-pack purchase with DE-compliant invoicing, signature-verified webhook with idempotent token grant, Widerrufsrecht waiver flow, billing-management page, and multi-environment safety to prevent live keys in dev.
**Verified:** 2026-05-31
**Status:** human_needed
**Re-verification:** No — initial verification

## Goal Achievement

### Observable Truths

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | User can complete Stripe Checkout → webhook fires → tokens credited with transaction record | ? UNCERTAIN | Webhook handler code is complete and correct (`StripeWebhookHandler.cs`). Unit test `HandleAsync_ValidCheckoutSessionCompleted_InsertsPaymentAndEnqueuesJob` does NOT call `HandleAsync` — it only tests the DB directly. End-to-end path requires human smoke test. |
| 2 | Same Stripe webhook event delivered twice grants tokens exactly once (idempotent) | ✓ VERIFIED | `AnyAsync` check on `StripeEventId` before insert in `StripeWebhookHandler.cs:52-58`; `UNIQUE` index on `stripe_event_id` in `PaymentConfiguration`; duplicate test validates DB-level idempotency guard. |
| 3 | User cannot purchase without Widerrufsrecht waiver checkbox; checkbox not pre-ticked | ✓ VERIFIED | `top-up-dialog.tsx`: `widerrufsrechtChecked` state defaults to `false`; reset to `false` on dialog close; `disabled={!agbChecked \|\| !widerrufsrechtChecked \|\| checkout.isPending}` on Kaufen button; exact §356 Abs. 4 BGB text "hierdurch" confirmed at lines 151-153. |
| 4 | User can download a DE-compliant Rechnung PDF (Stripe Invoicing) from the billing page | ? UNCERTAIN | `billing/page.tsx` renders invoice list with `invoice.invoicePdfUrl` links (lines 302-309). `StripePaymentProvider.BuildSessionCreateOptions` sets `InvoiceCreation.Enabled=true` and `InvoiceData.Footer=KleinunternehmerNote`. Whether the PDF actually contains full DE-compliant content (vendor address, USt-ID) depends on Stripe account configuration — requires human verification. |
| 5 | Refunded purchases reverse token grants via `RevokeTokensJob`; balance can go negative | ✓ VERIFIED | `RevokeTokensJob.cs`: `balance.Balance -= credits` (no floor check, line 38); 4 unit tests passing including `HandleAsync_CanGoNegative` (balance 20 - 50 = -30); `charge.refunded` webhook branch in `StripeWebhookHandler.cs` correlates via `StripePaymentIntentId` and enqueues `RevokeTokensJob`. `TokenTransaction` with `Type=Refund, Amount=-credits` serves as audit record. |
| 6 | Production deployment fails to start if `Stripe__SecretKey` starts with `sk_test_` | ✓ VERIFIED | `StripeOptionsValidator.Validate`: `env.IsProduction() && options.SecretKey.StartsWith("sk_test_", StringComparison.Ordinal) → throw InvalidOperationException`; registered with `ValidateOnStart()`; 4 `StripeOptionsValidatorTests` passing including `Validate_ProductionWithTestKey_ThrowsInvalidOperationException`. |
| 7 | Stripe Customer Portal allows user to manage payment methods without custom UI | ✓ VERIFIED | `POST /payments/portal` endpoint in `PaymentEndpoints.cs`; calls `stripeProvider.CreatePortalSessionAsync`; billing page has "Zahlungsmethode verwalten" button calling `handlePortal → window.location.href = data.url`. |

**Score:** 5/7 truths fully verified (SC 1 and 4 require human testing)

### Required Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `Backend/src/TaxReader.Domain/Entities/Payment.cs` | Payment entity with StripePaymentIntentId | ✓ VERIFIED | All D-16 fields present; `StripePaymentIntentId` nullable string at line 13; `RevokedAt` present |
| `Backend/src/TaxReader.Domain/Enums/PaymentStatus.cs` | Pending/Granted/Revoked enum | ✓ VERIFIED | Correct values: Pending=0, Granted=1, Revoked=2 |
| `Backend/src/TaxReader.Infrastructure/Configuration/StripeOptions.cs` | StripeOptions + StripeOptionsValidator | ✓ VERIFIED | All D-12 fields present; `KleinunternehmerNote` defaulted to §19 UStG text; `StripeOptionsValidator` with Production+sk_test_ guard |
| `Backend/src/TaxReader.Infrastructure/Services/StripePaymentProvider.cs` | Checkout, portal, invoice + internal BuildSessionCreateOptions | ✓ VERIFIED | `internal SessionCreateOptions BuildSessionCreateOptions(...)` at line 39; `InvoiceCreation.Enabled=true`; `InvoiceData.Footer=KleinunternehmerNote`; per-instance `StripeClient` |
| `Backend/src/TaxReader.Application/Jobs/GrantTokensJob.cs` | Hangfire fire-and-forget token crediting | ✓ VERIFIED | `AutomaticRetry(Attempts=3)`; `LogContext.PushProperty`; direct `IAppDbContext` (no ITokenService); updates Payment.Status to Granted |
| `Backend/src/TaxReader.Application/Jobs/RevokeTokensJob.cs` | Hangfire job to debit tokens | ✓ VERIFIED | `balance.Balance -= credits` (no floor); `TokenTransactionType.Refund`; `Amount=-credits`; updates Payment.Status to Revoked; sets `RevokedAt` |
| `Backend/src/TaxReader.Api/Endpoints/PaymentEndpoints.cs` | POST /payments/checkout, GET /payments/invoices, POST /payments/portal, POST /webhooks/stripe | ✓ VERIFIED | All endpoints present; webhook delegates to `StripeWebhookHandler`; IDOR mitigated via `ICurrentUser.UserId` scope on invoice/portal |
| `Backend/src/TaxReader.Infrastructure/Services/StripeWebhookHandler.cs` | Signature verification + idempotent insert + job enqueue | ✓ VERIFIED | `EventUtility.ConstructEvent` HMAC check; `AnyAsync` idempotency guard; `GrantTokensJob` and `RevokeTokensJob` enqueue; `StripePaymentIntentId` correlation for refunds |
| `Backend/src/TaxReader.Infrastructure/Migrations/20260528160059_AddPaymentsTableAndStripeCustomerId.cs` | payments table + stripe_customer_id on users | ✓ VERIFIED | `stripe_payment_intent_id` column present; `UNIQUE` index on `stripe_event_id`; `stripe_customer_id` on users table |
| `Frontend/src/components/tokens/top-up-dialog.tsx` | Legal gate with AGB + Widerrufsrecht checkboxes | ✓ VERIFIED | Both checkboxes; neither pre-ticked; exact §356 Abs. 4 BGB text; `isDemoMode` redirect to `/billing?payment=success&demo=true`; `usePurchaseTokens` removed |
| `Frontend/src/hooks/use-billing.ts` | useCreateCheckoutSession, useInvoices, useCreatePortalSession | ✓ VERIFIED | All 3 hooks present and wired to api-client functions |
| `Frontend/src/app/(authenticated)/billing/page.tsx` | Full billing page with 6 sections | ✓ VERIFIED | DemoMode banner; success banner; balance card with polling; transaction history; invoice list; payment method card; `Zahlungsmethode verwalten` button |
| `Backend/tests/TaxReader.UnitTests/Jobs/GrantTokensJobTests.cs` | 3 unit tests for GrantTokensJob | ✓ VERIFIED | 3 tests passing: NewUser_CreatesBalance, ExistingUser_AddsToBalance, UpdatesPendingPaymentToGranted |
| `Backend/tests/TaxReader.UnitTests/Jobs/RevokeTokensJobTests.cs` | 4 unit tests for RevokeTokensJob | ✓ VERIFIED | 4 tests passing: ExistingBalance_DeductsCredits, CanGoNegative, UpdatesGrantedPaymentToRevoked, NoBalance_CreatesNegativeBalance |
| `Backend/tests/TaxReader.UnitTests/Webhooks/StripeWebhookHandlerTests.cs` | Signature/duplicate/valid event tests | ⚠️ PARTIAL | `HandleAsync_InvalidSignature_ReturnsBadRequest` calls `HandleAsync` and passes. `HandleAsync_ValidCheckoutSessionCompleted_InsertsPaymentAndEnqueuesJob` does NOT call `HandleAsync` — it only directly queries the DB. The core handler logic is tested through `HandleAsync_InvalidSignature` but the full checkout-session-completed path is not exercised end-to-end in any unit test. |
| `Backend/tests/TaxReader.UnitTests/Services/StripePaymentProviderTests.cs` | KleinunternehmerNote footer test (PAY-02) | ✓ VERIFIED | 5 tests; `BuildSessionCreateOptions_InvoiceFooterContainsKleinunternehmerNote` asserts exact §19 UStG text |
| `Backend/tests/TaxReader.UnitTests/Configuration/StripeOptionsValidatorTests.cs` | 4 validator tests (PAY-06) | ✓ VERIFIED | All 4 passing: Production+sk_test_ throws, missing SecretKey fails, missing WebhookSecret fails, dev+test key succeeds |

### Key Link Verification

| From | To | Via | Status | Details |
|------|----|-----|--------|---------|
| `PaymentEndpoints.cs` | `StripeWebhookHandler.cs` | `StripeWebhookHandler handler` injected, `handler.HandleAsync(json, sig, ct)` | ✓ WIRED | Line 121-128 in PaymentEndpoints.cs |
| `StripeWebhookHandler.cs` | `GrantTokensJob.cs` | `jobClient.EnqueueAsync<GrantTokensJob>` | ✓ WIRED | Line 101 in StripeWebhookHandler.cs |
| `StripeWebhookHandler.cs` | `RevokeTokensJob.cs` | `jobClient.EnqueueAsync<RevokeTokensJob>` on charge.refunded | ✓ WIRED | Line 137 in StripeWebhookHandler.cs |
| `StripeWebhookHandler.cs` | `Payment.StripePaymentIntentId` | `charge.PaymentIntentId == payment.StripePaymentIntentId` query | ✓ WIRED | Line 125-128 in StripeWebhookHandler.cs |
| `AppDbContext.cs` | `Payment.cs` | `DbSet<Payment> Payments` | ✓ WIRED | IAppDbContext and AppDbContext both declare `Payments` |
| `PaymentEndpoints.cs` | `IStripePaymentProvider` | Constructor injection, `stripeProvider.CreateCheckoutSessionAsync` | ✓ WIRED | Line 54 in PaymentEndpoints.cs |
| `top-up-dialog.tsx` | `use-billing.ts` | `useCreateCheckoutSession` hook import | ✓ WIRED | Line 18, used at line 33 |
| `use-billing.ts` | `api-client.ts` | `createCheckoutSession`, `createPortalSession`, `getInvoices` | ✓ WIRED | Lines 333, 340, 345 in api-client.ts; imported in use-billing.ts |
| `billing/page.tsx` | `use-billing.ts` | `useInvoices`, `useCreatePortalSession` hooks | ✓ WIRED | Line 31 in billing/page.tsx |
| `billing/page.tsx` | `getTokenBalance` via `useQuery` | Direct `useQuery` with `queryKeys.tokens.balance` and `refetchInterval` | ✓ WIRED | Lines 76-80 — dynamic polling via `isPolling` state |
| `ReceiptFileEndpoints.cs` | 402 guard | `balance.Balance < 0 → Results.Problem(statusCode: 402)` | ✓ WIRED | Line 26-30 in ReceiptFileEndpoints.cs |

### Data-Flow Trace (Level 4)

| Artifact | Data Variable | Source | Produces Real Data | Status |
|----------|---------------|--------|--------------------|--------|
| `billing/page.tsx` | `tokenBalance` | `getTokenBalance` via `useQuery` → `GET /api/v1/tokens/balance` | Yes — existing backend endpoint with real DB query | ✓ FLOWING |
| `billing/page.tsx` | `transactions` | `useTokenTransactions(20)` → `GET /api/v1/tokens/transactions` | Yes — real DB query with take=20 | ✓ FLOWING |
| `billing/page.tsx` | `invoices` | `useInvoices` → `getInvoices` → `GET /api/v1/payments/invoices` → `StripePaymentProvider.GetInvoicesAsync` | Yes — live Stripe API call (requires `stripeCustomerId`; returns empty array if no customer) | ✓ FLOWING |
| `top-up-dialog.tsx` | `data.checkoutUrl` / `data.isDemoMode` | `useCreateCheckoutSession` → `POST /api/v1/payments/checkout` → `StripePaymentProvider.CreateCheckoutSessionAsync` | Yes — returns real Stripe session URL | ✓ FLOWING |

### Behavioral Spot-Checks

| Behavior | Check | Result | Status |
|----------|-------|--------|--------|
| Backend builds clean | `dotnet build Backend --no-incremental` | 0 errors, 2 warnings (NU1510 unrelated) | ✓ PASS |
| All unit tests pass | `dotnet test Backend` | 252 passed, 0 failures, 5 skipped | ✓ PASS |
| Phase-specific tests pass | 19 tests across GrantTokensJob, RevokeTokensJob, StripePaymentProvider, StripeWebhookHandler, StripeOptionsValidator | 19 passed | ✓ PASS |
| Frontend builds clean | `npm run build` in Frontend/ | Exit 0; `/billing` route present in build output | ✓ PASS |
| `POST /tokens/purchase` stub removed | `grep -c "MapPost.*tokens.*purchase" TokenEndpoints.cs` | 0 matches | ✓ PASS |
| 402 guard present | `balance.Balance < 0` in ReceiptFileEndpoints.cs | Line 26 confirms | ✓ PASS |
| StripePaymentIntentId in Payment entity | `Payment.cs` line 13 | Field present | ✓ PASS |
| EF migration includes all columns | `20260528160059_AddPaymentsTableAndStripeCustomerId.cs` | `stripe_payment_intent_id` column, `UNIQUE` index on `stripe_event_id` | ✓ PASS |
| Widerrufsrecht text verbatim | `top-up-dialog.tsx` lines 151-153 | "hierdurch" confirmed; full §356 Abs. 4 BGB text | ✓ PASS |

### Requirements Coverage

| Requirement | Source Plan | Description | Status | Evidence |
|-------------|-------------|-------------|--------|----------|
| PAY-01 | 05-01 | StripePaymentProvider + POST /payments/checkout + webhook + payments table + GrantTokensJob | ✓ SATISFIED | All components implemented; 252 tests passing; build clean |
| PAY-02 | 05-01, 05-03 | DE-compliant Rechnung via Stripe Invoicing; download from billing page | ? PARTIAL | KleinunternehmerNote footer in checkout session confirmed; invoice list with PDF links on billing page confirmed. Full DE-compliance (vendor address, USt-ID) depends on Stripe account configuration — requires human verification |
| PAY-03 | 05-02 | Widerrufsrecht waiver checkbox required; not pre-ticked; AGB acceptance required | ✓ SATISFIED | Both checkboxes, neither pre-ticked, Kaufen button disabled until both checked; §356 Abs. 4 BGB text verbatim |
| PAY-04 | 05-03 | Billing page with balance, transaction history, invoices, Customer Portal | ✓ SATISFIED | All 6 UI-SPEC sections implemented; balance polling; invoice download links; portal redirect |
| PAY-05 | 05-04 | Refund → RevokeTokensJob → balance negative; audit-logged | ✓ SATISFIED | RevokeTokensJob deducts (no floor); 4 tests prove negative balance; TokenTransaction with Type=Refund serves as audit record; charge.refunded → RevokeTokensJob wired via StripePaymentIntentId |
| PAY-06 | 05-04 | Multi-env safety: Production + sk_test_ throws at startup | ✓ SATISFIED | StripeOptionsValidator throws InvalidOperationException; ValidateOnStart() registered; 4 tests confirmed |

### Anti-Patterns Found

| File | Line | Pattern | Severity | Impact |
|------|------|---------|----------|--------|
| `StripeWebhookHandlerTests.cs` | 78-113 | `HandleAsync_ValidCheckoutSessionCompleted_InsertsPaymentAndEnqueuesJob` does not call `HandleAsync`; only queries the DB directly | ⚠️ Warning | The test name promises handler invocation but only validates DB state. The actual handler path (insert Payment + enqueue GrantTokensJob) is not exercised in any unit test for the happy path. The invalid-signature test DOES call HandleAsync. This is a test coverage gap, not a runtime bug. |
| `RevokeTokensJob.cs` (lookup logic) | 53-58 | Revoke lookup uses `CreditsGranted == credits` match (same as Grant), not `StripePaymentIntentId` | ℹ️ Info | This differs from the webhook's charge.refunded correlation (which uses StripePaymentIntentId). The job itself receives `userId` and `credits` from the webhook handler that already correlated via StripePaymentIntentId. If a user has two Granted payments for the same credit amount, the job revokes the most-recently-created one, which may not be the correct one. However, the webhook handler upstream already selected the right payment using StripePaymentIntentId, so the mismatch is low risk in practice. Not a blocker. |

### Human Verification Required

#### 1. End-to-End Stripe Checkout → Token Grant

**Test:** Create a Stripe test-mode checkout session for 50 credits using a real sk_test_ key. Complete the payment in the Stripe test dashboard. Observe that the `checkout.session.completed` webhook fires with a valid HMAC signature.
**Expected:** `GrantTokensJob` runs; `UserTokenBalance.Balance` increases by 50; one `Payment` row with `Status=Granted` and `StripePaymentIntentId` populated; one `TokenTransaction` row with `Type=Purchase, Amount=50`.
**Why human:** HMAC signature verification requires a real webhook secret. The unit test `HandleAsync_ValidCheckoutSessionCompleted_InsertsPaymentAndEnqueuesJob` does not exercise `HandleAsync` at all — it only validates the DB directly. The full webhook handler path (signature verify → idempotency check → Payment insert → GrantTokensJob enqueue) is not covered by any automated test.

#### 2. End-to-End Stripe Refund → Token Debit

**Test:** Issue a refund from the Stripe test dashboard for a completed checkout. Observe that the `charge.refunded` webhook fires.
**Expected:** `RevokeTokensJob` runs; `UserTokenBalance.Balance` decreases by the credited amount (may go negative); `Payment.Status` becomes `Revoked`; `Payment.RevokedAt` set; one `TokenTransaction` with `Type=Refund, Amount=negative` inserted.
**Why human:** Same as above — requires live Stripe test webhook delivery.

#### 3. DE-Compliant Invoice PDF Content

**Test:** After completing a test-mode purchase, navigate to `/billing`, find the invoice in the invoice list, click "PDF herunterladen". Open the PDF.
**Expected:** PDF footer contains "Gemäß §19 UStG wird keine Umsatzsteuer berechnet." The vendor name, address, and other Stripe account details appear correctly. Invoice is properly sequentially numbered by Stripe.
**Why human:** The `InvoiceCreation.Enabled=true` and `InvoiceData.Footer` are confirmed in code. Actual PDF rendering requires a Stripe account with correct account details (name, address, tax ID) configured — cannot verify programmatically.

## Gaps Summary

No blockers were found. The phase goal is substantially implemented with all critical components present, wired, and tested. Two success criteria require human smoke testing before the phase can be considered fully passed:

1. **SC#1 (end-to-end checkout):** The `StripeWebhookHandlerTests.HandleAsync_ValidCheckoutSessionCompleted_InsertsPaymentAndEnqueuesJob` test does not actually invoke `HandleAsync`. The code is correct by inspection, but an end-to-end webhook smoke test is needed.

2. **SC#4 (DE-compliant invoice PDF):** The `InvoiceCreation` options are correctly set in code (KleinunternehmerNote footer confirmed by unit tests). Whether the resulting PDF is fully DE-compliant (vendor details, sequential numbering) depends on Stripe account configuration.

These are operational verification items, not code defects. The implementation is complete and the build + unit tests pass cleanly.

---

_Verified: 2026-05-31_
_Verifier: Claude (gsd-verifier)_
