---
phase: 01-foundation-cleanup-ci
plan: 04
subsystem: infra
tags: [observability, logging, serilog, log-context, correlation-id, enrichers]

requires:
  - phase: 01-foundation-cleanup-ci/01
    provides: Microsoft.AspNetCore.Mvc.Testing in test rig (unused here, but clean baseline); 100/100 backend test suite green
provides:
  - Serilog enrichers (FromLogContext + WithEnvironmentName) wired through appsettings.json so every log line carries EnvironmentName and any LogContext-pushed properties
  - LogContext.PushProperty("ReceiptFileId", receiptFile.Id) scope around the per-file processing block in UploadReceiptFilesHandler.HandleAsync — every log line emitted by nested extractors/parsers/classifier now carries the receipt-file ID without changes to those services
  - Direct PackageReference Include="Serilog" on TaxReader.Application so handlers in the Application layer can use Serilog.Context types at compile time
  - Console output template upgraded to {Properties:j} so contextual properties land in stdout for incident-response readability
  - 3 new tests guarding the wiring: 2 runtime config-load assertions + 1 structural-grep assertion that the handler scope stays in place
affects: [01-03, 01-02, 02-*, 03-*]

tech-stack:
  added:
    - Serilog 4.2.0 (direct dependency on TaxReader.Application; matches version transitively pulled by Serilog.AspNetCore 9.0.0)
    - Serilog.Enrichers.Environment 3.0.1 (direct dependency on TaxReader.Api)
  patterns:
    - "Serilog enricher wiring via appsettings.json: paired Using[] (assemblies for reflection) with Enrich[] (enricher names) — Pitfall 2 mitigation"
    - "LogContext correlation scope at long-running-handler boundary: using (LogContext.PushProperty(K, V)) wrapping per-unit work; nested ILogger<T> users pick up the property automatically via AsyncLocal"
    - "Source-level structural grep test as a load-bearing wiring guard for invariants no runtime test can express cleanly"

key-files:
  created:
    - Backend/tests/TaxReader.UnitTests/Observability/SerilogEnrichmentTests.cs
    - .planning/phases/01-foundation-cleanup-ci/01-04-SUMMARY.md
  modified:
    - Backend/Directory.Packages.props
    - Backend/src/TaxReader.Application/TaxReader.Application.csproj
    - Backend/src/TaxReader.Api/TaxReader.Api.csproj
    - Backend/src/TaxReader.Api/appsettings.json
    - Backend/src/TaxReader.Application/Commands/UploadReceiptFilesHandler.cs

key-decisions:
  - "appsettings.Development.json deliberately not modified — IConfiguration array merging is index-based, so re-declaring Using/Enrich/WriteTo in Dev would risk silently overriding base; keeping the existing MinimumLevel-only override is the simpler/safer path (CLAUDE.md simplicity-first)"
  - "Serilog 4.2.0 pinned to match the version transitively flowed by Serilog.AspNetCore 9.0.0 — verified via `dotnet list package --include-transitive` before committing"
  - "LogContext push site is *after* the initial ReceiptFile + ProcessingRun insert + first SaveChanges (D-18 explicit guidance: scope wraps per-file processing block — extraction + parsing + classification — not the housekeeping inserts)"
  - "Push only non-PII identifiers (Guid). Comment in code documents the rule for future contributors so mitigation of T-01-04-01 (PII in log sinks) survives future edits"
  - "Serilog.Enrichers.Environment owned by TaxReader.Api project (not Application or test project) because the API is where Serilog config is loaded; test project receives the assembly transitively via existing ProjectReference to TaxReader.Api"

patterns-established:
  - "Enricher pairing rule: Every non-built-in entry in `Serilog:Enrich[]` MUST have a matching assembly entry in `Serilog:Using[]` AND a PackageReference in some shipping project. Without the PackageReference, Serilog.Settings.Configuration silently fails to load the enricher (Pitfall 2 in action)"
  - "Source-level structural grep test (xUnit + File.ReadAllText + FluentAssertions.Should().Contain) as a load-bearing assertion for cross-cutting wiring that can't be expressed cleanly as a runtime behavior test — used here for the LogContext scope in UploadReceiptFilesHandler"

requirements-completed: [OBS-02]

duration: 5min
completed: 2026-05-10
---

# Phase 1 Plan 04: Serilog Enrichers + LogContext Correlation Summary

**Wired `Serilog.Enrichers.Environment` + `Enrich.FromLogContext` via `appsettings.json`, wrapped the per-file processing block in `UploadReceiptFilesHandler` with `using (LogContext.PushProperty("ReceiptFileId", receiptFile.Id))` so every log line emitted by nested services during upload processing carries the receipt-file ID; 3 new tests guard the wiring.**

## Performance

- **Duration:** 5 min
- **Started:** 2026-05-10T10:39:48Z
- **Completed:** 2026-05-10T10:45:18Z
- **Tasks:** 2
- **Files modified:** 6 (1 new test file + 5 modified source/config)

## Accomplishments

- **Enricher config (D-17):** `appsettings.json` now declares `Serilog.Using[Serilog.Sinks.Console, Serilog.Enrichers.Environment]`, `Serilog.Enrich[FromLogContext, WithEnvironmentName]`, and a console `outputTemplate` containing `{Properties:j}` so contextual properties land in stdout. `appsettings.Development.json` left unchanged — its existing `MinimumLevel`-only override is sufficient and avoids the array-merge trap.
- **Handler correlation (D-18):** Per-file `try { ... } catch { ... }` block in `UploadReceiptFilesHandler.HandleAsync` is now wrapped in `using (LogContext.PushProperty("ReceiptFileId", receiptFile.Id))`. Nested services (`PdfPigTextExtractor`, `TesseractImageTextExtractor`, parsers, `AiOnlyClassificationService`) inject `ILogger<T>` and pick up the property automatically via Serilog's `AsyncLocal`-backed `LogContext` — zero changes to those services. JobId half (D-18) explicitly deferred to Phase 3 at the Hangfire boundary.
- **Project reference correctness:** `TaxReader.Application` now has a direct `<PackageReference Include="Serilog" />` so `using Serilog.Context;` resolves at compile time. `TaxReader.Api` now has `<PackageReference Include="Serilog.Enrichers.Environment" />` so the assembly ships with the runtime image (without it, `Serilog.Settings.Configuration` cannot reflect over the enricher and silently drops `WithEnvironmentName` — the active expression of Pitfall 2).
- **Test coverage:** 3 new tests in `Backend/tests/TaxReader.UnitTests/Observability/SerilogEnrichmentTests.cs`:
  1. `Config_Loads_FromLogContextEnricher_PropagatesContextProperty` — boots a `LoggerConfiguration().ReadFrom.Configuration(...)` against the real `appsettings.json`, captures emitted `LogEvent`s through a custom `ILogEventSink`, asserts `ReceiptFileId` lands on the event when pushed via `LogContext.PushProperty`.
  2. `Config_Loads_WithEnvironmentNameEnricher_AttachesEnvironmentName` — same harness with `ASPNETCORE_ENVIRONMENT=ci-test`, asserts `EnvironmentName=ci-test` lands on the event.
  3. `UploadReceiptFilesHandler_Source_ContainsReceiptFileIdLogContextScope` — structural-grep assertion using `File.ReadAllText` + `FluentAssertions.Should().Contain` to lock in `using Serilog.Context;` and the literal `using (LogContext.PushProperty("ReceiptFileId", receiptFile.Id))` line. Brittle by design — the load-bearing OBS-02 wiring guard.
- **Suite delta:** 100 → 103 backend tests, all green. Build is 0 errors / 0 warnings.

## Task Commits

1. **Task 1 — Add Serilog packages + configure enrichers via appsettings.json** — `e41a392` (feat)
2. **Task 2 — Wrap UploadReceiptFilesHandler with LogContext + enrichment tests** — `c108a8d` (feat)

_Note: Tasks were `tdd="true"` but in both cases the "RED" phase is implicit. Task 1's verification is JSON-parse + dotnet-build (no test was written for it directly — Task 2's `Config_Loads_*` tests cover its config-shape invariants). Task 2's tests sit in the same atomic commit as the handler edit, consistent with execute-plan.md's per-task commit protocol when no plan-level `type: tdd` gate is set._

## Resolved Versions

- **Serilog:** 4.2.0 (matches the version transitively pulled by `Serilog.AspNetCore 9.0.0` — verified via `dotnet list package --include-transitive` against TaxReader.Api before committing).
- **Serilog.Enrichers.Environment:** 3.0.1 (latest stable per RESEARCH.md; latest serilog org release).
- **Serilog.Settings.Configuration:** 9.0.0 (transitive — already in dependency tree via `Serilog.AspNetCore`; no need to add directly, contrary to plan note line 407).

## Files Created

- `Backend/tests/TaxReader.UnitTests/Observability/SerilogEnrichmentTests.cs` — 102 lines; 3 facts; uses a tiny private `CapturingSink : ILogEventSink` instead of pulling in `Serilog.Sinks.InMemory` (no new dependency)

## Files Modified

- `Backend/Directory.Packages.props` — added `<PackageVersion Include="Serilog" Version="4.2.0" />` and `<PackageVersion Include="Serilog.Enrichers.Environment" Version="3.0.1" />`, placed alphabetically next to existing Serilog entries
- `Backend/src/TaxReader.Application/TaxReader.Application.csproj` — added `<PackageReference Include="Serilog" />` so `using Serilog.Context;` resolves
- `Backend/src/TaxReader.Api/TaxReader.Api.csproj` — added `<PackageReference Include="Serilog.Enrichers.Environment" />` so the enricher assembly is discoverable by `Serilog.Settings.Configuration` at runtime (Rule 3 deviation — see below)
- `Backend/src/TaxReader.Api/appsettings.json` — replaced `Serilog` section: added `Using[]`, `Enrich[]`, and `WriteTo[].Args.outputTemplate` per RESEARCH.md Pattern 6 verbatim. Existing `ConnectionStrings`, `Tesseract`, `AllowedHosts` blocks unchanged
- `Backend/src/TaxReader.Application/Commands/UploadReceiptFilesHandler.cs` — added `using Serilog.Context;` import (alphabetical position between `Microsoft.EntityFrameworkCore` and `TaxReader.Application.DTOs`); wrapped the existing per-file `try { ... } catch { ... }` block (previously lines 104-156) in `using (LogContext.PushProperty("ReceiptFileId", receiptFile.Id))` scope. Existing extract → parse → SaveChanges → pending-add logic unchanged. Added a 4-line comment explaining D-18 + the non-PII rule (T-01-04-01 mitigation documented in code so future contributors don't push vendor names / item descriptions)

## Decisions Made

None beyond what the plan specified. All three plan-level decisions (D-17, D-18, D-19) executed verbatim. The `appsettings.Development.json` no-change choice was the explicitly-recommended simpler path in PATTERNS.md and the plan's Edit 4.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 — Blocking] Added `<PackageReference Include="Serilog.Enrichers.Environment" />` to TaxReader.Api.csproj**

- **Found during:** Task 2 (running the new `Config_Loads_*` tests after writing them)
- **Issue:** Task 1's Edit 1 added `Serilog.Enrichers.Environment 3.0.1` as a `<PackageVersion>` to `Directory.Packages.props`, and Task 1's Edit 3 added `"Serilog.Enrichers.Environment"` to the JSON `Using[]` array. But no `.csproj` consumed the package via `<PackageReference>`, so the assembly was never copied into any output `bin/` directory. At runtime, `Serilog.Settings.Configuration.ConfigurationReader.LoadConfigurationAssemblies` threw `FileNotFoundException: Could not load file or assembly 'Serilog.Enrichers.Environment'` for both runtime tests. This is exactly the silent-failure mode RESEARCH.md Pitfall 2 warned about, just with a louder symptom because the test harness exercises `ReadFrom.Configuration` directly.
- **Fix:** Added one `<PackageReference Include="Serilog.Enrichers.Environment" />` line to `Backend/src/TaxReader.Api/TaxReader.Api.csproj`, alphabetically between `Serilog.AspNetCore` and `Serilog.Sinks.Console`. The assembly now ships with the API runtime image, and the test project receives it transitively via its existing `<ProjectReference Include="..\..\src\TaxReader.Api\TaxReader.Api.csproj" />` (added in 01-01 for `WebApplicationFactory<Program>`).
- **Files modified:** `Backend/src/TaxReader.Api/TaxReader.Api.csproj`
- **Verification:** `dotnet test Backend --filter "FullyQualifiedName~SerilogEnrichmentTests"` → 3/3 pass. `dotnet test Backend` → 103/103 pass.
- **Committed in:** `c108a8d` (Task 2 commit — bundled with handler + test changes since the fix was discovered during Task 2 verification and is logically tied to Task 1's intent of "make the enrichers actually work")

---

**Total deviations:** 1 auto-fixed (1 blocking)
**Impact on plan:** The plan template at lines 146-150 added the `<PackageVersion>` but stopped short of the `<PackageReference>` step that activates it. This is a transcription gap — the same pattern Plan 01-01 hit with the missing `Microsoft.AspNetCore.Hosting` using-directive (deviation #1 in 01-01-SUMMARY.md). No structural change, no scope creep. The fix is the minimum that turns the plan's documented intent into actual runtime behavior.

## Issues Encountered

None beyond the deviation above. The `dotnet list package --include-transitive` query in Task 1's preflight step correctly identified Serilog 4.2.0 as the matching version, so no version-conflict iteration was needed.

## Verification Results

```
=== Package wiring ===
OK Directory.Packages.props has Serilog.Enrichers.Environment
OK Directory.Packages.props has Serilog package version
OK TaxReader.Application references Serilog
OK TaxReader.Api references Serilog.Enrichers.Environment

=== Handler wiring ===
OK Serilog.Context import
OK LogContext scope present

=== Config wiring (PowerShell JSON parse) ===
OK appsettings.json structure

=== Tests (filtered) ===
Bestanden!   : Fehler:     0, erfolgreich:     3, übersprungen:     0, gesamt:     3, Dauer: 347 ms

=== Full suite (regression check) ===
Bestanden!   : Fehler:     0, erfolgreich:   103, übersprungen:     0, gesamt:   103, Dauer: 6 s
```

All 5 must_haves.truths, all 3 must_haves.artifacts, all 2 key_links, and all <success_criteria> in the plan body — verified.

## TDD Gate Compliance

Plan-level type is `execute`, not `tdd`. Per-task `tdd="true"` markers were applied at task granularity. The git log shows two atomic per-task commits:

- `e41a392` feat(01-04): wire Serilog enrichers via appsettings.json (no test added in this commit — Task 1's invariants are config-shape ones, exercised by Task 2's `Config_Loads_*` tests)
- `c108a8d` feat(01-04): correlate upload logs with ReceiptFileId via LogContext (handler edit + 3 new tests bundled per execute-plan.md atomic-commit protocol)

This matches Plan 01-01's pattern (test + impl in the same task commit when no plan-level `type: tdd` gate is set).

## User Setup Required

None — no external services touched in this plan. All changes are internal to the backend logging surface.

## Next Phase Readiness

- **Wave 3 (Plan 01-03 — Sentry):** Unblocked. Sentry will read `appsettings.json` at startup; the Serilog enricher additions there don't conflict with Sentry's `"Sentry"` top-level section. Sentry's `BeforeSend` callback can safely emit log lines that flow through Serilog's enricher pipeline (`EnvironmentName` will appear on every Sentry-routed event) — but per D-19, no Sentry tag is wired here. Phase 1 Success Criterion #5 ("Long-running upload handlers emit log lines correlated by `ReceiptFileId` / `JobId`") — `ReceiptFileId` half **DONE**; `JobId` half lands at Phase 3's Hangfire boundary per D-18.
- **Wave 4 (Plan 01-02 — CI workflow + README):** Awaiting 01-03. The 03-test runs in CI will exercise the new SerilogEnrichmentTests, providing a living regression guard against future Serilog config drift.
- **Phase 3 (background-job upload pipeline):** Will add `using (LogContext.PushProperty("JobId", jobId))` at the Hangfire job entry point. The pattern is now established in `UploadReceiptFilesHandler.cs` line 104 — Phase 3 just adds another scope above it. The existing structural-grep test in `SerilogEnrichmentTests.cs` will need a sibling assertion for `JobId` once the job class lands.

## Self-Check: PASSED

- Created files exist:
  - `Backend/tests/TaxReader.UnitTests/Observability/SerilogEnrichmentTests.cs` — FOUND
- Modified files contain expected literals:
  - `Backend/Directory.Packages.props` contains `Serilog.Enrichers.Environment` — FOUND
  - `Backend/src/TaxReader.Application/TaxReader.Application.csproj` contains `<PackageReference Include="Serilog" />` — FOUND
  - `Backend/src/TaxReader.Api/TaxReader.Api.csproj` contains `<PackageReference Include="Serilog.Enrichers.Environment" />` — FOUND
  - `Backend/src/TaxReader.Api/appsettings.json` `Serilog.Using` includes `"Serilog.Enrichers.Environment"` — FOUND
  - `Backend/src/TaxReader.Api/appsettings.json` `Serilog.Enrich` includes `"FromLogContext"` and `"WithEnvironmentName"` — FOUND
  - `Backend/src/TaxReader.Application/Commands/UploadReceiptFilesHandler.cs` contains `using Serilog.Context;` — FOUND
  - `Backend/src/TaxReader.Application/Commands/UploadReceiptFilesHandler.cs` contains `using (LogContext.PushProperty("ReceiptFileId", receiptFile.Id))` — FOUND
- Commit hashes exist in git log:
  - `e41a392` — FOUND
  - `c108a8d` — FOUND
- Test counts: 3/3 new pass, 103/103 total pass

---

*Phase: 01-foundation-cleanup-ci*
*Completed: 2026-05-10*
