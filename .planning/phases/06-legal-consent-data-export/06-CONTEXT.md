# Phase 6: Legal + Consent + Data Export + AVVs - Context

**Gathered:** 2026-06-01
**Status:** Ready for planning

<domain>
## Phase Boundary

Launch-ready DE legal posture for commercial launch. Delivers: the four mandated legal pages (Impressum / Datenschutzerklärung / AGB / Widerrufsbelehrung) reachable from a site-wide footer, a TTDSG-compliant cookie consent banner that gates Sentry per-user, signed AVVs/DPAs with all four sub-processors (Anthropic, Stripe, Sentry, BetterStack), DSGVO Art. 20 self-serve data export, an append-only `audit_log` for sensitive operations (with Art. 15 self-service), and DPMA + EUIPO Marken clearance for "TaxReader".

In scope: LEG-01 through LEG-09 (see REQUIREMENTS.md).

Out of scope (other phases own these):
- BetterStack uptime monitors + status page — Phase 7 (OBS-03, QA-06)
- Final pre-launch lawyer sign-off of AGB + Datenschutz — Phase 7 (QA-07); this phase produces the drafts + tracking gate
- German localization audit of the whole app — Phase 7 (QA-04)
- Email/SMTP infrastructure — not built this milestone (see D-09)

</domain>

<decisions>
## Implementation Decisions

### Legal Content (LEG-01, LEG-02, LEG-03, LEG-04)
- **D-01:** I author full German **draft** copy for all four pages, grounded in already-locked facts: Kleinunternehmer §19 UStG → **no USt-IdNr.** in Impressum; sub-processor list = Anthropic, Stripe, Sentry, BetterStack; AGB carries StBerG-safe positioning ("Vertragsgegenstand ist Strukturierung, keine Steuerberatung") + GoBD non-applicability statement + Widerrufsrecht clause + refund policy + VSBG Streitbeilegung signpost; Widerruf page reproduces the §356 BGB text + Muster-Widerrufsformular. Every page renders a build-visible **"⚠ Entwurf – anwaltliche Prüfung ausstehend"** marker. Operator supplies real name/address/contact-email placeholders.
- **D-02:** Lawyer-review gate tracked via a `06-LEGAL-REVIEW.md` checklist (one row per page: Drafted → Lawyer-reviewed → Live) **plus a blocking HUMAN-UAT item**. Final sign-off happens in Phase 7 (QA-07); this phase must not silently ship unreviewed text as "final".
- **D-03:** `/agb` and `/widerruf` routes are created inside the existing `(legal)` route group, joining the existing `impressum/` and `datenschutz/` placeholder pages and reusing `(legal)/layout.tsx`. The existing placeholder content is replaced with the D-01 drafts.
- **D-04:** A new site-wide **Footer component** (none exists today) links Impressum, Datenschutzerklärung, AGB, Widerrufsbelehrung, and "Cookie-Einstellungen" from every page. This satisfies the LEG-01 "reachable from every footer" criterion.

### Cookie Consent (LEG-05)
- **D-05:** Custom **lightweight banner** — a `ConsentProvider` React context backed by `localStorage`; no CMP library/dependency. Justified: only one non-essential category exists (Sentry).
- **D-06:** **Two categories only** — "Notwendig" (always on; auth/session/security cookies; no toggle) and "Fehleranalyse" (Sentry; opt-in, **not** pre-ticked). Banner buttons "Alle akzeptieren" / "Nur notwendige" rendered with **equal prominence** (TTDSG); "Einstellungen" opens granular control.
- **D-07:** Sentry control — `NEXT_PUBLIC_SENTRY_ENABLED` remains the deploy-level master kill-switch. In `instrumentation-client.ts`, `Sentry.init` runs only when env-enabled **AND** runtime consent for "Fehleranalyse" is granted; `Sentry.close()` fires on revoke. **No page reload** in the consent flow.
- **D-08:** Consent revoke is reachable from the footer "Cookie-Einstellungen" link, which reopens the consent settings panel. Essential auth cookies are excluded from all toggles.

### Data Export (LEG-07)
- **D-09:** **Async Hangfire `ExportUserDataJob` + in-app download** in settings. **No email/SMTP infrastructure is introduced.** ⚠ **Deliberate deviation from the literal LEG-07 wording ("download link emailed within 24h")** — delivery is in-app (status → "Bereit – Herunterladen") rather than emailed. This satisfies the DSGVO Art. 20 intent without a new external dependency/sub-processor. The planner and verifier MUST treat in-app delivery as acceptance-satisfying for LEG-07.
- **D-10:** Generated bundle stored **transiently** (e.g. `/tmp/exports/{token}.zip`), served via an expiring, ownership-validated one-time token, and **purged after 24h by a Hangfire cleanup job**. This honors FND-01's no-persistent-storage hygiene ("PDFs no longer kept on disk").
- **D-11:** Bundle = **JSON + CSV, zipped**: receipts, items, classifications, token_transactions, the user's own `audit_log` entries (per D-15), plus a `README.txt` explaining contents. Excludes password hash and internal noise.
- **D-12:** The existing `(authenticated)/settings/page.tsx` gains a "Meine Daten exportieren" trigger with status states (Wird erstellt… → Bereit [Herunterladen]).

### Audit Log (LEG-08)
- **D-13:** **Explicit `IAuditLogger`** — interface in Application, implementation in Infrastructure, writing append-only `audit_log` rows. Invoked at each sensitive op: `DeleteAccountHandler` (account deletion), `GrantTokensJob` (payment grant), `RevokeTokensJob` (refund/revoke), `RefreshTokenService` (revoke-all on replay), and the override-rule creation handler (CLASS-05 "Diese Regel speichern"). Chosen over an EF SaveChanges interceptor so each event carries business meaning + context.
- **D-14:** Schema: `id` uuid PK, `action` (enum/text), `actor_user_id` uuid?, `subject_user_id` uuid?, `metadata` jsonb, `created_at` timestamptz. **Append-only** — no Update/Delete path in code; **retained indefinitely**; actor reference survives user account deletion (nullable / anonymized) for accountability.
- **D-15:** DSGVO **Art. 15 self-service is satisfied by folding the user's own audit entries into the LEG-07 export bundle** (`audit_log.json/.csv`). No separate audit-log endpoint or UI panel this phase.

### AVVs/DPAs + Marken — operator-tracked (LEG-06, LEG-09)
- **D-16:** AVV/DPA sign-off (Anthropic, Stripe, Sentry, BetterStack) tracked via a `06-AVV-TRACKING.md` checklist; the Datenschutzerklärung links each sub-processor's public DPA/AVV and includes the Drittland-Übermittlung note (Schrems II / TADPF, esp. Anthropic). The actual signing/filing is an operator HUMAN-UAT task.
- **D-17:** DPMA + EUIPO Marken search for "TaxReader" (Nizza classes 9 + 42) documented in `06-MARKEN-SEARCH.md`. The operator performs the actual register lookups (the executor cannot reliably query DPMA/EUIPO registers). Result recorded as cleared / conflicted / registered; if conflicted, a rename decision is forced before launch.

### Claude's Discretion (within CLAUDE.md conventions)
- Exact shadcn components for the banner and settings export panel (follow base-nova patterns).
- Exact German microcopy wording within the agreed page/section structure.
- Zip/compression approach for the export bundle (`System.IO.Compression`).
- `AuditAction` enum value naming.
- Whether the consent settings panel is a dialog or a footer-anchored route.

</decisions>

<specifics>
## Specific Ideas

- **Draft markers must be unmissable but environment-aware:** the "⚠ Entwurf – anwaltliche Prüfung ausstehend" marker is for pre-launch; removing it is gated by the `06-LEGAL-REVIEW.md` "Live" column.
- **Widerruf statutory text is locked from Phase 5:** "Ich verlange ausdrücklich, dass mit der Ausführung des Vertrags sofort begonnen wird. Mir ist bekannt, dass ich hierdurch mein Widerrufsrecht verliere." (§356 Abs. 4 BGB) — the `/widerruf` page is the canonical home of the full Widerrufsbelehrung the Phase-5 checkbox links to.
- **AGB checkbox link target** `/agb` and Widerruf link `/widerruf` (placeholders in Phase 5) must resolve to real pages after this phase.
- **Kleinunternehmer invoice/legal note:** "Gemäß §19 UStG wird keine Umsatzsteuer berechnet." — already used on invoices (Phase 5); Impressum/AGB must be consistent (no USt-IdNr.).

</specifics>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Project context
- `.planning/REQUIREMENTS.md` — LEG-01 through LEG-09 full text with acceptance criteria; note the LEG-07 wording vs D-09 deviation.
- `.planning/ROADMAP.md` — Phase 6 entry: 8 success criteria + 5 plan stubs (06-01 through 06-05).
- `.planning/PROJECT.md` — GDPR/StBerG/GoBD constraints; sub-processor + AVV requirement; solo-dev ops reality.

### Prior-phase decisions that constrain this phase
- `.planning/phases/05-commercial-surface-payments/05-CONTEXT.md` — D-05 (Widerruf §356 text + AGB checkbox), D-07 (Kleinunternehmer §19, no USt-IdNr.), `/agb` + `/widerruf` placeholders to make real, payments table audited by LEG-08.
- `.planning/phases/01-foundation-cleanup-ci/01-CONTEXT.md` — Sentry frontend gated on `NEXT_PUBLIC_SENTRY_ENABLED` in `instrumentation-client.ts`; "Phase 6 LEG-05 cookie banner flips the flag" (see D-07 here for the runtime-consent reconciliation). FND-01 no-persistent-storage hygiene (constrains D-10).
- `.planning/phases/03-background-pipeline-tesseract-pool/03-CONTEXT.md` — Hangfire `IBackgroundJobClient.Enqueue` fire-and-forget pattern + recurring cleanup job pattern (reused by `ExportUserDataJob` + its 24h purge job).

### Codebase intel
- `.planning/codebase/ARCHITECTURE.md` — layer rules (Application defines `IAuditLogger`; Infrastructure implements); anonymous endpoint pattern.
- `.planning/codebase/CONVENTIONS.md` — primary-constructor DI, `Result<T>`, structured logging, German `Sie`-form, EF snake_case + `IEntityTypeConfiguration<T>`.

### Existing files this phase will touch (read before editing)

#### Frontend — New
- `Frontend/src/app/(legal)/agb/page.tsx`, `Frontend/src/app/(legal)/widerruf/page.tsx` — NEW legal pages (D-03)
- `Frontend/src/components/layout/footer.tsx` (or similar) — NEW site-wide footer (D-04)
- `Frontend/src/providers/consent-provider.tsx` + `Frontend/src/components/consent/cookie-banner.tsx` — NEW consent context + banner (D-05..D-08)

#### Frontend — Modified
- `Frontend/src/app/(legal)/impressum/page.tsx`, `.../datenschutz/page.tsx` — replace placeholders with D-01 drafts; datenschutz adds sub-processor table + AVV links + Drittland note (D-16)
- `Frontend/instrumentation-client.ts` — add runtime-consent gate around `Sentry.init` (D-07)
- `Frontend/src/app/(authenticated)/settings/page.tsx` — add data-export trigger + status (D-12)
- Root/authenticated layout(s) — mount footer + cookie banner

#### Backend — New
- `Backend/src/TaxReader.Domain/Entities/AuditLogEntry.cs` + `Enums/AuditAction.cs` — NEW (D-13, D-14)
- `Backend/src/TaxReader.Application/Interfaces/IAuditLogger.cs` — NEW interface (D-13)
- `Backend/src/TaxReader.Infrastructure/Services/AuditLogger.cs` — NEW impl
- `Backend/src/TaxReader.Application/Jobs/ExportUserDataJob.cs` (+ export cleanup job) — NEW (D-09, D-10)
- `Backend/src/TaxReader.Api/Endpoints/` — export trigger + token-validated download endpoint
- `Backend/src/TaxReader.Infrastructure/Migrations/` — NEW migrations: AddAuditLog, (export needs no table if transient)

#### Backend — Modified (audit call sites — D-13)
- `Backend/src/TaxReader.Application/Commands/DeleteAccountHandler.cs`
- `Backend/src/TaxReader.Application/Jobs/GrantTokensJob.cs`, `RevokeTokensJob.cs`
- `Backend/src/TaxReader.Infrastructure/Services/RefreshTokenService.cs`
- The CLASS-05 override-rule creation handler

### Operator-tracked artifacts (this phase creates the docs; operator completes the action)
- `.planning/phases/06-legal-consent-data-export/06-LEGAL-REVIEW.md` — lawyer-review gate (D-02)
- `.planning/phases/06-legal-consent-data-export/06-AVV-TRACKING.md` — AVV/DPA sign-off checklist (D-16)
- `.planning/phases/06-legal-consent-data-export/06-MARKEN-SEARCH.md` — DPMA/EUIPO search record (D-17)

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- **`(legal)` route group + `layout.tsx`** already exist with placeholder Impressum + Datenschutz pages — extend, don't recreate.
- **Hangfire `IBackgroundJobClient` + recurring-job pattern** (Phase 3) — `ExportUserDataJob` and the 24h export-purge job follow the existing fire-and-forget / recurring patterns.
- **`(authenticated)/settings/page.tsx`** — existing home for the data-export trigger.
- **`Result<T>` + `ICurrentUser`** — export + audit handlers follow the established per-user-scoped handler pattern.
- **EF `IEntityTypeConfiguration<T>` + snake_case** — `audit_log` configuration follows existing per-entity config convention.
- **Sentry gate in `instrumentation-client.ts`** (Phase 1) — the consent runtime gate wraps the existing env-var check rather than replacing it.

### Established Patterns
- Application defines interfaces (`IAuditLogger`), Infrastructure implements — same as `IClassificationService`, `IPaymentProvider`.
- Anonymous vs authorized endpoints via `.AllowAnonymous()` opt-out of the global `RequireAuthorization()`.
- German `Sie`-form user-facing copy; structured logging with named placeholders (never interpolation).

### Integration Points
- **Cookie banner ↔ Sentry:** consent state (localStorage) read by `instrumentation-client.ts` and the consent provider; init/close on grant/revoke.
- **Export job ↔ audit log:** the export bundle reads the user's own `audit_log` rows (D-15), so audit logging must exist before/with export.
- **Audit log ↔ existing handlers:** five existing sensitive-op sites gain `IAuditLogger.RecordAsync` calls (D-13).
- **Datenschutz ↔ sub-processors:** the page enumerates Anthropic/Stripe/Sentry/BetterStack with AVV links + Drittland note (D-16), so the AVV tracking and the page content are coupled.

</code_context>

<deferred>
## Deferred Ideas

- **Email/SMTP infrastructure** — not built; data export is in-app (D-09). If a future need arises (verification emails, emailed export), it's a separate decision + a new sub-processor disclosure.
- **Third "Statistik"/analytics consent category** — not added until real analytics exist; disclosing an unused category is questionable under TTDSG.
- **Dedicated user-facing audit-log view/endpoint** — Art. 15 is met via the export bundle (D-15); a discoverable in-app audit view is a possible later enhancement.
- **Final pre-launch lawyer sign-off** — Phase 7 (QA-07). This phase produces drafts + the review gate only.
- **BetterStack uptime monitors + footer status-page link** — Phase 7 (OBS-03, QA-06); BetterStack is disclosed as a sub-processor here regardless.
- **Automatic Markenregister API integration** — out of scope; the search is a manual operator task documented in `06-MARKEN-SEARCH.md`.

</deferred>

---

*Phase: 06-legal-consent-data-export*
*Context gathered: 2026-06-01*
