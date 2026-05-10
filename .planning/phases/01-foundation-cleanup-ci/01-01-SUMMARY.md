---
phase: 01-foundation-cleanup-ci
plan: 01
subsystem: infra
tags: [hygiene, gitignore, dockerignore, cors, anthropic, serilog, xunit, webapplicationfactory]

requires:
  - phase: none
    provides: starting point — untracked working tree with leaked PII storage and code/compose model drift
provides:
  - Leaked PII removed from disk; .gitignore + Backend/.dockerignore prevent reintroduction
  - Anthropic model default locked to claude-haiku-4-5 across code, compose, env, and CLAUDE.md
  - Startup-log canary surfaces resolved Anthropic model so future drift is immediately visible
  - CORS production fail-mode is deny-all when CORS_ALLOWED_ORIGINS unset (fixed previous localhost:3000 fallback bug)
  - Microsoft.AspNetCore.Mvc.Testing 10.0.4 wired into the test project for WebApplicationFactory<Program>-based integration tests
  - 7 new unit/integration tests guarding the model default and the CORS policy
affects: [01-02, 01-03, 01-04, 02-*, 03-*]

tech-stack:
  added:
    - Microsoft.AspNetCore.Mvc.Testing 10.0.4 (Backend test project)
  patterns:
    - WebApplicationFactory<Program> integration test rig (first integration-test pattern in repo)
    - Startup-log canary for typed-options drift detection

key-files:
  created:
    - Backend/.dockerignore
    - Backend/tests/TaxReader.UnitTests/Configuration/AnthropicOptionsTests.cs
    - Backend/tests/TaxReader.UnitTests/Cors/CorsConfigurationTests.cs
    - .planning/phases/01-foundation-cleanup-ci/01-01-SUMMARY.md
  modified:
    - .gitignore
    - .env.example
    - docker-compose.yml
    - CLAUDE.md
    - Backend/Directory.Packages.props
    - Backend/tests/TaxReader.UnitTests/TaxReader.UnitTests.csproj
    - Backend/src/TaxReader.Api/Program.cs

key-decisions:
  - "Source-of-truth for Anthropic model is Backend/src/TaxReader.Infrastructure/Configuration/AnthropicOptions.cs (D-02); compose + env carry matching defaults so Docker still works with empty .env"
  - "CORS non-Development + unset CORS_ALLOWED_ORIGINS → empty Origins (deny-all) + Serilog warning naming the environment (D-07); same-origin Caddy traffic is unaffected"
  - "Backend/.dockerignore added because Backend/Dockerfile uses COPY . . — without it, leaked PDFs would land in the runtime container layer even after disk deletion"
  - "Test project gains a ProjectReference to TaxReader.Api so WebApplicationFactory<Program> can boot the host; this is the first cross-layer wiring of that type in the repo"

patterns-established:
  - "WebApplicationFactory<Program> + UseSetting overrides: integration tests boot the real host with test-only secrets/connection strings, then read resolved IOptions<CorsOptions> from the service provider"
  - "Startup-log canary: read IOptions<T>.Value post-builder.Build() and emit a structured LogInformation with named placeholders so drift between code defaults and env overrides is logged at every boot"

requirements-completed: [FND-01, FND-02, FND-03]

duration: 5min
completed: 2026-05-10
---

# Phase 1 Plan 01: Hygiene + Anthropic Alignment + CORS Deny-All Summary

**Leaked PII purged from disk and Docker context; Anthropic model default unified on `claude-haiku-4-5` with a startup-log canary; CORS production fail-mode flipped from `localhost:3000` fallback to deny-all; 7 new tests guard each invariant.**

## Performance

- **Duration:** 5 min
- **Started:** 2026-05-10T10:30:18Z
- **Completed:** 2026-05-10T10:35:54Z
- **Tasks:** 3
- **Files modified:** 11 (8 source/config + 3 new test/dockerignore files)

## Accomplishments

- **Hygiene (FND-01):** Removed `Backend/src/TaxReader.Api/storage/` (untracked PII PDFs) from disk; appended `.gitignore` rules for `Backend/src/TaxReader.Api/storage/`, `build-diag*.txt`, `*.binlog`; added `Backend/.dockerignore` so the runtime image cannot embed leaked content even after a developer recreates `storage/` locally.
- **Anthropic alignment (FND-02):** Updated `docker-compose.yml` and `.env.example` to use `claude-haiku-4-5` (matching `AnthropicOptions.cs`); added a paragraph to `CLAUDE.md` documenting the model decision and the source-of-truth file; inserted a structured-log line in `Program.cs` immediately after `builder.Build()` that resolves `IOptions<AnthropicOptions>` and reports `Model=...` and `CostPerClassification=...` once at startup.
- **CORS deny-all (FND-03):** Replaced the buggy non-Development fallback (`policy.WithOrigins("http://localhost:3000")` outside Dev) with an empty-Origins default policy plus a `Log.Warning("CORS_ALLOWED_ORIGINS unset in {Environment} environment ...", builder.Environment.EnvironmentName)`. Same-origin Caddy traffic (which never sends an `Origin` header) is unaffected; cross-origin browser traffic now fails closed.
- **Test coverage:** 7 new tests (4 `AnthropicOptionsTests` + 3 `CorsConfigurationTests`) lock in each invariant; full backend suite goes from 93 → 100 tests, all green.

## Task Commits

1. **Task 1 — Delete leaked PII + add hygiene .gitignore + .dockerignore** — `5c6ca31` (chore)
2. **Task 2 — Lock Anthropic model + startup canary + Wave 0 unit test** — `23fbac3` (feat)
3. **Task 3 — CORS deny-all + integration tests** — `708334f` (fix)

_Note: Tasks were `tdd="true"` but in all three cases the "RED" phase was either a filesystem precondition check (Task 1: directory present, gitignore missing) or a test-file-doesn't-yet-compile state (Tasks 2, 3). For Task 2, the `AnthropicOptions` source default was already correct per the plan's explicit design — the test serves as a regression guard, not a feature-driver. This is documented in the plan (line 103) and in the planning ROADMAP._

## Files Created

- `Backend/.dockerignore` — 8 lines; excludes `bin`, `obj`, `src/TaxReader.Api/storage`, `**/build-diag*.txt`, `**/*.binlog`, `.git`, `.dockerignore`, `Dockerfile` from the Docker build context
- `Backend/tests/TaxReader.UnitTests/Configuration/AnthropicOptionsTests.cs` — 4 facts asserting `Model = "claude-haiku-4-5"`, `CostPerClassification = 1`, `ApiKey = null`, `SectionName = "Anthropic"`
- `Backend/tests/TaxReader.UnitTests/Cors/CorsConfigurationTests.cs` — 3 facts using `WebApplicationFactory<Program>` to assert deny-all in Production-no-origins, localhost:3000 in Dev-no-origins, and pass-through in Production-with-origins

## Files Modified

- `.gitignore` — added 5 lines (3 new rules + 2 blank-line spacers); existing 6 rules preserved
- `.env.example` — line 19 changed from `claude-sonnet-4-5` to `claude-haiku-4-5`
- `docker-compose.yml` — line 38 changed from `claude-sonnet-4-5` to `claude-haiku-4-5`; `Anthropic__MaxTokens` (line 39) deliberately untouched per RESEARCH.md A7 (separate hygiene concern)
- `CLAUDE.md` — appended paragraph after the `## Project` constraints block documenting the model decision; the unrelated "BelegPilot" → "TaxReader" rebrand drift on line 4 deliberately untouched (surgical-changes rule + plan deferral)
- `Backend/Directory.Packages.props` — appended `<PackageVersion Include="Microsoft.AspNetCore.Mvc.Testing" Version="10.0.4" />`
- `Backend/tests/TaxReader.UnitTests/TaxReader.UnitTests.csproj` — appended `<PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" />` and `<ProjectReference Include="..\..\src\TaxReader.Api\TaxReader.Api.csproj" />`
- `Backend/src/TaxReader.Api/Program.cs` — inserted 7-line block after `builder.Build()` for the Anthropic startup-log canary; replaced 26-line CORS block with the 31-line deny-all variant (net +12 lines, no buggy `localhost:3000` non-Dev fallback)

## Decisions Made

None beyond what the plan already specified. All three plan-level decisions (D-02 source-of-truth + canary; D-04/D-05 hygiene rules; D-07 CORS deny-all) executed verbatim.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 — Blocking] Added missing `Microsoft.AspNetCore.Hosting` and `Microsoft.Extensions.Hosting` using directives to `CorsConfigurationTests.cs`**

- **Found during:** Task 3 (after writing the test file per the plan's verbatim shape, the build failed with `error CS1061: 'IWebHostBuilder' enthält keine Definition für 'UseEnvironment'`)
- **Issue:** The plan's test-file template included `WithWebHostBuilder(builder => { builder.UseEnvironment(environment); ... })` but the using-directive list provided in the plan (lines 427-434 of `01-01-PLAN.md`) omitted `Microsoft.AspNetCore.Hosting` (where `IWebHostBuilder.UseEnvironment` extension method lives) and `Microsoft.Extensions.Hosting` (host-builder API surface)
- **Fix:** Added two `using` lines to `Backend/tests/TaxReader.UnitTests/Cors/CorsConfigurationTests.cs`:
  ```csharp
  using Microsoft.AspNetCore.Hosting;
  using Microsoft.Extensions.Hosting;
  ```
- **Files modified:** `Backend/tests/TaxReader.UnitTests/Cors/CorsConfigurationTests.cs`
- **Verification:** `dotnet build Backend` → 0 errors / 0 warnings; `dotnet test Backend --filter "FullyQualifiedName~CorsConfigurationTests"` → 3/3 pass
- **Committed in:** `708334f` (Task 3 commit, included from the start since the build never reached an intermediate broken-staged state)

**2. [Plan Edit 4a was conditional and not needed] Did NOT add `public partial class Program;` declaration to `Program.cs`**

- **Found during:** Task 3 build verification
- **Issue:** Plan Edit 4a (line 506) said to add `public partial class Program;` to the bottom of `Program.cs` "ONLY if `dotnet build Backend` fails on missing `Program` symbol"
- **Outcome:** With the `ProjectReference` to `TaxReader.Api` added, `Program` was directly visible to `WebApplicationFactory<Program>` (modern .NET top-level statements expose `Program` as `internal partial`, and the test project already had `Microsoft.NET.Test.Sdk` which generates `InternalsVisibleTo` shims). The conditional declaration was therefore unnecessary.
- **Files modified:** none (decision to *not* add was the correct path)

---

**Total deviations:** 1 auto-fixed (1 blocking)
**Impact on plan:** The missing `using` was a transcription gap in the plan template, not a structural change. Plan Edit 4a's conditional declaration was correctly conditional and the condition was not met — no action needed. No scope creep.

## Issues Encountered

None beyond the deviation above. The plan's task ordering (config → tests → CORS code) interacted cleanly: build failures landed in the test file before they could reach `Program.cs`.

## Verification Results

```
=== Hygiene ===
OK no api-storage
OK gitignore api-storage
OK gitignore build-diag
OK dockerignore exists

=== Model alignment ===
OK compose haiku
OK env haiku
OK CLAUDE.md haiku
OK startup-log

=== CORS ===
OK deny-all warning

=== Tests ===
Bestanden!   : Fehler:     0, erfolgreich:     7, übersprungen:     0, gesamt:     7, Dauer: 5 s

=== Full suite (regression check) ===
Bestanden!   : Fehler:     0, erfolgreich:   100, übersprungen:     0, gesamt:   100, Dauer: 4 s
```

All 7 must_haves.truths, all 4 must_haves.artifacts, all 3 key_links, and all <success_criteria> in the plan body — verified.

## TDD Gate Compliance

Plan-level type is `execute`, not `tdd`. Per-task `tdd="true"` markers were applied at task granularity. The git log shows the full sequence as three single-commit tasks (each task includes both test additions and the implementation):

- `5c6ca31` chore(01-01): hygiene (no test infrastructure changes — pure filesystem + .gitignore + .dockerignore)
- `23fbac3` feat(01-01): Anthropic alignment (4 new AnthropicOptionsTests + Program.cs/compose/env/CLAUDE.md edits in one atomic commit)
- `708334f` fix(01-01): CORS deny-all (3 new CorsConfigurationTests + Program.cs CORS replacement + package wiring in one atomic commit)

Each test was written from the plan's verbatim shape *before* the related implementation edit (within the same task), so the tests function as the goal/gate per the plan's `tdd="true"` intent. The atomic per-task commit format reflects "task = test + impl together" rather than splitting the RED/GREEN/REFACTOR into separate commits, which is consistent with execute-plan.md's atomic-commit protocol when no plan-level `type: tdd` gate is set.

## User Setup Required

None — no external services touched in this plan.

## Next Phase Readiness

- **Wave 2 (Plan 01-04 — Serilog enrichers):** Unblocked. Will modify `Backend/Directory.Packages.props` again (append `Serilog.Enrichers.Environment`) and `Program.cs` (Serilog config). The new `Microsoft.AspNetCore.Mvc.Testing` is now available for any integration tests in 01-04.
- **Wave 3 (Plan 01-03 — Sentry):** Awaiting 01-04. Will use the same `IOptions<T>` post-build pattern this plan established for the startup-log canary if a Sentry config canary is desired.
- **Wave 4 (Plan 01-02 — CI workflow):** Awaiting 01-03 + 01-04. The hygiene-check job in 01-02 will codify the `.gitignore`/`.dockerignore` invariants this plan landed.

## Self-Check: PASSED

- Created files exist:
  - `Backend/.dockerignore` — FOUND
  - `Backend/tests/TaxReader.UnitTests/Configuration/AnthropicOptionsTests.cs` — FOUND
  - `Backend/tests/TaxReader.UnitTests/Cors/CorsConfigurationTests.cs` — FOUND
- Commit hashes exist in git log:
  - `5c6ca31` — FOUND
  - `23fbac3` — FOUND
  - `708334f` — FOUND
- Test counts: 7/7 new pass, 100/100 total pass

---

*Phase: 01-foundation-cleanup-ci*
*Completed: 2026-05-10*
