# Phase 5: Commercial Surface (Payments) - Context

**Gathered:** 2026-05-25
**Status:** Ready for planning

<domain>
## Phase Boundary

Wire the existing stub `POST /tokens/purchase` to real Stripe money flow. Delivers: Stripe Checkout Session creation + hosted-checkout redirect, signature-verified webhook with idempotent token grant (`GrantTokensJob`), Widerrufsrecht + AGB gate in `TopUpDialog`, DE-compliant invoicing via Stripe Invoicing (Kleinunternehmer / §19 UStG), a dedicated `/billing` page (token balance, transaction history, invoice download, Stripe Customer Portal embed), refund/chargeback handling (`RevokeTokensJob`), and multi-environment safety (test/live key separation + startup guard).

In scope: PAY-01 through PAY-06 (see REQUIREMENTS.md).

Out of scope (later phases own these):
- Legal copy for Widerrufsbelehrung standalone page `/widerruf` — Phase 6 (LEG-04)
- TTDSG cookie banner — Phase 6 (LEG-05)
- AVV/DPA sign-off with Stripe — Phase 6 (LEG-06)
- `audit_log` table for payment grants — Phase 6 (LEG-08)
- Mollie / alternative DE payment methods — v2 (INT-V2-02)
- Subscription / recurring billing — not in this milestone

</domain>

<decisions>
## Implementation Decisions

### Token Pack Pricing (PAY-01)
- **D-01:** Three packages locked — 50 credits / 4,99 €, 200 credits / 14,99 €, 500 credits / 29,99 €. These match what `TopUpDialog` already displays; no UI changes to prices or volumes.
- **D-02:** Stripe Products + Prices are created **manually in Stripe Dashboard** once per environment. Their Stripe Price IDs are stored in config (`StripeOptions.PricePacks` — an array of `{ Credits, StripePriceId }` in `appsettings.json`, overridden via env vars). The backend maps a user's selected credit count to the matching Price ID before creating the Checkout Session. No code creates or manages Stripe Products.

### Checkout UX Flow (PAY-01, PAY-03)
- **D-03:** Checkout uses **Stripe hosted checkout** (redirect to stripe.com). Backend endpoint `POST /payments/checkout` receives the selected credit count, creates a `CheckoutSession` with `mode=payment`, `success_url`, `cancel_url`, and returns the session URL. Frontend redirects the browser. No embedded Payment Element.
- **D-04:** `success_url` is `/billing?payment=success`. On arrival, the billing page detects the query param and shows a success banner ("Credits wurden Ihrem Konto gutgeschrieben — sobald die Zahlung bestätigt ist"). The banner handles the async webhook delay gracefully: credits may arrive 1–30 s after the redirect. TanStack Query `refetchInterval` polls `/tokens/balance` every 3 s for 15 s, then stops.
- **D-05:** Widerrufsrecht + AGB gate lives **inside `TopUpDialog`**. After selecting a package, the dialog shows two required checkboxes before the "Kaufen" button is enabled:
  1. AGB checkbox: "Ich akzeptiere die [Allgemeinen Geschäftsbedingungen](link)."
  2. Widerrufsrecht checkbox: "Ich verlange ausdrücklich, dass mit der Ausführung des Vertrags sofort begonnen wird. Mir ist bekannt, dass ich mit Beginn der Ausführung mein Widerrufsrecht verliere." (statutory waiver text — §356 Abs. 4 BGB). Neither checkbox is pre-ticked. Clicking "Kaufen" without both checked is blocked client-side. Backend `POST /payments/checkout` also validates `waiverAccepted: true` in the request body (server-side guard).

### VAT / MwSt Strategy (PAY-02)
- **D-06:** Prices displayed as **Bruttopreise** (VAT included). `TopUpDialog` shows "4,99 €" as the total. No separate VAT line in the UI — that's on the invoice.
- **D-07:** TaxReader is a **Kleinunternehmer (§19 UStG)** at launch — no USt-IdNr., no VAT charged. Stripe Invoicing configured with `tax_behavior: exclusive` and no tax rate applied (or Stripe Tax disabled). Invoice/receipt from Stripe includes the line "Gemäß §19 UStG wird keine Umsatzsteuer berechnet." Add a `StripeOptions.BusinessAddress` and `StripeOptions.KleinunternehmerNote` config key for the invoice footer copy. If/when the Kleinunternehmer threshold is exceeded (~€25k revenue), the operator updates Stripe Tax config — no code change needed.

### Billing Page (PAY-04)
- **D-08:** **Separate `/billing` route** — not a settings tab. New page under `(authenticated)` route group. Own sidebar nav item.
- **D-09:** Sidebar nav item label: **"Credits & Abrechnung"**, positioned after "Einstellungen".
- **D-10:** Billing page content: (1) Token balance card with "Aufladen" button that opens `TopUpDialog`; (2) transaction history table (last 20, paginated); (3) invoice list from Stripe Invoicing with "Herunterladen" link per invoice (client retrieves Stripe-hosted PDF URL via `/payments/invoices`); (4) "Zahlungsmethode verwalten" button that opens a Stripe Customer Portal session. No full Customer Portal embed — button triggers `POST /payments/portal` which returns the portal session URL and frontend redirects.
- **D-11:** When `balance < 0` (after a refund via `RevokeTokensJob`): balance displays as negative (e.g. "-50 Credits") in the billing page and the balance indicator in the header. **New uploads are blocked** — `POST /receipt-files` returns `402 Payment Required` with German message "Ihr Guthaben ist erschöpft. Bitte laden Sie Credits auf." Existing receipts, reports, and exports remain accessible. Unblocked as soon as next successful purchase credits the account.

### Multi-Environment Safety (PAY-06)
- **D-12:** Config structure: `StripeOptions` with `SecretKey` (string), `PublishableKey` (string), `WebhookSecret` (string), `PricePacks` (array), `DemoMode` (bool, default false), `BusinessAddress` (string), `KleinunternehmerNote` (string). Separate values per environment via `Stripe__SecretKey` env var (`__` nesting). No "SecretKey_Test / SecretKey_Live" split — one `SecretKey` per deployment, correct value set per env.
- **D-13:** **Startup guard** via `IValidateOptions<StripeOptions>` (registered with `ValidateOnStart()`): if `ASPNETCORE_ENVIRONMENT == "Production"` and `SecretKey.StartsWith("sk_test_")` → throw `InvalidOperationException("Stripe SecretKey ist ein Testschlüssel in einer Production-Umgebung.")`. If environment is Development/Staging and key starts with `sk_live_` → `logger.LogWarning(...)` (not throw — devs occasionally test against live Stripe, but they should know). Logged via Sentry as a critical alert in production.
- **D-14:** `Stripe__DemoMode=true` toggle: when enabled, `POST /payments/checkout` skips Stripe entirely and directly credits the selected pack's credit count via `ITokenService.AddTokensAsync`, returning a synthetic success response. Frontend behaves identically (redirects to `/billing?payment=success`). Billing page shows a persistent "Demo-Modus — keine echten Zahlungen" banner. Useful for demos and screenshots. **Not exposed via UI config** — only via server-side env var.

### Webhook + Idempotency (PAY-01)
- **D-15:** Webhook endpoint `POST /webhooks/stripe` is anonymous (`.AllowAnonymous()`), not under the `/api/v1` auth group. Stripe signature verified with `StripeClient.ConstructEvent(rawBody, stripeSignatureHeader, webhookSecret)` — raw request body must be read before any middleware touches it. On `checkout.session.completed`: extract `metadata.userId` and `metadata.credits`, insert into `payments` table with `(stripe_event_id UNIQUE)` guard (idempotent), enqueue `GrantTokensJob`. Duplicate event = `409 Conflict` swallowed silently (return 200 to Stripe so it doesn't retry).
- **D-16:** `payments` table (new): `id` (UUID PK), `user_id` (FK users), `stripe_event_id` (string, UNIQUE), `stripe_session_id` (string), `credits_granted` (int), `amount_cents` (int), `currency` (string), `status` (enum: Pending/Granted/Revoked), `created_at` (timestamptz), `revoked_at` (timestamptz?). `GrantTokensJob` sets status to Granted; `RevokeTokensJob` sets status to Revoked and fires `ITokenService.DeductTokensAsync(credits)`.

### Claude's Discretion (within CLAUDE.md conventions)
- Exact Stripe.net NuGet version (use latest stable `Stripe.net` 47.x or current — check NuGet at planning time)
- Whether `GrantTokensJob` is a Hangfire `IBackgroundJobClient.Enqueue` call (fire-and-forget) or a recurring check job
- Exact shadcn components for the billing page (Card, Table, Badge — follow existing patterns)
- Stripe Customer Portal redirect vs embed (redirect chosen by D-10; embed is out of scope)
- Exact Stripe metadata keys (`userId`, `credits`) on the Checkout Session
- `/billing?payment=success` polling implementation detail (refetchInterval duration)

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Project context
- `.planning/REQUIREMENTS.md` — PAY-01 through PAY-06 full text with acceptance criteria
- `.planning/ROADMAP.md` — Phase 5 entry with 7 success criteria and 4 plan stubs (05-01 through 05-04)
- `.planning/PROJECT.md` — "Stripe selected as payment provider" key decision; token economy overview; scale target (100–500 users, not thousands)

### Prior-phase patterns (must carry forward)
- `.planning/phases/03-background-pipeline-tesseract-pool/03-CONTEXT.md` — Hangfire job pattern; `IBackgroundJobClient.Enqueue` fire-and-forget; token pre-charge + refund pattern in `ClassifyBatchJob`
- `.planning/phases/04-classification-trustworthiness/04-CONTEXT.md` — `Result<T>` handler pattern; `ICurrentUser` for per-user scoping; German error strings in `Result<T>.Failure`

### Codebase intel
- `.planning/codebase/ARCHITECTURE.md` — Layer rules; anonymous endpoint pattern (`.AllowAnonymous()`); `IOptions<T>` + `IValidateOptions<T>` config pattern
- `.planning/codebase/CONVENTIONS.md` — Primary-constructor DI; `Async` suffix; structured logging; German `Sie`-form

### Files this phase will touch (read before editing)

#### Backend — New
- `Backend/src/TaxReader.Domain/Entities/Payment.cs` — NEW entity (D-16)
- `Backend/src/TaxReader.Domain/Enums/PaymentStatus.cs` — NEW enum (Pending/Granted/Revoked)
- `Backend/src/TaxReader.Infrastructure/Configuration/StripeOptions.cs` — NEW options class (D-12)
- `Backend/src/TaxReader.Infrastructure/Services/StripePaymentProvider.cs` — NEW: checkout session creation, portal session, invoice list
- `Backend/src/TaxReader.Application/Jobs/GrantTokensJob.cs` — NEW Hangfire job
- `Backend/src/TaxReader.Application/Jobs/RevokeTokensJob.cs` — NEW Hangfire job
- `Backend/src/TaxReader.Api/Endpoints/PaymentEndpoints.cs` — NEW: /payments/checkout, /payments/portal, /payments/invoices + anonymous /webhooks/stripe
- `Backend/src/TaxReader.Infrastructure/Migrations/` — NEW migration: AddPaymentsTable

#### Backend — Modified
- `Backend/src/TaxReader.Api/Endpoints/ReceiptFileEndpoints.cs` — Add balance check (402 guard) when `POST /receipt-files` (D-11)
- `Backend/src/TaxReader.Api/Program.cs` — Register StripeOptions + ValidateOnStart; register PaymentEndpoints; register GrantTokensJob + RevokeTokensJob
- `Backend/src/TaxReader.Infrastructure/DependencyInjection.cs` — Register StripePaymentProvider; add Stripe.net HttpClient
- `Backend/Directory.Packages.props` — Add `Stripe.net` package reference

#### Frontend — New
- `Frontend/src/app/(authenticated)/billing/page.tsx` — NEW /billing page (D-08 through D-11)
- `Frontend/src/lib/api-client.ts` — New: `createCheckoutSession`, `createPortalSession`, `getInvoices`
- `Frontend/src/hooks/use-billing.ts` — NEW: `useCreateCheckoutSession`, `useInvoices`, `useCreatePortalSession`

#### Frontend — Modified
- `Frontend/src/components/tokens/top-up-dialog.tsx` — Add AGB + Widerrufsrecht checkboxes (D-05); wire to real `createCheckoutSession` instead of `usePurchaseTokens` stub; add DemoMode banner when applicable
- `Frontend/src/components/layout/sidebar.tsx` (or nav component) — Add "Credits & Abrechnung" nav item (D-09)
- `Frontend/src/app/(authenticated)/billing/` — Add `/billing?payment=success` detection + polling (D-04)

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- **`TopUpDialog` (`Frontend/src/components/tokens/top-up-dialog.tsx`)** — Already has 3 package definitions (credits/price/popular), package selection UI, loading state. Replace `usePurchaseTokens` call with `useCreateCheckoutSession`; add legal checkboxes before submit; add DemoMode banner.
- **`POST /tokens/purchase` stub** — Remove or repurpose as the DemoMode path. The Kleinunternehmer `AddTokensAsync` call it makes becomes what `GrantTokensJob` does after webhook validation.
- **`ITokenService.AddTokensAsync`** — The existing token crediting mechanism. `GrantTokensJob` calls this after inserting the `payments` row. `RevokeTokensJob` calls a new `DeductTokensAsync` (or negative `AddTokensAsync`).
- **`TokenTransactionType` enum** — Extend with `Refund` or `Revoke` if not already present.
- **Hangfire `IBackgroundJobClient`** — Already registered from Phase 3. `GrantTokensJob` and `RevokeTokensJob` follow the same fire-and-forget pattern as `ProcessReceiptFileJob`.
- **`IValidateOptions<T>` pattern** — Not yet used in this project; `StripeOptionsValidator` is the first. Follow the `IOptions<T>` binding pattern from `JwtOptions`/`AnthropicOptions`.
- **`Result<T>` pattern** — New handlers follow `Result<TResponse>.Success` / `Result<TResponse>.Failure` exactly.

### Established Patterns
- **Anonymous endpoint registration**: `.AllowAnonymous()` on the webhook endpoint, same as the `/auth/*` endpoints that opt out of the global `RequireAuthorization()`.
- **Raw request body for webhook**: Must register raw body reading before `app.UseRouting()` — see `StripeRequestBodyMiddleware` or configure via `app.Use(async (ctx, next) => { ctx.Request.EnableBuffering(); ... })`. Required for Stripe signature validation.
- **German error strings in `Result<T>.Failure`**: "Ihr Guthaben ist erschöpft. Bitte laden Sie Credits auf." follows the existing pattern.
- **`ICurrentUser` via claim extraction**: Used in `PaymentEndpoints` to get `userId` for checkout session metadata.

### Integration Points
- **`POST /receipt-files` (balance guard)**: After D-11, this endpoint calls `ITokenService.GetOrCreateBalanceAsync` before processing. If `balance.Balance < 0` → `Results.StatusCode(402)`.
- **Stripe webhook → `GrantTokensJob`**: The webhook endpoint extracts `userId` + `credits` from `checkout.session.completed` metadata → inserts `Payment` row → enqueues `GrantTokensJob`. The job calls `ITokenService.AddTokensAsync`.
- **`/billing?payment=success` polling**: The frontend billing page uses `useEffect` to detect the query param and sets `refetchInterval: 3000` on the balance query for 15 s.

</code_context>

<specifics>
## Specific Ideas

- **Widerrufsrecht checkbox exact text (§356 Abs. 4 BGB):** "Ich verlange ausdrücklich, dass mit der Ausführung des Vertrags sofort begonnen wird. Mir ist bekannt, dass ich hierdurch mein Widerrufsrecht verliere." This is the statutory waiver text — do not paraphrase. Link to `/widerruf` page (which Phase 6 will create; in Phase 5, the link can 404 or be a placeholder).
- **AGB checkbox text:** "Ich akzeptiere die [Allgemeinen Geschäftsbedingungen](/agb)." (Link to `/agb` — also a Phase 6 placeholder in Phase 5.)
- **Kleinunternehmer invoice note:** "Gemäß §19 UStG wird keine Umsatzsteuer berechnet." This appears in the Stripe Invoice footer via `StripeOptions.KleinunternehmerNote`.
- **DemoMode banner wording:** "Demo-Modus — keine echten Zahlungen. Credits werden direkt gutgeschrieben." Shown at the top of TopUpDialog and as a persistent chip on the billing page.
- **Balance header indicator:** The existing token balance shown in the app header (if any) or on the billing page must show negative values clearly (e.g. "-50 Credits" in a red/warning style).
- **Stripe CLI local webhook forwarding command:** `stripe listen --forward-to http://localhost:5190/webhooks/stripe` — add this to the top-level README's development setup section.

</specifics>

<deferred>
## Deferred Ideas

- **Subscription / recurring billing** — token packs are one-time purchases. Recurring credit subscriptions deferred to v2.
- **Mollie / SEPA / giropay** — alternative DE payment methods for Stripe-averse users (INT-V2-02). Phase 5 is Stripe-only.
- **Volume discounts or enterprise packs** — not in scope; 3 fixed packs are sufficient for launch.
- **Self-hosted Stripe-equivalent (Paddle, LemonSqueezy)** — the Stripe decision is locked; no re-evaluation in this phase.
- **Stripe Tax automatic calculation** — not needed while Kleinunternehmer applies. When revenue exceeds threshold, operator enables Stripe Tax in Dashboard + sets `tax_behavior: exclusive` + DE 19% tax rate — no code change needed.
- **Audit log for payment grants** — Phase 6 (LEG-08). Phase 5 only writes to the `payments` table.
- **`/widerruf` and `/agb` legal pages** — Phase 6 (LEG-04, LEG-03). Phase 5 links to them as placeholders.
- **AVV with Stripe (DPA)** — Phase 6 (LEG-06). Phase 5 assumes the Stripe Data Processing Agreement covers the baseline.

</deferred>

---

*Phase: 05-commercial-surface-payments*
*Context gathered: 2026-05-25*
