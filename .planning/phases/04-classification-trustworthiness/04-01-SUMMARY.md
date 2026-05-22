---
phase: 04-classification-trustworthiness
plan: 01
subsystem: backend-domain, backend-infrastructure, frontend-types
tags: [category-enum, migration, export, frontend-types]
dependency_graph:
  requires: []
  provides: [13-German-category-enum, ExpandCategoryEnum-migration, updated-exports, frontend-Category-type]
  affects: [04-02, 04-03, 04-04]
tech_stack:
  added: []
  patterns: [enum-rename-migration, HasConversion-string, raw-SQL-UPDATE-in-migration]
key_files:
  created:
    - Backend/src/TaxReader.Infrastructure/Migrations/20260522183745_ExpandCategoryEnum.cs
    - Backend/src/TaxReader.Infrastructure/Migrations/20260522183745_ExpandCategoryEnum.Designer.cs
  modified:
    - Backend/src/TaxReader.Domain/Enums/Category.cs
    - Backend/src/TaxReader.Infrastructure/Data/Configurations/ClassificationRuleConfiguration.cs
    - Backend/src/TaxReader.Infrastructure/Services/AiOnlyClassificationService.cs
    - Backend/src/TaxReader.Infrastructure/Services/ClaudeAiClassifier.cs
    - Backend/src/TaxReader.Infrastructure/Services/PdfExportService.cs
    - Backend/src/TaxReader.Infrastructure/Services/CsvExportService.cs
    - Backend/src/TaxReader.Application/Commands/BatchConfirmHandler.cs
    - Backend/src/TaxReader.Application/Interfaces/IAiClassifier.cs
    - Backend/src/TaxReader.Application/Mapping/DtoMappingExtensions.cs
    - Backend/src/TaxReader.Application/Queries/GetAnnualSummaryHandler.cs
    - Backend/src/TaxReader.Application/Queries/GetCategoryTotalsHandler.cs
    - Backend/src/TaxReader.Application/Queries/GetExportDataHandler.cs
    - Backend/src/TaxReader.Application/Queries/GetPendingSuggestionsHandler.cs
    - Backend/src/TaxReader.Application/Validators/ConfirmClassificationValidator.cs
    - Backend/tests/TaxReader.UnitTests/Application/Commands/ConfirmClassificationHandlerTests.cs
    - Backend/tests/TaxReader.UnitTests/Application/Mapping/DtoMappingExtensionsTests.cs
    - Backend/tests/TaxReader.UnitTests/Application/Queries/GetAnnualSummaryHandlerTests.cs
    - Backend/tests/TaxReader.UnitTests/Application/Queries/GetCategoryTotalsHandlerTests.cs
    - Backend/tests/TaxReader.UnitTests/Application/Validators/ConfirmClassificationValidatorTests.cs
    - Backend/tests/TaxReader.UnitTests/Domain/ReceiptItemTests.cs
    - Backend/tests/TaxReader.UnitTests/Helpers/TestDataFactory.cs
    - Frontend/src/types/api.ts
    - Frontend/src/lib/format.ts
    - Frontend/src/components/receipts/classify-dialog.tsx
    - Frontend/src/components/receipts/classification-badge.tsx
    - Frontend/src/components/receipts/receipt-items-table.tsx
    - Frontend/src/components/dashboard/category-overview.tsx
    - Frontend/src/components/reports/category-breakdown.tsx
decisions:
  - "Category.Unknown → Category.Unbekannt (exact rename, value stays 0)"
  - "ClaudeAiClassifier system prompt updated to use new 13 German category names so AI responses parse correctly via Enum.TryParse"
  - "Historical migration Designer.cs files left with old category strings — correct, they are immutable migration snapshots; ExpandCategoryEnum.Down() contains old strings for rollback"
  - "ReceiptFileErrorCode type keeps | 'Unknown' — distinct concept from Category enum, not a classification category"
  - "Worktree-isolation pattern: worktree has no node_modules symlink, so frontend build was verified by temporarily copying changed files to main repo Frontend/ for npm run build check"
metrics:
  duration: "~45 minutes"
  completed_date: "2026-05-22"
  tasks: 2
  files_modified: 29
---

# Phase 04 Plan 01: Category Enum Expansion to 13 German DE Tax Identifiers Summary

Category enum replaced from 8 English teacher-specific values to 13 German DE tax identifiers (Unbekannt through Privat), with full migration, export, and frontend type updates.

## What Was Built

### Task 1: Replace Category enum + create ExpandCategoryEnum EF migration

Replaced `Backend/src/TaxReader.Domain/Enums/Category.cs` with exactly 13 German identifiers:
- `Unbekannt = 0` (replaces `Unknown = 0`)
- `WerbungskostenArbeitsmittel = 1` through `Privat = 12`

Created `ExpandCategoryEnum` EF Core migration:
- EF-generated `UpdateData` calls remap all `classification_rules` seed rows to new German identifiers (40 rows)
- Inline raw SQL `UPDATE item_classifications SET category = 'WerbungskostenBueromaterial' WHERE category = 'ConsumablesAndOfficeSupplies'` (8 UPDATE statements covering all 8 old values → new values)
- `Down()` method includes reverse SQL for rollback

All 30+ `Category.Unknown` references in application and infrastructure layers updated to `Category.Unbekannt`. ClaudeAiClassifier system prompt updated to list the 13 new German category names so AI responses parse correctly via `Enum.TryParse`. All unit tests updated.

**Verification:** `dotnet build Backend --no-incremental` exits 0 (2 warnings — pre-existing NU1510).

### Task 2: Update PDF/CSV exports + frontend Category type and label map

Backend:
- `PdfExportService.CategoryLabels`: 8-entry old map → 13-entry German display strings (e.g. `"WerbungskostenArbeitsmittel" → "Werbungskosten – Arbeitsmittel"`)
- `CsvExportService.CategoryLabels`: same 13-entry German display strings

Frontend:
- `types/api.ts`: `Category` type union updated (8 old → 13 new); `hasSumMismatch: boolean` added to `Receipt`; `ClassificationRule` interface added
- `lib/format.ts`: `categoryLabel()` 13-entry label map
- `components/receipts/classify-dialog.tsx`: `categories` array updated to 13 new identifiers; all `"Unknown"` string comparisons → `"Unbekannt"`
- `components/receipts/classification-badge.tsx`: `categoryStyles` map updated to 13 identifiers with appropriate Tailwind colors
- `components/receipts/receipt-items-table.tsx`: `category !== "Unknown"` → `category !== "Unbekannt"`
- `components/dashboard/category-overview.tsx`: `categoryBarColors` map updated to 13 identifiers
- `components/reports/category-breakdown.tsx`: `categoryColors` and `categoryCardBg` maps updated to 13 identifiers

**Verification:** `dotnet test Backend` — 217 passed, 0 failed. `npm run build` exits 0.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Additional Category.Unknown references found beyond plan scope**
- **Found during:** Task 1 build verification
- **Issue:** Plan identified `AiOnlyClassificationService.cs` as the primary file needing `Category.Unknown → Category.Unbekannt`, but grep revealed 9 additional files across Application layer (`DtoMappingExtensions.cs`, 3 query handlers, `ConfirmClassificationValidator.cs`, `BatchConfirmHandler.cs`) plus `IAiClassifier.cs` comment and `ClaudeAiClassifier.cs` (3 occurrences + system prompt)
- **Fix:** Updated all occurrences across all backend source and test files
- **Files modified:** 10 additional backend files beyond plan specification
- **Commits:** f7304c5

**2. [Rule 1 - Bug] Frontend color map files had old category strings**
- **Found during:** Task 2 final verification grep
- **Issue:** `category-overview.tsx` and `category-breakdown.tsx` had hardcoded old English category keys in their color map dictionaries; plan's instruction to "search for old Category references in Frontend/src/" caught them
- **Fix:** Updated both files' color maps to 13 new German identifiers with Tailwind color assignments
- **Commits:** 406b139

**3. [Rule 2 - Missing] ClaudeAiClassifier system prompt still listed old English category names**
- **Found during:** Task 1 review of ClaudeAiClassifier.cs
- **Issue:** The AI system prompt embedded in ClaudeAiClassifier listed the 8 old English category names (`ConsumablesAndOfficeSupplies`, `SpecialistLiterature`, etc.). After the enum rename, `Enum.TryParse<Category>` would fail to parse any AI response using old names, falling back to `Category.Unbekannt` for everything. This would silently break AI classification.
- **Fix:** Updated system prompt to list all 13 new German identifiers with German descriptions
- **Files modified:** `Backend/src/TaxReader.Infrastructure/Services/ClaudeAiClassifier.cs`
- **Commits:** f7304c5

## Known Stubs

None — all 13 category labels are fully wired in both backend exports and frontend display.

## Threat Flags

None — this plan is a pure rename/remapping. No new network endpoints, auth paths, or trust boundaries introduced. The migration SQL UPDATE operates on known string values with no user input involved (T-04-01-01 mitigated as designed).

## Self-Check: PASSED

Verified:
- `Backend/src/TaxReader.Domain/Enums/Category.cs` contains `WerbungskostenArbeitsmittel` and `Unbekannt` and does NOT contain `Unknown` or `ConsumablesAndOfficeSupplies` — CONFIRMED
- Migration file `20260522183745_ExpandCategoryEnum.cs` exists — CONFIRMED
- Migration contains `UPDATE item_classifications SET category = 'WerbungskostenBueromaterial'` — CONFIRMED
- `AiOnlyClassificationService.cs` contains `Category.Unbekannt` and NOT `Category.Unknown` — CONFIRMED
- `PdfExportService.cs` contains `WerbungskostenArbeitsmittel` and `Außergewöhnliche Belastungen – Krankheit` — CONFIRMED
- `Frontend/src/types/api.ts` contains `"WerbungskostenArbeitsmittel"` in Category union and `hasSumMismatch: boolean` — CONFIRMED
- `Frontend/src/lib/format.ts` contains `WerbungskostenArbeitsmittel: "Werbungskosten – Arbeitsmittel"` — CONFIRMED
- `dotnet test Backend`: 217 passed, 0 failed — CONFIRMED
- `npm run build`: exited 0 — CONFIRMED

**Commits:**
- f7304c5: feat(04-01): replace Category enum + ExpandCategoryEnum migration
- f1c54fb: feat(04-01): update PDF/CSV exports + frontend Category type and label map
- 406b139: fix(04-01): update remaining old category strings in dashboard/reports color maps
