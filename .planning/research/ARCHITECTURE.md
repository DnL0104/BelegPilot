# Architecture Research

**Domain:** DE B2C tax-receipt SaaS — architecture additions for hardening milestone
**Researched:** 2026-05-03
**Confidence:** HIGH for additive patterns (well-established); MEDIUM for build-order opinions (project-specific)

> **Scope note:** Existing Clean Architecture (.NET 10 layered + Next.js 16 frontend) is documented in `.planning/codebase/ARCHITECTURE.md` and is NOT being redesigned. This document specifies how the new pieces (background jobs, payment integration, rule+AI hybrid, refresh-token table) slot into the existing layout.

---

## High-Level Additions

```
┌─────────────────────────────────────────────────────────────────┐
│  Frontend (Next.js)                                              │
│  Caddy → web:3000 → /api/v1/* → api:8080                         │
│  + new pages: /settings/billing, /settings/data-export           │
│  + new flows: poll-job-status, Stripe-Checkout-redirect          │
└─────────────────────────────────────────────────────────────────┘
                          │
                          ▼
┌─────────────────────────────────────────────────────────────────┐
│  TaxReader.Api  (Minimal API endpoints, DI wiring)               │
│  + RateLimiter middleware                                        │
│  + Hangfire dashboard (auth-gated /hangfire)                     │
│  + Stripe webhook endpoint (signature-verified, anonymous)       │
│  + Job status endpoint (GET /receipt-files/{id}/status)          │
└─────────────────────────────────────────────────────────────────┘
              │
┌─────────────────────────────────────────────────────────────────┐
│  TaxReader.Application                                           │
│  + IJobScheduler interface                                       │
│  + IPaymentProvider interface                                    │
│  + IRuleClassifier interface (existing IClassificationService    │
│    becomes a composer of rules-then-AI)                          │
│  + UploadReceiptFilesCommand returns JobId, no longer awaits     │
│    end-to-end pipeline                                           │
│  + ProcessReceiptFileJob (Application-layer job orchestrator)    │
└─────────────────────────────────────────────────────────────────┘
              │                                        ▲
┌─────────────────────────────────────────────────────────────────┐
│  TaxReader.Domain                                                │
│  + RefreshToken entity + RefreshTokenStatus enum                 │
│  + Payment entity + PaymentStatus enum                           │
│  + ClassificationRule already exists — gets wired up             │
└─────────────────────────────────────────────────────────────────┘
              ▲ implemented by
┌─────────────────────────────────────────────────────────────────┐
│  TaxReader.Infrastructure                                        │
│  + HangfireJobScheduler : IJobScheduler                          │
│  + StripePaymentProvider : IPaymentProvider                      │
│  + RuleBasedClassifier : IRuleClassifier                         │
│  + HybridClassificationService : IClassificationService          │
│    (composes RuleBased + Anthropic)                              │
│  + TesseractEnginePool                                           │
│  + RefreshTokenService (replaces single-column logic in Auth)    │
└─────────────────────────────────────────────────────────────────┘
```

### Component Responsibilities

| Component | Responsibility | Implementation |
|-----------|----------------|----------------|
| `IJobScheduler` (App) | Enqueue background jobs from handlers | `HangfireJobScheduler` (Infra) wraps `BackgroundJob.Enqueue` |
| `ProcessReceiptFileJob` (App) | Orchestrate extract → parse → classify outside HTTP context | Application-layer job, resolved via DI; same handler logic, just async |
| `IPaymentProvider` (App) | Create checkout sessions, verify webhook signatures, fetch invoice | `StripePaymentProvider` (Infra) — wraps Stripe.net |
| `IRuleClassifier` (App) | Match items against persisted rules; return matches with confidence | `RuleBasedClassifier` (Infra) — DB-backed |
| `HybridClassificationService` (Infra) | Compose RuleBased + Anthropic; hand off unknowns | Implements existing `IClassificationService`; replaces `AiOnlyClassificationService` |
| `RefreshTokenService` | Issue / rotate / revoke refresh tokens; per-device tracking | New service in Infra; replaces `user.RefreshToken` column logic |
| `TesseractEnginePool` | Pool of `TesseractEngine` instances | Singleton, holds `Channel<TesseractEngine>` |

---

## Pattern 1 — Background-Job Upload Pipeline

**What:** The HTTP request lifecycle returns immediately with a `JobId`. The actual extract → parse → classify pipeline runs in Hangfire workers. The frontend polls a status endpoint or uses Server-Sent Events.

**When to use:** Any synchronous handler step that takes > 2 seconds and is user-initiated (uploads, exports, expensive reports).

**Trade-offs:**
- ✓ HTTP requests don't time out under multi-receipt uploads
- ✓ Process restarts don't lose user-paid work (Hangfire persists in Postgres)
- ✓ Concurrent uploads from same user can interleave
- ✗ Adds a layer to debug (now have to look at Hangfire dashboard)
- ✗ Token-charging must be careful: charge on enqueue or charge on success?

**Sequence:**

```
Frontend                    API                       Hangfire             Anthropic
  │                          │                            │                    │
  │ POST /receipt-files      │                            │                    │
  │ (multipart, N files)     │                            │                    │
  ├─────────────────────────>│                            │                    │
  │                          │ Validate auth + tokens     │                    │
  │                          │ Insert ReceiptFile rows    │                    │
  │                          │   (Status=Queued)          │                    │
  │                          │ Enqueue                    │                    │
  │                          │   ProcessReceiptFileJob   ─►                   │
  │                          │   (one job per file or     │                    │
  │                          │    one job for batch)      │                    │
  │ 202 Accepted             │                            │                    │
  │ { jobIds: [...] }        │                            │                    │
  │<─────────────────────────│                            │                    │
  │                          │                            │ Extract (PdfPig)   │
  │                          │                            │ Parse (Amazon/...)│
  │                          │                            │ Classify (rules)   │
  │                          │                            │   then unknowns ──►│
  │                          │                            │                    │
  │                          │                            │<───────────────────│
  │                          │                            │ Update Status,     │
  │                          │                            │ debit token, save  │
  │ GET /receipt-files/{id}  │                            │                    │
  ├─────────────────────────>│ Read ReceiptFile.Status    │                    │
  │ { status: "Processing" } │                            │                    │
  │<─────────────────────────│                            │                    │
  │ ... poll ...             │                            │                    │
  │ { status: "Completed" }  │                            │                    │
  │<─────────────────────────│                            │                    │
```

**Token-charging policy (key choice):**
- **Pre-charge** at enqueue time (with refund on parser-error / unknown classification per existing `AiOnlyClassificationService` behavior). Best for billing simplicity.
- Alternative: charge on success only. Bad if the job runs and the user has gone offline — they'd see no debit but also no result if the job fails.
- **Recommendation: keep current pre-charge with per-item refund pattern**; it's already implemented and tested.

**User identity in jobs:**
- Hangfire jobs are POCO method calls; `userId` is passed as an argument (Guid).
- Inside the job, resolve `IServiceScopeFactory` → create scope → get `IAppDbContext` → all reads/writes are inside the scope, scoped by the explicit `userId`.
- `ICurrentUser` is HTTP-context-bound; jobs do NOT use it. New job-side abstraction `IJobContext` (or just pass `userId` through arguments).

**Idempotency:**
- Jobs receive a `(receiptFileId, userId)` argument; the receipt file row already has a `Status` column.
- Job's first action: `if (file.Status == Completed) return;`
- Hangfire automatic retry on transient failure is fine; the status check makes it idempotent.

**Process crash mid-job:**
- Hangfire detects abandoned jobs (server hasn't checked in for N minutes) and re-enqueues them.
- A job that was halfway through `Classifying` will restart from `Extracting` — wasteful but safe.
- Optimization later: add intra-job checkpoints if Anthropic costs become a concern.

---

## Pattern 2 — Rule + AI Hybrid Classification

**What:** A `HybridClassificationService` composes a `RuleBasedClassifier` (DB-backed deterministic match against `ClassificationRule` entities) and the existing `ClaudeAiClassifier`. Rules run first; only items unmatched by rules go to AI.

**When to use:** Always for this product (replaces `AiOnlyClassificationService`).

**Trade-offs:**
- ✓ Determinism for known patterns ("anything from Eduki = SpecialistLiterature") — saves tokens, increases consistency
- ✓ User corrections become reusable rules — closes the audit/learning loop
- ✓ AI focuses on novel / ambiguous items
- ✗ Two systems to keep in sync
- ✗ A bad rule can systematically mis-classify many items

**Composition pattern (rules-first):**

```
HybridClassificationService.ClassifyItemsAsync(items, ct):
  1. ruleMatches = await ruleClassifier.MatchAsync(items, userId, ct)
  2. unmatchedItems = items.Where(i => !ruleMatches.ContainsKey(i.Id))
  3. aiResults = await aiClassifier.ClassifyBatchAsync(unmatchedItems, ct)
  4. results = ruleMatches ∪ aiResults
  5. for each result with confidence ≥ user.AutoConfirmThreshold:
       mark as Confirmed
  6. for each rule-matched result:
       record method = ClassificationMethod.Rule
       (existing AI-matched items continue to be recorded as Method.AI)
```

**Rule semantics (concrete):**
- `ClassificationRule` matches on: vendor name (substring, case-insensitive), source-file regex, item description regex
- Match wins → `Category` set, `Status = Suggested`, `Method = Rule`, `Reason = "Rule: {rule.Name}"`
- User confirmation works the same as for AI matches

**Rule learning loop:**
- "Add rule from this correction" UX — explicit user action, NOT auto-promotion
  - User overrides a classification → button "Diese Regel speichern: alle Belege von [Vendor X] → Kategorie [Y]?"
  - User confirms → new `ClassificationRule` with `UserId` set (per-user rules, not global)
- Auto-promotion (after N corrections of the same vendor → same category) is deferred to v1.x

**"Trustworthy classification" wins:**
- Rules give reproducibility users can explain
- AI gives coverage rules can't anticipate
- Hybrid threading both is the only way to deliver Core Value at scale

---

## Pattern 3 — Payment Integration

**What:** `IPaymentProvider` abstracts Stripe; webhook endpoint is anonymous (signature-verified) and grants tokens via the existing `TokenService`.

**Sequence:**

```
Frontend                    API                       Stripe              Hangfire
  │                          │                          │                     │
  │ POST /tokens/checkout    │                          │                     │
  │ { packId: "pack-100" }   │                          │                     │
  ├─────────────────────────>│                          │                     │
  │                          │ stripe.CreateCheckoutSession                   │
  │                          ├─────────────────────────>│                     │
  │                          │ session URL              │                     │
  │                          │<─────────────────────────│                     │
  │ { url, sessionId }       │                          │                     │
  │<─────────────────────────│                          │                     │
  │ window.location → url    │                          │                     │
  │ ─────────────────────────────────────────────────-->│                     │
  │ User pays in Stripe                                 │                     │
  │ Redirect back with sessionId                        │                     │
  │<─────────────────────────────────────────────────---│                     │
  │                          │                          │                     │
  │                          │   POST /webhooks/stripe  │                     │
  │                          │     payment_intent.      │                     │
  │                          │     succeeded            │                     │
  │                          │<─────────────────────────│                     │
  │                          │ Verify signature         │                     │
  │                          │ Lookup PaymentIntent.id  │                     │
  │                          │   in payments table      │                     │
  │                          │ if already_processed     │                     │
  │                          │   → 200 (idempotent)     │                     │
  │                          │ else                     │                     │
  │                          │   Insert payments row    │                     │
  │                          │   Enqueue                │                     │
  │                          │     GrantTokensJob ─────────────────────────►  │
  │                          │ 200 OK                   │                     │
  │                          │─────────────────────────>│                     │
  │                          │                          │  TokenService       │
  │                          │                          │  .CreditAsync       │
  │                          │                          │  + transaction      │
  │                          │                          │    record           │
  │ Polls /tokens/balance    │                          │                     │
  ├─────────────────────────>│                          │                     │
  │ { balance: 110 }         │                          │                     │
  │<─────────────────────────│                          │                     │
```

**Idempotency:**
- Stripe webhook events have unique IDs (`evt_xxx`).
- The `payments` table has `(stripe_event_id UNIQUE)` constraint.
- Insert-OR-noop pattern: try INSERT, on `unique_violation` → return 200 immediately.
- This makes Stripe's automatic retry-on-non-200 safe.

**Webhook signature verification:**
- Use `Stripe.EventUtility.ConstructEvent(body, sigHeader, webhookSecret)`.
- Webhook endpoint MUST receive raw request body (not parsed JSON) — configure via `EnableBuffering()` middleware.
- `webhookSecret` in env vars; rotate via Stripe dashboard.

**Refund / chargeback flow:**
- Stripe webhook `charge.refunded` → enqueue `RevokeTokensJob`
- `TokenService.DebitAsync(userId, amount, reason: "Refund")` — creates negative `TokenTransaction`
- If user has already spent the refunded tokens, balance can go negative — block new uploads until balance ≥ 0; surface as a "balance dispute" notification

**Multi-environment safety:**
- Stripe API keys: `Stripe__SecretKey_Test` vs `Stripe__SecretKey_Live` — separate env-var keys
- Webhook endpoints: separate webhook signing secrets per environment
- Dev / stage runs against Stripe test mode (`sk_test_...`) — production runs against live (`sk_live_...`)
- Hard guard at startup: throw if `ASPNETCORE_ENVIRONMENT == Production` and `Stripe__SecretKey` starts with `sk_test_`

---

## Pattern 4 — Refresh-Token Table

**What:** Replace `user.RefreshToken` single column with a `refresh_tokens` table supporting multi-device, rotation, replay detection.

**Schema:**

```sql
CREATE TABLE refresh_tokens (
  id              UUID PRIMARY KEY,
  user_id         UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
  token_hash      VARCHAR(128) NOT NULL,
  expires_at      TIMESTAMPTZ NOT NULL,
  created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  last_used_at    TIMESTAMPTZ,
  revoked_at      TIMESTAMPTZ,
  user_agent      TEXT,
  ip_address      INET,
  replaced_by_id  UUID REFERENCES refresh_tokens(id)
);
CREATE INDEX ix_refresh_tokens_user_id ON refresh_tokens(user_id) WHERE revoked_at IS NULL;
CREATE INDEX ix_refresh_tokens_expires_at ON refresh_tokens(expires_at);
```

**Rotation pattern:**

```
On /auth/refresh with token T:
  1. Find row by hash(T)
  2. If not found OR expired OR revoked OR replaced_by_id IS NOT NULL:
       Reject. If replaced_by_id IS NOT NULL → REPLAY DETECTED → revoke all user tokens
  3. Issue new access + refresh token T'
  4. Insert new row for T'
  5. Set old row.revoked_at = NOW(), replaced_by_id = T'.id
  6. Return access + T' to client
```

**"Log out all devices":**
```sql
UPDATE refresh_tokens SET revoked_at = NOW() WHERE user_id = $1 AND revoked_at IS NULL;
```

**Storage:** Only `token_hash` stored (SHA-256). Plaintext token returned once at issue, never persisted.

**Cleanup:** Hangfire recurring job — daily — deletes rows where `expires_at < NOW() - 90 days`.

**Migration from single column:** New schema deployed; old `user.RefreshToken` column kept for 1 release for rollback safety; users re-issued on next refresh; old column dropped in next release.

---

## Pattern 5 — Async Cancellation + Job Lifecycle

**What:** A user closing the browser mid-upload should NOT cancel the in-flight job (they paid tokens, they want results). A user explicitly clicking "Cancel" SHOULD propagate.

**Mechanism:**
- Hangfire jobs receive a `CancellationToken` that fires only on Hangfire shutdown — not on client disconnect.
- For explicit user cancellation: a `cancelled_at` column on `receipt_files`; the job polls this between major steps (after extract, after parse, before AI call).
- `cancellation_at` is set by `POST /receipt-files/{id}/cancel`.
- Tokens already debited at enqueue are refunded if cancellation happens before AI call.

---

## Pattern 6 — Multi-Environment Safety

**Problem:** The same Anthropic API key may be used in dev + production. The same Stripe live key cannot be exposed in dev.

**Recommendation:**
- Anthropic: separate API keys per environment, even if it costs more — name them `ANTHROPIC_API_KEY_DEV` etc., and select via `ASPNETCORE_ENVIRONMENT`.
- Stripe: never use live key in dev. Test mode keys are free; use them.
- Hard checks at startup:
  - If `Production` and any key matches a `*_TEST_*` pattern → throw.
  - If `Development` and any key matches a `*_LIVE_*` pattern → log warning loudly.
- Banner in Hangfire dashboard: "STAGING" or "PRODUCTION" badge so the solo dev can't get confused.

---

## Pattern 7 — Audit Logging

**What:** A separate `audit_log` table records sensitive operations: account deletion, classification override patterns, payment grants, refresh token revocations.

**Schema:**

```sql
CREATE TABLE audit_log (
  id          UUID PRIMARY KEY,
  user_id     UUID,
  event_type  TEXT NOT NULL,
  metadata    JSONB,
  ip_address  INET,
  occurred_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
```

**Use cases:**
- Customer support ("when did I delete my account?" / "what was charged?")
- DSGVO Art. 15 right of access — user can see their own audit log
- Forensics if a payment is disputed

---

## Project Structure Additions

```
Backend/
├── src/
│   ├── TaxReader.Api/
│   │   ├── Endpoints/
│   │   │   ├── PaymentEndpoints.cs       (NEW — checkout, customer portal)
│   │   │   ├── WebhookEndpoints.cs       (NEW — /webhooks/stripe)
│   │   │   ├── JobStatusEndpoints.cs     (NEW — GET /receipt-files/{id}/status)
│   │   │   └── DataExportEndpoints.cs    (NEW — DSGVO Art. 20)
│   │   └── Middleware/
│   │       └── (RateLimiter wired in Program.cs, no new file)
│   ├── TaxReader.Application/
│   │   ├── Commands/
│   │   │   ├── UploadReceiptFilesHandler.cs   (modified — enqueues, returns JobId)
│   │   │   ├── CreateCheckoutSessionHandler.cs (NEW)
│   │   │   ├── ProcessStripeWebhookHandler.cs  (NEW)
│   │   │   ├── ExportUserDataHandler.cs        (NEW)
│   │   │   └── RevokeAllTokensHandler.cs       (NEW)
│   │   ├── Jobs/
│   │   │   ├── ProcessReceiptFileJob.cs   (NEW — background pipeline)
│   │   │   ├── GrantTokensJob.cs          (NEW — webhook-driven)
│   │   │   ├── ExportUserDataJob.cs       (NEW)
│   │   │   └── CleanupExpiredRefreshTokensJob.cs (NEW)
│   │   ├── Interfaces/
│   │   │   ├── IJobScheduler.cs           (NEW)
│   │   │   ├── IPaymentProvider.cs        (NEW)
│   │   │   └── IRuleClassifier.cs         (NEW)
│   ├── TaxReader.Domain/
│   │   ├── Entities/
│   │   │   ├── RefreshToken.cs            (NEW)
│   │   │   ├── Payment.cs                 (NEW)
│   │   │   └── AuditLogEntry.cs           (NEW)
│   ├── TaxReader.Infrastructure/
│   │   ├── Services/
│   │   │   ├── HangfireJobScheduler.cs    (NEW)
│   │   │   ├── StripePaymentProvider.cs   (NEW)
│   │   │   ├── RuleBasedClassifier.cs     (NEW)
│   │   │   ├── HybridClassificationService.cs (NEW — replaces AiOnlyClassificationService)
│   │   │   ├── TesseractEnginePool.cs     (NEW — replaces TesseractImageTextExtractor's Singleton lock)
│   │   │   ├── RefreshTokenService.cs     (NEW)
│   │   │   └── AuditLogger.cs             (NEW)
│   │   └── Migrations/
│   │       └── (new migrations for refresh_tokens, payments, audit_log, removing user.RefreshToken)
└── tests/
    └── TaxReader.IntegrationTests/        (NEW project — Testcontainers)

Frontend/
└── src/
    ├── app/
    │   ├── (authenticated)/
    │   │   ├── settings/
    │   │   │   ├── billing/page.tsx        (NEW)
    │   │   │   └── data-export/page.tsx    (NEW)
    │   │   └── upload/
    │   │       └── page.tsx                (modified — poll job status)
    │   ├── (legal)/
    │   │   ├── impressum/page.tsx          (NEW)
    │   │   ├── datenschutz/page.tsx        (NEW)
    │   │   ├── agb/page.tsx                (NEW)
    │   │   └── widerruf/page.tsx           (NEW)
    │   └── checkout/
    │       └── return/page.tsx             (NEW — Stripe redirect handler)
    ├── components/
    │   └── consent-banner.tsx              (NEW — TTDSG)
    └── lib/
        ├── stripe.ts                       (NEW — Stripe.js loader)
        └── api-client.ts                   (modified — purchaseTokens stub becomes real)
```

---

## Build Order — Phase Dependencies

A solo dev with 3 months cannot work everything in parallel. Order matters.

**Pre-flight (must come first or other work is wasted):**
1. **Hygiene**: Remove `storage/`, `build-diag.txt`, `.gitignore` updates, fix Anthropic model default mismatch, lock CORS — concerns #3, #4, #6, #14. Trivial; do first to unblock clean test runs.
2. **CI/CD baseline**: Build + test + lint in GitHub Actions — concern #1. Without this, every later change is unverified.

**Foundation (other phases depend on these):**
3. **Refresh-token table + migration** — required before rate limiting, because rate limiting on `/auth/refresh` needs to NOT lock out legitimate token rotation.
4. **Background-job pipeline (Hangfire) + job-status endpoint** — required by Tesseract pool, payment integration, data export.
5. **Tesseract pool** — coupled with background jobs; once jobs exist, pool slots in.
6. **Sentry + structured logging + correlation IDs** — minimal cost, high value once the app is generating real errors. Should land before background jobs introduce more complex error paths.

**Functional hardening:**
7. **Rule + AI hybrid classification** — wire `ClassificationRule`, replace `AiOnlyClassificationService`. Independently valuable; separable from above.
8. **DE category expansion** — co-changes with rule classifier (categories drive rules).
9. **Rate limiting** — depends on (3); easy once token rotation is multi-device-safe.
10. **User-friendly upload error mapping** — small, do alongside background-job migration.

**Commercial layer:**
11. **Payment integration (Stripe)** — depends on (4) for webhook→job pattern.
12. **DE legal pages** — independent; can start on day 1, parallelizable.
13. **Account portal pages (billing, data export)** — depend on (11).
14. **Self-serve data export (DSGVO Art. 20)** — depends on (4); slot after payments.
15. **Cookie consent banner** — independent; coordinate with Datenschutz wording.

**Quality + ops:**
16. **Frontend test suite** — install + first happy-path test early; expand throughout.
17. **PostgreSQL integration tests** — install early; gain ROI as more features land.
18. **BetterStack uptime + status page** — final polish before launch.
19. **German localization audit + responsive QA** — final polish.

**Recommended phase grouping (5-7 phases @ Standard granularity per config):**

- **Phase 1 — Foundation cleanup + CI**: hygiene, CI/CD, Sentry, structured logging
- **Phase 2 — Auth + rate-limit hardening**: refresh-token table, rate limiter, account-deletion friction
- **Phase 3 — Background pipeline + Tesseract pool**: Hangfire, job-status endpoint, Tesseract pool, error mapping, useful empty/loading states for upload
- **Phase 4 — Classification trustworthiness**: rule + AI hybrid, DE category expansion, audit/reasoning UX, "create rule from this" flow
- **Phase 5 — Commercial surface**: Stripe checkout + webhooks, invoicing, billing portal, data export
- **Phase 6 — Legal + consent**: Impressum, Datenschutz, AGB, Widerrufsbelehrung, cookie banner, support email + status page
- **Phase 7 — Test depth + launch QA**: PG integration tests, broader Vitest + Playwright coverage, German localization audit, mobile responsive pass, BetterStack uptime live

---

## Anti-Patterns

### Anti-Pattern 1: "Just queue everything" with `Channel<T>`

**What people do:** Skip Hangfire because in-process queue feels lighter.

**Why it's wrong:** Process restart loses in-flight work. Users paid tokens; you owe them results. No retry, no dashboard, no observability.

**Do this instead:** Hangfire + Postgres. Cost = one extra schema in the DB.

### Anti-Pattern 2: Pre-charge tokens at upload, charge again at AI call

**What people do:** Two-phase deduction "to be safe."

**Why it's wrong:** Users see the same purchase deducted twice in the ledger; support tickets follow.

**Do this instead:** Single deduction at job enqueue, refund unknowns post-AI. Existing pattern is fine.

### Anti-Pattern 3: Webhook handler that does heavy work synchronously

**What people do:** Insert payment + grant tokens + send confirmation email all inside the webhook HTTP handler.

**Why it's wrong:** Stripe times out if webhook handler takes > 5s; retry storms ensue.

**Do this instead:** Webhook does only signature-verify + idempotent insert into `payments`. Enqueue Hangfire job. Return 200 immediately.

### Anti-Pattern 4: Exposing Hangfire dashboard without auth

**What people do:** Mount `/hangfire` and forget.

**Why it's wrong:** Anyone can see job names, args, and trigger jobs.

**Do this instead:** `app.UseHangfireDashboard("/hangfire", new() { Authorization = new[] { new HangfireAuthFilter() } });` with admin-role check.

### Anti-Pattern 5: Storing webhook secret in `appsettings.json`

**What people do:** Commit the secret.

**Why it's wrong:** GitHub leak detector catches it; Stripe rotates; chaos.

**Do this instead:** Env var only; use `dotnet user-secrets` for dev.

### Anti-Pattern 6: Trusting Stripe redirect parameters as authoritative

**What people do:** On `?session_id=xxx` redirect, immediately grant tokens.

**Why it's wrong:** Anyone can craft that URL. Webhook is the only authoritative event.

**Do this instead:** Redirect just shows "Vielen Dank! Ihre Tokens werden gleich gutgeschrieben…" — frontend polls `/tokens/balance` to confirm. Webhook does the credit.

### Anti-Pattern 7: Rule classifier without per-user scoping

**What people do:** Global rules.

**Why it's wrong:** What's "Fachliteratur" for a teacher is "Privat" for someone else.

**Do this instead:** `ClassificationRule.UserId` mandatory; global rules only via admin (and only if explicitly desired).

---

## Integration Points

### External Services

| Service | Integration Pattern | Notes |
|---------|---------------------|-------|
| Stripe | REST + webhooks (signature-verified, idempotent insert + enqueue) | Test mode for dev; AVV from Stripe DPA |
| Anthropic | HTTP client + retry on transient errors | Already integrated; new env-key separation |
| Sentry | SDK (.NET + Next.js) | Scrub PII in `BeforeSend` |
| BetterStack | External health checks + Slack/email integration | One-time setup |

### Internal Boundaries

| Boundary | Communication | Notes |
|----------|---------------|-------|
| API ↔ Application | Direct injection via interfaces (existing) | No change |
| Application ↔ Infrastructure | Interface-based DI (existing) | New interfaces: IJobScheduler, IPaymentProvider, IRuleClassifier |
| API ↔ Hangfire dashboard | HTTP, auth-gated | Hangfire owns its own request pipeline behind the gate |
| HTTP request ↔ Background job | DTO via job arguments (POCO + Guid IDs) | Never share `HttpContext` or `ICurrentUser` across the boundary |
| Webhook handler ↔ Token grant | Async via Hangfire enqueue | Idempotency at the `payments` table |

---

## Sources

- Hangfire docs — `docs.hangfire.io`
- Stripe Best Practices for Webhooks — `stripe.com/docs/webhooks#best-practices`
- ASP.NET Core 10 Rate Limiting — `learn.microsoft.com/aspnet/core/performance/rate-limit`
- DSGVO Art. 15, 20, 22 (right of access, portability, no-purely-automated-decisions)
- Existing project structure: `.planning/codebase/ARCHITECTURE.md`, `STACK.md`

---
*Architecture research for: TaxReader hardening milestone (DE commercial launch)*
*Researched: 2026-05-03*
