---
phase: 01-foundation-cleanup-ci
plan: 02
subsystem: infra
tags: [ci, github-actions, hygiene, readme, docker, central-package-management, branch-protection]

requires:
  - phase: 01-foundation-cleanup-ci/01
    provides: hygiene baseline (.gitignore + Backend/.dockerignore) so the CI hygiene-check passes against the post-Phase-1 working tree; 100/100 baseline
  - phase: 01-foundation-cleanup-ci/04
    provides: Serilog enrichers + 3 new tests (103/103) — backend-build-test executes them in CI as a regression guard
  - phase: 01-foundation-cleanup-ci/03
    provides: Sentry SDK + 10 new SentryScrubbingTests (113/113) + frontend Sentry scaffold + conditional withSentryConfig — frontend-lint-build runs without Sentry env vars in CI thanks to Pitfall 6
provides:
  - Merge-blocking CI workflow (`.github/workflows/ci.yml`) running three parallel jobs on `pull_request` to `main` and `push` to `main` — first ever CI in the repo
  - `hygiene-check` job that fails the build if any forbidden directory (`storage/`, `Backend/storage/`, `Backend/src/TaxReader.Api/storage/`) or build-artifact (`build-diag*.txt`, `*.binlog`) is committed — codifies Plan 01-01's `.gitignore` invariants as a CI gate
  - `backend-build-test` job that runs `dotnet restore` + `dotnet build --configuration Release` + `dotnet test --configuration Release` against `Backend/` with CPM-aware cache key
  - `frontend-lint-build` job that runs `npm ci` + `npm run lint` + `npm run build` against `Frontend/`
  - Concurrency group that cancels stale PR runs but never `main` (`${{ github.event_name == 'pull_request' }}`)
  - Top-level `README.md` documenting prerequisites, quick-start, common tasks, and links to `CLAUDE.md` + `.planning/` — first README at repo root
  - Manual-pending follow-up to enable branch protection on `main` after first green CI run (Task 3)
affects: [02-*, 03-*, 04-*, 05-*, 06-*, 07-*]

tech-stack:
  added:
    - "GitHub Actions workflow surface (no new package dependencies; uses pinned `actions/checkout@v4`, `actions/setup-dotnet@v4`, `actions/setup-node@v4`)"
  patterns:
    - "CPM-aware cache key: `cache-dependency-path` includes `Backend/Directory.Packages.props` (the central NuGet manifest) so version bumps invalidate cache even when no `packages.lock.json` is present (RESEARCH.md Pitfall 5)"
    - "Concurrency group with conditional cancel-in-progress — cancels superseded PR runs (preserves CI minutes), never cancels main (preserves auditable post-merge run history)"
    - "Hygiene-check codified at two layers: `.gitignore` (Plan 01-01) prevents accidental staging; `hygiene-check` CI job catches explicit `git add -f` overrides — defence in depth"

key-files:
  created:
    - .github/workflows/ci.yml
    - README.md
    - .planning/phases/01-foundation-cleanup-ci/01-02-SUMMARY.md
  modified: []

key-decisions:
  - "Existing `.github/workflows/ci.yml` (written by predecessor agent before rate-limit interrupt) verified verbatim against plan must_haves.truths and committed as-is — every must_have passed, no fixes needed, no deviations"
  - "Frontend builds in CI without `SENTRY_ORG`/`SENTRY_PROJECT` thanks to Plan 01-03's conditional `withSentryConfig` (Pitfall 6) — confirmed by reading `Frontend/next.config.ts`; no CI env vars required for green Phase 1 build"
  - "Branch protection (Task 3) is manual-pending per D-10 + plan's explicit instruction — executor agent does NOT auto-enable repo security settings; operator runs the GitHub UI workflow after first PR merges and CI registers job names"
  - "README.md uses English (not German) per D-12 — convention boundary between dev tooling (English) and end-user UI (German `Sie`-form). Matches existing `Backend/README.md` analog"
  - "Local empty `storage/` directory is benign — gitignored (line 7 of `.gitignore`), zero tracked files, never reaches CI runner via `actions/checkout@v4`. Plan's local-side smoke check fails on directory presence; CI-side check passes on tracked-file absence. Documented under Issues Encountered as a local-vs-CI delta worth knowing for future debug sessions, NOT a hygiene regression"

patterns-established:
  - "Resumption-agent re-verification protocol: when continuing after a rate-limit interrupt where files were created but not committed, re-run every must_have.truths grep against the already-on-disk file before committing — catches transcription drift between predecessor's work and the plan template"
  - "CI cache key for Central Package Management: include the central manifest (`Directory.Packages.props`) in `cache-dependency-path` even when no `packages.lock.json` exists — invalidates cache on version bumps without forcing lock-file generation"

requirements-completed: [FND-04, FND-05]

duration: 74min
completed: 2026-05-10
---

# Phase 1 Plan 02: GitHub Actions CI Workflow + Top-Level README Summary

**Shipped the first ever merge-blocking CI workflow for the repo — three parallel jobs (`hygiene-check`, `backend-build-test`, `frontend-lint-build`) on every PR to `main` and every push to `main`, with concurrency that cancels stale PR runs but never main; codified Plan 01-01's `.gitignore` invariants as a CI gate via the `hygiene-check` job; created the first top-level `README.md` covering the four prerequisites + the canonical `cp .env.example .env` -> `docker compose up --build` quick-start path; branch protection on `main` is a manual-pending operator step per D-10. Executed as a resumption after the predecessor agent's rate-limit interrupt — the on-disk `ci.yml` was verified verbatim against all plan must_haves.truths before committing.**

## Performance

- **Duration:** 74 min (resumption-agent wall-clock; includes ~30 min reading prior plan summaries + planning context, ~5 min systematic must_haves verification, ~5 min editing + committing, ~25 min self-check + summary writing). Predecessor agent's pre-interrupt work (writing `ci.yml`) added a separate ~10 min that does NOT count against this wall-clock.
- **Started:** 2026-05-10T16:21:05Z (resumption-agent start)
- **Completed:** 2026-05-10T17:34:50Z
- **Tasks:** 2 auto + 1 manual-pending
- **Files touched:** 3 (2 new + 1 new SUMMARY)
- **Test delta:** 0 (CI workflow is process-only; backend tests stay at 113/113, frontend has no test framework)

## Accomplishments

- **CI workflow (FND-04):** `.github/workflows/ci.yml` triggers on `pull_request: [main]` and `push: [main]` with three parallel jobs:
  1. `hygiene-check` — `find` glob for `build-diag*.txt` / `*.binlog` + directory existence checks for `storage/`, `Backend/storage/`, `Backend/src/TaxReader.Api/storage/` — exit 1 with violation list on any match
  2. `backend-build-test` — `actions/setup-dotnet@v4` with `dotnet-version: '10.0.x'`, CPM-aware cache key (`Backend/**/packages.lock.json` + `Backend/Directory.Packages.props`), then `dotnet restore` -> `dotnet build --no-restore --configuration Release` -> `dotnet test --no-build --configuration Release --verbosity normal`
  3. `frontend-lint-build` — `actions/setup-node@v4` with `node-version: '22'`, npm cache keyed off `Frontend/package-lock.json`, then `npm ci` -> `npm run lint` -> `npm run build`
  Concurrency group is `${{ github.workflow }}-${{ github.ref }}` with `cancel-in-progress: ${{ github.event_name == 'pull_request' }}` — stale PR runs are cancelled when superseded; main runs always complete (audit trail preserved). No `secrets:`, no `permissions:` block (default `contents: read` is sufficient), no Postgres service container (D-09 defers integration tests to Phase 7's `QA-01`).

- **Top-level README (FND-05):** `README.md` at repo root covers the four prerequisites with version probes (`dotnet --version` reports `10.x`, `node --version` reports `v22.x`, `docker compose version`, Tesseract for non-container dev with platform-specific install commands for macOS/Linux/Windows), the canonical quick-start (`git clone` -> `cp .env.example .env` -> edit `JWT_SECRET`/`ANTHROPIC_API_KEY`/`POSTGRES_PASSWORD` -> `docker compose up --build` -> `https://localhost`), a common-tasks reference table (`dotnet build/test`, `npm run dev/build`, `docker compose down -v`, `dotnet ef migrations add ...`), and links to `CLAUDE.md`, `.planning/PROJECT.md`, `.planning/ROADMAP.md`, `.planning/codebase/`. English (D-12 convention boundary). No screenshots. No CI badge yet (branch protection isn't enabled — adding a green badge before first run would be misleading).

- **Branch protection (Task 3 — manual-pending):** Per D-10 + the plan's explicit "Do NOT attempt to enable branch protection automatically" instruction, this task is documented as operator-pending. The plan's locked configuration (PR required with 0 reviewers; three required status checks `hygiene-check`, `backend-build-test`, `frontend-lint-build`; signed-commit OFF; linear-history OFF; admin bypass disallowed) is recorded in this summary's "Pending Operator Action" section so the operator can apply it via GitHub UI after the first PR merges and the three job names register.

## Task Commits

1. **Task 1 — Create `.github/workflows/ci.yml` with three parallel jobs** — `6bbf7b8` (feat)
2. **Task 2 — Create top-level `README.md`** — `1bea8a0` (docs)
3. **Task 3 — Branch protection on `main`** — manual-pending (no commit; see Pending Operator Action below)

_Note: Task 1's commit is `feat` because it adds new project capability (CI gate). Task 2's commit is `docs` because it's pure documentation. Both are atomic per execute-plan.md protocol — same pattern as Plan 01-01, 01-04, 01-03._

## Resumption Context

This plan was executed as a **continuation** after the predecessor executor agent for `01-02-PLAN.md` was interrupted by a rate-limit AFTER writing `.github/workflows/ci.yml` to disk but BEFORE committing it, BEFORE creating `README.md`, and BEFORE producing this SUMMARY.

**Resumption-agent protocol followed:**
1. Verified the on-disk `ci.yml` against every must_haves.truth in the plan (8 grep checks for triggers, jobs, action versions, cache paths, concurrency expression, no-secrets) — all 8 passed cleanly.
2. Manually parsed the YAML structure to confirm exactly three jobs at the expected indentation, with the right names — confirmed via `grep -E "^  (hygiene-check|backend-build-test|frontend-lint-build):"  | wc -l` returning 3.
3. Verified no Postgres service container, no `secrets.*` references, no `permissions:` block — confirmed via the inverse grep (`! grep -q "secrets\."`).
4. Treated the on-disk file as trusted predecessor output once the verification passed; committed as-is (no edits required — file matched the plan's verbatim Pattern 9 + Pattern 8 templates).
5. Wrote `README.md` from the plan's verbatim template (Task 2 — straightforward, no predecessor partial work to reconcile).
6. Wrote this SUMMARY documenting both the substance of the work AND the resumption context for audit trail.

**Why this matters for future debug sessions:** if `git log` shows a `feat(01-02)` commit on `2026-05-10` but the surrounding STATE.md / ROADMAP.md was last updated by Plan 01-03's commit on the same day, that's a sign the resumption agent finished the workflow correctly — not a sign of plan-skip or partial execution.

## Files Created

- `.github/workflows/ci.yml` — 91 lines; YAML; first GitHub Actions workflow in the repo. Pre-existing on disk from predecessor agent; verified verbatim against plan must_haves before committing.
- `README.md` — 63 lines; English Markdown; first repo-root README. Covers tagline, prerequisites, quick start, common tasks, documentation links, license placeholder.
- `.planning/phases/01-foundation-cleanup-ci/01-02-SUMMARY.md` — this file.

## Files Modified

None. The plan touches no existing source files (`Backend/README.md` and `Frontend/README.md` left untouched per plan; `.gitignore` already covers the workflow path; no test files added because Frontend has no test framework and the workflow itself is a yaml artifact, not testable in isolation by xUnit).

## Decisions Made

All four `key-decisions` listed in frontmatter executed as planned. No new decisions required during execution. The most consequential nuance was the local-vs-CI delta on the empty `storage/` directory (see Issues Encountered) — verified as benign because `actions/checkout@v4` only restores tracked files, and zero `storage/` files are tracked.

## Deviations from Plan

None. The on-disk `ci.yml` matched the plan's verbatim template (RESEARCH.md Pattern 8 + Pattern 9) exactly; no edits required. The `README.md` was written from the plan's verbatim template; only minor wording carried over verbatim from the plan (no rephrasing).

**Total deviations:** 0
**Impact on plan:** None. Resumption agent produced strictly the deliverables the plan specified, in the order the plan specified, with the exact commit message style established by Plans 01-01 / 01-04 / 01-03.

## Issues Encountered

**1. Local empty `storage/` directory triggers plan's verbatim local-smoke check (NOT a regression — local-vs-CI delta).**

- **Found during:** Plan-level verification block step "Smoke: hygiene-check passes against the current tree"
- **Symptom:** The plan's verbatim shell smoke `bash -c '... if [ -d "$path" ]; then exit 1; fi'` exited 1 with `FAIL: storage exists`
- **Investigation:** `ls -la storage/` -> empty directory (just `.` and `..`); `git ls-files storage/` -> empty (zero tracked files); `git check-ignore -v storage/` -> `.gitignore:7:storage/` (correctly gitignored)
- **Verdict:** Benign. The directory exists locally as a residual from previous `dotnet run` / Docker bind-mount activity. It is correctly gitignored (Plan 01-01 added the rule), zero PII files are tracked, and `actions/checkout@v4` on the CI runner does NOT restore gitignored or untracked directories — so the CI hygiene-check on a fresh checkout will pass. Not a Plan 01-01 regression; not a Plan 01-02 issue. The plan's local smoke is over-strict relative to actual CI behavior on this codebase.
- **Mitigation:** Documented for future executor / debug agents; no code or workflow change. The CI hygiene-check correctly enforces the post-merge invariant ("no `storage/` ever lands in `main`"), which is what Phase 1 success criterion #2 requires.

**2. Predecessor agent's commit-attempt history (none to recover).**

- **Found during:** Resumption-agent startup
- **Symptom:** `git log --oneline -10` shows `5e3ae76` as HEAD (Plan 01-03's metadata commit); no Plan 01-02 commits exist; `.github/workflows/ci.yml` is untracked but exists on disk
- **Verdict:** Predecessor agent finished writing `ci.yml` but was rate-limited before its commit step. Resumption agent picked up from a clean state with the file ready to commit. No partial commits to recover from, no force-pushes to undo, no rebasing needed. Clean resumption.

## Verification Results

```
=== Workflow file presence + structure ===
OK ci.yml exists
OK 3 required jobs at correct indentation (grep "^  (hygiene-check|backend-build-test|frontend-lint-build):" | wc -l == 3)

=== Plan Task 1 verbatim verification command ===
OK ci.yml exists
OK hygiene-check job present
OK backend-build-test job present
OK frontend-lint-build job present
OK triggers branches: [main]
OK actions/setup-dotnet@v4
OK dotnet-version: '10.0.x'
OK actions/setup-node@v4
OK node-version: '22'
OK Backend/Directory.Packages.props in cache key
OK Frontend/package-lock.json in cache key
OK concurrency expression github.event_name == 'pull_request'
OK no secrets.* references

=== Plan Task 2 verbatim verification command ===
OK README.md exists
OK "docker compose up --build" present
OK ".env.example" present
OK "CLAUDE.md" linked
OK ".NET 10" listed in prerequisites
OK "Node.js 22" listed in prerequisites
OK "Tesseract" listed in prerequisites
OK "https://localhost" referenced
OK "TaxReader" appears (rebrand honoured)
OK first-line heading == "# TaxReader"
OK zero image-syntax matches (^!\[ count == 0; no screenshots per D-12)

=== CI-equivalent hygiene-check (simulating actions/checkout@v4 — tracked-only view) ===
OK no tracked storage paths (git ls-tree -r HEAD | grep -E "^(storage|Backend/storage|Backend/src/TaxReader.Api/storage)/" returns empty)
OK no tracked logs (git ls-tree -r HEAD | grep -E "(build-diag.*\.txt|\.binlog)$" returns empty)

=== No accidental file deletions in either commit ===
OK 6bbf7b8: 0 deletions
OK 1bea8a0: 0 deletions
```

All 8 must_haves.truths, all 2 must_haves.artifacts, all 3 key_links, and all `<success_criteria>` in the plan body — verified.

## TDD Gate Compliance

Plan-level type is `execute`, not `tdd`. No per-task `tdd="true"` markers either — Task 1 and Task 2 are pure-text artifacts (yaml + markdown) with no testable runtime behavior. Verification is grep-based / structural, not unit-test-based. Per execute-plan.md's TDD Gate Enforcement note: applicable only when plan-level type is `tdd`.

## CI First-Run Telemetry (To Be Recorded Post-Merge)

The plan's `<output>` block requests the following data points be captured after the first PR carrying these commits merges to `main`. Recording placeholders here for the operator to fill in:

- **First-PR CI run URL:** _Pending — record from GitHub Actions tab after first PR merges_
- **CPM cache strategy verdict:** Expected outcome is "no `packages.lock.json` files exist; cache key falls back to `Backend/Directory.Packages.props` SHA on cold runs; warm runs hit cache when no version bump occurred." If the first cold-run logs show `Cache not found for input keys: [...]` followed by full `dotnet restore` from nuget.org, that's expected. RESEARCH.md Pitfall 5 fallback (add `<RestorePackagesWithLockFile>true</RestorePackagesWithLockFile>` to `Backend/Directory.Build.props`) only required if cache invalidation proves broken on a version-bump PR.
- **CI duration baseline (cold + warm):** _Pending. Plan's expected ceiling is < 6 min total; longer signals investigation._
- **Branch-protection rule status:** Pending operator action (Task 3).

## Pending Operator Action — Task 3 Branch Protection

**Reason for manual-only execution:** D-10 specifies one-time human-driven setup; auto-enabling repo security via `gh` CLI requires admin OAuth scope and is explicitly out of executor scope per the plan's text.

**Steps the operator runs after first PR merges and CI runs at least once green:**

1. Open `Settings -> Branches -> Add branch protection rule` in the GitHub UI for the `TaxReader` repo
2. **Branch name pattern:** `main`
3. **Require a pull request before merging:** ON
   - Required approvals: **0** (solo-dev posture)
   - Dismiss stale pull request approvals when new commits are pushed: leave default
4. **Require status checks to pass before merging:** ON
   - Require branches to be up to date before merging: ON
   - Required status checks (search and select all three by their job-name labels):
     - `Hygiene check (no PII / build artifacts)` (job `hygiene-check`)
     - `Backend build + test` (job `backend-build-test`)
     - `Frontend lint + build` (job `frontend-lint-build`)
5. **Require conversation resolution before merging:** leave default (off)
6. **Require signed commits:** OFF (D-10)
7. **Require linear history:** OFF (D-10)
8. **Require deployments to succeed before merging:** OFF
9. **Lock branch:** OFF
10. **Do not allow bypassing the above settings:** ON (admins follow the rule unless they explicitly bypass)
11. **Restrict who can push to matching branches:** leave default
12. Save the rule.
13. Verify by opening a draft PR with a trivial change — confirm the three required checks appear and merge is blocked until they pass.

**Validation:** Once enabled, Phase 1 Success Criterion #1 ("Every PR has merge-blocking checks") flips from "checks RUN" to "checks BLOCK merges" — the workflow alone makes the checks run; branch protection is what makes them merge-gating.

## User Setup Required

Beyond Task 3 above (branch protection), no other operator setup is required by this plan. The CI workflow itself runs without any user-side action — first run fires automatically when the next PR is opened against `main` (or when the next push lands on `main`).

## Next Phase Readiness

- **Phase 1 itself — verification & close-out:** All 4 plans now landed. Phase 1 Success Criteria progress:
  - #1 (Every PR has merge-blocking checks): **CI runs** — yes; **CI BLOCKS merges** — pending Task 3
  - #2 (No `storage/` / `build-diag.txt`; CI fails on regression): **DONE** — `hygiene-check` enforces it
  - #3 (Anthropic model alignment + CLAUDE.md doc): **DONE** by Plan 01-01
  - #4 (Sentry receives errors with PII scrubbed; alerts don't fire on noise): **Backend half DONE** by Plan 01-03; **frontend half dormant by design** (D-16 — flips on at Phase 6's TTDSG cookie banner); **alert-rule-tuning DONE-after-real-traffic** at Phase 7's QA-06
  - #5 (Long-running upload handlers correlated by `ReceiptFileId` / `JobId`): **`ReceiptFileId` half DONE** by Plan 01-04; **`JobId` half deferred** to Phase 3 at the Hangfire boundary
  - #6 (New developer can run `docker compose up --build` from README): **DONE** by this plan
- **Phase 2 (Auth + Rate-Limit Hardening):** Unblocked once Phase 1 verification passes. The CI gate this plan ships becomes the first line of regression defence for AUTH-01's `refresh_tokens` table migration, AUTH-02's account-deletion confirmation, and AUTH-03's rate-limiter tests.
- **Phase 7 (QA-01 — Postgres integration tests via Testcontainers):** When the Phase 7 plan adds Testcontainers tests, the existing `backend-build-test` job picks them up automatically (no workflow change needed) — `dotnet test` runs everything in the test project, and Testcontainers-managed containers come up inside the runner for the duration of the test.
- **Future CI hardening (deferred — out of Phase 1 scope):**
  - Pin third-party actions to SHA instead of major-version tag (T-01-02-06 — accepted for solo-dev velocity per CONTEXT.md `<deferred>`; revisit at Phase 7 QA-06)
  - Add `dotnet list package --vulnerable` and `npm audit` smoke steps (T-01-03-11 — Phase 7 QA-06)
  - Optional: Add `dotnet list package --include-transitive | grep -i sentry` to guard against Sentry SDK type-name drift (Plan 01-03 deviation #2 fix would have been caught by this)

## Self-Check: PASSED

- Created files exist:
  - `.github/workflows/ci.yml` — FOUND (91 lines, parses, all must_haves grep-pass)
  - `README.md` — FOUND (63 lines, all required strings present, zero image refs, first heading correct)
  - `.planning/phases/01-foundation-cleanup-ci/01-02-SUMMARY.md` — FOUND (this file)
- Commit hashes exist in git log:
  - `6bbf7b8` (Task 1 — feat) — FOUND
  - `1bea8a0` (Task 2 — docs) — FOUND
- Manual-pending Task 3 documented under "Pending Operator Action" with full D-10 configuration

---

*Phase: 01-foundation-cleanup-ci*
*Completed: 2026-05-10*
*Resumption note: executed as a continuation after the predecessor agent's rate-limit interrupt; on-disk `ci.yml` was verified verbatim against all 8 plan must_haves.truths before committing.*
