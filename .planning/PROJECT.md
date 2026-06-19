# TaxReader

## What This Is

TaxReader is a web application that helps German private taxpayers turn a pile of receipt PDFs and images into a clean per-category-per-year expense summary they can transcribe into ELSTER or hand to their Steuerberater. The core pipeline already works (text extraction → format-specific parsing → AI classification → German-localized PDF/CSV report). This milestone hardens that existing build to a commercial DE launch standard — payments, operational visibility, data durability, legal compliance, a full UI redesign, and proven classification trust.

## Core Value

Trustworthy classification — every line item correctly categorized into the right tax category, with reasoning the user can audit and override. If accuracy fails, the report is worthless.

## Business Context

- **Customer**: German private taxpayers preparing their own returns or prepping receipts for a Steuerberater.
- **Revenue model**: Token economy — users buy credits (via Stripe) that are consumed per AI classification. 10 free welcome tokens on signup.
- **Success metric**: Paying users in the first 6 months (target 100–500).
- **Strategy notes**: Commercial DE launch positioned as a "Helfer, not Berater" tool (StBerG-safe).

## Requirements

### Validated

<!-- Inferred from existing code / .planning/codebase/ — shipped and relied upon. -->

- ✓ Upload receipt PDFs and images — existing
- ✓ Text extraction via PdfPig (PDF) and Tesseract OCR (images, deu+eng) — existing
- ✓ Format-specific parsing: Amazon → Eduki → Generic fallback (priority order) — existing
- ✓ AI classification into 13 DE tax categories via Anthropic (`claude-haiku-4-5` default) — existing
- ✓ Historical (append-only) classification with manual confirm/override + reclassify — existing
- ✓ Per-category-per-year aggregation + German-localized PDF/CSV export — existing
- ✓ JWT auth (register/login/refresh with rotation), BCrypt hashing, cascading account deletion — existing
- ✓ Token ledger + Stripe webhook → token grant on purchase — existing
- ✓ Background processing via Hangfire (OCR/parse/classify jobs) — existing
- ✓ Audit logging of user actions — existing
- ✓ Self-hosted Docker Compose stack (Postgres, API, web, Caddy edge with TLS) — existing

### Active

<!-- The six launch-gating workstreams for this milestone. All are hard gates: quality is the gate, the tax-season date is a target. -->

- [ ] **Payment top-up flow** — wire the real Stripe checkout so users can actually buy token credits from the UI (the `/tokens/purchase` endpoint is currently a placeholder; webhook→grant already works).
- [ ] **Error alerting / operational visibility** — populate Sentry DSN (backend + frontend), bound Sentry request body size, and surface Hangfire job failures as paging-style alerts so a solo operator is never blind in production.
- [ ] **Backups & disaster recovery** — automated PostgreSQL backups, a tested restore procedure, and documented RTO/RPO so DB loss doesn't mean total data loss or inability to honor GDPR deletion.
- [ ] **GDPR / StBerG legal compliance** — Anthropic AVV in place, "Helfer, not Berater" positioning enforced in copy, verified data export & deletion correctness, audit-log retention policy, privacy/legal copy in German.
- [ ] **Full UI redesign** — complete visual + UX rework across all screens (not a polish pass): fix confusing flows, raise accessibility to standard, eliminate inconsistency, and reach a look that earns paying German customers' trust. Built using the UI Skills.
- [ ] **Classification trust hardening** — prove and protect the Core Value: an evaluation/accuracy approach, replace the silent `Category.Unbekannt` fallback with clear error surfacing (parse-failure vs. genuine "unknown"), and a solid override UX.

### Out of Scope

- Tech-stack rewrites — stack is locked (.NET 10 / EF Core / PostgreSQL / Next.js 16 / shadcn/ui / Anthropic / Tesseract) for this milestone.
- Migration to managed cloud — hosting stays self-hosted Docker Compose with Caddy edge this milestone.
- Scaling beyond 100–500 users — design for that target, not thousands (read replicas, sharding, Redis Hangfire deferred).
- Mobile/native app — web-first.
- Multi-user / team / Steuerberater portal accounts — single authenticated-user role only.
- Replacing the classification model or adding a second AI provider — Anthropic Haiku stays the production default.

## Context

- Brownfield: the app is well past prototype. The codebase is already mapped under `.planning/codebase/` (ARCHITECTURE, STACK, CONVENTIONS, INTEGRATIONS, STRUCTURE, TESTING, CONCERNS) as of 2026-06-19.
- `.planning/codebase/CONCERNS.md` is effectively the hardening backlog — it enumerates the payment gap, missing Sentry DSN, silent Hangfire failures, absent backups, GDPR test gaps, the silent `Unbekannt` fallback, and more. The six Active workstreams trace directly to it.
- Architecture: Clean-ish layering (Domain → Application → Infrastructure → API), `Result<T>` for control flow, CQRS-style handlers (no MediatR), EF Core direct (no repository pattern), hand-written DTO mapping (no AutoMapper).
- Operations: solo developer with a paging-style alerting expectation. No on-call rotation or support team — automation must compensate.
- Localization: all end-user UI and copy must be German.

## Constraints

- **Timeline**: Target the tax-season peak (~July 2026; commercial launch within ~3 months of 2026-05-02). Decided this milestone: the six gates take precedence over the date — launch may slip past the July peak rather than ship incomplete.
- **Tech stack**: Locked — .NET 10 / EF Core / PostgreSQL 17 / Next.js 16 / shadcn/ui / Anthropic / Tesseract. No rewrites.
- **Operations**: Solo developer, paging-style alerting expectation, no support team — automation must compensate.
- **Scale**: 100–500 paying users in the first 6 months. Design for that, not thousands.
- **Compliance**: GDPR mandatory; StBerG "Helfer, not Berater" positioning mandatory; GoBD where applicable; Anthropic AVV required for processing personal data.
- **Localization**: All end-user UI and copy in German.
- **Hosting**: Self-hosted Docker Compose with Caddy edge. No managed-cloud migration this milestone.
- **Budget**: Pre-revenue solo product. AI inference cost is pass-through via the token economy; other tooling (Sentry, payment fees, monitoring) bounded by what the product can absorb.

## Key Decisions

| Decision | Rationale | Outcome |
|----------|-----------|---------|
| All six workstreams are hard launch gates | Won't take a paying customer with payments, alerting, backups, legal, UI, or classification trust unproven | — Pending |
| Quality is the gate; tax-season date is a target | Solo dev + six tracks is tight; better to slip past July peak than launch incomplete | — Pending |
| UI work is a full redesign, not a polish pass | Current UI is confusing, inconsistent, has a11y gaps, and doesn't look trustworthy for paying DE customers | — Pending |
| Use the UI Skills for the redesign | baseline-ui / fixing-accessibility / fixing-metadata / fixing-motion-performance via ui-skills-root | — Pending |
| Classification accuracy gets a dedicated workstream | It's the Core Value and is not yet proven trustworthy for launch | — Pending |
| Replace silent `Unbekannt` fallback with explicit error surfacing | Silent fallback erodes trust when AI parse/quota fails | — Pending |

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
*Last updated: 2026-06-19 after initialization*
