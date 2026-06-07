# TaxReader

## What This Is

TaxReader is a web application that helps German private taxpayers turn a pile of receipt PDFs and images into a clean per-category-per-year expense summary they can transcribe into ELSTER or hand to their Steuerberater. Receipts are text-extracted (PdfPig + Tesseract OCR), parsed by format-specific parsers (Amazon, Eduki, Generic), AI-classified into tax-relevant categories, and aggregated into a German-localized PDF/CSV report. **This milestone hardens the existing build for a commercial DE launch by tax season.**

## Core Value

Trustworthy classification — every line item correctly categorized into the right tax category, with reasoning the user can audit and override. If accuracy fails, the report is worthless.

## Requirements

### Validated

<!-- Inferred from existing codebase. See .planning/codebase/ for full map. -->

- ✓ Receipt upload — PDF + image (JPG/PNG/WEBP) — existing
- ✓ Duplicate detection via SHA-256 hash, scoped per user — existing
- ✓ PDF text extraction (PdfPig, bounding-box-based line reconstruction) — existing
- ✓ Image OCR fallback (Tesseract, German + English language packs) — existing
- ✓ Format-specific receipt parsers — Amazon, Eduki, Generic fallback — existing
- ✓ AI classification via Claude (Anthropic API), single batched call per upload — existing
- ✓ Per-item classification reasoning surfaced to user — existing
- ✓ Manual classification confirmation / override — existing
- ✓ Single-receipt reclassification — existing
- ✓ Token-based usage economy — `UserTokenBalance` + `TokenTransaction` ledger — existing
- ✓ Auto-confirm classifications above user-set confidence threshold — existing
- ✓ Per-category, per-year aggregated reporting — existing
- ✓ Annual summary report — existing
- ✓ German-localized PDF report export (QuestPDF Community) — existing
- ✓ CSV export — existing
- ✓ Email/password registration + 10 welcome tokens — existing
- ✓ JWT access + refresh token auth, BCrypt password hashing — existing
- ✓ Per-user data isolation (handler-side `userId` filtering on all queries) — existing
- ✓ Self-service account deletion with cascading data removal — existing
- ✓ Self-hosted Docker Compose deployment (db, api, web, caddy) with TLS at the edge — existing

### Active

<!-- The hardening milestone — what we're shipping toward commercial launch. Hypotheses until validated. -->

**Payment & monetization**
- [ ] Working payment integration so token purchases actually charge money (current `POST /tokens/purchase` is a stub — concern #7)
- [ ] Self-serve token-purchase flow with receipts/invoices for users
- [ ] Refund / failure handling for payment errors

**Compliance & legal**
- [x] GDPR posture — privacy policy, ToS, cookie/consent handling, AVV with Anthropic, GoBD where applicable — _Validated in Phase 6 (LEG-01..05): legal pages + TTDSG cookie consent shipped; AVV/DPA signing is a tracked operator action (LEG-06)_
- [x] StBerG-safe positioning — clear "Helfer, not Berater" framing in copy and ToS — _Validated in Phase 6 (LEG-03 AGB; lawyer review deferred to Phase 7 QA-07)_
- [x] Self-serve data export (user can download all their data) — _Validated in Phase 6 (LEG-07; 06-04 + 06-06 — bundle includes receipts, parsed_receipts, items, classifications, token_transactions, audit_log)_
- [x] Documented data-deletion path (already implemented — confirm + document) — _Validated in Phase 6 (LEG-08 audit log records account deletions)_

**Classification quality** (drives Core Value)
- [ ] Broaden tax-relevant categories beyond the teacher set (Anyone DE)
- [ ] Rule + AI hybrid classification — wire up the existing-but-unused `ClassificationRule` entity (concern #16) for consistent baseline + AI for edge cases
- [ ] Per-user override patterns — when a user corrects a classification, learn from it (or at least surface it to a rule editor)

**Reliability & throughput**
- [ ] Background-job upload pipeline — return `202 Accepted`, process async, frontend polls (concern #8)
- [ ] Tesseract pool or background-job parallelization (concern #9 — current Singleton + lock serializes OCR)
- [ ] PdfPig zero-words fallback routes to Tesseract OCR (concern #11)
- [ ] User-friendly error messages on upload failures, no internal exception leakage (concern #12)
- [ ] Rate limiting on `/auth/*` and concurrency limit on `/receipt-files` (concern #13)
- [ ] Refresh token table with rotation + multi-device support (concern #10)

**Operability** (solo-dev with paging)
- [ ] CI/CD pipeline — build, test, lint as merge-blocking checks (concern #1)
- [x] Frontend test suite — Vitest + React Testing Library + Playwright happy paths (concern #2) — _Validated in Phase 7 (QA-02/QA-03): Vitest (19 tests) + Playwright DE-locale happy-path (07-04/07-05); live E2E runs in CI heavy job (07-06)_
- [x] PostgreSQL integration tests via Testcontainers (concern #15) — _Validated in Phase 7 (QA-01): TaxReader.IntegrationTests with Testcontainers + Respawn (07-01); runs in CI heavy job (07-06)_
- [ ] Structured logging with correlation IDs threaded through long-running handlers (concern #20)
- [ ] Error tracking with paging (Sentry or equivalent)
- [x] Uptime monitoring on the public surface — _Validated in Phase 7 (OBS-03/QA-06): anonymous /health + /api/v1/health endpoints (07-03) + BetterStack keyword-monitor + Sentry quiet-hours wiring docs (07-07 OPS-SETUP); live monitor provisioning is a tracked operator action_
- [ ] Top-level README with local-dev onboarding (concern #18)

**Hygiene & security**
- [ ] Remove leaked `storage/` directory + `build-diag.txt` from working tree, add to `.gitignore` (concerns #3, #4)
- [ ] Reconcile Anthropic model default between code and `docker-compose.yml` (concern #6)
- [ ] CORS production policy: deny all when `CORS_ALLOWED_ORIGINS` unset in non-Development (concern #14)
- [x] German localization audit — every user-facing string — _Validated in Phase 7 (QA-04): automated DE-localization CI guard on Frontend/src + Playwright de-DE/Europe-Berlin locale E2E (07-05/07-06); native-speaker polish review is a tracked non-blocking (D-06) item_

### Out of Scope

<!-- Explicit boundaries for this milestone. Includes reasoning to prevent re-adding. -->

- Native mobile apps (iOS/Android) — web-only; mobile browser is sufficient
- Multi-user accounts / orgs / teams / shared workspaces — single user per account; couples filing jointly use one account
- Bank account / DATEV / Lexware automated sync — PDF/image upload is the only ingest path
- Tax advice / "hand-holding" recommendations — we are a Helfer, not a Steuerberater (StBerG)
- Full ELSTER filing via ERiC — requires official certification + audit; separate multi-month product
- Native ELSTER import-format export — `Mein ELSTER` does not accept the kind of data we produce in any structured way; PDF/CSV for manual transcription is the right level
- Horizontal scaling / Kubernetes / managed cloud — single Docker Compose stack is the deployment target for hundreds-of-users scale
- Multi-tenant rule editor for advanced users — defer; per-user override learning is a hypothesis to test first
- Year-prior-to-2024 historical filing support — focus on current-year + last-year tax periods
- AI-generated tax tips / "you can also claim X" features — explicitly excluded by StBerG-safe positioning

## Context

- **Brownfield**: substantial codebase already built (.NET 10 / EF Core 10 / PostgreSQL 17 / Next.js 16 / shadcn/ui / Caddy / Anthropic / Tesseract). Full map: `.planning/codebase/`.
- **Concerns inventory**: codebase analysis on 2026-04-29 flagged 20 concerns — 7 High, 8 Medium, 5 Low. Active requirements above tackle all High items and most Medium.
- **Original framing drift**: legacy `CLAUDE.md` describes the product as "BelegPilot" for teachers; actual implementation under the name "TaxReader" already supports broader use. This milestone formalizes the broadening to Anyone DE.
- **AI dependency**: Anthropic Claude (Haiku/Sonnet) provides classification with structured per-item reasoning. The reasoning surface is what makes the audit/override UX possible — backbone of Core Value.
- **Cost model**: each classification consumes Anthropic API tokens. The token economy is the existing pass-through mechanism; the missing piece is real money in.
- **OCR ceiling**: Tesseract is local, in-container, German + English. Currently a Singleton with internal locking — throughput collapses under concurrent image-receipt uploads. Real concern at hundreds-of-users scale.
- **Tax season window**: German Steuererklärung deadline for non-advised filers is end of July; with advisor, end of February of the following year. Launching for the 2025 return cycle requires a useful product before late summer 2026.

## Constraints

- **Tech stack**: .NET 10 / EF Core / PostgreSQL / Next.js 16 / shadcn/ui / Anthropic / Tesseract — locked. No rewrites in this milestone.
- **Timeline**: Commercial launch within ~3 months of 2026-05-02 — target window is by tax-season peak (~July 2026).
- **Operations**: Solo developer with paging-style alerting expectation. No on-call rotation, no support team. Automation must compensate.
- **Scale target**: 100–500 paying users in first 6 months. Design for that, not thousands.
- **Compliance**: GDPR mandatory. StBerG positioning ("Helfer, not Berater") mandatory. GoBD where applicable. Anthropic AVV (Auftragsverarbeitungsvertrag) required for processing personal data.
- **Localization**: All end-user UI and copy in German.
- **Hosting**: Self-hosted Docker Compose stack with Caddy edge. Not migrating to managed cloud in this milestone.
- **Budget**: AI inference cost flows through the token economy (pass-through). Other tooling (Sentry, payment provider fees, monitoring) bounded by what a pre-revenue solo product can absorb.

## Key Decisions

| Decision | Rationale | Outcome |
|----------|-----------|---------|
| Audience: Anyone DE (not just teachers) | Wider TAM during tax season; existing data model already category-agnostic | — Pending |
| Timeline: ~3 months to commercial launch | German tax-season demand window is real; missing it = wait a full year | — Pending |
| No ELSTER/ERiC integration this milestone | Certification, audit, and tax-software liability are out of scope for the timeline | — Pending |
| Output is PDF/CSV summary, not automated submission | Matches what `Mein ELSTER` actually accepts from external tools (effectively nothing structured) | — Pending |
| Solo dev with paging, design-for-hundreds | Honest about ops capacity; over-engineering for thousands wastes the timeline | — Pending |
| Core value = trustworthy classification > speed/UX polish | When something has to give, accuracy and audit-ability are protected | — Pending |
| Rule + AI hybrid for classification | AI-only is fragile under broader categories; existing `ClassificationRule` entity becomes load-bearing | — Pending |
| Payment provider deferred to research phase | Stripe vs Mollie vs SEPA-direct depends on real DE-market and cost analysis | — Pending |
| StBerG-safe positioning in copy + ToS | Non-negotiable legal posture for a non-licensed party operating in DE tax space | — Pending |
| Background-job upload pipeline | Synchronous request lifecycle does not survive Tesseract + Anthropic at hundreds-of-users scale | — Pending |

## Evolution

This document evolves at phase transitions and milestone boundaries.

**After each phase transition** (via `/gsd-transition`):
1. Requirements invalidated? → Move to Out of Scope with reason
2. Requirements validated? → Move to Validated with phase reference
3. New requirements emerged? → Add to Active
4. Decisions to log? → Add to Key Decisions
5. "What This Is" still accurate? → Update if drifted

**After each milestone** (via `/gsd-complete-milestone`):
1. Full review of all sections
2. Core Value check — still the right priority?
3. Audit Out of Scope — reasons still valid?
4. Update Context with current state

---
*Last updated: 2026-06-07 — Phase 7 (Test Depth + Launch QA) complete; QA-01..04/OBS-03 validated code-side (9/9 code-verifiable). Milestone v1.0 is code-complete. Launch gates tracked pending in `phases/07-test-depth-launch-qa/07-GO-NO-GO.md` (go/no-go PENDING) + `07-HUMAN-UAT.md`: CI heavy-suite green on main, lawyer sign-off (QA-07), legal placeholders + four AVVs, BetterStack monitors + Sentry quiet-hours (QA-06), mobile phone-camera UAT (QA-05), native-speaker DE review. All 7 phases complete; close milestone via /gsd-complete-milestone once launch gates are green.*
