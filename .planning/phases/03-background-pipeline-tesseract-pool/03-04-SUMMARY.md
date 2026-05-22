---
phase: 3
plan: "04"
subsystem: pipeline-error-catalog + frontend-ui-states
tags: [error-handling, german-ux, polling, shadcn, tdd]
dependency_graph:
  requires: [03-02, 03-03]
  provides: [UploadErrorCatalog, ReceiptFileStatusBadge, ReceiptFileCard, useReceiptFileStatus, useCancelReceiptFile]
  affects: [upload-form, receipts-list, receipt-detail, dashboard, reports]
tech_stack:
  added:
    - UploadErrorCatalog static class (Application/Common)
    - shadcn Alert component (base-nova style, cva-driven)
    - TanStack Query polling via refetchInterval gated on isTerminal()
  patterns:
    - Exception → (ErrorCode, GermanMessage) catalog mapping (D-21)
    - TanStack Query refetchInterval=false gate on terminal ProcessingStatus
    - "use client" components with embedded mutation hooks for cancel button UX
key_files:
  created:
    - Backend/src/TaxReader.Application/Common/UploadErrorCatalog.cs
    - Backend/src/TaxReader.Application/Exceptions/NoTextExtractedException.cs
    - Backend/src/TaxReader.Application/Exceptions/ParserNotFoundException.cs
    - Backend/src/TaxReader.Application/Exceptions/InsufficientTokensException.cs
    - Backend/tests/TaxReader.UnitTests/Pipeline/UploadErrorCatalogTests.cs
    - Backend/tests/TaxReader.UnitTests/Pipeline/JobErrorLeakageTests.cs
    - Frontend/src/components/ui/alert.tsx
    - Frontend/src/components/upload/receipt-file-status-badge.tsx
    - Frontend/src/components/upload/receipt-file-card.tsx
    - .planning/phases/03-background-pipeline-tesseract-pool/03-HUMAN-UAT.md
  modified:
    - Backend/src/TaxReader.Application/Jobs/ProcessReceiptFileJob.cs
    - Backend/src/TaxReader.Application/Jobs/ClassifyBatchJob.cs
    - Backend/tests/TaxReader.UnitTests/Pipeline/ClassifyBatchJobTests.cs
    - Frontend/src/types/api.ts
    - Frontend/src/lib/api-client.ts
    - Frontend/src/hooks/use-receipt-files.ts
    - Frontend/src/hooks/use-receipts.ts
    - Frontend/src/components/receipts/receipts-table.tsx
    - Frontend/src/components/upload/upload-form.tsx
    - Frontend/src/app/(authenticated)/page.tsx
    - Frontend/src/app/(authenticated)/receipts/page.tsx
    - Frontend/src/app/(authenticated)/receipts/[id]/page.tsx
    - Frontend/src/app/(authenticated)/reports/page.tsx
decisions:
  - "UploadErrorCatalog placed in Application/Common (not Infrastructure) — zero infrastructure deps"
  - "ClassifyBatchJob cancellation test updated to use CancellationTokenSource.Callback() — IsCancellationRequested must be true during catch, not pre-cancelled before EF queries"
  - "Upload form fully rewritten for 202 Accepted shape; old UploadReceiptFilesResponse and synchronous result cards removed"
  - "ReceiptFile.status field treated as ProcessingStatus on frontend for in-flight detection on receipts/page.tsx (FileStatus enum values map similarly)"
  - "Alert component created manually matching base-nova style (npx shadcn@latest add alert is interactive-only; created file directly with matching cva/data-slot pattern)"
metrics:
  duration_minutes: 90
  tasks_completed: 7
  tasks_total: 7
  files_created: 10
  files_modified: 13
  tests_added: 16
  completed_date: "2026-05-22"
---

# Phase 3 Plan 04: UploadErrorCatalog + Frontend UI States Summary

**One-liner:** German exception catalog (D-21) wired into Hangfire jobs + shadcn Skeleton/Alert/Badge UI states across 5 frontend surfaces with TanStack Query polling.

## What Was Built

### Backend (T1 + T2)

**UploadErrorCatalog** (`Application/Common/UploadErrorCatalog.cs`):
- Static class mapping 6 exception types to `(ErrorCode, GermanMessage)` pairs
- Codes: `NoTextExtracted`, `ParserMissing`, `AiUnavailable`, `InsufficientTokens`, `Cancelled`, `Unknown`
- All German strings use Sie-form per CONVENTIONS.md; invariant verified by test
- `cancellationRequested` parameter distinguishes user-cancel (`OperationCanceledException` with signal) from AI timeout

**Application Exception Types** — all three were created:
- `NoTextExtractedException` (English message, logging-only)
- `ParserNotFoundException`
- `InsufficientTokensException`

**Job Error Wiring:**
- `ProcessReceiptFileJob`: single `catch (Exception ex)` block using `UploadErrorCatalog.Classify(ex, cancellationToken.IsCancellationRequested)`; `MarkFailedAsync` receives `terminalStatus` param; cancellation suppresses rethrow
- `ClassifyBatchJob`: same pattern; previous specific `catch (OperationCanceledException)` replaced with generic catalog path

**Test Coverage (16 new tests):**
- `UploadErrorCatalogTests.cs`: 10 tests covering all 6 exception types, Sie-form invariant, min-length check, code constant count
- `JobErrorLeakageTests.cs`: 6 tests proving raw `ex.Message` never reaches `processing_runs.error_message` (D-21 invariant); includes structural grep tests on job source files

### Frontend (T3–T7)

**Alert component** (`Frontend/src/components/ui/alert.tsx`):
- Created manually matching base-nova style (cva-driven, data-slot, default/destructive variants)
- `npx shadcn@latest add alert` is interactive-only and cannot be automated; direct creation matches the project's shadcn component pattern exactly

**Types** (`api.ts` additions):
- `ProcessingStatus` union (8 values, PascalCase matching backend enum serialization)
- `ReceiptFileErrorCode` union (6 stable codes)
- `ReceiptFileStatus` interface (D-13 shape)
- `TERMINAL_STATUSES` constant array
- `isTerminal()` helper function
- `UploadAcceptedResponse` / `UploadAcceptedFile` (202 Accepted shape)

**API client additions** (`api-client.ts`):
- `getReceiptFileStatus(id)` → `Promise<ReceiptFileStatus>`
- `cancelReceiptFile(id)` → `Promise<void>`; 409/404 propagate as `AxiosError`
- `uploadReceiptFiles()` updated from old `UploadReceiptFilesResponse` (200) to `UploadAcceptedResponse` (202)

**Hooks** (`use-receipt-files.ts`):
- `useReceiptFileStatus(receiptFileId)`: polls at 2s, stops via `refetchInterval: false` when `isTerminal()`
- `useCancelReceiptFile()`: mutation with German sonner toasts for 204/409/404/5xx outcomes

**Components:**
- `ReceiptFileStatusBadge`: Badge per ProcessingStatus with German labels + appropriate shadcn variants
- `ReceiptFileCard`: Card with live status badge, cancel button (disabled during mutation), Alert on failure errorMessage, link on Completed

**5 Frontend Surfaces Updated:**
1. **upload-form.tsx**: Rewritten for 202 shape — uploads returns `UploadAcceptedFile[]`, renders `ReceiptFileCard` stack
2. **receipts/page.tsx**: `ProcessingFileRow` component shows in-flight files with status badges; `refetchInterval` propagated to `ReceiptsTable` while non-terminal
3. **receipts/[id]/page.tsx**: `useReceiptFileStatus` drives Skeleton during processing, Alert on Failed/Cancelled
4. **dashboard/page.tsx**: Error Alert + Skeleton + empty-state card ("Noch keine Belege vorhanden")
5. **reports/page.tsx**: Error Alert + Skeleton + empty-state card ("Für dieses Jahr liegen noch keine bestätigten Belege vor")

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] ClassifyBatchJobTests cancellation test used pre-cancelled token**
- **Found during:** T2 integration
- **Issue:** Existing test passed `CancellationToken.None` but new generic catch checks `IsCancellationRequested`; test expected `Cancelled` status but got `Failed`
- **Fix:** Updated test to use `CancellationTokenSource` with Moq `Callback(() => cts.Cancel())` — token cancelled DURING the classify call, not before entry
- **Files modified:** `Backend/tests/TaxReader.UnitTests/Pipeline/ClassifyBatchJobTests.cs`
- **Commit:** f23abdb

**2. [Rule 2 - Missing functionality] shadcn Alert not installable via CLI**
- **Found during:** T3
- **Issue:** `npx shadcn@latest add alert` is interactive-only; cannot be automated in CI/executor context
- **Fix:** Created `alert.tsx` directly following the same base-nova style as `badge.tsx`, `skeleton.tsx`, `card.tsx` — cva + data-slot pattern, identical to what the CLI would generate
- **Files modified:** `Frontend/src/components/ui/alert.tsx`
- **Commit:** e13d04a

**3. [Rule 1 - Bug] Old upload form used synchronous 200 response shape**
- **Found during:** T6
- **Issue:** `upload-form.tsx` consumed `UploadReceiptFilesResponse.successful[]` / `.failed[]` which no longer matches the 202 shape
- **Fix:** Fully rewrote upload form to consume `UploadAcceptedResponse.files[]` and render `ReceiptFileCard` stack
- **Files modified:** `Frontend/src/components/upload/upload-form.tsx`
- **Commit:** 0e7c6fe

## Known Stubs

None — all status polling, error display, and cancel flows are wired to real backend endpoints.

## Threat Flags

None beyond the threat model in the plan (T-03-41..T-03-47). The alert component renders `data.errorMessage` as React text (not `dangerouslySetInnerHTML`), satisfying T-03-43.

## Self-Check: PASSED

- `Backend/src/TaxReader.Application/Common/UploadErrorCatalog.cs` — exists
- `Backend/tests/TaxReader.UnitTests/Pipeline/UploadErrorCatalogTests.cs` — exists
- `Backend/tests/TaxReader.UnitTests/Pipeline/JobErrorLeakageTests.cs` — exists
- `Frontend/src/components/ui/alert.tsx` — exists
- `Frontend/src/components/upload/receipt-file-status-badge.tsx` — exists
- `Frontend/src/components/upload/receipt-file-card.tsx` — exists
- `.planning/phases/03-background-pipeline-tesseract-pool/03-HUMAN-UAT.md` — exists
- Backend test suite: 217 passed, 0 failed
- Frontend build: succeeded (Next.js 16.2.2 Turbopack, all 12 routes)
- Frontend TypeScript: 0 errors
