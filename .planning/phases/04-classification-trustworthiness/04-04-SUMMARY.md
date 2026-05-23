---
phase: 04-classification-trustworthiness
plan: 04
subsystem: backend-domain, backend-application, backend-infrastructure, frontend-components
tags: [sum-validation, receipt-entity, ef-migration, classify-batch-job, receipt-dto, tdd]
dependency_graph:
  requires: [04-01, 04-02]
  provides: [HasSumMismatch-flag, AddHasSumMismatchToReceipts-migration, sum-validation-in-ClassifyBatchJob, class-06]
  affects: []
tech_stack:
  added: []
  patterns: [tdd-red-green, entity-flag-migration, dto-positional-record, inline-warning-icon]
key_files:
  created:
    - Backend/src/TaxReader.Infrastructure/Migrations/20260523165841_AddHasSumMismatchToReceipts.cs
    - Backend/src/TaxReader.Infrastructure/Migrations/20260523165841_AddHasSumMismatchToReceipts.Designer.cs
    - Backend/tests/TaxReader.UnitTests/Pipeline/SumValidationTests.cs
  modified:
    - Backend/src/TaxReader.Domain/Entities/Receipt.cs
    - Backend/src/TaxReader.Infrastructure/Data/Configurations/ReceiptConfiguration.cs
    - Backend/src/TaxReader.Infrastructure/Migrations/AppDbContextModelSnapshot.cs
    - Backend/src/TaxReader.Application/DTOs/ReceiptDto.cs
    - Backend/src/TaxReader.Application/Mapping/DtoMappingExtensions.cs
    - Backend/src/TaxReader.Application/Jobs/ClassifyBatchJob.cs
    - Frontend/src/components/receipts/receipts-table.tsx
decisions:
  - "Sum validation inserted BEFORE Finalize block in ClassifyBatchJob so HasSumMismatch saves in the same SaveChangesAsync as ProcessingStatus.Completed"
  - "D-16 tolerance is strict: Math.Abs(...) > 0.50m — difference of exactly €0.50 is NOT a mismatch"
  - "TDD RED: tests fail because ReceiptDto lacks HasSumMismatch; GREEN: ReceiptDto + ToDto() + ClassifyBatchJob updated; 8 new tests all pass"
  - "hasSumMismatch already present in Frontend/src/types/api.ts from Plan 04-01 — no change needed"
  - "AlertTriangle icon placed inside vendor cell flex div, inline after vendor name — does not replace or hide existing content"
metrics:
  duration: "~5 minutes"
  completed_date: "2026-05-23"
  tasks: 2
  files_modified: 7
---

# Phase 04 Plan 04: HasSumMismatch Sum Validation Summary

Receipt.HasSumMismatch bool flag with EF migration, ClassifyBatchJob D-16 sum validation (€0.50 absolute tolerance), ReceiptDto wiring, and frontend warning badge on mismatched receipts — delivering CLASS-06.

## What Was Built

### Task 1: Receipt entity + ReceiptConfiguration + EF migration

Added `HasSumMismatch` (bool, default false) to `Backend/src/TaxReader.Domain/Entities/Receipt.cs` after the `ParsedAt` property.

Updated `Backend/src/TaxReader.Infrastructure/Data/Configurations/ReceiptConfiguration.cs`:
- `builder.Property(e => e.HasSumMismatch).HasDefaultValue(false).IsRequired();`

Created `20260523165841_AddHasSumMismatchToReceipts` EF Core migration:
- `AddColumn<bool>("has_sum_mismatch", "receipts", nullable: false, defaultValue: false)`
- `Down()` drops the column

**Verification:** `dotnet build Backend --no-incremental` exits 0 (2 pre-existing NU1510 warnings only).

### Task 2: ClassifyBatchJob sum validation + ReceiptDto wiring + frontend warning badge

**TDD RED phase** — `SumValidationTests.cs` written first with 8 tests covering:
- `HandleAsync_ItemSumsMatchTotal_HasSumMismatchIsFalse` — exact match → no mismatch
- `HandleAsync_ItemSumsExceedToleranceDifference_HasSumMismatchIsTrue` — |diff| > €0.50 → mismatch
- `HandleAsync_ItemSumDifferenceExactlyAtTolerance_HasSumMismatchIsFalse` — exactly €0.50 → no mismatch (strict `>`)
- `HandleAsync_ItemSumDifferenceJustOverTolerance_HasSumMismatchIsTrue` — €0.51 → mismatch
- `HandleAsync_SumValidation_SavedInSameCallAsCompletedStatus` — both flags persisted together
- `ReceiptDtoSumMismatchMappingTests` (3 tests) — ReceiptDto.HasSumMismatch field + ToDto() wiring

Tests failed to compile (RED gate confirmed — `ReceiptDto` had no `HasSumMismatch` field).

**TDD GREEN phase:**

1. `Backend/src/TaxReader.Application/DTOs/ReceiptDto.cs`: Added `bool HasSumMismatch` as required positional parameter between `UnknownCount` and optional `string? RawExtractedText`.

2. `Backend/src/TaxReader.Application/Mapping/DtoMappingExtensions.cs`: Added `entity.HasSumMismatch` as 13th positional argument in `Receipt.ToDto()` return constructor.

3. `Backend/src/TaxReader.Application/Jobs/ClassifyBatchJob.cs`: Inserted D-16 sum validation block AFTER the classification foreach loop and BEFORE the Finalize block:
   ```csharp
   // D-16: sum validation — compare item totals against receipt total (€0.50 absolute tolerance).
   foreach (var run in runs.Where(r => r.ReceiptFile.Receipt is not null))
   {
       var receipt = run.ReceiptFile.Receipt!;
       var itemsSum = receipt.Items.Sum(i => i.TotalPrice);
       if (Math.Abs(itemsSum - receipt.TotalAmount) > 0.50m)
           receipt.HasSumMismatch = true;
   }
   ```
   Both `HasSumMismatch` and `r.Status = ProcessingStatus.Completed` are saved in the same `dbContext.SaveChangesAsync(cancellationToken)` call.

4. `Frontend/src/components/receipts/receipts-table.tsx`: Added `AlertTriangle` import from `lucide-react`. Added inline warning icon after vendor name in the vendor cell for rows where `receipt.hasSumMismatch === true`:
   ```tsx
   {receipt.hasSumMismatch && (
     <AlertTriangle
       className="inline h-3.5 w-3.5 text-amber-500 ml-1"
       aria-label="Summe stimmt nicht überein"
     />
   )}
   ```

**Verification:** `dotnet test Backend` → 231 passed, 0 failed (223 pre-existing + 8 new). `npm run build` exits 0.

## Deviations from Plan

None — plan executed exactly as written.

## Known Stubs

None — `HasSumMismatch` is fully wired from domain entity through EF, DTO, mapping, API response, and frontend indicator. No placeholders or hardcoded values.

## Threat Flags

None — no new network endpoints introduced. T-04-04-01 (tampering) is mitigated as designed: `HasSumMismatch` can only be set to `true` by `ClassifyBatchJob` (automated, no user control). T-04-04-02 (information disclosure) accepted: flag reveals only sum mismatch, no PII, scoped to authenticated user's own receipts. T-04-04-03 (DoS) accepted: bounded item count, O(n) LINQ Sum.

## Self-Check: PASSED

Verified:
- `Backend/src/TaxReader.Domain/Entities/Receipt.cs` contains `public bool HasSumMismatch { get; set; } = false;` — CONFIRMED (grep returns 1)
- Migration `20260523165841_AddHasSumMismatchToReceipts.cs` exists — CONFIRMED
- Migration contains `defaultValue: false` for `has_sum_mismatch` column — CONFIRMED
- `ReceiptConfiguration.cs` contains `HasDefaultValue(false)` and `HasSumMismatch` — CONFIRMED
- `ClassifyBatchJob.cs` contains `Math.Abs(itemsSum - receipt.TotalAmount) > 0.50m` — CONFIRMED (grep returns 1)
- Sum validation block appears BEFORE `r.Status = ProcessingStatus.Completed` in source (lines 119-127 vs 133) — CONFIRMED
- `ClassifyBatchJob.cs` contains `ThenInclude` — CONFIRMED (grep returns 2)
- `ReceiptDto.cs` contains `bool HasSumMismatch` — CONFIRMED (grep returns 1)
- `DtoMappingExtensions.cs` contains `entity.HasSumMismatch` — CONFIRMED (grep returns 1)
- `Frontend/src/types/api.ts` contains `hasSumMismatch: boolean` — CONFIRMED (grep returns 1)
- `Frontend/src/components/receipts/receipts-table.tsx` contains `hasSumMismatch` and `AlertTriangle` — CONFIRMED
- `dotnet test Backend`: 231 passed, 0 failed — CONFIRMED
- `npm run build`: exits 0 — CONFIRMED

## TDD Gate Compliance

- RED gate: `test(04-04): add failing sum validation tests (RED phase)` commit (31dda18) — tests failed to compile (ReceiptDto missing HasSumMismatch parameter — compile error is a valid RED state)
- GREEN gate: `feat(04-04): ClassifyBatchJob sum validation + ReceiptDto wiring + frontend warning badge` commit (611871c) — all 8 new tests pass

**Commits:**
- ea58a0b: feat(04-04): Receipt entity + ReceiptConfiguration + AddHasSumMismatchToReceipts migration
- 31dda18: test(04-04): add failing sum validation tests (RED phase)
- 611871c: feat(04-04): ClassifyBatchJob sum validation + ReceiptDto wiring + frontend warning badge
