---
phase: 5
slug: commercial-surface-payments
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-05-28
---

# Phase 5 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit 2.9.2 + FluentAssertions 7.0.0 + Moq 4.20.72 |
| **Config file** | `Backend/tests/TaxReader.UnitTests/TaxReader.UnitTests.csproj` |
| **Quick run command** | `dotnet test Backend/tests/TaxReader.UnitTests --filter "Payment OR Stripe OR GrantTokens OR RevokeTokens"` |
| **Full suite command** | `dotnet test Backend` |
| **Estimated runtime** | ~30 seconds |

---

## Sampling Rate

- **After every task commit:** Run `dotnet test Backend/tests/TaxReader.UnitTests --filter "Payment OR Stripe OR GrantTokens OR RevokeTokens"`
- **After every plan wave:** Run `dotnet test Backend`
- **Before `/gsd-verify-work`:** Full suite must be green
- **Max feedback latency:** 30 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| 05-01-T1 | 01 | 1 | PAY-01 | T-01 (Forged webhook) | `EventUtility.ConstructEvent` validates HMAC-SHA256; invalid sig → 400 | unit | `dotnet test Backend --filter "StripeWebhook"` | ❌ W0 | ⬜ pending |
| 05-01-T2 | 01 | 1 | PAY-01 | T-02 (Duplicate event) | UNIQUE on `stripe_event_id`; second delivery → 200, no second grant | unit | `dotnet test Backend --filter "GrantTokensJob"` | ❌ W0 | ⬜ pending |
| 05-01-T3 | 01 | 1 | PAY-01 | — | `CheckoutSession` created with correct metadata (`userId`, `credits`) | unit | `dotnet test Backend --filter "CheckoutSession"` | ❌ W0 | ⬜ pending |
| 05-01-T4 | 01 | 1 | PAY-01 | — | `GrantTokensJob` credits correct token count to correct user | unit | `dotnet test Backend --filter "GrantTokensJob"` | ❌ W0 | ⬜ pending |
| 05-04-T1 | 04 | 4 | PAY-05 | — | `RevokeTokensJob` debits tokens; balance can go negative | unit | `dotnet test Backend --filter "RevokeTokensJob"` | ❌ W0 | ⬜ pending |
| 05-04-T2 | 04 | 4 | PAY-06 | T-03 (Production test key) | `StripeOptionsValidator`: Production + `sk_test_*` → `InvalidOperationException` | unit | `dotnet test Backend --filter "StripeOptionsValidator"` | ❌ W0 | ⬜ pending |
| 05-04-T3 | 04 | 4 | PAY-06 | — | `StripeOptionsValidator`: missing `SecretKey` → `ValidateOptionsResult.Fail` | unit | `dotnet test Backend --filter "StripeOptionsValidator"` | ❌ W0 | ⬜ pending |
| 05-03-T1 | 03 | 3 | PAY-04 | T-04 (IDOR invoices) | `GET /payments/invoices` scoped to `ICurrentUser.UserId`; cannot access other user's invoices | unit | `dotnet test Backend --filter "PaymentInvoices"` | ❌ W0 | ⬜ pending |
| 05-02-T1 | 02 | 2 | PAY-03 | — | Widerrufsrecht waiver text matches §356 Abs. 4 BGB statutory text exactly | manual | — | N/A | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] `Backend/tests/TaxReader.UnitTests/Jobs/GrantTokensJobTests.cs` — stubs for PAY-01 (token crediting, idempotency)
- [ ] `Backend/tests/TaxReader.UnitTests/Jobs/RevokeTokensJobTests.cs` — stubs for PAY-05 (token revocation, negative balance)
- [ ] `Backend/tests/TaxReader.UnitTests/Configuration/StripeOptionsValidatorTests.cs` — stubs for PAY-06
- [ ] `Backend/tests/TaxReader.UnitTests/Webhooks/StripeWebhookHandlerTests.cs` — stubs for PAY-01 (signature, duplicate event, routing)

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Widerrufsrecht checkbox text matches §356 Abs. 4 BGB statutory text exactly | PAY-03 | Statutory copy correctness cannot be verified by grep — requires human legal review | Read `TopUpDialog` checkbox label; compare to: "Ich verlange ausdrücklich, dass mit der Ausführung des Vertrags sofort begonnen wird. Mir ist bekannt, dass ich hierdurch mein Widerrufsrecht verliere." |
| Stripe Customer Portal opens successfully from billing page | PAY-04 | Requires Stripe Dashboard configuration (one-time operator step) + live Stripe account | Click "Zahlungsmethode verwalten" on `/billing`; verify redirect to Stripe Customer Portal URL |
| DE-compliant Rechnung PDF contains Kleinunternehmer note | PAY-02 | Requires live Stripe Invoicing configuration | Trigger test purchase → download invoice → verify "Gemäß §19 UStG wird keine Umsatzsteuer berechnet." in footer |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 30s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
