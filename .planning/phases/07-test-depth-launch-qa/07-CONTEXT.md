# Phase 7: Test Depth + Launch QA - Context

**Gathered:** 2026-06-05
**Status:** Ready for planning

<domain>
## Phase Boundary

Final quality gates and ops readiness before commercial launch. Postgres integration tests (Testcontainers + Respawn) catch schema/constraint bugs the in-memory provider hides; Vitest + Playwright cover the frontend (which has zero tests today); BetterStack monitors go live; every user-facing string is German (`Sie`-form, EUR-formatted); mobile receipt-upload + classification-confirm flows work at `sm`/`md`; and the lawyer's final sign-off + a go/no-go decision close the milestone.

Scope is fixed by ROADMAP Phase 7 (QA-01..07, OBS-03) and the 5 sketched plans (07-01..07-05). Discussion below clarifies HOW within that boundary — it does NOT add new product capabilities.
</domain>

<decisions>
## Implementation Decisions

### Test Coverage Scope
- **D-01:** Backend test scope = the QA-01/02/03 named critical paths **plus** a high-risk backfill of the currently-untested money/security services that have zero tests today: `AuthService` (register/login/refresh/BCrypt verify/refresh-token rotation+replay), `AiOnlyClassificationService` (token pre-charge, refund-on-Unknown, refund-on-failure, auto-confirm threshold), and `TokenService` (atomic ledger operations). Rationale: these directly guard Core Value (trustworthy classification) and revenue, and are the highest-risk untested surface per `codebase/TESTING.md`.
- **D-02:** Explicitly OUT of scope this phase (protect launch timeline — comprehensive backfill rejected): `PdfPigTextExtractor` bounding-box algorithm, `TesseractImageTextExtractor` pool/locking, `PdfExportService`/`CsvExportService` formatting, `ClaudeAiClassifier` HTTP/JSON-parsing. Captured as deferred.

### CI Execution Model
- **D-03:** Keep the three existing lightweight jobs (`hygiene-check`, `backend-build-test`, `frontend-lint-build`) running on every PR. Add the slow suites — Postgres Testcontainers integration (QA-01) + Playwright E2E (QA-03) — as a **separate "heavy" CI job/workflow** that runs on push-to-`main`, with an optional PR label (e.g. `run-heavy`) to trigger on demand. Rationale: keep PR feedback fast; spin up Docker/browsers only when needed.
- **D-04:** Vitest unit/component tests (QA-02) are fast → run on **every PR** (fold into the frontend job or a sibling fast job), NOT the heavy job.

### Launch Go/No-Go Gate
- **D-05:** HARD launch blockers — must be green before "go":
  1. All automated suites green in CI: QA-01 Postgres integration + QA-02 Vitest + QA-03 Playwright E2E.
  2. Final lawyer sign-off on AGB + Datenschutzerklärung (QA-07); draft markers removed.
  3. Phase 6 operator items closed: real Impressum/legal contact data filled (the 06-07 CI placeholder guard goes green) **and** all four AVVs/DPAs signed (Anthropic, Stripe, Sentry, BetterStack).
- **D-06:** Tracked but NON-blocking (surface in the go/no-go report; do NOT gate launch): native-speaker DE polish review beyond the automated guard, and prior-phase manual UAT debt (Phases 2/3/4 HUMAN-UAT items).

### German Localization Audit
- **D-07:** Enforce DE localization (QA-04) with BOTH layers: (a) an **automated CI guard** extending the 06-07 `hygiene-check` bash pattern — flag likely-English user-facing strings and assert money is rendered via `Intl.NumberFormat('de-DE', { style: 'currency', currency: 'EUR' })`; (b) a **one-time native-speaker review pass** at launch time. The automated guard is the regression-proofing; the manual pass is launch polish (and is the non-blocking item per D-06).

### Monitoring & Alerting (within OBS-03 / QA-06)
- **D-08:** BetterStack monitors per OBS-03: `/health` (DB ping) + `/api/v1/health` (DB + Anthropic config); status page linked from the footer; deploy-maintenance windows configurable. Sentry alert "quiet hours" 23:00–07:00 = HIGH-severity pages only. Exact alert-delivery channel for solo-dev paging (email + push default) left to research/planning.

### Claude's Discretion
- Exact Testcontainers/Respawn wiring, Playwright project config, Vitest setup, and test-file organization.
- **PITFALLS.md authoring:** QA-07 references a "Looks done but isn't" checklist at `PITFALLS.md`, but **no such file exists yet** — create it during this phase (likely in 07-05) as the canonical pre-launch verification checklist.
</decisions>

<specifics>
## Specific Ideas

- Reuse the established `WebApplicationFactory<Program>` + `[Collection(DisableParallelization = true)]` integration-test pattern from Phase 2 (`RateLimiterTestCollection`) for the QA-01 Postgres integration project — top-level `Program.cs` statements break under parallel WAF runs.
- The 06-07 legal-placeholder CI guard is the template for the D-07 localization guard: `shell: bash` + `set -e` + grep-inside-`if` + `exit 1`, as a step in the existing `hygiene-check` job (no new tooling).
- Playwright happy path is explicitly: register → login → upload-receipt → see-classification → confirm → see-report → export, in DE locale, against the standalone Next.js server.
</specifics>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Phase scope & requirements
- `.planning/ROADMAP.md` — Phase 7 section: goal, 9 success criteria, and the 5 sketched plans (07-01..07-05)
- `.planning/REQUIREMENTS.md` — QA-01..QA-07 + OBS-03 definitions with pinned tool versions (Testcontainers.PostgreSql 4.x, Respawn 6.x, Vitest 3, Playwright 1.50)

### Current test state
- `.planning/codebase/TESTING.md` — current coverage map + "Not covered" list. **Stale (2026-04-29):** predates Phase 1 CI and the Phase 2/6 `WebApplicationFactory` integration tests — verify against the live test project before trusting the "no integration tests / no CI" claims.
- `.planning/codebase/CONCERNS.md` — the testing-gap concern this phase closes

### Launch-gate dependencies (from Phase 6)
- `.planning/phases/06-legal-consent-data-export/06-HUMAN-UAT.md` — operator/lawyer/UI items; several become D-05 hard blockers
- `.planning/phases/06-legal-consent-data-export/06-LEGAL-REVIEW.md` — lawyer-review gate doc (QA-07)
- `.planning/phases/06-legal-consent-data-export/06-AVV-TRACKING.md` — AVV/DPA signing tracker (D-05 #3)

### CI / guards
- `.github/workflows/ci.yml` — existing 3-job CI + the 06-07 `hygiene-check` bash-guard pattern to extend (D-03, D-07)

### To be created this phase
- `PITFALLS.md` — does NOT exist yet; QA-07 depends on it. Author it as the "Looks done but isn't" checklist (Claude's Discretion above).
</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `Backend/tests/TaxReader.UnitTests/Helpers/TestDataFactory.cs` — static factory (CreateReceiptFile/Receipt/ReceiptItem/Classification/Rule) for seeding; reuse for integration-test fixtures.
- Phase 2 `WebApplicationFactory<Program>` + `RateLimiterTestCollection` serialization pattern — the integration-test harness blueprint for QA-01.
- 06-07 `hygiene-check` bash-guard step — the template for the D-07 localization guard.
- Sentry already wired (Phase 1, `SentryScrubbing.cs`) + consent-gated (Phase 6) — QA-06 only tunes alert rules, no new install.

### Established Patterns
- Test naming `Method_Scenario_Result`; xUnit constructor-as-setup, fresh in-memory DB per test, `IDisposable` cleanup.
- DE `Sie`-form localization across all user-facing copy; German error messages in services.

### Integration Points
- `/health` and `/api/v1/health` endpoints (OBS-03) — confirm they exist / their probe depth before BetterStack wiring (research item).
- Heavy CI job needs Docker-in-CI (Testcontainers) + Playwright browser install — new CI infrastructure on top of the existing workflow.
</code_context>

<deferred>
## Deferred Ideas

- Comprehensive backend test backfill (PdfPigTextExtractor, TesseractImageTextExtractor, PdfExport/CsvExport, ClaudeAiClassifier) — future hardening phase / backlog (per D-02).
- Treating native-speaker DE review and prior-phase (P2/3/4) manual UAT as hard launch gates — kept non-blocking this milestone (per D-06).

</deferred>

---

*Phase: 07-test-depth-launch-qa*
*Context gathered: 2026-06-05*
