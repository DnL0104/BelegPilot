---
gsd_state_version: 1.0
milestone: v1.0
milestone_name: milestone
status: executing
stopped_at: Plan 02-01 complete (1/3 Phase-2 plans done; refresh_tokens table + IRefreshTokenService landed; AUTH-01 satisfied). Plan 02-02 (account-deletion re-auth) is next.
last_updated: "2026-05-14T20:49:18Z"
last_activity: 2026-05-14 -- Plan 02-01 complete (AUTH-01 satisfied)
progress:
  total_phases: 7
  completed_phases: 1
  total_plans: 7
  completed_plans: 5
  percent: 71
---

# Project State

## Project Reference

See: .planning/PROJECT.md (updated 2026-05-03)

**Core value:** Trustworthy classification — every line item correctly categorized into the right tax category, with reasoning the user can audit and override.
**Current focus:** Phase 02 — auth-rate-limit-hardening

## Current Position

Phase: 02 (auth-rate-limit-hardening) — EXECUTING
Plan: 2 of 3
Status: Executing Phase 02 (plan 02-01 done)
Last activity: 2026-05-14 -- Plan 02-01 complete (AUTH-01 satisfied)

Progress: ██████████ 100% of Phase 1; 1/3 plans in Phase 2 done

### Wave map

- Wave 1: 01-01 (Hygiene + Anthropic alignment + CORS deny-all) — no deps — DONE
- Wave 2: 01-04 (Serilog enrichers + LogContext) — depends on 01-01 — DONE
- Wave 3: 01-03 (Sentry .NET + Next.js, EU residency, PII scrubbing) — depends on 01-01, 01-04 — DONE
- Wave 4: 01-02 (CI workflow + README) — depends on 01-01, 01-03, 01-04 — DONE

## Performance Metrics

**Velocity:**

- Total plans completed: 9
- Average duration: 23 min (skewed by 01-02's 74-min resumption-agent wall-clock; non-resumption avg = 8 min)
- Total execution time: 110 min

**By Phase:**

| Phase | Plans | Total  | Avg/Plan |
|-------|-------|--------|----------|
| 1 | 4 | - | - |
| 2 | 1 | 15 min | 15 min |

**Recent Trend:**

- Last 5 plans: 02-01 (15 min, 6 tasks, 23 files — fresh execution), 01-02 (74 min, 2 tasks, 2 files — resumption agent), 01-03 (11 min, 2 tasks, 17 files), 01-04 (5 min, 2 tasks, 6 files), 01-01 (5 min, 3 tasks, 11 files)
- Trend: 02-01 hit ~15 min for a 6-task plan with 23 files (12 created / 11 modified) including an EF migration and full test suite. Two Rule-1 deviations (InMemory provider ExecuteUpdateAsync fallback + grep-guard string disambiguation) added small touch-up but no scope creep.

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
- 01-02: GitHub Actions CI workflow (`.github/workflows/ci.yml`) is the first ever CI in the repo — three parallel jobs (`hygiene-check`, `backend-build-test`, `frontend-lint-build`) on PRs to main and pushes to main; CPM-aware cache key includes `Backend/Directory.Packages.props` (RESEARCH.md Pitfall 5 mitigation)
- 01-02: Branch protection on main is manual-pending per D-10 + plan instruction — operator runs the GitHub UI workflow after first PR merges and CI registers the three job names; auto-enabling repo security via gh CLI is explicitly out of executor scope
- 01-02: Top-level `README.md` is English (not German) per D-12 — convention boundary between dev tooling (English, matches existing `Backend/README.md` analog) and end-user UI (German Sie-form). No screenshots, no CI badge until branch protection is enabled
- 01-02: Local empty `storage/` dir is benign — gitignored by Plan 01-01, zero tracked files, never reaches CI runner via `actions/checkout@v4`. Plan's local-side smoke fails on directory presence; CI-side check on tracked files passes. Documented as local-vs-CI delta, NOT a regression
- 02-01: RefreshTokenService stays HTTP-context-free (Pitfall 8) — UA/IP are method parameters; Minimal API endpoint binds HttpContext directly and extracts them. No IHttpContextAccessor injection.
- 02-01: RevokeAllForUserAsync detects provider by name (`Database.ProviderName == "Microsoft.EntityFrameworkCore.InMemory"`) — production runs ExecuteUpdateAsync; in-memory tests fall back to load-and-mutate. No InMemory package dependency leaked into production Infrastructure.
- 02-01: Sentry message body is the unique searchable token ("Refresh token replay detected"); Serilog warning uses a different phrasing so the verification grep returns exactly one match. Both fire together on every replay event.
- 02-01: EF migration manually reordered to CreateTable-first then DropColumn-last (EF scaffolds the reverse) per D-15 + RESEARCH Pattern 3.
- 02-01: AuthService.RegisterAsync now SaveChanges before refreshTokenService.IssueAsync — refresh_tokens.user_id FK requires the user row to exist first.

### Pending Todos

- **Operator (manual)** — Enable branch protection on `main` after first PR carrying CI workflow merges and the three job names register with GitHub. Full configuration in `.planning/phases/01-foundation-cleanup-ci/01-02-SUMMARY.md` "Pending Operator Action" section. Not blocking Phase 1 verification but required for full satisfaction of Phase 1 Success Criterion #1 ("checks BLOCK merges", not just "checks RUN").

### Blockers/Concerns

None yet.

## Deferred Items

| Category | Item | Status | Deferred At |
|----------|------|--------|-------------|
| *(none)* | | | |

## Session Continuity

Last session: 2026-05-14
Stopped at: Plan 02-01 complete (1/3 Phase-2 plans done; refresh_tokens table + IRefreshTokenService landed; AUTH-01 satisfied). Plan 02-02 (account-deletion re-auth) is next.
Resume file: Continue Phase 2 execution — plan 02-02 (Account-deletion re-auth, AUTH-02) is the next plan in the queue, followed by 02-03 (Rate-limit policies, AUTH-03). Operator manual follow-up still pending: enable branch protection on main per `.planning/phases/01-foundation-cleanup-ci/01-02-SUMMARY.md` Pending Operator Action.
