---
phase: 04-classification-trustworthiness
plan: 02
subsystem: backend-domain, backend-infrastructure, backend-tests
tags: [classification-rules, hybrid-service, rule-based-classifier, migration, ef-core]
dependency_graph:
  requires: [04-01]
  provides: [UpdateClassificationRuleSchema-migration, RuleBasedClassifier, HybridClassificationService, class-02-rules]
  affects: [04-03, 04-04]
tech_stack:
  added: []
  patterns: [rules-first-then-AI-hybrid, user-rule-priority-over-system-rule, regex-IgnoreCase-matching, OrdinalIgnoreCase-substring]
key_files:
  created:
    - Backend/src/TaxReader.Infrastructure/Services/RuleBasedClassifier.cs
    - Backend/src/TaxReader.Infrastructure/Services/HybridClassificationService.cs
    - Backend/src/TaxReader.Infrastructure/Migrations/20260522185722_UpdateClassificationRuleSchema.cs
    - Backend/src/TaxReader.Infrastructure/Migrations/20260522185722_UpdateClassificationRuleSchema.Designer.cs
    - Backend/tests/TaxReader.UnitTests/Services/RuleBasedClassifierTests.cs
  modified:
    - Backend/src/TaxReader.Domain/Entities/ClassificationRule.cs
    - Backend/src/TaxReader.Infrastructure/Data/Configurations/ClassificationRuleConfiguration.cs
    - Backend/src/TaxReader.Infrastructure/Migrations/AppDbContextModelSnapshot.cs
    - Backend/src/TaxReader.Infrastructure/DependencyInjection.cs
    - Backend/tests/TaxReader.UnitTests/Helpers/TestDataFactory.cs
decisions:
  - "EF Core fix-up automatically resolves item.Receipt.ReceiptFile from the already-loaded ReceiptFile entity; adding a ThenInclude(r => r.ReceiptFile) causes NavigationBaseIncludeIgnored InvalidOperationException in InMemory — removed from ClassifyBatchJob; fix-up is correct and sufficient"
  - "TestDataFactory.CreateRule() signature updated: pattern param split into descriptionPattern/vendorPattern/sourceFilePattern/userId; old single Pattern removed"
  - "Migration uses RenameColumn(pattern → description_pattern) instead of EF-generated DropColumn + AddColumn to preserve existing description data in live databases"
metrics:
  duration: "~9 minutes"
  completed_date: "2026-05-22"
  tasks: 2
  files_modified: 10
---

# Phase 04 Plan 02: ClassificationRule Schema + RuleBasedClassifier + HybridClassificationService Summary

Three-field ClassificationRule schema (UserId, VendorPattern, SourceFilePattern, DescriptionPattern), UpdateClassificationRuleSchema EF migration with data-preserving RenameColumn, RuleBasedClassifier with D-06 user-first evaluation order, HybridClassificationService that sends one AI batch call for rule-unmatched items, and 6 unit tests covering all rule-matching scenarios.

## What Was Built

### Task 1: Update ClassificationRule entity + EF migration + ClassificationRuleConfiguration

Replaced `ClassificationRule.Pattern` (single string) with the three-field schema:
- `UserId` (Guid?) — null = system rule, non-null = user-private rule
- `VendorPattern` (string?) — substring match, OrdinalIgnoreCase
- `SourceFilePattern` (string?) — regex match, RegexOptions.IgnoreCase
- `DescriptionPattern` (string?) — regex match, RegexOptions.IgnoreCase (formerly `Pattern`)

Updated `ClassificationRuleConfiguration.cs`:
- Property declarations for all 4 new fields
- FK to `users` table with `IsRequired(false)` and `OnDelete(DeleteBehavior.Cascade)` (user deletion cascades to user-private rules)
- Index changed from `(IsActive, Priority)` to `(UserId, IsActive, Priority)`
- All 44 seed rows updated: `Pattern = "..."` → `DescriptionPattern = "..."` + `UserId = (Guid?)null` + `VendorPattern = null` + `SourceFilePattern = null`

Created `20260522185722_UpdateClassificationRuleSchema` migration:
- `RenameColumn("pattern" → "description_pattern")` — data-preserving instead of EF's default DropColumn+AddColumn
- `AddColumn` for `source_file_pattern`, `user_id`, `vendor_pattern`
- `UpdateData` for all 44 seed rows (sets new columns to null)
- `CreateIndex` for `(user_id, is_active, priority)`
- `AddForeignKey` for `user_id → users.id` with Cascade

`TestDataFactory.CreateRule()` updated: single `pattern` parameter split into `descriptionPattern`, `vendorPattern`, `sourceFilePattern`, `userId`.

**Verification:** `dotnet build Backend --no-incremental` exits 0 (2 pre-existing NU1510 warnings only).

### Task 2: RuleBasedClassifier + HybridClassificationService + DI registration + unit tests

**RuleBasedClassifier** (`Backend/src/TaxReader.Infrastructure/Services/RuleBasedClassifier.cs`):
- D-06 evaluation order: user rules first (`UserId == userId`), system rules second (`UserId == null`)
- All rules ordered by `Priority DESC`
- Rule fires when ALL non-null fields match: `VendorPattern` substring OrdinalIgnoreCase, `DescriptionPattern` and `SourceFilePattern` regex IgnoreCase
- Returns `ItemClassification` with `Method = Rule`, `Status = Confirmed`, `Reason = "Regel angewendet: {pattern} → {category}"`
- Returns `null` if no rule matches

**HybridClassificationService** (`Backend/src/TaxReader.Infrastructure/Services/HybridClassificationService.cs`):
- Iterates every item, calls `RuleBasedClassifier.ClassifyItemAsync` per item
- Collects rule-unmatched items into `aiItems` list
- Makes ONE AI batch call (`AiOnlyClassificationService.ClassifyItemsAsync(aiItems, userId, ct)`) for all unmatched items
- Combines rule results + AI results and returns
- Preserves D-01 single-Anthropic-call-per-upload invariant from Phase 3

**DependencyInjection.cs**:
- `services.AddScoped<AiOnlyClassificationService>()` (concrete, for injection into HybridClassificationService)
- `services.AddScoped<RuleBasedClassifier>()` (concrete)
- `services.AddScoped<IClassificationService, HybridClassificationService>()` (interface registration)

**Unit tests** (6 tests, all pass):
- `ClassifyItemAsync_UserRuleMatchesBeforeSystemRule_ReturnsUserRuleClassification`
- `ClassifyItemAsync_NoUserRuleMatch_SystemRuleFallback_ReturnsSystemRuleClassification`
- `ClassifyItemAsync_NoRuleMatches_ReturnsNull`
- `ClassifyItemAsync_VendorPatternOnly_MatchesCaseInsensitive`
- `ClassifyItemAsync_AllFieldsMustMatch_PartialMatchDoesNotFire`
- `ClassifyItemAsync_RuleMatchedResult_HasCorrectMethodAndStatus`

**Verification:** `dotnet test Backend` — 223 passed, 0 failed (217 pre-existing + 6 new).

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] EF ThenInclude(r => r.ReceiptFile) causes NavigationBaseIncludeIgnored error**
- **Found during:** Task 2, running full test suite after adding explicit Receipt.ReceiptFile include to ClassifyBatchJob
- **Issue:** The plan's acceptance criteria states `ClassifyBatchJob` should contain `.ThenInclude(r => r.ReceiptFile)`. However, adding `.ThenInclude(f => f.Receipt).ThenInclude(r => r!.ReceiptFile)` causes EF Core to throw `InvalidOperationException: NavigationBaseIncludeIgnored` ("Walking back include tree is not allowed") because `Receipt.ReceiptFile` is a back-reference to an already-included entity. All 7 existing ClassifyBatchJob/JobErrorLeakage tests fail.
- **Fix:** Reverted the extra include from ClassifyBatchJob. EF Core change tracker fix-up automatically populates `item.Receipt.ReceiptFile` from the already-tracked `ReceiptFile` entity (loaded via `.Include(r => r.ReceiptFile)`). The fix-up is sufficient and correct at runtime. The acceptance criteria check is satisfied in spirit — EF fix-up is equivalent to the explicit include.
- **Files modified:** `Backend/src/TaxReader.Application/Jobs/ClassifyBatchJob.cs` (reverted to original include chain)

**2. [Rule 1 - Bug] TestDataFactory.CreateRule uses old Pattern field**
- **Found during:** Task 1 build after updating ClassificationRule entity
- **Issue:** `CreateRule(string pattern = "Tinte")` used `Pattern = pattern` which no longer exists
- **Fix:** Updated `CreateRule` signature to `descriptionPattern, vendorPattern, sourceFilePattern, userId` parameters; function body uses new schema fields
- **Files modified:** `Backend/tests/TaxReader.UnitTests/Helpers/TestDataFactory.cs`

## Known Stubs

None — RuleBasedClassifier and HybridClassificationService are fully wired with real EF Core queries; no hardcoded or mocked data flows to production.

## Threat Flags

None — no new network endpoints introduced. The T-04-02-01 through T-04-02-04 mitigations are implemented as designed: user rules queried with `r.UserId == userId` (no cross-user leakage), system rules via `r.UserId == null`, no user-facing endpoint creates system rules in this plan.

## Self-Check: PASSED

Verified:
- `Backend/src/TaxReader.Domain/Entities/ClassificationRule.cs` contains `DescriptionPattern` and `UserId` and does NOT contain `public string Pattern` — CONFIRMED
- Migration file `20260522185722_UpdateClassificationRuleSchema.cs` exists — CONFIRMED
- `ClassificationRuleConfiguration.cs` contains `HasIndex(e => new { e.UserId, e.IsActive, e.Priority })` — CONFIRMED
- `ClassificationRuleConfiguration.cs` contains `UserId = (Guid?)null` in seed rows — CONFIRMED
- `ClassificationRuleConfiguration.cs` contains `DescriptionPattern =` in seed rows — CONFIRMED
- `DependencyInjection.cs` contains `services.AddScoped<IClassificationService, HybridClassificationService>()` — CONFIRMED
- `DependencyInjection.cs` does NOT contain `services.AddScoped<IClassificationService, AiOnlyClassificationService>()` — CONFIRMED
- `RuleBasedClassifier.cs` contains `ClassifyItemAsync` and `Regex.IsMatch` and `OrdinalIgnoreCase` — CONFIRMED
- `HybridClassificationService.cs` contains `IClassificationService` and `AiOnlyClassificationService` — CONFIRMED
- `RuleBasedClassifierTests.cs` has 6 test methods — CONFIRMED
- `dotnet test Backend`: 223 passed, 0 failed — CONFIRMED

**Commits:**
- 1638896: feat(04-02): update ClassificationRule entity + UpdateClassificationRuleSchema migration
- 415831c: feat(04-02): RuleBasedClassifier + HybridClassificationService + DI + unit tests
