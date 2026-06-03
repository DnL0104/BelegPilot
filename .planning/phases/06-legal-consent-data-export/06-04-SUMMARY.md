---
phase: 06-legal-consent-data-export
plan: 04
subsystem: backend+frontend
tags: [data-export, dsgvo-art20, hangfire, zip, one-time-token, idor, tdd, leg-07]
dependency_graph:
  requires:
    - 06-03 (IAuditLogger, AuditLogEntry, AuditAction, AuditLogEntries DbSet — export bundle queries these)
    - 05-commercial-surface-payments (IBackgroundJobClient, Hangfire wiring)
  provides:
    - IExportTokenStore + ExportTokenStore (Application interface + Infrastructure Singleton)
    - ExportUserDataJob (Hangfire fire-and-forget, JSON+CSV zip bundle)
    - ExportCleanupJob (recurring 24h purge)
    - ExportEndpoints (/export/request, /export/status, /export/download)
    - Frontend hook + settings-page trigger with status states
  affects:
    - Settings page (new export section)
    - RecurringJobsBootstrap (new export-cleanup entry)
    - DependencyInjection (new Singleton registration)
tech_stack:
  added:
    - IExportTokenStore interface (Application, ExportRecord/ExportStatus types)
    - ExportTokenStore (Infrastructure, ConcurrentDictionary Singleton)
    - ExportUserDataJob (Application/Jobs, System.IO.Compression + System.Text.Json)
    - ExportCleanupJob (Application/Jobs)
    - ExportEndpoints (Api/Endpoints)
    - use-data-export.ts (Frontend hook, TanStack Query polling)
    - downloadExportBundle() in api-client.ts (responseType: "blob")
  patterns:
    - ConcurrentDictionary for thread-safe Singleton token store
    - ExportRecord sealed record with ExpiresAtUtc check in TryGet (Expired flip)
    - ZipArchive (BCL System.IO.Compression) — no external package
    - One-time invalidation: Invalidate(token) after FileStream opened for streaming
    - TanStack Query refetchInterval gated on status === "Generating"
    - Blob download via axios (carries JWT) + object URL + programmatic <a> click
key_files:
  created:
    - Backend/src/TaxReader.Application/Interfaces/IExportTokenStore.cs
    - Backend/src/TaxReader.Infrastructure/Services/ExportTokenStore.cs
    - Backend/src/TaxReader.Application/Jobs/ExportUserDataJob.cs
    - Backend/src/TaxReader.Application/Jobs/ExportCleanupJob.cs
    - Backend/src/TaxReader.Api/Endpoints/ExportEndpoints.cs
    - Backend/tests/TaxReader.UnitTests/Application/ExportTokenStoreTests.cs
    - Backend/tests/TaxReader.UnitTests/Application/ExportDownloadEndpointTests.cs
    - Backend/tests/TaxReader.UnitTests/Jobs/ExportUserDataJobTests.cs
    - Backend/tests/TaxReader.UnitTests/Jobs/ExportCleanupJobTests.cs
    - Frontend/src/hooks/use-data-export.ts
  modified:
    - Backend/src/TaxReader.Infrastructure/DependencyInjection.cs (AddSingleton<IExportTokenStore, ExportTokenStore>)
    - Backend/src/TaxReader.Api/Hangfire/RecurringJobsBootstrap.cs (export-cleanup at 02:00 UTC)
    - Backend/src/TaxReader.Api/Program.cs (api.MapExportEndpoints())
    - Frontend/src/lib/api-client.ts (requestDataExport, getExportStatus, downloadExportBundle)
    - Frontend/src/app/(authenticated)/settings/page.tsx (data export card section)
decisions:
  - "ExportStatus enum and ExportRecord record placed in Application layer (IExportTokenStore.cs) to avoid Infrastructure→Application reverse dependency"
  - "Generating status checked before Expired in TryGet — a Generating token is never flipped to Expired even if past expiry (job is still running)"
  - "Download endpoint opens FileStream before calling Invalidate() — ensures file handle is obtained before token is gone"
  - "downloadExportBundle uses responseType:blob via axios — carries JWT Authorization header (bare <a href> would not)"
  - "ExportUserDataJob injects IExportTokenStore (not ICurrentUser) — ICurrentUser reads HttpContext unavailable in Hangfire workers"
metrics:
  duration: "45 minutes"
  completed_date: "2026-06-03"
  tasks_completed: 3
  files_changed: 14
---

# Phase 06 Plan 04: DSGVO Art. 20 Data Export (LEG-07) Summary

**One-liner:** Hangfire `ExportUserDataJob` writes a per-user JSON+CSV zip (receipts, items, classifications, token_transactions, own audit_log rows, README.txt; no password hash) into `/tmp/taxreader-exports/{token}.zip`, served via ownership-validated one-time download with 24h auto-purge, triggered from the settings page with idle/generating/ready/expired states.

## What Was Built

### Backend

**`IExportTokenStore` + `ExportTokenStore` (Singleton)**
- `ExportStatus` enum: `Generating | Ready | Expired` — placed in Application layer
- `ExportRecord` sealed record: `UserId`, `ExpiresAtUtc`, `Status`
- `ConcurrentDictionary<string, ExportRecord>` — thread-safe across requests and Hangfire workers
- `TryGet` flips `Ready → Expired` when `DateTime.UtcNow > ExpiresAtUtc`
- `Invalidate()` removes token after download (one-time — T-06-42)

**`ExportUserDataJob`** (Hangfire fire-and-forget, LEG-07 / D-11)
- Primary constructor: `(IAppDbContext, ILogger<ExportUserDataJob>, IExportTokenStore)` — no `ICurrentUser`
- `[AutomaticRetry(Attempts = 3, DelaysInSeconds = [30, 60, 120])]`
- Queries: `ReceiptFiles`, `Receipts`, `ReceiptItems`, `ItemClassifications`, `TokenTransactions`, `AuditLogEntries` — all filtered by `userId`
- T-06-44: Projected anonymous shapes explicitly exclude `PasswordHash`
- T-06-46: Audit log filtered `SubjectUserId == userId` only
- Writes JSON + CSV for each dataset + `README.txt` (German) into `ZipArchive`
- Calls `tokenStore.Register(token, userId, UtcNow+24h)` after zip fully written and closed

**`ExportCleanupJob`** (recurring daily, T-06-43)
- `[DisableConcurrentExecution(600)]`, `[AutomaticRetry(Attempts = 0)]`
- Purges `*.zip` older than 24h from `/tmp/taxreader-exports/`
- Missing directory → no-op (no exception)
- Registered at `02:00 UTC` in `RecurringJobsBootstrap`

**`ExportEndpoints`** (`/api/v1/export/*`, all RequireAuthorization)
- `POST /export/request`: generates `Guid.NewGuid().ToString("N")` token (T-06-41), calls `MarkGenerating`, enqueues job, records `DataExportRequested` audit (token_prefix only — T-06-41), returns `{ exportToken }`
- `GET /export/status?token=`: returns `Expired` for foreign/unknown tokens (no existence leak — T-06-41), `Generating/Ready/Expired` for own tokens
- `GET /export/download?token=`: IDOR check `rec.UserId != currentUser.UserId → 403` (T-06-40), expired → 410, not-ready → 202, streams file, invalidates token (T-06-42), records `DataExportDownloaded` audit

### Frontend

**`use-data-export.ts`**
- `requestExport()` mutation sets `exportToken` state on success
- `getExportStatus(token)` polled every 3s while `status === "Generating"` via TanStack Query `refetchInterval`
- Polling stops automatically when `Ready` or `Expired`

**`settings/page.tsx`** (data export card section)
- Blue icon badge (`bg-blue-100 text-blue-700 dark:bg-blue-950/50 dark:text-blue-400`) + `Download` lucide icon
- Heading: "Meine Daten exportieren"
- Body: "Erstellt einen vollständigen Export Ihrer Daten … gemäß DSGVO Art. 20. Der Link ist 24 Stunden gültig."
- States: idle → "Daten exportieren" button; generating → spinner + muted status text; ready → "Export bereit" badge + "Herunterladen" button; expired → amber warning + re-trigger button
- Download handler: `downloadExportBundle(token)` via axios `responseType: "blob"` (JWT carried), then object URL + programmatic `<a>` click

**`api-client.ts`** additions:
- `requestDataExport()` → `POST /export/request`
- `getExportStatus(token)` → `GET /export/status?token=`
- `downloadExportBundle(token)` → `GET /export/download?token=` with `responseType: "blob"`

## Tests

| File | Tests | What They Cover |
|---|---|---|
| `ExportTokenStoreTests.cs` | 6 | Register→Ready, Invalidate→false, expired→Expired, MarkGenerating, unknown→false, Invalidate unknown no-throw |
| `ExportDownloadEndpointTests.cs` | 5 | Ownership check passes (T-06-40), IDOR fails (T-06-40), one-time invalidation (T-06-42), expired status (T-06-43), no existence leak (T-06-41) |
| `ExportUserDataJobTests.cs` | 5 | Zip created at expected path, all required entries present, no PasswordHash in bundle (T-06-44), token marked Ready, audit_log contains only owner rows (T-06-46) |
| `ExportCleanupJobTests.cs` | 2 | Old zip deleted / fresh kept (T-06-43), missing directory no-op |

**Test results:** 280 passed, 5 skipped (pre-existing infrastructure tests), 0 failed.
**Frontend build:** `npm run build` exits 0.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] ExportStatus/ExportRecord placed in Infrastructure, causing circular dependency**
- **Found during:** Writing `IExportTokenStore.cs` — interface in Application cannot reference Infrastructure types.
- **Issue:** Initial draft put `ExportStatus` enum and `ExportRecord` record in `Infrastructure/Services/ExportTokenStore.cs`, which `IExportTokenStore` in Application would then need to import — violating the architecture rule "Application does NOT reference Infrastructure".
- **Fix:** Moved `ExportStatus` enum and `ExportRecord` sealed record into `Application/Interfaces/IExportTokenStore.cs`. Infrastructure's `ExportTokenStore` imports from Application (valid direction).
- **Files modified:** `IExportTokenStore.cs`, `ExportTokenStore.cs`
- **Commit:** 26bc9e4

**2. [Rule 1 - Bug] Category enum value name mismatch in test**
- **Found during:** First build of GREEN phase — `Category.ConsumablesAndOfficeSupplies` does not exist.
- **Issue:** Test used a CLAUDE.md domain-term name that doesn't match the actual German enum value.
- **Fix:** Changed to `Category.WerbungskostenBueromaterial`.
- **Files modified:** `ExportUserDataJobTests.cs`
- **Commit:** 26bc9e4

## Known Stubs

None. All data flows connect to real DB queries, real zip file I/O, and real API endpoints.

## Threat Flags

No new threat surfaces introduced beyond the planned threat register for this plan (T-06-40 through T-06-46). All mitigations implemented:

| Threat ID | Mitigation Implemented |
|-----------|----------------------|
| T-06-40 | Download endpoint: `rec.UserId != currentUser.UserId → Results.Forbid()` |
| T-06-41 | Token = `Guid.NewGuid().ToString("N")` (128-bit CSPRNG); status endpoint returns "Expired" for foreign tokens |
| T-06-42 | `tokenStore.Invalidate(token)` after FileStream opened; second download → 404 |
| T-06-43 | `ExportCleanupJob` deletes *.zip older than 24h; TryGet flips Ready→Expired past ExpiresAtUtc |
| T-06-44 | All projected shapes exclude PasswordHash; test asserts no "PasswordHash" string in any zip entry |
| T-06-45 | Token is hex-only Guid; path is `Path.Combine(tmp, token + ".zip")` — no user-supplied segments |
| T-06-46 | Audit query: `.Where(a => a.SubjectUserId == userId)` — cross-user rows excluded; test asserts OtherUserId absent |

## Manual Verification Required (Human UAT)

The final task in this plan is a `checkpoint:human-verify` gate that requires a running docker stack and browser. The automated suite covers all backend behavior; the following steps are deferred to human UAT:

1. `docker compose up --build`. Log in as a user with at least one receipt + token transaction.
2. Go to Settings → "Meine Daten exportieren" → click "Daten exportieren". Confirm status shows "Wird erstellt…", then transitions to "Export bereit" within a few seconds.
3. Click "Herunterladen" → a `taxreader-export.zip` downloads. Open it: confirm it contains `receipts.json/csv`, `items.json/csv`, `classifications.json/csv`, `token_transactions.json/csv`, `audit_log.json/csv`, and `README.txt`. Confirm NO password hash appears anywhere in the bundle.
4. Confirm `audit_log.csv` contains only YOUR rows (e.g. a `DataExportRequested` entry), not other users'.
5. **IDOR check:** Copy the export token from the network tab; in a SECOND logged-in account, call `GET /api/v1/export/download?token={firstUserToken}` → must return 403 (not the file).
6. Click "Herunterladen" a second time with the same token → should return 404/expired since downloads are one-time.

## Self-Check: PASSED

All 10 created files exist on disk (verified above). All 4 task commits confirmed in git log:
- `26273d2` — TDD RED: failing test files (4 test files)
- `26bc9e4` — GREEN: IExportTokenStore + ExportTokenStore + ExportUserDataJob + ExportCleanupJob + DI + RecurringJobs
- `c0d21af` — ExportEndpoints + Program.cs wiring
- `0db4622` — Frontend: use-data-export hook + settings page + api-client functions

Build: `dotnet build Backend` exits 0 (2 pre-existing NU1510 warnings, 0 errors)
Tests: `dotnet test Backend` exits 0 — 280 passed, 5 skipped, 0 failed
Frontend: `npm run build` exits 0
