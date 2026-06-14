---
status: testing
phase: 01-foundation-cleanup-ci
source: [01-01-SUMMARY.md, 01-02-SUMMARY.md, 01-03-SUMMARY.md, 01-04-SUMMARY.md, 01-HUMAN-UAT.md]
started: 2026-06-09T00:00:00Z
updated: 2026-06-14T00:00:00Z
---

## Current Test

number: 7
name: CI workflow runs three jobs (FND-04)
expected: |
  Opening a PR to `main` (or pushing to `main`) triggers the GitHub Actions
  workflow with three jobs — `hygiene-check`, `backend-build-test`,
  `frontend-lint-build` — all green on the current tree.
awaiting: user response

## Tests

### 1. Cold Start Smoke Test
expected: From a clean state — `docker compose down -v` then `docker compose up --build` — all four services (db, api, web, caddy) boot without errors, DB migrations complete, and https://localhost loads the app (register/login works).
result: pass
note: "Initially failed (blocker) — web image build broke on `next build` type-checking vitest.config.mts (Next 16 rolldown-vite vs vitest rollup-vite Plugin type skew). Fixed by excluding test tooling from Frontend/tsconfig.json `exclude`; cold start re-run confirmed green. See Gaps."

### 2. Anthropic model startup canary (FND-02)
expected: On API boot the logs show one structured line resolving the model, e.g. `Anthropic configuration resolved: Model=claude-haiku-4-5, CostPerClassification=1`. Confirms code/compose/env are aligned and drift would be visible.
result: pass

### 3. CORS deny-all in production (FND-03)
expected: With `CORS_ALLOWED_ORIGINS` unset in a non-Development environment, a cross-origin browser request is blocked (no permissive `localhost:3000` fallback), and the API logs `CORS_ALLOWED_ORIGINS unset in {Environment} environment ...`. Same-origin traffic through Caddy is unaffected.
result: pass
note: "Stack runs ASPNETCORE_ENVIRONMENT=Production with CORS_ALLOWED_ORIGINS unset (deny-all branch active). OPTIONS preflight to /api/v1/auth/login with Origin https://evil.example.com returned 204 with NO access-control-allow-origin header — foreign origin denied. Same-origin via Caddy unaffected."

### 4. Hygiene — no leaked PII / build artifacts (FND-01)
expected: The repo tracks no `storage/` PII PDFs, no `build-diag*.txt`, no `*.binlog`; `.gitignore` + `Backend/.dockerignore` prevent reintroduction. Re-confirm nothing leaked back in.
result: pass
note: "git ls-files: no tracked build-diag*.txt or *.binlog; no storage/ PDFs. .gitignore lines 11/20/21 cover storage/, build-diag*.txt, *.binlog; Backend/.dockerignore excludes src/TaxReader.Api/storage. Only tracked PDF is Frontend/e2e/fixtures/sample-receipt.pdf — intentional Playwright E2E fixture, not PII. Verified directly from repo."

### 5. Serilog ReceiptFileId correlation (OBS-02)
expected: During an upload, backend log lines emitted by extractor/parser/classifier all carry the `ReceiptFileId` property, so one upload's pipeline can be traced end-to-end in stdout.
result: pass
note: "Correlation works but property was renamed at the Phase-3 Hangfire boundary (D-18): pipeline logs carry `JobId`, not `ReceiptFileId`. ProcessReceiptFileJob.cs:56 pushes JobId=<receiptFileId> (extract/parse); ClassifyBatchJob.cs:34 pushes JobId=<uploadBatchId> (classify). Live logs confirmed both stages tag every line with JobId (e.g. ProcessReceiptFileJob JobId=36a8f951..., ClassifyBatchJob JobId=809c28cd...). Console template renders properties via {Properties:j}. Intent (per-upload traceability) satisfied. DOC FOLLOW-UP: 01-HUMAN-UAT / OBS-02 text is stale (ReceiptFileId→JobId). MINOR NOTE: extract/parse and classify use different correlation ids (file id vs batch id), so not one identical id across all 3 stages — accepted as Phase-3 design, not a gap."

### 6. Sentry empty-DSN no-op boot (OBS-01)
expected: With no `SENTRY_DSN_BACKEND` set, the API boots cleanly — no Sentry exception or warning. Frontend Sentry stays OFF (`NEXT_PUBLIC_SENTRY_ENABLED=false`). (When a DSN is later set, backend errors should arrive in Sentry with PII scrubbed.)
result: pass
note: "Empty Sentry.Dsn in appsettings.json, no override. API booted cleanly (tests 1-5 all functional); docker compose logs api showed no Sentry exception/error/warning. Frontend Sentry off by default."

### 7. CI workflow runs three jobs (FND-04)
expected: Opening a PR to `main` (or pushing to `main`) triggers the GitHub Actions workflow with three jobs — `hygiene-check`, `backend-build-test`, `frontend-lint-build` — all green on the current tree.
result: blocked
blocked_by: other
reason: "2026-06-14 re-attempt: remote wired (origin=DnL0104/BelegPilot, now PUBLIC), leaked Anthropic keys scrubbed from history (see SECURITY INCIDENT below), pushed to main → CI pipeline now FIRES and works. After two fix rounds (run 27496467606): Backend build+test ✓ GREEN, Frontend lint+build ✓ GREEN (fixed: quotes, eslint set-state-in-effect→warn, vitest @ alias — commit d0e4766). The ONLY remaining red on the 3 required jobs is Hygiene ✗ — legal-placeholder guard hitting [Name]/[Anschrift]/[PLZ Ort]/[kontakt@taxreader.de] in agb/widerruf/datenschutz. That IS launch gate 6.1/7.5 and is BLOCKED on operator's real legal-entity data (operator confirmed not available yet, 2026-06-14). Separately, gated Heavy-suite ✗ on Playwright E2E (QA-03) — not one of the 3 required jobs; needs its own investigation. NET: pipeline proven; 2/3 required green; 3rd gated on legal data. Re-run after Impressum data lands → expect all 3 green."

> **SECURITY INCIDENT (2026-06-14, handled):** First force-push was blocked by GitHub secret scanning — two live Anthropic API keys (`sk-ant-api03-h0…` in `appsettings.Development.json`, `…8w…` + `h0` in historical `.claude/settings.local.json`) were committed in history. Repo is PUBLIC, so bypass was refused. Remediation: operator rotated/deleted both keys + created a new dev key (2026-06-14); current `appsettings.Development.json` ApiKey emptied (commit 9f7f57c, now reads from `Anthropic__ApiKey` env); all 274 commits rewritten with git-filter-repo redacting both keys to `***REMOVED***` (verified CLEAN — no sk-ant string survives); force-pushed scrubbed history (2c9a1c5→98a23e3). New dev key is NOT in any tracked file.

### 8. README quick-start onboarding (FND-05)
expected: A new developer can follow the top-level `README.md` quick-start (`cp .env.example .env` → fill secrets → `docker compose up --build`) and reach a running app at https://localhost without extra tribal knowledge.
result: pass
note: "FIXED 2026-06-14 (commit 8f94b51, pushed). README Quick Start step 2 now lists REFRESHTOKEN_HASHKEY with generation hint (`openssl rand -base64 32`) and notes the API refuses to boot without it. A fresh dev following the README now fills all four boot-required secrets — no silent api-container boot failure. Original issue: step 2 omitted REFRESHTOKEN_HASHKEY (boot-required per 02-CR-01 fail-fast validator)."

### 9. [Operator] Decide on local build-diag.txt
expected: The ~1.8MB local `build-diag.txt` is either accepted as a local-only artifact or deleted. It's gitignored, never tracked, never reaches CI. Behavioural pass = no tracked `build-diag*.txt`/`*.binlog`.
result: pass
note: "Local build-diag.txt (1.8MB, gitignored, untracked, Apr 15) deleted at operator's choice. Behavioural criterion already held (Test 4: no tracked build-diag*/*.binlog)."

### 10. [Operator] Branch protection on main (SC #1)
expected: GitHub → Settings → Branches → rule for `main` with three required checks (`Hygiene check (no PII / build artifacts)`, `Backend build + test`, `Frontend lint + build`); 0 reviewers; signed-commits OFF; linear-history OFF; admin bypass disallowed. Confirm a PR is BLOCKED until checks pass.
result: blocked
blocked_by: other
reason: "TWO blockers. (1) Branch protection API returns 403 'Upgrade to GitHub Pro or make this repository public' — classic branch protection on a PRIVATE repo requires a paid plan (Pro/Team). (2) Gated on Test 7: CI has never run, so the three required-check job names aren't registered to select. Operator chose 'Block + decide later' (2026-06-14): revisit after choosing GitHub Pro/Team, public repo, or accepting no protection. Affects launch gate SC #1."

### 11. [Operator] Sentry dashboard + alert rules (SC #4)
expected: Sentry EU org created, two projects (`taxreader-api`, `taxreader-web`), two D-15 alert rules (new-error-type 1h cooldown + sustained ≥10 events/min for ≥5min), default "new issues" rule disabled, Email-only delivery, both DSNs recorded in `.env`.
result: blocked
blocked_by: third-party
reason: ".env shows SENTRY_DSN_BACKEND empty, NEXT_PUBLIC_SENTRY_DSN empty, NEXT_PUBLIC_SENTRY_ENABLED=false. Sentry EU org/projects/alert rules not yet created — operator dashboard task, not machine-verifiable from here. Overlaps Test 12 (Sentry DPA) and launch gates."

### 12. [Operator] Anthropic AVV + Sentry DPA signed
expected: Anthropic AVV and Sentry DPA signed and filed before any DSN is flipped to non-empty in production. Compliance prerequisite, not a code artifact.
result: blocked
blocked_by: third-party
reason: "06-AVV-TRACKING.md shows Anthropic DPA = — (pending) and Sentry DPA = — (pending); file status 'Pending operator action — not cleared for commercial launch'. Legal/compliance signing task, not machine-verifiable. = launch gates 6.2/7.4."

## Summary

total: 12
passed: 8
issues: 0
pending: 0
skipped: 0
blocked: 4

## Gaps

- truth: "Cold start (`docker compose down -v && up --build`) brings the full stack up; web image builds cleanly."
  status: failed
  reason: "User reported: web build fails during `npm run build` — Next.js type-check errors on vitest.config.mts (Plugin<any>[] not assignable to PluginOption[], rolldownVersion missing); build worker exits code 1; image build aborts."
  severity: blocker
  test: 1
  root_cause: "tsconfig.json `include` globs (`**/*.mts`, `**/*.tsx`) pull vitest.config.mts and test files into `next build`'s type-check. Next 16.2.2 (rolldown-vite) and vitest ^3.2.6 (rollup-vite) expose divergent Plugin/PluginContextMeta types."
  artifacts:
    - path: "Frontend/tsconfig.json"
      issue: "include globs cover test tooling; no exclude for vitest config / test files"
  missing:
    - "Exclude vitest.config.mts, vitest.setup.ts, test files, and e2e/ from the Next build's TypeScript program"
  status_resolved: "FIXED in session 2026-06-13 — Frontend/tsconfig.json `exclude` extended to [node_modules, vitest.config.mts, vitest.setup.ts, **/*.test.ts, **/*.test.tsx, e2e/**]. `tsc --noEmit` green; cold start re-run passed. Uncommitted — commit before milestone close."

- truth: "A fresh dev follows README quick-start, fills the listed secrets, and reaches a running app at https://localhost without extra tribal knowledge."
  status: failed
  reason: "User reported (confirmed): README step 2 lists only JWT_SECRET/ANTHROPIC_API_KEY/POSTGRES_PASSWORD; .env.example ships REFRESHTOKEN_HASHKEY empty; 02-CR-01 fail-fast validator refuses boot without a valid 32-byte pepper. Fresh dev hits hard api boot failure with no README pointer."
  severity: major
  test: 8
  root_cause: "README.md Quick Start step 2 omits REFRESHTOKEN_HASHKEY (a boot-required secret per 02-CR-01 RefreshTokenOptionsValidator + ValidateOnStart)."
  artifacts:
    - path: "README.md"
      issue: "Quick Start step 2 lists 3 secrets; missing REFRESHTOKEN_HASHKEY (generate: openssl rand -base64 32)"
  missing:
    - "Add REFRESHTOKEN_HASHKEY (with `openssl rand -base64 32`) to README Quick Start step 2 required-secrets list"
