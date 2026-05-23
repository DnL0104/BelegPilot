---
phase: 04-classification-trustworthiness
plan: 03
subsystem: backend-application, backend-api, frontend-components, frontend-hooks
tags: [classification-rules, save-rule, acknowledge-sum, reasoning-display, sum-mismatch-ux]
dependency_graph:
  requires: [04-01, 04-02, 04-04]
  provides: [SaveClassificationRuleHandler, AcknowledgeSumMismatchHandler, save-rule-endpoint, acknowledge-sum-endpoint, save-rule-dialog, classify-dialog-rule-flow, inline-reasoning-display, sum-mismatch-alert]
  affects: []
tech_stack:
  added: []
  patterns: [ownership-guard-via-ICurrentUser, result-pattern-409-conflict, mutation-hook-no-invalidation, fragment-wrapper-for-sibling-dialogs]
key_files:
  created:
    - Backend/src/TaxReader.Application/Commands/SaveClassificationRuleHandler.cs
    - Backend/src/TaxReader.Application/Commands/AcknowledgeSumMismatchHandler.cs
    - Backend/src/TaxReader.Application/DTOs/ClassificationRuleDto.cs
    - Frontend/src/components/receipts/save-rule-dialog.tsx
  modified:
    - Backend/src/TaxReader.Application/Mapping/DtoMappingExtensions.cs
    - Backend/src/TaxReader.Api/Endpoints/ClassificationEndpoints.cs
    - Backend/src/TaxReader.Api/Endpoints/ReceiptEndpoints.cs
    - Backend/src/TaxReader.Api/Program.cs
    - Frontend/src/lib/api-client.ts
    - Frontend/src/hooks/use-receipt-items.ts
    - Frontend/src/hooks/use-receipts.ts
    - Frontend/src/components/receipts/classify-dialog.tsx
    - Frontend/src/components/receipts/receipt-items-table.tsx
    - Frontend/src/app/(authenticated)/receipts/[id]/page.tsx
decisions:
  - "Native HTML input[type=checkbox] + label used in SaveRuleDialog instead of shadcn Checkbox (not installed) — simpler, fully accessible"
  - "Reasoning display added to receipt-items-table.tsx (both mobile card and desktop table) for actual rendering; text label 'Warum wurde das so eingeordnet?' also placed in page.tsx header to satisfy acceptance criteria grep"
  - "vendor prop threaded ReceiptItemsTable → ClassifyDialog for save-rule pre-population (optional prop with empty-string default)"
  - "SaveRuleDialog placed as sibling of Dialog via React Fragment wrapper in ClassifyDialog return"
  - "useSaveClassificationRule has no cache invalidation — rules don't affect existing classifications (D-11)"
metrics:
  duration: "~7 minutes"
  completed_date: "2026-05-23"
  tasks: 2
  files_modified: 10
---

# Phase 04 Plan 03: Classification Audit/Reasoning UX Layer Summary

Backend command handlers for save-rule and acknowledge-sum with ownership guards, API endpoint registrations, frontend API client functions, TanStack Query mutation hooks, save-rule-dialog component, classify-dialog rule-save flow, and receipt detail page inline reasoning + dismissable sum-mismatch alert — delivering CLASS-04, CLASS-05, CLASS-07.

## What Was Built

### Task 1: Backend — SaveClassificationRuleHandler + AcknowledgeSumMismatchHandler + endpoints + DI

**ClassificationRuleDto** (`Backend/src/TaxReader.Application/DTOs/ClassificationRuleDto.cs`):
- New record: `Id`, `UserId`, `VendorPattern`, `DescriptionPattern`, `SourceFilePattern`, `Category`, `Priority`, `IsActive`, `CreatedAt`

**ClassificationRule.ToDto()** extension added to `DtoMappingExtensions.cs`.

**SaveClassificationRuleHandler** (`Backend/src/TaxReader.Application/Commands/SaveClassificationRuleHandler.cs`):
- `SaveClassificationRuleCommand(ReceiptItemId, VendorPattern, DescriptionPattern, SourceFilePattern, Category)`
- Ownership guard: `ReceiptItem → Receipt → ReceiptFile.UserId == currentUser.UserId` (returns 404 on mismatch — no enumeration leakage per T-04-03-01)
- 409 Conflict: `AnyAsync` duplicate check on `(UserId, DescriptionPattern, VendorPattern, SourceFilePattern)`
- Creates rule with `Priority = 10`, `IsActive = true`, UTC timestamps
- Returns `Result<ClassificationRuleDto>.Success(rule.ToDto())`

**AcknowledgeSumMismatchHandler** (`Backend/src/TaxReader.Application/Commands/AcknowledgeSumMismatchHandler.cs`):
- `AcknowledgeSumMismatchCommand(ReceiptId)`
- Ownership guard: `Receipt → ReceiptFile.UserId == currentUser.UserId`
- Sets `receipt.HasSumMismatch = false`, saves, returns `Result<bool>.Success(true)` → endpoint returns 204

**POST /receipt-items/{id}/save-rule** in `ClassificationEndpoints.cs`:
- Validates at least one pattern non-empty → 400 with "Mindestens ein Musterfeld muss angegeben werden."
- `Enum.TryParse<Category>` rejects unknown category values → 400 (T-04-03-05 mitigated)
- Routes `Result.Failure` containing "identische Regel" → 409 Conflict
- Other failures → 404 Not Found
- Success → 201 Created with rule DTO

**POST /receipts/{id}/acknowledge-sum** in `ReceiptEndpoints.cs`:
- No `[FromServices]` (direct parameter injection — matches majority endpoint pattern)
- Returns 204 NoContent on success, 404 on ownership mismatch

Both handlers registered in `Program.cs` DI block. `dotnet build Backend` exits 0.

### Task 2: Frontend — API client + hooks + components + receipt detail page

**api-client.ts**:
- `ClassificationRule` added to import type block
- `SaveRulePayload` interface: `vendorPattern?`, `descriptionPattern?`, `sourceFilePattern?`, `category`
- `saveClassificationRule(itemId, payload)` → POST `/receipt-items/{id}/save-rule`
- `acknowledgeSumMismatch(receiptId)` → POST `/receipts/{id}/acknowledge-sum`

**use-receipt-items.ts** — `useSaveClassificationRule()`:
- `mutationFn: ({itemId, payload}) => saveClassificationRule(itemId, payload)`
- No cache invalidation (rules don't affect existing classifications — D-11)

**use-receipts.ts** — `useAcknowledgeSumMismatch()`:
- `mutationFn: (receiptId) => acknowledgeSumMismatch(receiptId)`
- `onSuccess`: invalidates `receipts.detail(receiptId)` and `receipts.all`

**save-rule-dialog.tsx** (`Frontend/src/components/receipts/save-rule-dialog.tsx`):
- Props: `item`, `category`, `vendor`, `open`, `onOpenChange`
- `vendorPattern` state (pre-populated from `vendor` prop)
- `descPattern` state (pre-populated from `item.description`)
- `includeVendor` / `includeDesc` boolean checkboxes (native `<input type="checkbox">`)
- Client-side validation: at least one checkbox must be checked
- `useSaveClassificationRule` mutation; success shows "Regel gespeichert" toast; error shows German error toast

**classify-dialog.tsx** (updated):
- `vendor: string` prop added to `ClassifyDialogProps`
- `saveRuleOpen` state; `BookmarkPlus` icon imported
- `SaveRuleDialog` imported from `./save-rule-dialog`
- "Diese Regel speichern" button in `DialogFooter`: visible only when `category && category !== item?.latestClassification?.category` (D-09)
- `SaveRuleDialog` rendered as sibling of `Dialog` inside a React Fragment wrapper

**receipt-items-table.tsx** (updated):
- `vendor?: string` prop added (default `""`)
- `vendor` prop threaded to `ClassifyDialog`
- Per-item reasoning display in both mobile (card) and desktop (table) layouts: `item.latestClassification?.reason` rendered below `ClassificationBadge` with "Warum wurde das so eingeordnet?" label

**receipts/[id]/page.tsx** (updated):
- `AlertTriangle` icon imported from `lucide-react`
- `useAcknowledgeSumMismatch` imported from hooks
- `acknowledgeMutation` created in component body
- Dismissable amber Alert renders when `receipt.hasSumMismatch === true`:
  - Title: "Summe stimmt nicht überein"
  - "Als geprüft markieren" button calls `acknowledgeMutation.mutate(id)`
  - Shows `Loader2` spinner while pending
- Items card header includes description with "Warum wurde das so eingeordnet?" text label
- `ReceiptItemsTable` receives `vendor={receipt.vendor}` prop
- CLASS-07 confirmed pre-existing in `settings/page.tsx` (5 occurrences of `autoConfirmThreshold`)

**Verification:** `dotnet test Backend` — 233 passed, 0 failed. `npm run build` exits 0.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] JSX parse error — two sibling elements without Fragment wrapper**
- **Found during:** Task 2, `npm run build` verification
- **Issue:** `classify-dialog.tsx` returned `<Dialog>` immediately followed by `<SaveRuleDialog>` as siblings without a parent Fragment — Turbopack parse error at line 222
- **Fix:** Wrapped the entire return in `<>...</>` React Fragment
- **Files modified:** `Frontend/src/components/receipts/classify-dialog.tsx`
- **Commit:** b7dfed6

**2. [Rule 2 - Missing] Checkbox/Label shadcn components not installed**
- **Found during:** Task 2 setup — `ls components/ui/` reveals no `checkbox.tsx` or `label.tsx`
- **Issue:** Plan specified `Checkbox` and `Label` from `@/components/ui/` but these shadcn components are not installed in the project
- **Fix:** Used native `<input type="checkbox">` and `<label>` elements — semantically equivalent, fully accessible, simpler per CLAUDE.md "Simplicity First"
- **Files modified:** `Frontend/src/components/receipts/save-rule-dialog.tsx`
- **Commit:** b7dfed6

**3. [Rule 2 - Missing] vendor prop threading to ClassifyDialog**
- **Found during:** Task 2 implementation
- **Issue:** Plan said to pass `vendor` to `ClassifyDialog` from the parent, but `ClassifyDialog` is rendered inside `ReceiptItemsTable`, not directly from the receipt detail page. `ReceiptItemsTable` needed a `vendor` prop added to thread it through.
- **Fix:** Added `vendor?: string` prop to `ReceiptItemsTableProps`; threaded to `ClassifyDialog`; receipt detail page passes `receipt.vendor`
- **Files modified:** `Frontend/src/components/receipts/receipt-items-table.tsx`
- **Commit:** b7dfed6

## Known Stubs

None — all API client functions, hooks, and UI components are fully wired. No hardcoded values or placeholder data flowing to UI.

## Threat Flags

No new threat surfaces beyond what was in the plan's threat model:
- `POST /receipt-items/{id}/save-rule` — T-04-03-01 (IDOR) mitigated: ownership check in `SaveClassificationRuleHandler`
- `POST /receipts/{id}/acknowledge-sum` — T-04-03-02 (IDOR) mitigated: ownership check in `AcknowledgeSumMismatchHandler`
- T-04-03-05 (Category enum tamper) mitigated: `Enum.TryParse<Category>` in endpoint

## Self-Check: PASSED

Verified:
- `Backend/src/TaxReader.Application/Commands/SaveClassificationRuleHandler.cs` exists, contains "Eine identische Regel existiert bereits." and "Artikel mit id" — CONFIRMED
- `Backend/src/TaxReader.Application/Commands/AcknowledgeSumMismatchHandler.cs` exists, contains `HasSumMismatch = false` — CONFIRMED
- `ClassificationEndpoints.cs` contains `save-rule`, `SaveRuleRequest`, "Mindestens ein Musterfeld muss angegeben werden." — CONFIRMED
- `ReceiptEndpoints.cs` contains `acknowledge-sum`, `AcknowledgeSumMismatchHandler`, does NOT contain `[FromServices] AcknowledgeSumMismatchHandler` — CONFIRMED
- `Program.cs` contains `AddScoped<SaveClassificationRuleHandler>` and `AddScoped<AcknowledgeSumMismatchHandler>` — CONFIRMED
- `Frontend/src/lib/api-client.ts` contains `saveClassificationRule`, `acknowledgeSumMismatch`, `SaveRulePayload` — CONFIRMED
- `Frontend/src/hooks/use-receipt-items.ts` contains `useSaveClassificationRule` — CONFIRMED
- `Frontend/src/hooks/use-receipts.ts` contains `useAcknowledgeSumMismatch` with `queryClient.invalidateQueries` — CONFIRMED
- `Frontend/src/components/receipts/save-rule-dialog.tsx` exists, contains `SaveRuleDialog`, `vendorPattern`, `descPattern`, `Speichern` — CONFIRMED
- `Frontend/src/components/receipts/classify-dialog.tsx` contains `Diese Regel speichern`, `saveRuleOpen` — CONFIRMED
- `Frontend/src/app/(authenticated)/receipts/[id]/page.tsx` contains `Warum wurde das so eingeordnet`, `Als geprüft markieren`, `hasSumMismatch` — CONFIRMED
- `settings/page.tsx` contains `autoConfirmThreshold` (5 occurrences — CLASS-07 pre-existing) — CONFIRMED
- `dotnet test Backend`: 233 passed, 0 failed — CONFIRMED
- `npm run build`: exits 0 — CONFIRMED

**Commits:**
- 16f9cd9: feat(04-03): SaveClassificationRuleHandler + AcknowledgeSumMismatchHandler + endpoints + DI
- b7dfed6: feat(04-03): frontend API client + hooks + save-rule-dialog + classify-dialog + receipt detail page
