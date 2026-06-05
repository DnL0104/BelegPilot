---
phase: 06-legal-consent-data-export
plan: 06
subsystem: export
tags: [dsgvo, export, leg-07, gap-closure, tdd, security]
requires:
  - LEG-07 export subsystem (06-04): ExportUserDataJob, ExportTokenStore, ExportEndpoints
provides:
  - parsed_receipts.json + parsed_receipts.csv in the DSGVO Art. 20 bundle
  - terminal Expired recovery for failed/stuck export jobs (MarkExpired + TTL flip)
  - resource-safe download (audit-before-stream, Invalidate-after-stream, Results.File-owned handle)
affects:
  - Backend/src/TaxReader.Application/Jobs/ExportUserDataJob.cs
  - Backend/src/TaxReader.Application/Interfaces/IExportTokenStore.cs
  - Backend/src/TaxReader.Infrastructure/Services/ExportTokenStore.cs
  - Backend/src/TaxReader.Api/Endpoints/ExportEndpoints.cs
tech-stack:
  added: []
  patterns:
    - "Reuse existing terminal enum value (Expired) instead of introducing a new status to avoid frontend churn"
    - "Audit before acquiring a disposable resource; transfer ownership to Results.File for guaranteed disposal"
key-files:
  created: []
  modified:
    - Backend/src/TaxReader.Application/Jobs/ExportUserDataJob.cs
    - Backend/src/TaxReader.Application/Interfaces/IExportTokenStore.cs
    - Backend/src/TaxReader.Infrastructure/Services/ExportTokenStore.cs
    - Backend/src/TaxReader.Api/Endpoints/ExportEndpoints.cs
    - Backend/tests/TaxReader.UnitTests/Jobs/ExportUserDataJobTests.cs
    - Backend/tests/TaxReader.UnitTests/Application/ExportTokenStoreTests.cs
decisions:
  - "Reused Expired (not a new Failed status) for job failure — frontend already surfaces Expired recovery; zero frontend change"
  - "MarkExpired is idempotent; a later successful Hangfire retry re-flips the token to Ready via Register"
  - "Results.File owns and disposes the FileStream after the response is written — no leaked handle"
metrics:
  duration: ~25m
  completed: 2026-06-05
---

# Phase 6 Plan 6: Export Gap-Closure Summary

Closed the four LEG-07 export gaps from 06-VERIFICATION.md — wrote parsed receipt data into the DSGVO Art. 20 bundle, added a terminal Expired recovery path for failed/stuck export jobs, and made the one-time download resource-safe — all via TDD with the full backend suite green (284 passed / 5 skipped).

## What Was Built

### Task 1 — parsed_receipts in the bundle (GAP 1, BLOCKER)
The already-queried `parsedReceipts` projection (vendor, purchase_date, total_amount, currency, parsed_at) is now serialized into `parsed_receipts.json` + `parsed_receipts.csv` inside the export zip, and the README lists them. Without this, the DSGVO Art. 20 export was incomplete. The projection is user-scoped (`r.ReceiptFile.UserId == userId`) and adds no new fields — the PasswordHash-absence test still passes (T-06-50).

### Task 2 — token store expiry + failure transition (WR-04 + WR-02 store side)
- `TryGet` now flips **any** non-terminal token (Ready **or** Generating) to Expired once past its TTL. The old guard excluded `Generating`, so a token whose job died stayed Generating forever and the UI spun indefinitely.
- Added `MarkExpired(string token)` to `IExportTokenStore` + `ExportTokenStore` — an idempotent terminal flip used by the job-failure path.

### Task 3 — job failure recovery + download FileStream safety (WR-02 job side + CR-01)
- `ExportUserDataJob.HandleAsync` body is wrapped in try/catch: on failure it logs a structured error (named placeholders, userId + 8-char token prefix only) and calls `tokenStore.MarkExpired(exportToken)` before re-throwing so Hangfire still records the failure and honours `[AutomaticRetry]`. The `LogContext.PushProperty` stays outside the try so the JobId tag also covers the error log.
- Download handler reordered: audit is recorded **before** the FileStream is opened, and `tokenStore.Invalidate(token)` runs **only after** the stream is opened, immediately before `Results.File`. IDOR 403 (`Results.Forbid()`), the `/api/v1` `RequireAuthorization`, and all prior status guards are untouched (T-06-51, T-06-52).

## Required Documentation (per plan output spec)

1. **Why Expired (not a new Failed status) was reused.** The frontend already defines `ExportStatus = "Generating" | "Ready" | "Expired"` and renders the `"Expired"` branch as *"Der Export-Link ist abgelaufen. Bitte fordern Sie einen neuen Export an."* with a re-trigger button. Reusing `Expired` as the recoverable terminal state for job failure means **zero frontend changes** and no new German copy. A new `Failed` enum value would have required frontend + localization work for no behavioural gain.

2. **MarkExpired idempotency + Register re-flip to Ready on retry.** `MarkExpired` is a no-op if the token is unknown and simply sets `Status = Expired` otherwise — calling it on every failed Hangfire attempt is harmless. Because `[AutomaticRetry]` re-runs the job, a later **successful** attempt calls `tokenStore.Register(...)` which writes a fresh `Ready` record, overriding any prior `Expired` state. So transient failures self-heal: failed attempt → Expired → successful retry → Ready.

3. **Results.File owns and disposes the FileStream.** The download handler hands the open `FileStream` to `Results.File(stream, ...)`, which takes ownership and disposes the stream after the response is written. The handle is therefore always released. By auditing **before** opening the stream and invalidating **after**, an exception during delivery setup (the audit call) neither leaks a file handle nor consumes the one-time token (CR-01 / T-06-52).

## Deviations from Plan

None — plan executed exactly as written. The 2 `NU1510` NuGet warnings on the Release build are pre-existing (Microsoft.Extensions.Http trimming) and unrelated to this plan; left untouched per scope boundary.

## Verification

- `dotnet test Backend` → 284 passed / 5 skipped / 0 failed (was 280/5 before; +4 new tests).
- `dotnet build Backend --configuration Release` → 0 errors (2 pre-existing NU1510 warnings).
- New tests: `HandleAsync_ValidUser_ParsedReceiptsCarryRealData`, two new `parsed_receipts` Contain assertions in `ZipContainsAllRequiredEntries`, `TryGet_GeneratingTokenPastTtl_ReturnsExpired`, `MarkExpired_GeneratingToken_TryGetReturnsExpired`, `HandleAsync_JobFails_MarksTokenExpired`.
- Download handler grep ordering confirmed: `RecordAsync` (L116) < `new FileStream(zipPath` (L125) < `tokenStore.Invalidate(token)` (L129); `Results.Forbid()` present; no `.AllowAnonymous()` call.

## TDD Gate Compliance

Each task followed RED → GREEN with separate commits:
- Task 1: `test(06-06)` 3dc326f (RED) → `feat(06-06)` 9c173e9 (GREEN)
- Task 2: `test(06-06)` e7cf60d (RED) → `fix(06-06)` 3c4bbb6 (GREEN)
- Task 3: `test(06-06)` 00744a4 (RED) → `feat(06-06)` b3c0fc7 (GREEN)

## Must-Have Truths (all green)

- ✅ Bundle contains `parsed_receipts.json` + `parsed_receipts.csv` with real vendor/amount/date.
- ✅ Download never leaks a FileStream handle and never consumes the token when delivery setup fails.
- ✅ A token stuck in Generating past its TTL flips to Expired.
- ✅ A job that fails after retries transitions the token to terminal Expired (already surfaced by the UI).
- ✅ IDOR 403 and RequireAuthorization remain intact after the refactor.

## Self-Check: PASSED

All 4 modified source files exist; all 6 per-task commits (3 RED + 3 GREEN) are present in git history.
