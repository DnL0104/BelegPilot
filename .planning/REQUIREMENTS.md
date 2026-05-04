# Requirements: TaxReader

**Defined:** 2026-05-03
**Core Value:** Trustworthy classification — every line item correctly categorized into the right tax category, with reasoning the user can audit and override.

> **Brownfield note:** This document scopes the **hardening milestone** for commercial DE launch. Already-shipped capabilities are tracked in `.planning/PROJECT.md` under "Validated" and are not re-listed here. v1 below = the hardening milestone deliverables.

---

## v1 Requirements

### Foundation & Hygiene

- [ ] **FND-01**: Remove `storage/` directory + `build-diag.txt` from working tree; update `.gitignore` to prevent regression; verify no code path writes receipts to disk
- [ ] **FND-02**: Reconcile Anthropic model default between `AnthropicOptions.cs` and `docker-compose.yml`; document chosen default in `CLAUDE.md`
- [ ] **FND-03**: Lock CORS production policy — deny all origins when `CORS_ALLOWED_ORIGINS` is unset in non-Development environments
- [ ] **FND-04**: GitHub Actions CI workflow — `dotnet build`, `dotnet test`, `npm run lint`, `npm run build` as merge-blocking checks on every PR
- [ ] **FND-05**: Top-level `README.md` covering required tools, env-var setup (link `.env.example`), `docker compose up --build`, where to point a browser

### Observability

- [ ] **OBS-01**: Sentry installed for both .NET API and Next.js frontend with EU data residency; PII scrubbing in `BeforeSend`; conservative alert rules (no per-error pages, only sustained-rate or new-error-type with cooldown)
- [ ] **OBS-02**: Serilog enrichers (`Environment`, `CorrelationId`) configured; long-running handlers use `LogContext.PushProperty` for `ReceiptFileId` / `JobId` correlation
- [ ] **OBS-03**: BetterStack Uptime monitors on `/health` (DB ping) and `/api/v1/health` (DB + Anthropic config); status page linked from footer; deploy maintenance windows configurable

### Authentication & Rate Limiting

- [ ] **AUTH-01**: `refresh_tokens` table with hash-only storage, multi-row per user, rotation on refresh, replay detection that revokes all tokens on collision; `RefreshTokenService` replacing `user.RefreshToken` column logic
- [ ] **AUTH-02**: Account-deletion confirmation modal — re-authentication required + irreversibility warning before `DELETE /auth/account` fires
- [ ] **AUTH-03**: ASP.NET Core `AddRateLimiter` policies — fixed-window 5 req/min on `/auth/login` + `/auth/register` per IP, 30 req/min on `/auth/refresh` per user, concurrency-2 on `/receipt-files` per user, global 60 req/min per IP

### Pipeline & Reliability

- [ ] **PIPE-01**: Hangfire installed with Postgres storage; dashboard at `/hangfire` auth-gated to admin role; recurring cleanup jobs registered (expired refresh tokens, abandoned `Failed` jobs)
- [ ] **PIPE-02**: `ProcessReceiptFileJob` running the extract → parse → classify pipeline as a Hangfire background job; `POST /receipt-files` returns `202 Accepted` with jobIds; token pre-charge + per-item refund pattern preserved
- [ ] **PIPE-03**: `GET /receipt-files/{id}/status` for frontend polling; `POST /receipt-files/{id}/cancel` for explicit cancellation; status reflects (Queued, Extracting, Parsing, Classifying, Completed, Failed, Cancelled)
- [ ] **PIPE-04**: `TesseractEnginePool` (configurable size, default 3-5) using `Channel<TesseractEngine>`; replaces Singleton + lock pattern in `TesseractImageTextExtractor`
- [ ] **PIPE-05**: User-friendly German error messages on upload failure — known exception types mapped to safe strings; raw exceptions logged to Serilog only, never returned in HTTP body or persisted in `processing_runs.error_message`
- [ ] **PIPE-06**: Empty / loading / error states implemented across upload page, receipts list page, receipt detail page, dashboard, reports — no blank-screen-of-thinking states

### Classification Trustworthiness

- [ ] **CLASS-01**: `RuleBasedClassifier` (DB-backed) wired against existing `ClassificationRule` entity; matches on vendor name (substring), source-file regex, and item description regex; per-user scoped rules
- [ ] **CLASS-02**: `HybridClassificationService` composing rules-first-then-AI replaces `AiOnlyClassificationService` as the registered `IClassificationService`; rule matches recorded with `ClassificationMethod.Rule`
- [ ] **CLASS-03**: `Category` enum expanded to 13 values: WerbungskostenArbeitsmittel, WerbungskostenFachliteratur, WerbungskostenBueromaterial, WerbungskostenReisekosten, WerbungskostenFortbildung, WerbungskostenTelekommunikation, SonderausgabenSpenden, SonderausgabenVorsorgeaufwendungen, AussergewoehnlicheBelastungenKrankheit, HaushaltsnaheDienstleistung, Handwerkerleistung, Privat, Unbekannt — with EF migration and PDF-export updates
- [ ] **CLASS-04**: Per-classification reasoning surfaced prominently in receipt detail UI — visible without click-to-expand; "Warum wurde das so eingeordnet?" label; descriptive (not prescriptive — no "Sie können absetzen") wording
- [ ] **CLASS-05**: "Diese Regel speichern" button on classification override → creates a user-scoped `ClassificationRule` matched against the current item's vendor + description pattern
- [ ] **CLASS-06**: Sum-validation rule — line-item totals must sum to receipt total within €0.50 tolerance; mismatch flags receipt as `Unverified` and surfaces an audit prompt to the user
- [ ] **CLASS-07**: Auto-confirm threshold visible and user-settable in settings (default conservative — requires manual confirmation); applied uniformly across rule and AI matches

### Commercial Layer

- [ ] **PAY-01**: `StripePaymentProvider` + `POST /payments/checkout` endpoint creating Stripe Checkout sessions for token packs; `/webhooks/stripe` anonymous endpoint with signature verification; `payments` table with `(stripe_event_id UNIQUE)` for idempotency; `GrantTokensJob` enqueued from webhook
- [ ] **PAY-02**: DE-compliant Rechnung per purchase via Stripe Invoicing — vendor name, address, USt-ID where applicable, invoice number, line items, VAT line, sequential numbering; user can download from billing page
- [ ] **PAY-03**: Pre-checkout flow — Widerrufsbelehrung shown, active checkbox waiver ("Ich verlange ausdrücklich…") required before Stripe redirect; checkbox not pre-ticked; AGB acceptance also required
- [ ] **PAY-04**: Account → Billing page surfacing token balance, transaction history, invoice list with download links, embedded Stripe Customer Portal for payment-method management
- [ ] **PAY-05**: Refund / chargeback flow — Stripe `charge.refunded` webhook enqueues `RevokeTokensJob`; balance can go negative until reconciled; audit-logged with reason
- [ ] **PAY-06**: Multi-environment safety — separate `Stripe__SecretKey_Test` / `Stripe__SecretKey_Live` env vars; startup-time guard throws if Production runs with `sk_test_*`; loud warning if Development runs with `sk_live_*`

### Legal & Consent

- [ ] **LEG-01**: Impressum page (TMG §5) with name, address, contact email, USt-ID where applicable, ODR link; reachable from every page footer
- [ ] **LEG-02**: Datenschutzerklärung covering DSGVO Art. 13 (purposes, legal bases, data categories), Art. 22 (automated decision-making with human-override disclosure), Art. 28 (sub-processor list: Anthropic, Stripe, Sentry, BetterStack), Drittland-Übermittlung (Schrems II / TADPF for Anthropic with link to certification)
- [ ] **LEG-03**: AGB (BGB §305+) — StBerG-safe positioning ("Vertragsgegenstand ist Strukturierung, keine Steuerberatung"), GoBD non-applicability statement, Widerrufsrecht clause, refund policy, support response SLA, Streitbeilegung signpost (VSBG); lawyer-reviewed
- [ ] **LEG-04**: Dedicated `/widerruf` page with full Widerrufsbelehrung text and Muster-Widerrufsformular
- [ ] **LEG-05**: TTDSG-compliant cookie banner — "Alle akzeptieren" + "Nur notwendige" / "Ablehnen" equally prominent, no pre-ticked checkboxes, settings reachable from footer to revoke; essential auth cookies excluded; Sentry/analytics gated on consent
- [ ] **LEG-06**: AVVs/DPAs signed and on file — Anthropic, Stripe, Sentry, BetterStack — links from Datenschutzerklärung
- [ ] **LEG-07**: Self-serve data export (DSGVO Art. 20) — user triggers from settings; `ExportUserDataJob` produces JSON + CSV bundle of receipts + items + classifications + token transactions; download link emailed within 24h
- [ ] **LEG-08**: `audit_log` table + `AuditLogger` recording account deletions, payment grants, refresh-token revocations, classification-override-rule creations; user can request own audit log via DSGVO Art. 15 path
- [ ] **LEG-09**: DPMA + EUIPO Marken search for "TaxReader" in classes 9 + 42; if clear, optionally register; if conflicted, rename before launch

### Quality & Launch QA

- [ ] **QA-01**: PostgreSQL integration test project using `Testcontainers.PostgreSql` 4.x + `Respawn` 6.x — covers duplicate detection, cascade deletes, refresh-token rotation + replay, payment idempotency unique constraint, migration smoke test against populated DB
- [ ] **QA-02**: Vitest 3 unit + component tests — auth hooks (login, logout, JWT refresh shared-promise pattern), upload state machine, RHF + Zod form validation, classification-confirm/override flow
- [ ] **QA-03**: Playwright 1.50 E2E happy path — register → login → upload-receipt → see-classification → confirm → see-report → export, in DE locale, against the standalone Next.js server
- [ ] **QA-04**: German localization audit — every user-facing string in DE (`Sie`-form for tax product); EUR-formatting via `Intl.NumberFormat('de-DE', { style: 'currency', currency: 'EUR' })`; native-speaker review
- [ ] **QA-05**: Mobile-responsive QA pass at `sm` (640px) and `md` (768px) breakpoints — receipts list, upload page, classification-confirm flow, dashboard, reports; photo-receipt upload from phone tested end-to-end
- [ ] **QA-06**: Sentry alert rules tuned against real-traffic baseline (post-Phase-1 install); status-page deploy-maintenance windows configured; "quiet hours" 23:00-07:00 only HIGH-severity pages
- [ ] **QA-07**: Final pre-launch lawyer review of AGB + Datenschutzerklärung; "Looks done but isn't" checklist (PITFALLS.md) verified end-to-end

---

## v2 Requirements

Deferred to follow-up milestone. Not in current roadmap. Some emerge from research (`FEATURES.md`).

### Classification (Advanced)

- **CLASS-V2-01**: Bulk re-classify by rule UI — surfacing of existing rule engine for retroactive application
- **CLASS-V2-02**: Plain-language category descriptions matched to Anlage-line hints ("geht in Anlage N, Zeile 41")
- **CLASS-V2-03**: Auto-promotion of N-corrections-into-rule (after N consistent overrides, suggest a rule)
- **CLASS-V2-04**: Reclassification history per user (audit-trail UI for advisor + own confidence)

### Reporting (Advanced)

- **REP-V2-01**: Year-over-year trend by category in dashboard
- **REP-V2-02**: PDF export specifically formatted for handing to Steuerberater (logo-less, neutral, machine-readable section + summary)

### Mobile / PWA

- **MOB-V2-01**: PWA manifest + add-to-home-screen prompt
- **MOB-V2-02**: Mobile photo-capture optimization (perspective correction, lighting hints)

### Self-employed mode

- **SELF-V2-01**: Anlage S / EÜR support with appropriate categories
- **SELF-V2-02**: Vorsteuer-Anteil tracking and VAT-rate-aware totals

### Integrations

- **INT-V2-01**: DATEV / Lexware-compatible CSV export
- **INT-V2-02**: Mollie alongside Stripe for DE-only payment methods (Sofort, giropay)

---

## Out of Scope

Explicitly excluded. Documented to prevent scope creep. Aligns with `.planning/PROJECT.md` Out of Scope section.

| Feature | Reason |
|---------|--------|
| Native mobile apps (iOS/Android) | Solo dev cannot maintain three platforms; mobile web is sufficient |
| Multi-user accounts / orgs / teams / shared workspaces | Single user per account; couples filing jointly use one account |
| Bank account / DATEV / Lexware automated sync | PDF/image upload is the only ingest path for v1 |
| Tax advice / "what can I claim" recommendations | StBerG line — Helfer not Berater |
| Tax-advice chat / AI ask-me-anything | StBerG land-mine even if disclaimed |
| Full ELSTER ERiC submission | Requires official certification + audit; separate ~6-month product |
| Native ELSTER import-format export | `Mein ELSTER` does not accept structured CSV imports for the data we produce |
| Horizontal scaling / Kubernetes / managed cloud | Single Docker Compose is the deployment target for hundreds-of-users scale |
| Year-prior-to-2024 historical filing support | Focus on current + last tax year |
| AI-generated tax tips emails | StBerG + DSGVO marketing-consent risk |
| Real-time collaboration | Single-user product, no value |
| Public sharing of reports | PII risk |
| Free unlimited tier | Anthropic costs scale with usage; abuse risk; 10-token welcome credit is the trial |
| Auto-categorize Vorsteuer for self-employed | Vorsteuer rules non-trivial; v2 self-employed mode handles |

---

## Traceability

Which phases cover which requirements. Populated during roadmap creation.

| Requirement | Phase | Status |
|-------------|-------|--------|
| FND-01 | Phase 1 | Pending |
| FND-02 | Phase 1 | Pending |
| FND-03 | Phase 1 | Pending |
| FND-04 | Phase 1 | Pending |
| FND-05 | Phase 1 | Pending |
| OBS-01 | Phase 1 | Pending |
| OBS-02 | Phase 1 | Pending |
| OBS-03 | Phase 7 | Pending |
| AUTH-01 | Phase 2 | Pending |
| AUTH-02 | Phase 2 | Pending |
| AUTH-03 | Phase 2 | Pending |
| PIPE-01 | Phase 3 | Pending |
| PIPE-02 | Phase 3 | Pending |
| PIPE-03 | Phase 3 | Pending |
| PIPE-04 | Phase 3 | Pending |
| PIPE-05 | Phase 3 | Pending |
| PIPE-06 | Phase 3 | Pending |
| CLASS-01 | Phase 4 | Pending |
| CLASS-02 | Phase 4 | Pending |
| CLASS-03 | Phase 4 | Pending |
| CLASS-04 | Phase 4 | Pending |
| CLASS-05 | Phase 4 | Pending |
| CLASS-06 | Phase 4 | Pending |
| CLASS-07 | Phase 4 | Pending |
| PAY-01 | Phase 5 | Pending |
| PAY-02 | Phase 5 | Pending |
| PAY-03 | Phase 5 | Pending |
| PAY-04 | Phase 5 | Pending |
| PAY-05 | Phase 5 | Pending |
| PAY-06 | Phase 5 | Pending |
| LEG-01 | Phase 6 | Pending |
| LEG-02 | Phase 6 | Pending |
| LEG-03 | Phase 6 | Pending |
| LEG-04 | Phase 6 | Pending |
| LEG-05 | Phase 6 | Pending |
| LEG-06 | Phase 6 | Pending |
| LEG-07 | Phase 6 | Pending |
| LEG-08 | Phase 6 | Pending |
| LEG-09 | Phase 6 | Pending |
| QA-01 | Phase 7 | Pending |
| QA-02 | Phase 7 | Pending |
| QA-03 | Phase 7 | Pending |
| QA-04 | Phase 7 | Pending |
| QA-05 | Phase 7 | Pending |
| QA-06 | Phase 7 | Pending |
| QA-07 | Phase 7 | Pending |

**Coverage:**
- v1 requirements: 47 total
- Mapped to phases: 47
- Unmapped: 0 ✓

---
*Requirements defined: 2026-05-03*
*Last updated: 2026-05-03 after initial definition*
