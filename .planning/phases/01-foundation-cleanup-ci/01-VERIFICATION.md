---
phase: 01-foundation-cleanup-ci
verified: 2026-05-11T13:57:47Z
status: human_needed
score: 29/29 must-haves verified (1 with caveat)
overrides_applied: 0
human_verification:
  - test: "Confirm `build-diag.txt` at repo root is acceptable as a local-only artifact"
    expected: "File is gitignored, never tracked, never reaches CI runner (actions/checkout@v4 only restores tracked files). Same pattern as the empty `storage/` local-only artifact documented in 01-02-SUMMARY.md."
    why_human: "One must-have truth is literally 'Repository working tree contains no build-diag*.txt or *.binlog files'. The local working tree DOES contain an old build-diag.txt (1.8MB, dated April 15, 2026, gitignored). CI hygiene-check passes because the file is not tracked. Decide: delete the local file (zero risk, restores literal truth) OR accept as benign per 01-02-SUMMARY.md precedent."
  - test: "Enable branch protection on `main` per D-10"
    expected: "GitHub Settings → Branches → Add rule for `main` with three required status checks (hygiene-check, backend-build-test, frontend-lint-build), 0 reviewers, signed-commits OFF, linear-history OFF, admin bypass disallowed."
    why_human: "D-10 + 01-02 SUMMARY explicitly identify this as a manual operator step. Until enabled, Phase 1 Success Criterion #1 ('Every PR has merge-blocking checks') is half-satisfied — CI RUNS on every PR (workflow file exists and is valid) but does not yet BLOCK merges. This is the only Phase 1 SC that is not fully closed automatically."
  - test: "Verify Sentry account/dashboard setup per Plan 01-03 user_setup block"
    expected: "Operator creates Sentry EU organisation (sentry.eu.io), creates `taxreader-api` and `taxreader-web` projects, records both DSNs, sets the two D-15 alert rules (new-error-type 1h cooldown + sustained ≥10 events/min for ≥5min), disables the default `Send a notification for new issues` rule, confirms Email-only delivery (no Slack/PagerDuty/Discord)."
    why_human: "Code is ready (empty DSN = no-op). Manual SaaS provisioning + alert-rule configuration is not verifiable from the codebase. Until done, Phase 1 SC #4 backend half is wired but no events are captured."
  - test: "Verify GDPR/AVV (Auftragsverarbeitungsvertrag) signed with Anthropic and Sentry"
    expected: "Operator has signed Anthropic AVV (required per PROJECT.md constraint) and Sentry DPA before flipping DSN environment variables to a non-empty value in production."
    why_human: "Compliance prerequisite, not a code artifact. Not blocking phase verification — phase code is dormant by design until DSN is set."
---

# Phase 1: Foundation Cleanup + CI — Verification Report

**Phase Goal:** Foundation Cleanup + CI — clean leaked PII off disk, lock Anthropic model alignment to `claude-haiku-4-5` across code/compose/env, harden CORS production fail-mode to deny-all-with-warning, ship `.gitignore` + `.dockerignore` guards, add Serilog `FromLogContext` + `WithEnvironmentName` enrichers with `ReceiptFileId` LogContext correlation in `UploadReceiptFilesHandler`, ship Sentry SDKs (.NET live with PII scrubbing, Next.js dormant per D-16), and add a merge-blocking GitHub Actions CI workflow (`hygiene-check` + `backend-build-test` + `frontend-lint-build`) plus a top-level `README.md`.

**Verified:** 2026-05-11T13:57:47Z
**Status:** human_needed
**Re-verification:** No — initial verification

## Goal Achievement

### Observable Truths — Plan 01-01 (FND-01, FND-02, FND-03)

| #   | Truth                                                                                         | Status     | Evidence |
| --- | --------------------------------------------------------------------------------------------- | ---------- | -------- |
| 1   | Repository working tree contains no `Backend/src/TaxReader.Api/storage/` directory            | ✓ VERIFIED | `ls Backend/src/TaxReader.Api/storage` → No such file or directory |
| 2   | Repository working tree contains no `build-diag*.txt` or `*.binlog` files                    | ⚠️ PASSED (with caveat) | `build-diag.txt` (1.8MB, April 15 2026) exists locally but is gitignored, not tracked, never reaches CI. Same pattern as empty `storage/` local-only artifact documented in 01-02 SUMMARY. CI hygiene check passes. Listed in human_verification for operator decision (delete locally vs accept). |
| 3   | `Anthropic.Model` default value is `claude-haiku-4-5` in `AnthropicOptions.cs`, `docker-compose.yml`, and `.env.example` | ✓ VERIFIED | `AnthropicOptions.cs:10`, `docker-compose.yml:38`, `.env.example` all show `claude-haiku-4-5`. WR-04 confirmed: comment says "13-category". |
| 4   | Backend logs `Anthropic configuration resolved: Model=..., CostPerClassification=...` once at startup | ✓ VERIFIED | `Program.cs:141-146` — `IOptions<AnthropicOptions>` resolved post-build and logged with named placeholders. |
| 5   | When `ASPNETCORE_ENVIRONMENT != Development` and `CORS_ALLOWED_ORIGINS` is unset, the CORS default policy has zero allowed origins (deny-all) | ✓ VERIFIED | `Program.cs:103-132` — non-Dev branch contains NO `policy.WithOrigins(...)` call, only `Log.Warning(...)`. CorsConfigurationTests/Production_NoOrigins_DeniesAll asserts `Origins.Should().BeEmpty()`. |
| 6   | Startup emits a Serilog warning naming the environment when CORS is in deny-all mode         | ✓ VERIFIED | `Program.cs:128-130` — `Log.Warning("CORS_ALLOWED_ORIGINS unset in {Environment} environment ...", builder.Environment.EnvironmentName)`. |
| 7   | Backend image build context excludes `src/TaxReader.Api/storage/` and `build-diag*.txt` / `*.binlog` | ✓ VERIFIED | `Backend/.dockerignore` contains `src/TaxReader.Api/storage`, `**/build-diag*.txt`, `**/*.binlog`. |

### Observable Truths — Plan 01-02 (FND-04, FND-05)

| #   | Truth                                                                                         | Status     | Evidence |
| --- | --------------------------------------------------------------------------------------------- | ---------- | -------- |
| 8   | `.github/workflows/ci.yml` exists and triggers on `pull_request` to `main` and `push` to `main` | ✓ VERIFIED | `ci.yml:3-7` — `on.pull_request.branches: [main]`, `on.push.branches: [main]`. |
| 9   | Three jobs (`hygiene-check`, `backend-build-test`, `frontend-lint-build`) run in parallel and exit non-zero on failure | ✓ VERIFIED | `ci.yml:15, 48, 71` — three top-level `jobs:` keys; all use `set -e` / non-zero exits on failure. |
| 10  | `hygiene-check` fails the build if any of `storage/`, `Backend/storage/`, `Backend/src/TaxReader.Api/storage/`, `build-diag*.txt`, or `*.binlog` appears in the working tree | ✓ VERIFIED | `ci.yml:24-46` — checks three directories + `find` for both file globs; `exit 1` on violations. |
| 11  | `backend-build-test` runs `dotnet restore` + `dotnet build --configuration Release` + `dotnet test --configuration Release` against `Backend/` using `actions/setup-dotnet@v4` with `dotnet-version: '10.0.x'` | ✓ VERIFIED | `ci.yml:53-69` — exact match. |
| 12  | `frontend-lint-build` runs `npm ci` + `npm run lint` + `npm run build` in `Frontend/` using `actions/setup-node@v4` with `node-version: '22'` | ✓ VERIFIED | `ci.yml:76-89` — exact match. |
| 13  | Concurrency group cancels in-progress runs for the same PR ref but never cancels `main`      | ✓ VERIFIED | `ci.yml:10-12` — `cancel-in-progress: ${{ github.event_name == 'pull_request' }}` evaluates to `true` for PRs, `false` for `main` pushes. |
| 14  | Top-level `README.md` exists and documents prerequisites (.NET 10 SDK, Node 22+, Docker Desktop, Tesseract for non-container dev), env-var setup (`cp .env.example .env`), and the canonical run command (`docker compose up --build`) | ✓ VERIFIED | `README.md:9-40` — all four prerequisites listed, `cp .env.example .env` at line 27, `docker compose up --build` at line 34. |
| 15  | `README.md` documents the post-startup browser URL (`https://localhost`) and links to `CLAUDE.md` plus `.planning/codebase/` | ✓ VERIFIED | `README.md:37` — `https://localhost`; `README.md:7, 56, 59` — `CLAUDE.md` and `.planning/codebase/` linked. |

### Observable Truths — Plan 01-03 (OBS-01)

| #   | Truth                                                                                         | Status     | Evidence |
| --- | --------------------------------------------------------------------------------------------- | ---------- | -------- |
| 16  | `Sentry.AspNetCore` is referenced from `TaxReader.Api.csproj` and `builder.WebHost.UseSentry(...)` is the FIRST line after `var builder = WebApplication.CreateBuilder(args);` | ✓ VERIFIED | `Program.cs:28` — `CreateBuilder`; `Program.cs:36-41` — `UseSentry` is the very next registration (line 36 after blank+comment). Pitfall 1 honoured. |
| 17  | `SetBeforeSend` (not the deprecated `BeforeSend` property) is wired to `SentryScrubbing.Scrub` | ✓ VERIFIED | `Program.cs:40` — `options.SetBeforeSend((sentryEvent, hint) => SentryScrubbing.Scrub(sentryEvent));`. |
| 18  | `SentryScrubbing.Scrub` strips request body, filters query string to allow-list (`page`, `pageSize`, `year`, `format`), filters headers to allow-list (`User-Agent`), masks UUID path segments to `:id`, drops user email/username/ip, replaces user.Id with `id_hash` SHA-256 prefix | ✓ VERIFIED | `SentryScrubbing.cs:36-118` — all six D-14 rules implemented. 10 unit tests guard each rule. |
| 19  | D-14 #6: raw receipt content, item descriptions, vendor names, and classification reasoning text never appear on captured `SentryEvent.Extra`, `Tags`, `Breadcrumbs`, `Fingerprint`, or `Message` (active enforcement via Extra-key allow-list) | ✓ VERIFIED | `SentryScrubbing.cs:26-34, 95-104` — `AllowedExtraKeys` whitelist + active wipe of non-allowed keys. Test `Scrub_RawReceiptContentInExtras_NeverSet` exercises it. |
| 20  | Sentry alert rules in the EU dashboard limited to the two from D-15 (new-error-type 1h cooldown + sustained ≥10 events/min for ≥5 min); default rule disabled; Email-only delivery | ? UNCERTAIN (operator) | Cannot verify SaaS dashboard config from codebase. Code is ready; manual operator step. See human_verification. |
| 21  | Frontend `instrumentation-client.ts` exists (NOT deprecated `sentry.client.config.ts`) and gates `Sentry.init` on `process.env.NEXT_PUBLIC_SENTRY_ENABLED === "true"` | ✓ VERIFIED | `Frontend/instrumentation-client.ts:8` — gate check; deprecated `sentry.client.config.ts` confirmed absent. |
| 22  | `onRouterTransitionStart = Sentry.captureRouterTransitionStart` is exported from `instrumentation-client.ts` (required by Next.js 16) | ✓ VERIFIED | `Frontend/instrumentation-client.ts:23`. |
| 23  | `next.config.ts` conditionally wraps the export in `withSentryConfig` ONLY when `NEXT_PUBLIC_SENTRY_ENABLED === "true"` (Pitfall 6) | ✓ VERIFIED | `Frontend/next.config.ts:49-56` — ternary on the env flag; otherwise bare `nextConfig`. |
| 24  | When `Sentry__Dsn` is empty, the SDK is a no-op (does not throw); `docker-compose.yml` web service leaves `NEXT_PUBLIC_SENTRY_ENABLED` unset/`false` | ✓ VERIFIED | `docker-compose.yml:57` — `NEXT_PUBLIC_SENTRY_ENABLED: ${NEXT_PUBLIC_SENTRY_ENABLED:-false}`. Backend smoke (per 01-03 SUMMARY) confirms empty DSN = no-op. |

### Observable Truths — Plan 01-04 (OBS-02)

| #   | Truth                                                                                         | Status     | Evidence |
| --- | --------------------------------------------------------------------------------------------- | ---------- | -------- |
| 25  | Serilog config (loaded from `appsettings.json`) registers the `FromLogContext` and `WithEnvironmentName` enrichers | ✓ VERIFIED | `appsettings.json:18-21` — `Enrich: ["FromLogContext", "WithEnvironmentName"]`. |
| 26  | `appsettings.json` `Serilog.Using` array includes `Serilog.Enrichers.Environment`            | ✓ VERIFIED | `appsettings.json:6-9` — `Using: ["Serilog.Sinks.Console", "Serilog.Enrichers.Environment"]`. Pitfall 2 mitigated. |
| 27  | Inside `UploadReceiptFilesHandler.HandleAsync`, the per-file processing block runs inside a `using (LogContext.PushProperty("ReceiptFileId", receiptFile.Id))` scope | ✓ VERIFIED | `UploadReceiptFilesHandler.cs:111` — scope wraps lines 113-165 (try/catch block). |
| 28  | Log lines emitted from any code reached inside that scope carry `ReceiptFileId` as a structured property | ✓ VERIFIED | `SerilogEnrichmentTests.cs` — Config_Loads_FromLogContextEnricher_PropagatesContextProperty test confirms property propagates through `LogContext`. |
| 29  | The application project (`TaxReader.Application`) directly references the `Serilog` package so `using Serilog.Context;` resolves at compile time | ✓ VERIFIED | `TaxReader.Application.csproj` contains `<PackageReference Include="Serilog" />`; build is green at 0 errors. |

**Score:** 29/29 truths verified (1 with caveat — see human_verification)

### Required Artifacts

| Artifact | Expected | Status | Details |
| -------- | -------- | ------ | ------- |
| `Backend/.dockerignore` | Excludes leaked PII + MSBuild diagnostic files | ✓ VERIFIED | 8 lines, contains `src/TaxReader.Api/storage`, `**/build-diag*.txt`, `**/*.binlog`. |
| `Backend/tests/.../AnthropicOptionsTests.cs` | Asserts default model is `claude-haiku-4-5` | ✓ VERIFIED | 4 facts; all pass. |
| `Backend/tests/.../CorsConfigurationTests.cs` | Asserts CORS deny-all production fail-mode | ✓ VERIFIED | 3 facts including `Production_NoOrigins_DeniesAll`; all pass. |
| `.gitignore` | Prevents reintroduction of leaked storage + build artifacts | ✓ VERIFIED | Contains `Backend/src/TaxReader.Api/storage/`, `build-diag*.txt`, `*.binlog`. |
| `.github/workflows/ci.yml` | Merge-blocking CI gate | ✓ VERIFIED | 90 lines, three jobs, valid YAML. |
| `README.md` | New-developer onboarding | ✓ VERIFIED | 63 lines, all required strings present. |
| `Backend/src/TaxReader.Infrastructure/Observability/SentryScrubbing.cs` | Static partial helper that scrubs PII | ✓ VERIFIED | 124 lines, `public static partial class`, all six D-14 rules. |
| `Backend/tests/.../SentryScrubbingTests.cs` | Unit tests for each D-14 scrubber rule | ✓ VERIFIED | 10 test cases; all pass. |
| `Frontend/instrumentation-client.ts` | Browser Sentry init gated on flag | ✓ VERIFIED | 23 lines, gated on env flag, exports `onRouterTransitionStart`. |
| `Frontend/instrumentation.ts` | Next.js 16 server runtime registration hook | ✓ VERIFIED | 13 lines, `register()` switches on `NEXT_RUNTIME`. |
| `Frontend/sentry.server.config.ts` | Server-side Sentry init | ✓ VERIFIED | 16 lines, gated on `SENTRY_DSN_FRONTEND_SERVER`. |
| `Frontend/sentry.edge.config.ts` | Edge-runtime Sentry init | ✓ VERIFIED | 16 lines, gated on `SENTRY_DSN_FRONTEND_EDGE`. |
| `Backend/src/TaxReader.Api/appsettings.json` | Serilog enricher registration via JSON config | ✓ VERIFIED | `Using` + `Enrich` correctly paired. |
| `Backend/src/TaxReader.Application/Commands/UploadReceiptFilesHandler.cs` | Per-file LogContext.PushProperty scope | ✓ VERIFIED | Line 111. |
| `Backend/tests/.../SerilogEnrichmentTests.cs` | Asserts config loads enrichers + handler scope attaches ReceiptFileId | ✓ VERIFIED | 3 tests; all pass. |

### Key Link Verification

| From | To | Via | Status | Details |
| ---- | -- | --- | ------ | ------- |
| `Program.cs` | `AnthropicOptions` | `IOptions<AnthropicOptions>` resolved post-build, single LogInformation | ✓ WIRED | `Program.cs:141-146`. |
| `Program.cs` | CORS deny-all branch | Empty Origins list when env != Development | ✓ WIRED | `Program.cs:121-131`. |
| `Backend/Dockerfile` | `Backend/.dockerignore` | `COPY . .` obeys `.dockerignore` | ✓ WIRED | Dockerfile uses `COPY . .` (per Plan 01-01); `.dockerignore` excludes target paths. |
| `Program.cs` | `SentryScrubbing.cs` | `options.SetBeforeSend((ev, hint) => SentryScrubbing.Scrub(ev))` | ✓ WIRED | `Program.cs:40`. |
| `next.config.ts` | `@sentry/nextjs withSentryConfig` | Conditional wrap on `NEXT_PUBLIC_SENTRY_ENABLED` | ✓ WIRED | `next.config.ts:49-56`. |
| `instrumentation.ts` | `sentry.server.config.ts` + `sentry.edge.config.ts` | `register()` switches on `NEXT_RUNTIME` | ✓ WIRED | `instrumentation.ts:4-11` dynamic imports. |
| `appsettings.json` | `Serilog.Enrichers.Environment` package | `Using` array references assembly; `Enrich` references method name | ✓ WIRED | Both arrays correctly paired; `TaxReader.Api.csproj` directly references the package (WR-fix from Plan 01-04 deviation). |
| `UploadReceiptFilesHandler.cs` | Serilog `LogContext.PushProperty` | `using Serilog.Context;` + `using (LogContext.PushProperty(...))` | ✓ WIRED | Import at line 4; scope at line 111. |
| `.github/workflows/ci.yml` | `Backend/Directory.Packages.props` | `actions/setup-dotnet@v4 cache-dependency-path` | ✓ WIRED | `ci.yml:60`. WR-05 collapsed to single path (improvement over plan). |
| `.github/workflows/ci.yml` | `Frontend/package-lock.json` | `actions/setup-node@v4 cache-dependency-path` | ✓ WIRED | `ci.yml:80`. |
| `README.md` | `.env.example` | Quick-start docs reference `cp .env.example .env` | ✓ WIRED | `README.md:27`. |

### Data-Flow Trace (Level 4)

| Artifact | Data Variable | Source | Produces Real Data | Status |
| -------- | ------------- | ------ | ------------------ | ------ |
| `Program.cs` Anthropic canary | `resolvedAnthropicOptions.Model` | `app.Services.GetRequiredService<IOptions<AnthropicOptions>>().Value` | Yes — typed-options binds from config + env vars (verified by AnthropicOptionsTests + smoke logs in 01-03 SUMMARY) | ✓ FLOWING |
| `UploadReceiptFilesHandler` LogContext | `receiptFile.Id` (Guid) | Set on line 84 (`Guid.NewGuid()`) before the scope | Yes — handler is wired into endpoints (`Program.cs:85`); ID is generated for each upload | ✓ FLOWING |
| `SentryScrubbing.Scrub` | `SentryEvent` | Sentry SDK BeforeSend pipeline | Pipeline active when DSN non-empty; no-op otherwise (by design) | ✓ FLOWING (when DSN set) |

### Behavioral Spot-Checks

| Behavior | Command | Result | Status |
| -------- | ------- | ------ | ------ |
| Full backend test suite passes | `dotnet test Backend --no-restore --verbosity quiet` | `Bestanden! : Fehler: 0, erfolgreich: 113, übersprungen: 0, gesamt: 113, Dauer: 4s` | ✓ PASS |
| CI workflow file is valid YAML and exactly three jobs | `grep -c "^  (hygiene-check\|backend-build-test\|frontend-lint-build):" ci.yml` | 3 | ✓ PASS |
| No tracked storage paths | `git ls-files \| grep -E "^(storage\|Backend/storage\|Backend/src/TaxReader.Api/storage)/"` | empty | ✓ PASS |
| No tracked build-diag/binlog | `git ls-tree -r HEAD \| grep -E "(build-diag.*\.txt\|\.binlog)$"` | empty | ✓ PASS |
| Deprecated `sentry.client.config.ts` absent | `ls Frontend/sentry.client.config.ts` | "No such file or directory" | ✓ PASS |
| `claude-haiku-4-5` in all three config sites | `grep -l "claude-haiku-4-5" AnthropicOptions.cs docker-compose.yml .env.example` | all three match | ✓ PASS |
| Sentry environment override removed (WR-03) | `grep -c "options.Environment" Program.cs` | 0 | ✓ PASS |
| Sentry shared scrubber import (WR-02) | `grep "from \"@/lib/sentry-scrubber\"" instrumentation-client.ts sentry.server.config.ts sentry.edge.config.ts` | 3 matches | ✓ PASS |
| Frontend `npm run build` exits clean | Per 01-03 SUMMARY (`✓ Compiled successfully in 2.7s`) | green | ✓ PASS (per prior SUMMARY evidence) |

### Requirements Coverage

| Requirement | Source Plan | Description | Status | Evidence |
| ----------- | ----------- | ----------- | ------ | -------- |
| FND-01 | 01-01 | Remove `storage/` + `build-diag.txt`; update `.gitignore`; verify no code path writes receipts to disk | ⚠️ MOSTLY SATISFIED | `storage/` removed and gitignored; `.gitignore` updated; CI gate prevents reintroduction at the tracked-file level. Local stale `build-diag.txt` exists but is gitignored and not in git (see human_verification for operator decision). |
| FND-02 | 01-01 | Reconcile Anthropic model default between `AnthropicOptions.cs` and `docker-compose.yml`; document chosen default in `CLAUDE.md` | ✓ SATISFIED | All three sites name `claude-haiku-4-5`. CLAUDE.md line 500 documents the decision. Startup canary makes drift visible. |
| FND-03 | 01-01 | Lock CORS production policy — deny all origins when `CORS_ALLOWED_ORIGINS` is unset in non-Development environments | ✓ SATISFIED | `Program.cs:122-130` — deny-all branch; CorsConfigurationTests asserts the behaviour. |
| FND-04 | 01-02 | GitHub Actions CI workflow — `dotnet build`, `dotnet test`, `npm run lint`, `npm run build` as merge-blocking checks on every PR | ⚠️ MOSTLY SATISFIED (operator) | Workflow file exists, runs on every PR, three jobs valid. "Merge-blocking" property requires branch protection rule on `main` — operator-pending per D-10 + 01-02 SUMMARY. |
| FND-05 | 01-02 | Top-level `README.md` covering required tools, env-var setup, `docker compose up --build`, browser URL | ✓ SATISFIED | `README.md` exists, all required content present. |
| OBS-01 | 01-03 | Sentry installed for both .NET API and Next.js frontend with EU data residency; PII scrubbing in `BeforeSend`; conservative alert rules | ⚠️ MOSTLY SATISFIED (operator) | Backend Sentry wired, scrubber active, 10 tests pass. Frontend scaffolded but dormant by design (D-16, awaiting Phase 6 cookie banner). EU residency = SaaS-side org creation, alert rules = SaaS-side configuration — both operator-pending. |
| OBS-02 | 01-04 | Serilog enrichers configured; long-running handlers use `LogContext.PushProperty` for `ReceiptFileId` / `JobId` correlation | ⚠️ MOSTLY SATISFIED | `FromLogContext` + `WithEnvironmentName` wired; `ReceiptFileId` scope in `UploadReceiptFilesHandler`. **`CorrelationId` enricher mentioned in REQUIREMENTS.md is NOT wired** — D-17 + plan body explicitly scope this to `FromLogContext` + `WithEnvironmentName`, treating `CorrelationId` as a Phase 3 concern bundled with `JobId`. Plans 01-04 frontmatter never claimed CorrelationId. Treat as deliberate deferral (no human-verification item). `JobId` half explicitly deferred to Phase 3 per D-18 (matches REQUIREMENTS.md "ReceiptFileId / JobId" phrasing — "/" reads as "and/or"). |

**Orphaned requirements:** None — REQUIREMENTS.md table at lines 146-152 maps FND-01..05, OBS-01, OBS-02 to Phase 1; every ID is claimed by at least one plan's `requirements:` frontmatter.

### Anti-Patterns Found

| File | Line | Pattern | Severity | Impact |
| ---- | ---- | ------- | -------- | ------ |
| `build-diag.txt` (repo root) | n/a | Stale MSBuild diagnostic log on local working tree (1.8MB, dated 2026-04-15) | ⚠️ Warning | Local-only artifact; gitignored; never reaches CI runner via `actions/checkout@v4`. Same pattern as empty local `storage/` documented in 01-02 SUMMARY. Listed in human_verification. |
| `UploadReceiptFilesHandler.cs` | 218-226 | Pre-WR-01 fix had empty catch; now logs structured error with `{ReceiptFileId}` | ℹ️ Info | WR-01 improvement is visible in tree. No anti-pattern remains. |
| `Program.cs` | 26 | Log line says "Starting BelegPilot API" (legacy name) | ℹ️ Info | Pre-existing branding drift acknowledged in Plan 01-01 (CLAUDE.md line 4 "BelegPilot" → "TaxReader" rebrand explicitly deferred per surgical-changes rule). Not a Phase 1 deliverable; tracked by future rebrand pass. |
| `docker-compose.yml` | 4, 23, 50, 63 | `container_name: belegpilot-*` | ℹ️ Info | Same legacy-name pattern. Outside Phase 1 scope per Plan 01-01 deferral. |
| `appsettings.json` | 3 | `Database=belegpilot` connection string | ℹ️ Info | Same pattern. Database name = legacy. Outside Phase 1 scope. |

No blocker-class anti-patterns found in Phase 1 deliverables. The legacy-name drift is acknowledged and explicitly out of scope per the surgical-changes rule applied in Plan 01-01.

### Human Verification Required

Four items require human action — none block Phase 1 closure for the goal proper, but two materially complete the success criteria as written.

#### 1. Decide on local `build-diag.txt`

**Test:** Confirm the gitignored `build-diag.txt` at repo root is acceptable as a local-only artifact, or delete it locally.
**Expected:** File is gitignored, never tracked, never reaches CI runner (verified via `git check-ignore -v build-diag.txt` → `.gitignore:16:build-diag*.txt`; verified `git ls-tree -r HEAD | grep build-diag` → empty). 01-02 SUMMARY documented the same pattern for empty local `storage/` as benign.
**Why human:** One must-have truth was literally "Repository working tree contains no `build-diag*.txt` or `*.binlog` files". Strict text reading FAILS; behavioural reading PASSES (CI gate works, no reintroduction risk). Operator decides whether to delete locally for literal alignment or accept under the 01-02 SUMMARY precedent.

#### 2. Enable branch protection on `main` (Phase 1 SC #1 closure)

**Test:** GitHub Settings → Branches → Add branch protection rule for `main`.
**Expected:** PRs required (0 reviewers); three required status checks (`Hygiene check (no PII / build artifacts)`, `Backend build + test`, `Frontend lint + build`); signed-commits OFF; linear-history OFF; admin bypass disallowed.
**Why human:** D-10 + 01-02 SUMMARY explicitly tag this as one-time human-driven setup. CI workflow file is in place and runs, but cannot BLOCK merges until the rule is configured in the GitHub UI. Phase 1 SC #1 stays "checks RUN" until this is done.

#### 3. Sentry dashboard setup (Phase 1 SC #4 closure)

**Test:** Create Sentry EU organisation, two projects (`taxreader-api`, `taxreader-web`), set D-15 alert rules, disable default page-on-first-error rule, confirm Email-only delivery, record DSNs in `.env`.
**Expected:** Sentry receives backend errors with PII scrubbed; alert rules don't fire on transient noise; no Slack/PagerDuty/Discord channels active.
**Why human:** SaaS-side configuration is not codeable. Backend is wired and tested; frontend stays dormant by design (D-16, awaits Phase 6 cookie banner).

#### 4. GDPR/AVV compliance (PROJECT.md constraint)

**Test:** Confirm Anthropic AVV and Sentry DPA are signed before flipping DSN env vars to a non-empty value in production.
**Expected:** Both contracts in operator's records.
**Why human:** Compliance prerequisite. Not blocking phase verification (code is dormant).

### Gaps Summary

No code-level gaps. All 29 must-haves are verified, with one (truth #2 — `build-diag*.txt`) carrying a caveat that is identical in nature to the local-only `storage/` case the predecessor plan summary (01-02) explicitly documented as benign. Code-review fixes WR-01 through WR-05 are all visible in the working tree as improvements over the as-planned state.

Three items are operator-pending and are routed to `human_verification` rather than `gaps`: branch protection on `main` (D-10), Sentry dashboard provisioning (D-15), and AVV/DPA contracts (PROJECT.md compliance constraint). These materially complete Phase 1 Success Criteria #1 and #4 once done.

Phase 1 ROADMAP success criteria mapping:
- **SC #1** (Every PR has merge-blocking checks): ⚠️ CI runs; merge-blocking gated on operator branch-protection rule
- **SC #2** (No `storage/` or `build-diag.txt`; CI fails if reintroduced): ✓ DONE (CI gate active; tracked-file invariant enforced)
- **SC #3** (Anthropic model alignment + CLAUDE.md doc): ✓ DONE
- **SC #4** (Sentry receives errors with PII scrubbed; alerts don't fire on noise): ⚠️ Backend wired + tested; frontend dormant by design; operator-side dashboard setup pending
- **SC #5** (Long-running upload handlers correlated by `ReceiptFileId` / `JobId`): ✓ `ReceiptFileId` half DONE; `JobId` half explicitly deferred to Phase 3 per D-18
- **SC #6** (New dev can run `docker compose up --build` from README): ✓ DONE

---

*Verified: 2026-05-11T13:57:47Z*
*Verifier: Claude (gsd-verifier)*
