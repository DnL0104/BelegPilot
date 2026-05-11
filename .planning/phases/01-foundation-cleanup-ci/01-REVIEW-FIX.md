---
phase: 01-foundation-cleanup-ci
iteration: 1
fix_scope: critical_warning
findings_in_scope: 5
fixed: 5
skipped: 0
status: all_fixed
---

# Phase 01: Code Review Fix Report

**Fixed at:** 2026-05-11T00:00:00Z
**Source review:** .planning/phases/01-foundation-cleanup-ci/01-REVIEW.md
**Iteration:** 1

**Summary:**
- Findings in scope: 5 (5 Warning, 0 Critical, Info skipped per fix_scope)
- Fixed: 5
- Skipped: 0

All five warnings landed cleanly. Regression suite: `dotnet test Backend` reports 113/113 passing; `cd Frontend && npm run build` completes with zero TypeScript errors and 12/12 pages generated.

## Fixed Issues

### WR-01: Empty catch in `MarkFailedAsync` swallows DB persistence failures silently

**Files modified:** `Backend/src/TaxReader.Application/Commands/UploadReceiptFilesHandler.cs`, `Backend/tests/TaxReader.UnitTests/Application/Commands/UploadReceiptFilesHandlerTests.cs`
**Commits:** `ee8922b` (handler), `6bed03f` (test ctor fixup)
**Applied fix:**
- Added `ILogger<UploadReceiptFilesHandler> logger` to the handler's primary constructor (per CLAUDE.md "primary constructors for DI" convention).
- Replaced `try { ... } catch { /* best-effort */ }` with a typed `catch (Exception ex)` that calls `logger.LogError(ex, "Failed to persist failure status for ReceiptFile {ReceiptFileId}; status may be stale.", file.Id)` — structured logging with named placeholder, no string interpolation (CLAUDE.md "Structured logging always").
- The catch remains inside the `LogContext.PushProperty("ReceiptFileId", ...)` scope at the call sites, so the correlation ID rides on the log line for free.
- Updated `UploadReceiptFilesHandlerTests` constructor to pass `Mock.Of<ILogger<UploadReceiptFilesHandler>>()` for the new param. The test file was previously untracked in git, so its commit includes the full file under `create mode 100644`.

### WR-02: Frontend `sentry.server.config.ts` and `sentry.edge.config.ts` skip the D-14 PII scrubber

**Files modified:** `Frontend/src/lib/sentry-scrubber.ts` (new), `Frontend/instrumentation-client.ts`, `Frontend/sentry.server.config.ts`, `Frontend/sentry.edge.config.ts`
**Commit:** `4a48352`
**Applied fix:**
- Extracted `scrubEvent` + helper constants (`ALLOWED_QUERY_KEYS`, `ALLOWED_HEADERS`, `UUID_RE`, `filterQueryString`) from `instrumentation-client.ts` into a new module `Frontend/src/lib/sentry-scrubber.ts` (matches the `@/lib/...` alias convention per `tsconfig.json` paths).
- Module exports a single named function `scrubEvent(event: Sentry.ErrorEvent): Sentry.ErrorEvent | null` and uses `import type * as Sentry from "@sentry/nextjs"` so it is purely structural at runtime.
- All three runtime configs now import `scrubEvent` from `@/lib/sentry-scrubber` and reference it in `beforeSend`. The client config also drops the duplicated scrubber/helpers (was ~50 lines, now zero).
- Verified with `npx tsc --noEmit` (no errors) and `npm run lint` (only pre-existing project errors in `auth-provider.tsx`, not touched).

### WR-03: `docker-compose.yml` `Sentry__Environment` is dead config

**Files modified:** `Backend/src/TaxReader.Api/Program.cs`
**Commit:** `5a8c4ad`
**Applied fix:** Dropped `options.Environment = builder.Environment.EnvironmentName;` from the `UseSentry` lambda (Program.cs line 36 in the pre-fix file). The Sentry .NET SDK now binds `Environment` from configuration alone — the `Sentry__Environment` env var in `docker-compose.yml` becomes the single source of truth, with `ASPNETCORE_ENVIRONMENT` as the SDK's default fallback when unset. Updated the comment above `UseSentry` to document the binding. Picked the surgical option per the user's directive and CLAUDE.md simplicity-first (one-line code change vs. modifying compose).

### WR-04: Stale doc comment on `AnthropicOptions.Model` says "8-category"

**Files modified:** `Backend/src/TaxReader.Infrastructure/Configuration/AnthropicOptions.cs`
**Commit:** `aebb4cd`
**Applied fix:** Replaced the comment text "Haiku is plenty for an 8-category classification choice" with "Haiku is plenty for the 13-category DE tax classification choice". Comment-only change; no code path affected. (File was previously untracked in git, so the commit registers as `create mode 100644`.)

### WR-05: CI NuGet cache key references non-existent `packages.lock.json` files

**Files modified:** `.github/workflows/ci.yml`
**Commit:** `f071ad6`
**Applied fix:** Picked Option A from REVIEW.md (drop the dead glob). Changed `cache-dependency-path` from a multi-line block listing `Backend/**/packages.lock.json` + `Backend/Directory.Packages.props` to a single-string value `Backend/Directory.Packages.props`. The comment above (referring to Directory.Packages.props as the central manifest) remains accurate and is preserved. No YAML parser available locally for Tier 2 verification, but re-read confirms the file is well-formed against the `actions/setup-dotnet@v4` schema (which accepts both string and list values for `cache-dependency-path`).

---

## Skipped Issues

None — all five in-scope warnings were fixed and verified.

---

## Out-of-Scope Findings (Info)

The five Info findings (IN-01 through IN-05) were not touched per `fix_scope: critical_warning`. They remain documented in `01-REVIEW.md` for future iterations.

---

_Fixed: 2026-05-11T00:00:00Z_
_Fixer: Claude (gsd-code-fixer)_
_Iteration: 1_
