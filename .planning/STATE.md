---
gsd_state_version: 1.0
milestone: v1.0
milestone_name: milestone
status: executing
stopped_at: Plan 01-03 complete (10/10 new tests pass, 113/113 total green; Frontend npm run build succeeds); ready for Wave 4 (Plan 01-02 — CI workflow + README)
last_updated: "2026-05-10T11:02:33Z"
last_activity: 2026-05-10 -- Completed 01-03-PLAN.md
progress:
  total_phases: 7
  completed_phases: 0
  total_plans: 4
  completed_plans: 3
  percent: 11
---

# Project State

## Project Reference

See: .planning/PROJECT.md (updated 2026-05-03)

**Core value:** Trustworthy classification — every line item correctly categorized into the right tax category, with reasoning the user can audit and override.
**Current focus:** Phase 1 — foundation-cleanup-ci

## Current Position

Phase: 1 (foundation-cleanup-ci) — EXECUTING
Plan: 4 of 4 (Wave 4 — 01-02 CI workflow + README)
Status: Plan 01-03 complete; ready for Wave 4
Last activity: 2026-05-10 -- Completed 01-03-PLAN.md

Progress: █░░░░░░░░░ 11%

### Wave map

- Wave 1: 01-01 (Hygiene + Anthropic alignment + CORS deny-all) — no deps — DONE
- Wave 2: 01-04 (Serilog enrichers + LogContext) — depends on 01-01 — DONE
- Wave 3: 01-03 (Sentry .NET + Next.js, EU residency, PII scrubbing) — depends on 01-01, 01-04 — DONE
- Wave 4: 01-02 (CI workflow + README) — depends on 01-01, 01-03, 01-04

## Performance Metrics

**Velocity:**

- Total plans completed: 3
- Average duration: 7 min
- Total execution time: 21 min

**By Phase:**

| Phase | Plans | Total  | Avg/Plan |
|-------|-------|--------|----------|
| 1     | 3/4   | 21 min | 7 min    |

**Recent Trend:**

- Last 5 plans: 01-03 (11 min, 2 tasks, 17 files), 01-04 (5 min, 2 tasks, 6 files), 01-01 (5 min, 3 tasks, 11 files)
- Trend: 01-03 took ~2x longer because it spans both stacks (.NET package install + scrubber + 7 tests + Next.js install of 148 packages + 4 frontend TS files + conditional withSentryConfig wrap). Two Rule 3 auto-fixes (Sentry SDK type rename `Request` → `SentryRequest`; `IDictionary<>` cast for `Extra` mutation) added one extra build round-trip.

*Updated after each plan completion*

## Accumulated Context

### Decisions

Decisions are logged in PROJECT.md Key Decisions table.

Recent decisions affecting current work:

- Init: Audience broadened from teachers to "Anyone DE"
- Init: Output bar = PDF/CSV summary for manual transcription (no ELSTER/ERiC)
- Init: Stripe selected as payment provider (deferred research)
- Init: Hangfire chosen over Channel<T> for background-job pipeline
- Init: Rule + AI hybrid classification (load-bearing for Core Value)
- 01-01: AnthropicOptions.cs is the source of truth for the model default; compose + env + CLAUDE.md mirror it; startup-log canary surfaces drift
- 01-01: CORS non-Dev + unset CORS_ALLOWED_ORIGINS = empty Origins (deny-all) + Serilog warning
- 01-01: Backend/.dockerignore added to prevent leaked PDFs from re-entering the runtime image via COPY . .
- 01-01: WebApplicationFactory<Program> integration-test pattern established (test project now references TaxReader.Api)
- 01-04: Serilog enrichers wired via appsettings.json (FromLogContext + WithEnvironmentName); appsettings.Development.json deliberately unchanged (array-merge avoidance)
- 01-04: LogContext.PushProperty correlation scope established at long-running-handler boundary (UploadReceiptFilesHandler per-file block); JobId variant deferred to Phase 3 Hangfire boundary per D-18
- 01-04: Serilog.Enrichers.Environment owned by TaxReader.Api project; test project receives it transitively via existing ProjectReference
- 01-04: Source-level structural-grep test pattern (File.ReadAllText + Should().Contain) added as a load-bearing wiring guard for cross-cutting invariants no runtime test can express cleanly
- 01-03: Sentry SDK init is the FIRST builder.WebHost registration after CreateBuilder (Pitfall 1 — DI-time exceptions reach Sentry); SetBeforeSend (not deprecated BeforeSend) wired to SentryScrubbing.Scrub
- 01-03: PII scrubber (D-14) lives at Backend/src/TaxReader.Infrastructure/Observability/SentryScrubbing.cs (not Api/) — matches "Infrastructure implements external concerns" architectural rule + OcrTextNormalizer.cs analog; cost is one PackageReference + one FrameworkReference on the Infrastructure csproj
- 01-03: AllowedExtraKeys allow-list ({receipt_id, processing_run_id, request_id, job_id, phase}) actively wipes Extra keys not in the set — defence-in-depth so future Sentry.SetExtra("vendor", ...) cannot leak receipt content (D-14 #6 active enforcement, not just call-site contract)
- 01-03: Frontend Sentry stays OFF in Phase 1 (D-16) — instrumentation-client.ts (NOT deprecated sentry.client.config.ts) gates Sentry.init on NEXT_PUBLIC_SENTRY_ENABLED === "true"; Phase 6 LEG-05 cookie banner flips the flag
- 01-03: Conditional withSentryConfig in next.config.ts (Pitfall 6) — production builds work without SENTRY_ORG/SENTRY_PROJECT in Phase 1 CI because the wrap is skipped when the flag is off

### Pending Todos

None yet.

### Blockers/Concerns

None yet.

## Deferred Items

| Category | Item | Status | Deferred At |
|----------|------|--------|-------------|
| *(none)* | | | |

## Session Continuity

Last session: 2026-05-10
Stopped at: Plan 01-03 complete (10/10 new tests pass, 113/113 total green; Frontend npm run build succeeds); ready for Wave 4 (Plan 01-02 — CI workflow + README)
Resume file: .planning/phases/01-foundation-cleanup-ci/01-02-PLAN.md (Wave 4 — GitHub Actions CI workflow + top-level README)
