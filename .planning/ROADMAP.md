# Roadmap: TaxReader

## Overview

TaxReader is a brownfield product that already classifies German tax receipts (PDF/image → AI-categorized → PDF/CSV report). This roadmap covers the **hardening milestone** that takes it from "working code" to "paid commercial DE SaaS by tax-season 2026." Seven phases move from foundation hygiene → through auth, reliability, classification trustworthiness, payments, legal/compliance → to launch QA. Build order is deliberate: foundation/CI must come first (otherwise downstream work is unverified); refresh-token rotation comes before rate limiting; background-jobs come before payments and data export. Core Value (trustworthy classification) is protected by Phase 4 landing before Phase 5's commercial surface.

## Phases

**Phase Numbering:**
- Integer phases (1, 2, 3): Planned milestone work
- Decimal phases (2.1, 2.2): Urgent insertions (marked with INSERTED)

Decimal phases appear between their surrounding integers in numeric order.

- [x] **Phase 1: Foundation Cleanup + CI** — Hygiene fixes, CI/CD, Sentry, structured logging _(4/4 plans complete; pending `/gsd-verify-phase` and operator-side branch protection on `main`)_ (completed 2026-05-11)
- [x] **Phase 2: Auth + Rate-Limit Hardening** — Refresh-token table, rate limiter, account-deletion friction _(3/3 plans complete; pending `/gsd-verify-phase`)_ (completed 2026-05-15)
- [ ] **Phase 3: Background Pipeline + Tesseract Pool** — Hangfire jobs, async upload, OCR pool, error UX
- [ ] **Phase 4: Classification Trustworthiness** — DE category expansion, rule + AI hybrid, audit/override UX, sum validation
- [ ] **Phase 5: Commercial Surface (Payments)** — Stripe checkout + webhooks, Widerrufsrecht waiver, billing page, multi-env safety
- [ ] **Phase 6: Legal + Consent + Data Export** — Impressum, Datenschutz, AGB, Widerrufsbelehrung, cookie banner, AVVs, DSGVO data export, audit log, Marken search
- [ ] **Phase 7: Test Depth + Launch QA** — PG integration tests, Vitest + Playwright, BetterStack live, DE localization audit, mobile QA, lawyer final review

## Phase Details

### Phase 1: Foundation Cleanup + CI
**Goal**: Establish CI/CD with build-test-lint gates, observability via Sentry + structured logging, and clean working tree — so every later phase can be verified.
**Depends on**: Nothing (first phase)
**Requirements**: FND-01, FND-02, FND-03, FND-04, FND-05, OBS-01, OBS-02
**Success Criteria** (what must be TRUE):
  1. Every PR has `dotnet build`, `dotnet test`, `npm run lint`, `npm run build` as merge-blocking checks
  2. No `storage/` directory or `build-diag.txt` is in the working tree; CI fails if reintroduced
  3. Anthropic model used in production matches what's documented in `CLAUDE.md` (no code/compose mismatch)
  4. Sentry receives errors from .NET API and Next.js frontend with PII scrubbed; alert rules don't fire on transient noise
  5. Long-running upload handlers emit log lines correlated by `ReceiptFileId` / `JobId`
  6. New developer can run `docker compose up --build` from `README.md` instructions and reach the app
**Plans**: 4 plans

Plans:

**Wave 1** *(no dependencies — start here)*
- [x] 01-01: Hygiene cleanup — remove `storage/` + `build-diag.txt`, fix Anthropic model alignment, lock CORS, add `.gitignore` rules

**Wave 2** *(blocked on Wave 1 — needs `Backend/Directory.Packages.props` post-01-01)*
- [x] 01-04: Serilog enrichers + correlation-ID `LogContext` in long-running handlers

**Wave 3** *(blocked on Waves 1+2 — appsettings.json + Directory.Packages.props serialization)*
- [x] 01-03: Sentry integration — .NET + Next.js, EU residency, PII scrubbing, conservative alert rules

**Wave 4** *(blocked on Waves 1–3 — runs CI against full Phase 1 surface for first green tick)*
- [x] 01-02: GitHub Actions CI workflow — build/test/lint gates as merge-blocking; top-level `README.md`

**Cross-cutting constraints:** *(must_haves.truths shared across ≥ 2 plans)*
- All plans honour structured-logging convention (named placeholders, never `$"..."` interpolation) per CLAUDE.md
- Plans 01-01 and 01-03 both modify `docker-compose.yml` and `.env.example` — disjoint sections (01-01: Anthropic; 01-03: Sentry); execution order via wave assignment prevents conflict
- Plans 01-04 and 01-03 both modify `Backend/src/TaxReader.Api/appsettings.json` — 01-04 replaces `Serilog` block; 01-03 adds new `Sentry` top-level section after; serialized by Wave 2 → Wave 3 ordering
- Plans 01-01, 01-04, 01-03 all touch `Backend/Directory.Packages.props` — each appends entries; serialized by wave order

### Phase 2: Auth + Rate-Limit Hardening
**Goal**: Multi-device-safe authentication via a `refresh_tokens` table with rotation + replay detection, plus rate limiting that doesn't lock out legitimate token rotation, plus DSGVO-friendly account-deletion confirmation.
**Depends on**: Phase 1
**Requirements**: AUTH-01, AUTH-02, AUTH-03
**Success Criteria** (what must be TRUE):
  1. User can stay logged in on phone and laptop simultaneously across multiple refreshes
  2. A leaked refresh token replayed after rotation triggers full revocation of all the user's tokens
  3. Brute-force login attempts from one IP get rate-limited within 5 attempts/min without blocking legitimate users
  4. Account deletion requires re-authentication via password before firing
  5. Rate-limited responses include German error copy + `Retry-After` header
**Plans**: 3 plans

Plans:
- [x] 02-01: `refresh_tokens` table + `RefreshTokenService` with rotation + replay detection (replaces single-column model)
- [x] 02-02: Account-deletion confirmation modal + re-auth requirement
- [x] 02-03: ASP.NET Core `AddRateLimiter` policies on `/auth/*`, `/auth/refresh`, `/receipt-files`, global

### Phase 3: Background Pipeline + Tesseract Pool
**Goal**: Move the upload pipeline (extract → parse → classify) to Hangfire background jobs so HTTP requests return immediately, OCR scales via a Tesseract pool, and users see useful status + error messages.
**Depends on**: Phase 2
**Requirements**: PIPE-01, PIPE-02, PIPE-03, PIPE-04, PIPE-05, PIPE-06
**Success Criteria** (what must be TRUE):
  1. Multi-receipt upload returns `202 Accepted` with `jobIds` within 1 second
  2. User sees real-time status (Queued / Extracting / Parsing / Classifying / Completed / Failed / Cancelled) on receipts list
  3. Killing the API container mid-upload — job survives and completes on restart (Hangfire persistence)
  4. 10 concurrent image-receipt uploads from one user do not block each other (Tesseract pool, not Singleton-lock)
  5. Upload errors show user-friendly German messages; raw exceptions never returned in HTTP body
  6. Hangfire dashboard at `/hangfire` requires admin authentication; anonymous access returns 401
  7. User can cancel an in-flight upload; tokens debited at enqueue refund correctly on cancellation
**Plans**: 4 plans

Plans:
- [ ] 03-01: Hangfire installation (Postgres-backed) + dashboard auth gate + recurring cleanup jobs
- [ ] 03-02: `ProcessReceiptFileJob` + `202 Accepted` API response + status polling endpoint + cancellation endpoint
- [ ] 03-03: `TesseractEnginePool` (3-5 engines, `Channel<TesseractEngine>`) replacing Singleton-lock pattern
- [ ] 03-04: User-friendly upload error mapping (German strings) + empty/loading/error UI states across upload + receipts list pages

### Phase 4: Classification Trustworthiness
**Goal**: Deliver Core Value — every line item correctly categorized into the right tax category (across 13 DE categories), with rule + AI hybrid for consistency, prominent reasoning users can audit and override, and sum-validation that catches AI hallucinations.
**Depends on**: Phase 3
**Requirements**: CLASS-01, CLASS-02, CLASS-03, CLASS-04, CLASS-05, CLASS-06, CLASS-07
**Success Criteria** (what must be TRUE):
  1. Receipts from a vendor with a saved rule classify deterministically with `ClassificationMethod.Rule` (no Anthropic call needed)
  2. Per-classification reasoning is visible without click-to-expand on receipt detail page; wording is descriptive ("Diese Position passt zu...") not prescriptive ("Sie können absetzen")
  3. User can override a classification and save the override as a per-user `ClassificationRule`
  4. A receipt where line-items don't sum to total (within €0.50 tolerance) flags as `Unverified` and surfaces an audit prompt
  5. All 13 DE tax categories appear in the classification dropdown, the PDF export, and the CSV export — grouped by Anlage where sensible
  6. Auto-confirm threshold is editable in settings; default requires manual confirmation; threshold value is documented
  7. AI-only classification continues to work for items without rule matches; the existing token pre-charge + per-item refund pattern is preserved
**Plans**: 4 plans

Plans:
- [ ] 04-01: `Category` enum expansion to 13 values + EF migration + PDF/CSV export updates
- [ ] 04-02: `RuleBasedClassifier` + `HybridClassificationService` (rules-then-AI) replacing `AiOnlyClassificationService`
- [ ] 04-03: Audit/reasoning UX in receipt detail + "Diese Regel speichern" override-to-rule flow + auto-confirm threshold setting
- [ ] 04-04: Sum-validation rule (€0.50 tolerance) flagging receipts as `Unverified` + UI surface

### Phase 5: Commercial Surface (Payments)
**Goal**: Working Stripe-mediated token-pack purchase with DE-compliant invoicing, signature-verified webhook with idempotent token grant, Widerrufsrecht waiver flow, billing-management page, and multi-environment safety to prevent live keys in dev.
**Depends on**: Phase 3 (background-job pattern), Phase 4 (Core Value before commerce)
**Requirements**: PAY-01, PAY-02, PAY-03, PAY-04, PAY-05, PAY-06
**Success Criteria** (what must be TRUE):
  1. User can complete a Stripe Checkout flow → webhook fires → tokens credited to their balance with a transaction record
  2. The same Stripe webhook event delivered twice grants tokens exactly once (idempotent insert into `payments`)
  3. User cannot purchase without actively checking the Widerrufsrecht waiver checkbox; checkbox is not pre-ticked
  4. User can download a DE-compliant Rechnung PDF (Stripe Invoicing) from the billing page
  5. Refunded purchases reverse token grants via `RevokeTokensJob`; balance can go negative; audit-logged
  6. Production deployment fails to start if `Stripe__SecretKey` starts with `sk_test_` (and vice-versa for dev)
  7. Stripe Customer Portal embed allows user to manage payment methods without custom UI
**Plans**: 4 plans

Plans:
- [ ] 05-01: `StripePaymentProvider` + checkout-session endpoint + signature-verified webhook + idempotent `payments` table + `GrantTokensJob`
- [ ] 05-02: Pre-checkout Widerrufsrecht waiver flow (active checkbox + AGB acceptance before Stripe redirect)
- [ ] 05-03: Account → Billing page (token balance, transaction history, Stripe Invoicing-driven Rechnungen, Customer Portal embed)
- [ ] 05-04: Refund/chargeback handling (`RevokeTokensJob`, audit log) + multi-environment safety (test/live key separation, startup-time guards)

### Phase 6: Legal + Consent + Data Export + AVVs
**Goal**: Launch-ready legal posture — all mandated DE pages, TTDSG cookie consent, signed AVVs/DPAs with all sub-processors, DSGVO Art. 20 self-serve data export, audit log for sensitive operations, and Markenrechte clearance.
**Depends on**: Phase 1 (audit log lives on hardening foundation), Phase 5 (billing-related copy in AGB)
**Requirements**: LEG-01, LEG-02, LEG-03, LEG-04, LEG-05, LEG-06, LEG-07, LEG-08, LEG-09
**Success Criteria** (what must be TRUE):
  1. Impressum, Datenschutzerklärung, AGB, Widerrufsbelehrung pages exist, lawyer-reviewed, and are linked from every footer
  2. Cookie banner shows on first visit with equally prominent "Alle akzeptieren" + "Nur notwendige" / "Ablehnen" options; revoke option reachable from footer; essential auth cookies excluded
  3. Datenschutzerklärung lists Anthropic, Stripe, Sentry, BetterStack as sub-processors with AVV/DPA links and Drittland-Übermittlung disclosure (Schrems II / TADPF)
  4. AVVs/DPAs from Anthropic, Stripe, Sentry, BetterStack are signed and on file
  5. User can trigger data export from settings → `ExportUserDataJob` produces JSON+CSV bundle → downloadable link emailed within 24h
  6. `audit_log` table records account deletions, payment grants, refresh-token revocations, classification-override-rule creations
  7. DPMA + EUIPO Marken search complete; results documented (cleared, conflicted, or registered)
  8. AGB explicit on StBerG-safe positioning ("Vertragsgegenstand ist Strukturierung, keine Steuerberatung") and GoBD non-applicability
**Plans**: 5 plans

Plans:
- [ ] 06-01: Legal pages content (Impressum, Datenschutz, AGB, Widerrufsbelehrung) + lawyer review track
- [ ] 06-02: TTDSG cookie banner + consent gating for Sentry/analytics
- [ ] 06-03: AVVs/DPAs sign-off (Anthropic, Stripe, Sentry, BetterStack) + `audit_log` table + `AuditLogger` for sensitive operations
- [ ] 06-04: Self-serve data export (`ExportUserDataJob`, JSON+CSV bundle, email-link delivery)
- [ ] 06-05: DPMA + EUIPO Marken search + optional registration

### Phase 7: Test Depth + Launch QA
**Goal**: Final quality gates and ops readiness before commercial launch — Postgres integration tests catch schema bugs, Vitest + Playwright cover frontend, BetterStack monitors are live, all user-facing copy is German, mobile flows work, lawyer's final sign-off complete.
**Depends on**: Phase 6 (legal copy must exist before final lawyer review)
**Requirements**: QA-01, QA-02, QA-03, QA-04, QA-05, QA-06, QA-07, OBS-03
**Success Criteria** (what must be TRUE):
  1. CI runs Postgres integration tests against real Postgres in Docker (Testcontainers), covering duplicate detection, cascade deletes, refresh-token rotation, payment idempotency, migration smoke
  2. Vitest covers auth hooks, upload state machine, RHF + Zod form validation; component tests for login, register, upload, classification-confirm
  3. Playwright happy-path E2E test runs end-to-end in DE locale on every PR
  4. BetterStack uptime monitors are live on `/health` + `/api/v1/health`; status page accessible from footer; deploy maintenance windows configured
  5. Every user-facing string is in German (`Sie`-form); native-speaker review complete; EUR-formatted via `Intl.NumberFormat('de-DE')`
  6. Mobile receipts upload + classification-confirm flow works on `sm` (640px) and `md` (768px) breakpoints
  7. Sentry alert rules tuned against real-traffic baseline (post Phase 1); "quiet hours" 23:00-07:00 only HIGH severity
  8. AGB + Datenschutzerklärung have lawyer's final pre-launch sign-off
  9. "Looks done but isn't" checklist (PITFALLS.md) verified end-to-end
**Plans**: 5 plans

Plans:
- [ ] 07-01: PostgreSQL integration test project (Testcontainers + Respawn) covering critical paths
- [ ] 07-02: Vitest unit + component tests; Playwright E2E happy path; CI integration
- [ ] 07-03: BetterStack go-live (monitors, status page, maintenance windows) + Sentry alert rules tuned
- [ ] 07-04: German localization audit + mobile-responsive QA pass
- [ ] 07-05: Final lawyer review + "Looks done but isn't" checklist verification + go/no-go launch decision

## Progress

**Execution Order:**
Phases execute in numeric order: 1 → 2 → 3 → 4 → 5 → 6 → 7

| Phase | Plans Complete | Status | Completed |
|-------|----------------|--------|-----------|
| 1. Foundation Cleanup + CI | 4/4 | Complete    | 2026-05-11 |
| 2. Auth + Rate-Limit Hardening | 3/3 | Complete    | 2026-05-15 |
| 3. Background Pipeline + Tesseract Pool | 0/4 | Not started | - |
| 4. Classification Trustworthiness | 0/4 | Not started | - |
| 5. Commercial Surface (Payments) | 0/4 | Not started | - |
| 6. Legal + Consent + Data Export | 0/5 | Not started | - |
| 7. Test Depth + Launch QA | 0/5 | Not started | - |

**Coverage:** 47 v1 requirements mapped to 7 phases (29 plans). Zero unmapped.

---
*Roadmap created: 2026-05-03*
