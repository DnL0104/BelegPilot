# Project Research Summary

**Project:** TaxReader
**Domain:** DE B2C tax-receipt SaaS — hardening for commercial launch
**Researched:** 2026-05-03
**Confidence:** HIGH

---

## Executive Summary

TaxReader is a brownfield .NET 10 + Next.js 16 product that already classifies German receipts via Anthropic + Tesseract and produces PDF/CSV reports. The hardening milestone closes the gap between "working code" and "paid commercial DE SaaS by tax-season 2026." The core challenge is not new feature work but: (1) installing the commercial layer (Stripe, invoicing, AGB, Datenschutz, Impressum, Widerrufsbelehrung, cookie consent, AVVs), (2) closing reliability gaps the existing synchronous architecture has at hundreds-of-users scale (Hangfire-backed background-job pipeline, Tesseract pool), and (3) raising classification trustworthiness via the unused-but-already-present rule + AI hybrid (load-bearing for Core Value).

The recommended additive stack is Stripe (over Mollie/SEPA-direct — best .NET ecosystem + Stripe Tax + Invoicing for DE compliance), Hangfire (over Channel<T> — persistence is non-negotiable for paid work), Sentry + BetterStack (solo-dev friendly, EU-residency available), Vitest + Playwright + Testcontainers.PostgreSql for the missing test layers. The biggest non-code risks are legal/regulatory: StBerG line-crossing (the AI's reasoning must describe categorization, not advise on claimability), DSGVO Art. 22 disclosure (automated decision-making + Anthropic AVV), and Widerrufsrecht waiver flow at checkout. These need lawyer review before launch — the largest hidden cost line.

The architecture changes are additive, not destructive. Existing Clean Architecture layers absorb each new piece via new interfaces (`IJobScheduler`, `IPaymentProvider`, `IRuleClassifier`) plus new concrete services in Infrastructure. The build order is opinionated: foundation cleanup + CI first (otherwise every later change is unverified), auth + rate-limit hardening second (refresh-token table is a pre-req for safe rate limiting on `/auth/refresh`), background-job pipeline third (a foundation that payment integration, Tesseract pool, and data export all depend on), then classification trustworthiness, commercial surface, legal, and finally test depth + launch QA.

---

## Key Findings

### Recommended Stack

See `STACK.md` for full detail. Summary:

**Additions to existing stack:**
- **Stripe** (47.x .NET SDK) — payment provider; Stripe Tax for DE VAT, Stripe Invoicing for §14 UStG-compliant Rechnungen
- **Hangfire** (1.8.x) + Hangfire.PostgreSql — background-job orchestration on existing Postgres, no Redis needed
- **Sentry** (.NET + Next.js SDKs) — error tracking with EU residency option; GlitchTip self-hosted as Plan B
- **BetterStack Uptime** — external health checks, status page included, DE/EU vantage points
- **Vitest 3 + RTL + Playwright 1.50** — frontend tests (Next.js 16 + React 19 native)
- **Testcontainers.PostgreSql 4 + Respawn 6** — replace EF in-memory tests with real PG
- **ASP.NET Core built-in `AddRateLimiter`** — no third-party package needed
- **Serilog enrichers + LogContext.PushProperty** — structured logging with correlation IDs

**Picks NOT to use:**
- Mollie / SumUp / direct SEPA — defer until DE-only payment-method demand justifies the integration cost
- AspNetCoreRateLimit package — built-in supersedes
- Channel<T>-only background queue — loses paid work on restart
- AWS Textract / Google Vision — adds US data residency cost; pool TesseractEngine instead
- ChatGPT-generated AGB — must be lawyer-reviewed for DE consumer law

### Expected Features

See `FEATURES.md` for full detail.

**Must have (table stakes for paid DE launch):**
- Working Stripe checkout + webhook → token grant
- DE-compliant invoice (Rechnung) per purchase
- Impressum (TMG §5), Datenschutzerklärung (DSGVO Art. 13), AGB (BGB §305+), Widerrufsbelehrung (BGB §312g)
- TTDSG-compliant cookie banner
- Background-processed uploads with status visibility
- Self-serve data export (DSGVO Art. 20)
- Useful upload error messages (no exception leakage)
- DE category set: Werbungskosten / Sonderausgaben / agB / Haushaltsnah / Handwerker
- DE-localized UI + EUR-formatted numbers
- Rule + AI hybrid classification with auditable reasoning
- Self-serve account portal (billing, password change, data export, account deletion w/ confirm)
- Email support + status page link

**Should have (differentiators):**
- AI reasoning prominently surfaced (Core Value)
- Bulk re-classify by rule (deferred UI; backend rule engine in v1)
- Plain-language category descriptions matched to Anlagen lines
- Year-over-year trend by category
- Privacy-positive marketing posture (no ads, no tracking, EU-only)

**Defer (v1.x or later):**
- Self-employed mode (Anlage S / EÜR)
- DATEV / Lexware export
- ELSTER ERiC (separate certified product)
- Mollie alongside Stripe
- Auto-promotion of corrections-into-rules

**Anti-features (deliberately NOT built):**
- "What can I claim?" recommendations — StBerG line
- Tax-advice chat — StBerG land-mine
- Multi-user / shared workspaces — scope explosion
- Mobile native apps — solo dev cannot maintain
- AI-generated tax tips emails — StBerG + DSGVO marketing-consent risk

### Architecture Approach

See `ARCHITECTURE.md` for full detail.

The existing Clean Architecture (.NET 10: API / Application / Domain / Infrastructure + Next.js frontend) is preserved. New components slot in via interface-based DI:

**Major components added:**
1. **`HangfireJobScheduler` + `ProcessReceiptFileJob`** — moves the upload pipeline (extract / parse / classify) off the HTTP request lifecycle. API returns `202 Accepted` + jobId; frontend polls.
2. **`StripePaymentProvider` + webhook endpoint** — anonymous, signature-verified, idempotent insert into `payments` table, then enqueue token-grant job.
3. **`HybridClassificationService`** — composes `RuleBasedClassifier` (DB-backed deterministic match against `ClassificationRule`) and existing `ClaudeAiClassifier`. Rules first; AI for unmatched. Replaces `AiOnlyClassificationService`.
4. **`RefreshTokenService` + `refresh_tokens` table** — multi-row, hash-only storage, rotation with replay detection. Replaces `user.RefreshToken` single column.
5. **`TesseractEnginePool`** — `Channel<TesseractEngine>` for 3-5 pooled instances; replaces Singleton + lock pattern.
6. **`AuditLogger` + `audit_log` table** — sensitive operations (account deletion, payment grants, refresh-token revocations) for support + DSGVO Art. 15.

**Build-order dependencies (driving phase structure):**
- Foundation cleanup + CI must come first — otherwise downstream work is unverifiable.
- Refresh-token table before rate limiting (otherwise rate limiting on `/auth/refresh` either locks legitimate users or is too lenient).
- Background-job pipeline before Tesseract pool (pooling sync OCR is silly), and before payment integration (webhook → enqueue), and before data export (export is async).

### Critical Pitfalls

See `PITFALLS.md` for full detail. Top 5 launch risks:

1. **Crossing the StBerG line** — AI reasoning that says "Sie können dies absetzen" instead of "Diese Position passt zu Kategorie X" is regulated tax advice. Prevention: descriptive reasoning only, AGB §1 explicit, marketing copy reviewed by lawyer.
2. **Anthropic AVV not signed at launch** — DSGVO Art. 28 violation. Start the AVV/DPA process on Phase 6 day 1; can take 1-3 weeks.
3. **Webhook double-grant** — without unique-constraint dedup on `stripe_event_id`, Stripe retries can grant tokens twice. Idempotent insert + enqueue pattern is non-negotiable.
4. **Widerrufsrecht waiver hidden or pre-ticked** — pre-checkout flow MUST require active acknowledgement; without it, users can demand refunds within 14 days even after spending tokens.
5. **AI hallucinating amounts** — never let AI re-read amounts; AI is for classification only; line-item totals must validate against receipt total within €0.50 tolerance.

Additional notable pitfalls: solo-dev paging burnout (conservative alert rules from day 1), Markenrechte search before launch (DPMA/EUIPO), `storage/` directory PII residue (CI check), Stripe live key accidentally in dev (startup-time guard), GoBD scope creep (AGB explicit non-applicability).

---

## Implications for Roadmap

Based on research, recommended **7-phase structure** (Standard granularity per `.planning/config.json`):

### Phase 1: Foundation Cleanup + CI

**Rationale:** Every later phase needs CI gates and clean test runs. Hygiene fixes are trivially cheap and remove confusion. Sentry installed early so the rest of development surfaces real-error data.

**Delivers:**
- Removed `storage/`, `build-diag.txt` from working tree + `.gitignore` updates
- Anthropic model default mismatch fixed (code + compose aligned)
- CORS production policy lock-down
- GitHub Actions CI: build + test + lint as merge-blocking
- Sentry .NET + Next.js installed, EU residency, conservative alert rules
- Serilog enrichers + correlation-ID `LogContext` push
- Top-level README with onboarding

**Addresses features:** Foundation only — no user-visible features.
**Avoids pitfalls:** `storage/` PII (#12), live-migration breakage (CI populated-DB tests), solo-dev paging burnout (conservative rules from day 1).

### Phase 2: Auth + Rate-Limit Hardening

**Rationale:** Refresh-token table is a pre-req for safe rate limiting on `/auth/refresh`. Account-deletion friction is a small DSGVO + UX win bundled here. Multi-device support is itself a paid-product table stake.

**Delivers:**
- `refresh_tokens` table + RefreshTokenService with rotation + replay detection
- Account-deletion confirmation modal (re-auth + irreversibility warning)
- ASP.NET Core `AddRateLimiter` policies on `/auth/*`, `/auth/refresh`, `/receipt-files`, global

**Uses stack:** Built-in rate limiter, no new package.
**Implements architecture:** Refresh-token rotation pattern (ARCHITECTURE.md Pattern 4).
**Avoids pitfalls:** Refresh-token replay (#13), credential-stuffing (concern #13).

### Phase 3: Background-Job Pipeline + Tesseract Pool

**Rationale:** Foundation pattern for payment integration, data export, and any async work. Tesseract pool only pays off once jobs exist. User-friendly upload errors and empty/loading states are coupled to the upload-flow rewrite.

**Delivers:**
- Hangfire installed (Postgres-backed) + dashboard auth-gated at `/hangfire`
- `ProcessReceiptFileJob` orchestrating extract → parse → classify off the HTTP path
- `GET /receipt-files/{id}/status` for frontend polling
- `TesseractEnginePool` (3-5 instances, Channel<T>-based)
- User-friendly German upload error mapping (concern #12 fix)
- Empty/loading/error states for upload + receipts list
- `POST /receipt-files/{id}/cancel` + status reflecting cancellation

**Uses stack:** Hangfire 1.8 + Hangfire.PostgreSql.
**Implements architecture:** Background-job pattern (Pattern 1), Tesseract pool, async cancellation (Pattern 5).
**Avoids pitfalls:** Sync upload pipeline (concern #8), Tesseract serialization (concern #9), error leakage (concern #12), Hangfire dashboard exposure (anti-pattern #4).

### Phase 4: Classification Trustworthiness

**Rationale:** This phase IS Core Value. Without rule + AI hybrid + DE category expansion + audit/reasoning UX, "Anyone DE" cannot trust the output.

**Delivers:**
- `RuleBasedClassifier` wired up against existing `ClassificationRule` entity
- `HybridClassificationService` replacing `AiOnlyClassificationService`
- DE category enum expanded (Werbungskosten Arbeitsmittel / Fachliteratur / Büromaterial / Reisekosten / Fortbildung / Telekommunikation; Sonderausgaben Spenden / Vorsorge; agB Krankheit; Haushaltsnah; Handwerker; Privat; Unbekannt) — migrations + data-model updates
- Per-classification reasoning surfaced prominently in UI ("Warum wurde das so eingeordnet?")
- "Diese Regel speichern" button on overrides → creates user-scoped `ClassificationRule`
- Sum-validation: line-item totals vs receipt total within €0.50; mismatch → `Unverified` flag
- Auto-confirm threshold visible + user-settable in settings

**Uses stack:** No new packages — leverages existing.
**Implements architecture:** Hybrid classification (Pattern 2).
**Avoids pitfalls:** AI hallucinating amounts (#8), German number/Umlaut issues (#9), DSGVO Art. 22 disclosure foundation (#2).

### Phase 5: Commercial Surface (Payments)

**Rationale:** Without working payment, there is no paid product. Depends on Phase 3 (webhook → background-job pattern). Stripe Customer Portal embeds reduce custom-UI scope.

**Delivers:**
- `StripePaymentProvider` + checkout-session creation endpoint
- `/webhooks/stripe` anonymous, signature-verified, idempotent (`payments` table with UNIQUE on `stripe_event_id`)
- `GrantTokensJob` enqueued from webhook handler
- `RevokeTokensJob` for refunds/chargebacks
- Account → Billing page (token packs, history, invoices via Stripe Customer Portal)
- Pre-checkout Widerrufsrecht waiver flow (active checkbox before Stripe redirect)
- Multi-environment safety: separate test/live keys, startup-time guard against `sk_live_*` in non-Production

**Uses stack:** Stripe.net 47.x, Stripe Tax, Stripe Invoicing.
**Implements architecture:** Payment integration (Pattern 3), multi-environment safety (Pattern 6).
**Avoids pitfalls:** Webhook double-grant (#5), Widerrufsrecht hidden (#6), Stripe live in dev (#14), trusting redirect URL params (anti-pattern #6).

### Phase 6: Legal + Consent + Data Export + AVVs

**Rationale:** Many independent items but coordinated under one phase because they're all "non-code launch readiness" and benefit from a single lawyer engagement. Self-serve data export pairs naturally because it's the technical fulfillment of DSGVO Art. 20.

**Delivers:**
- Impressum, Datenschutzerklärung, AGB, Widerrufsbelehrung pages (Next.js `(legal)/` route group)
- TTDSG-compliant cookie banner (Klaro or CookieConsent v3)
- Self-serve data export (DSGVO Art. 20) — `ExportUserDataJob` produces JSON+CSV bundle, email link
- AVVs/DPAs signed: Anthropic, Stripe, Sentry, BetterStack
- Anthropic Drittland-Übermittlung disclosure (Schrems II / TADPF) in Datenschutz
- DPMA/EUIPO Marken search; trade-name registration if clear
- StBerG-safe positioning audit (AGB §1 + product copy)
- GoBD non-applicability statement in AGB
- Lawyer review of AGB + Datenschutz (not blocking to development; coordinate parallel)
- `audit_log` table + `AuditLogger` for sensitive operations

**Uses stack:** Cookie banner library, no other packages.
**Avoids pitfalls:** StBerG line-crossing (#1), Anthropic AVV missing (#3), Schrems II / TADPF gap (#4), Markenrechte conflict (#7), GoBD scope creep (#15), Verbraucherzentrale escalation (#17).

### Phase 7: Test Depth + Launch QA

**Rationale:** Final hardening + UX polish + ops readiness. Some items (DE localization audit, mobile-responsive QA) deliberately late so they review the assembled product, not in-progress pieces.

**Delivers:**
- PostgreSQL integration tests (Testcontainers + Respawn) covering: duplicate detection, cascade deletes, migrations smoke, refresh-token rotation, payment idempotency
- Vitest unit + component tests: auth hooks, upload state, form validation
- Playwright E2E happy path: register → login → upload → see classification → confirm → see report → export
- BetterStack Uptime monitors live, status page link in footer, deploy maintenance windows configured
- Sentry alert rules tuned to real-traffic baselines
- German localization audit (every user-facing string, native-speaker review)
- Mobile-responsive QA pass (sm/md breakpoints, photo-receipt upload from phone)
- "Looks done but isn't" pre-launch checklist verified end-to-end
- Final pre-launch DSGVO + StBerG + AGB review with lawyer

**Uses stack:** Vitest 3, Playwright 1.50, Testcontainers.PostgreSql 4, Respawn 6.
**Avoids pitfalls:** Migration breakage (#10), status page over-sensitivity (#16), localization gaps, mobile-flow breakage.

### Phase Ordering Rationale

- **Phase 1 first**: foundation/CI/observability — without this, every later phase is unverified.
- **Phase 2 next**: refresh-token table is a hard pre-req for rate-limit on `/auth/refresh` (Phase 2 itself).
- **Phase 3 third**: background-job pipeline is the substrate for payment webhooks (Phase 5), data export (Phase 6), and Tesseract pool (Phase 3 internal).
- **Phase 4 vs Phase 5 ordering**: Classification trustworthiness (4) before commercial surface (5) because Core Value is "trustworthy classification" — paid users must trust the output before being asked to pay. Phases are otherwise independent and could swap if commercial pressure dominates.
- **Phase 6 separable**: Most legal work parallels Phases 4-5 (lawyer review on its own track); placed as a phase to ensure it isn't skipped.
- **Phase 7 last**: validates the assembled product, not pieces in flight.

### Research Flags

Phases needing **deeper per-phase research during planning**:

- **Phase 5 (Commercial Surface):** Stripe-specific DE invoicing rules (Kleinunternehmer §19 vs Stripe Tax UStG handling), exact Widerrufsrecht waiver wording, refund/chargeback ledger reconciliation pattern. Worth a phase-research spawn before planning.
- **Phase 6 (Legal):** Anthropic AVV process specifics (response time, signature flow), GDPR Drittland-Übermittlung wording, Markenrechte search execution. Likely needs WebFetch + lawyer-conversation outside the agent loop.
- **Phase 4 (Classification trustworthiness):** OCR test corpus design (which receipt formats to include, accuracy benchmarks), category-mapping decision (do we keep `Category` enum + add subcategories or refactor?). Domain-modelling decision worth dedicated planning.

Phases with **standard patterns** (skip per-phase research):

- **Phase 1 (Foundation Cleanup + CI):** GitHub Actions, Sentry, gitignore — well-documented.
- **Phase 2 (Auth + Rate-Limit):** Patterns are textbook (refresh-token table, ASP.NET rate limiter).
- **Phase 3 (Background Pipeline):** Hangfire docs sufficient.
- **Phase 7 (Test Depth):** Standard Testcontainers + Vitest + Playwright patterns.

---

## Confidence Assessment

| Area | Confidence | Notes |
|------|------------|-------|
| Stack | HIGH | Picks tied to existing stack constraints; payment-provider rationale grounded in the user's "Help me decide" + DE-market posture; versions current to 2026 |
| Features | HIGH | Legal-page requirements regulation-driven; UX patterns industry-standard for paid SaaS |
| Architecture | HIGH | Patterns are well-established (background jobs, hybrid classification, refresh-token rotation, payment webhooks); slots cleanly into existing Clean Architecture |
| Pitfalls | HIGH | DSGVO/StBerG citations specific; codebase concerns extended with launch-specific risks; lawyer review still required for final wording |

**Overall confidence:** HIGH

### Gaps to Address

Open questions that require resolution during planning of specific phases or as out-of-band tasks:

- **Kleinunternehmer §19 status**: Will the user operate as Kleinunternehmer (revenue ≤ €25k) at launch, or register for VAT immediately? Affects Stripe Tax setup and invoice templates. Resolve with user/lawyer before Phase 5.
- **Anthropic data-residency option**: Anthropic offers EU-residency endpoints for some plans; check current availability for the user's plan. May require negotiating DPA terms. Resolve in Phase 6 day 1.
- **Existing PDF export schema vs new categories**: The QuestPDF-rendered PDF already exists for the teacher categories. Refactor to handle 13-category set; possibly group on output by Anlage. Decide during Phase 4 planning.
- **Frontend localization library choice**: `next-intl` vs hand-rolled string-key map. Lean toward `next-intl` for type safety and DE/EN future-proofing, but the project may already have conventions in `Frontend/src/lib/`. Decide in Phase 7 prep.
- **Stripe Customer Portal vs custom billing UI**: Stripe Customer Portal is free + DE-localized, but customisation is limited. Decide based on UX requirements during Phase 5 planning.

---

## Sources

### Primary (HIGH confidence)
- Stripe Docs (`stripe.com/docs/payments/sepa-debit`, `stripe.com/docs/tax`, `stripe.com/docs/api/idempotent_requests`, `stripe.com/docs/webhooks#best-practices`)
- Hangfire Docs (`docs.hangfire.io`)
- ASP.NET Core 10 Rate Limiting (`learn.microsoft.com/aspnet/core/performance/rate-limit`)
- Sentry .NET / Next.js Docs (`docs.sentry.io/platforms/dotnet/`, `docs.sentry.io/platforms/javascript/guides/nextjs/`)
- Testcontainers for .NET (`dotnet.testcontainers.org/modules/postgres/`)
- DSGVO (Art. 6, 13, 14, 20, 22, 28)
- StBerG §1, §5
- BGB §305-310, §312g + EGBGB Art. 246a
- TMG §5, TTDSG §25
- §14, §19 UStG; §147 AO
- Project artifacts: `.planning/PROJECT.md`, `.planning/codebase/{ARCHITECTURE,STACK,CONCERNS,STRUCTURE,CONVENTIONS,INTEGRATIONS,TESTING}.md`

### Secondary (MEDIUM confidence)
- TADPF (Trans-Atlantic Data Privacy Framework) certification status of Anthropic — verify current status at `dataprivacyframework.gov` before Phase 6
- DE B2C SaaS pricing patterns (industry observation of smartsteuer / WISO / Taxfix surfaces)
- Tesseract pool sizing (3-5 starting point; tune at deploy)

### Tertiary (LOW confidence — to validate during execution)
- Real-traffic Sentry alert thresholds — set conservatively in Phase 1, tune in Phase 7
- Specific Anthropic AVV negotiating timeline — start Phase 6 day 1; revisit if blocking launch
- Markenrechte clearance for "TaxReader" — DPMA + EUIPO search needed; result determines whether rename precedes launch

---
*Research completed: 2026-05-03*
*Ready for roadmap: yes*
