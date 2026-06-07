---
phase: 07-test-depth-launch-qa
plan: 06
subsystem: infra
tags: [ci, github-actions, vitest, playwright, testcontainers, de-localization, bash-guard]

# Dependency graph
requires:
  - phase: 07-test-depth-launch-qa/07-01
    provides: TaxReader.IntegrationTests project with Testcontainers Postgres suite
  - phase: 07-test-depth-launch-qa/07-04
    provides: Vitest 3 installed + 19 passing unit/component tests; "test" script in package.json
  - phase: 07-test-depth-launch-qa/07-05
    provides: Playwright 1.60 installed; playwright.config.ts; e2e/happy-path.spec.ts DE locale spec
provides:
  - DE-localization guard step in hygiene-check CI job (grep-inside-if for bare toLocaleString)
  - Vitest step in frontend-lint-build job (fast tier, every PR)
  - Gated heavy-suite job running Postgres Testcontainers integration + Playwright E2E (push-to-main + run-heavy label)
  - Playwright browser cache via actions/cache@v4
  - Playwright report artifact upload on failure
affects: [phase-07-launch-qa, go-no-go-report, 07-07]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "DE-localization guard: grep-inside-if bash pattern (Pitfall 4 compliant) — scope to Frontend/src *.tsx/*.ts only"
    - "Heavy CI job gating: job-level if condition for push OR PR label"
    - "Testcontainers in CI: ubuntu-latest Docker + no services: postgres block"
    - "Playwright browser caching: actions/cache@v4 keyed on package-lock.json hash"
    - "Backend E2E setup: docker compose up -d db + dotnet run background + health poll"

key-files:
  created: []
  modified:
    - .github/workflows/ci.yml

key-decisions:
  - "DE guard uses grep-inside-if skeleton (Pitfall 4) scoped tightly to Frontend/src *.tsx/*.ts — avoids false positives on code identifiers"
  - "Vitest step added after Build in frontend-lint-build (folded into fast tier, D-04) rather than a separate sibling job — keeps the job count low and shares the npm ci install"
  - "heavy-suite uses docker compose up -d db + dotnet run (already restored/built) for E2E backend — avoids rebuilding Docker image; API exposes port 5190 (Next.js default BACKEND_API_URL)"
  - "Integration tests (Testcontainers) run before Node setup in heavy-suite — maximises parallelism with docker pull time"
  - "Playwright browser cache keyed on hashFiles(Frontend/package-lock.json) — invalidates when Playwright version changes"
  - "API startup health poll uses /health endpoint (built in 07-03) with || true fallback — job continues even if health endpoint times out so Playwright can surface a clearer failure"

patterns-established:
  - "Bash CI guard pattern: set -e + grep-inside-if + printf + exit 1 (cloned from 06-07 legal-placeholder template)"
  - "Heavy job secrets pattern: supply minimal test-placeholder values for validators that require non-null secrets at startup"

requirements-completed: [QA-04, QA-01, QA-02, QA-03]

# Metrics
duration: 25min
completed: 2026-06-07
---

# Phase 07 Plan 06: CI Extensions — DE Guard, Vitest, Gated Heavy Suite

**DE-localization bash guard in hygiene-check + Vitest on every PR + gated heavy-suite job running Testcontainers Postgres integration and Playwright E2E on push-to-main and the run-heavy label**

## Performance

- **Duration:** ~25 min
- **Started:** 2026-06-07T11:40:00Z
- **Completed:** 2026-06-07T12:05:00Z
- **Tasks:** 2
- **Files modified:** 1

## Accomplishments

- Extended `hygiene-check` with a DE-localization guard that fails on bare `toLocaleString()` (no locale) across `Frontend/src/*.tsx/*.ts`; verified `guard-ok` locally (no violations in current codebase)
- Added `Test (Vitest)` step to `frontend-lint-build` after Build; all 19 existing Vitest tests pass; runs on every PR (D-04)
- Added `heavy-suite` job gated on `push` to main or `run-heavy` PR label; runs the Postgres Testcontainers integration suite (QA-01), installs Playwright browsers with `--with-deps`, starts the backend via `docker compose + dotnet run`, and runs Playwright E2E (QA-03); uploads playwright-report artifact on failure
- YAML validated via `npx js-yaml` — parses cleanly

## Task Commits

1. **Task 1: DE-localization guard + Vitest on every PR** - `4fec76b` (feat)
2. **Task 2: Gated heavy-suite job** - `e478c74` (feat)
3. **Plan metadata (SUMMARY)** - [see final commit below]

## Files Created/Modified

- `.github/workflows/ci.yml` — Added DE guard step to `hygiene-check`, Vitest step to `frontend-lint-build`, and new `heavy-suite` job (97 lines added)

## Decisions Made

- **DE guard placement:** Step added after the existing legal-placeholder guard in `hygiene-check` — natural order (legal integrity, then localization correctness)
- **Vitest placement:** Folded into `frontend-lint-build` after Build rather than a separate job; shares the `npm ci` install and keeps the fast-tier job count at 3
- **Backend for E2E:** `docker compose up -d db` for Postgres + `dotnet run` (already built) on port 5190 in background; Next.js standalone proxies `/api/v1/*` to `BACKEND_API_URL=http://localhost:5190` (the default in `next.config.ts`). This avoids rebuilding the full Docker image in CI.
- **Health poll fallback:** `|| true` on the health-wait step so that if `/health` is slow Playwright still runs and produces a clear error, rather than a confusing timeout
- **Secrets pattern:** Integration tests receive placeholder values for `Stripe__SecretKey`, `RefreshToken:HashKey`, etc. because the `StripeOptionsValidator` and `RefreshTokenOptionsValidator` reject missing/invalid values at startup (STATE.md 02-CR-01); Testcontainers overrides the actual DB connection string in the WAF

## Deviations from Plan

None — plan executed exactly as written. The E2E backend start approach was explicitly left to executor discretion ("If a full compose-up is impractical in this step, document the chosen approach inline") and documented above.

## Issues Encountered

None.

## Known Stubs

None — this plan is CI-only YAML; no UI rendering or data stubs.

## Threat Flags

None — no new network endpoints, auth paths, file access patterns, or schema changes introduced. The CI job supplies placeholder secrets that never reach production.

## YAML Validity Verification

Verified via `npx --yes js-yaml .github/workflows/ci.yml` — exits 0 with `ci.yml-valid-yaml`. The file uses 2-space indentation matching the existing jobs; no tabs introduced.

## Next Phase Readiness

- All three lightweight CI jobs (`hygiene-check`, `backend-build-test`, `frontend-lint-build`) run unchanged on every PR
- `heavy-suite` is wired but will only run green on GitHub Actions (Docker/Playwright browser download requires the remote runner environment)
- Phase 07 remaining: 07-07 (native-speaker DE review — non-blocking per D-06)
- D-05 hard launch blockers now depend on `heavy-suite` being green in CI: QA-01 (integration) + QA-03 (E2E)

---
*Phase: 07-test-depth-launch-qa*
*Completed: 2026-06-07*
