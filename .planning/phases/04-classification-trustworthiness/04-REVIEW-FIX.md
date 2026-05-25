---
phase: 04-classification-trustworthiness
fixed_at: 2026-05-25T00:00:00Z
review_path: .planning/phases/04-classification-trustworthiness/04-REVIEW.md
iteration: 1
findings_in_scope: 10
fixed: 10
skipped: 0
status: all_fixed
---

# Phase 04: Code Review Fix Report

**Fixed at:** 2026-05-25T00:00:00Z
**Source review:** .planning/phases/04-classification-trustworthiness/04-REVIEW.md
**Iteration:** 1

**Summary:**
- Findings in scope: 10 (3 Critical, 7 Warning)
- Fixed: 10
- Skipped: 0

## Fixed Issues

### CR-01: Unvalidated Regex Input in User-Controlled Classification Rules Enables ReDoS

**Files modified:** `Backend/src/TaxReader.Infrastructure/Services/RuleBasedClassifier.cs`, `Backend/src/TaxReader.Api/Endpoints/ClassificationEndpoints.cs`
**Commit:** a775d14
**Applied fix:** Both Option A and Option B from the review applied together. In `RuleBasedClassifier.Matches`, added `TimeSpan.FromMilliseconds(200)` timeout to both `Regex.IsMatch` calls so catastrophic backtracking is bounded at runtime. In `ClassificationEndpoints.cs`, added pre-save validation for `DescriptionPattern` and `SourceFilePattern` that attempts to construct a `Regex` with a 100ms timeout — any `ArgumentException` returns a German `BadRequest` error before the pattern reaches the database.

---

### CR-02: Duplicate-Rule Guard Ignores Category — Logic Error Produces Incorrect 409 Behaviour

**Files modified:** `Backend/src/TaxReader.Application/Commands/SaveClassificationRuleHandler.cs`
**Commit:** e3a41c3
**Applied fix:** Added `&& r.Category == command.Category` to the `AnyAsync` predicate in `SaveClassificationRuleHandler`. Two rules with identical patterns but different categories are now correctly treated as distinct rules and will not trigger a false 409.

---

### CR-03: English Error Message in ConfirmClassificationHandler Violates German Localisation Requirement

**Files modified:** `Backend/src/TaxReader.Application/Commands/ConfirmClassificationHandler.cs`, `Backend/tests/TaxReader.UnitTests/Application/Commands/ConfirmClassificationHandlerTests.cs`
**Commit:** 8f9dc7b
**Applied fix:** Changed the not-found failure message from `"Receipt item with id '...' not found."` to `"Artikel mit id '...' nicht gefunden."`. Also translated the `Reason` field from `"Manually confirmed as {category}"` to `"Manuell bestätigt als {category}"`. Updated the test assertion from `Contain("not found")` to `Contain("nicht gefunden")`. All 233 tests pass.

---

### WR-01: Report Totals Include Unconfirmed AI Suggestions — Financial Figures May Be Wrong

**Files modified:** `Backend/src/TaxReader.Application/Queries/GetCategoryTotalsHandler.cs`, `Backend/src/TaxReader.Application/Queries/GetAnnualSummaryHandler.cs`, `Backend/tests/TaxReader.UnitTests/Application/Queries/GetCategoryTotalsHandlerTests.cs`, `Backend/tests/TaxReader.UnitTests/Application/Queries/GetAnnualSummaryHandlerTests.cs`
**Commits:** 5a97d76 (handler fixes), 8b1faf3 (test updates)
**Applied fix:** In both `GetCategoryTotalsHandler` and `GetAnnualSummaryHandler`, changed `FirstOrDefault()` to `FirstOrDefault(c => c.Status == ClassificationStatus.Confirmed)` when selecting the latest classification for totals. Items with only `Suggested` classifications now map to `Category.Unbekannt` and are excluded from financial totals. Updated the two failing query tests to seed `ClassificationStatus.Confirmed` data, which is the correct state for items that should appear in reports.
**Note:** requires human verification — the logic change (Confirmed-only filtering) is correct per the review, but behavior change may surface in the UI as previously-visible totals disappearing until the user confirms suggestions.

---

### WR-02: AcknowledgeSumMismatch Unconditionally Clears Flag Without Re-Checking Sums

**Files modified:** `Backend/src/TaxReader.Application/Commands/ReclassifyReceiptHandler.cs`
**Commit:** ef2ef05
**Applied fix:** Added sum-mismatch recalculation in `ReclassifyReceiptHandler.HandleAsync` after all classifications are added but before `SaveChangesAsync`. Uses the same `Math.Abs(itemsSum - receipt.TotalAmount) > 0.50m` threshold as `ClassifyBatchJob`. This ensures reclassification refreshes the mismatch flag rather than leaving a stale acknowledged state.

---

### WR-03: RuleBasedClassifier Makes Two Sequential DB Round-Trips Per Item — N+1 Pattern at Scale

**Files modified:** `Backend/src/TaxReader.Infrastructure/Services/RuleBasedClassifier.cs`, `Backend/src/TaxReader.Infrastructure/Services/HybridClassificationService.cs`
**Commit:** fec943d
**Applied fix:** Added a new synchronous `Classify(item, vendor, fileName, userRules, systemRules)` method to `RuleBasedClassifier` that accepts pre-loaded rule lists and performs no DB queries. The existing async `ClassifyItemAsync` is retained for backward compatibility (used by tests). Updated `HybridClassificationService` to accept `IAppDbContext`, load both rule lists once before the item loop (2 total queries instead of 2×N), and call the synchronous `Classify` method per item. All existing `RuleBasedClassifierTests` continue to pass via the unchanged `ClassifyItemAsync` path.

---

### WR-04: BatchConfirmHandler Accepts Unrestricted ItemId Count — No Ceiling Enforced

**Files modified:** `Backend/src/TaxReader.Application/Commands/BatchConfirmHandler.cs`
**Commit:** 9194f6f
**Applied fix:** Added a guard immediately after the empty-list check: if `command.ReceiptItemIds.Count > 500`, returns `Result<int>.Failure("Maximal 500 Artikel pro Anfrage erlaubt.")`. The endpoint returns `BadRequest` with this message, preventing oversized IN-list queries to PostgreSQL.

---

### WR-05: SaveRuleDialog Does Not Reset State When `item` or `vendor` Prop Changes

**Files modified:** `Frontend/src/components/receipts/save-rule-dialog.tsx`
**Commit:** 03f7fef
**Applied fix:** Added `useEffect` (imported alongside `useState`) that fires when `open`, `vendor`, or `item?.description` changes. When the dialog opens (`open === true`), it resets `vendorPattern`, `descPattern`, `includeVendor`, and `includeDesc` to their initial values derived from the current props. Frontend build passes cleanly.

---

### WR-06: `GetPendingSuggestionsHandler` Loads All Items for User Into Memory — No Year or Limit Filter

**Files modified:** `Backend/src/TaxReader.Application/Queries/GetPendingSuggestionsQuery.cs`, `Backend/src/TaxReader.Application/Queries/GetPendingSuggestionsHandler.cs`, `Backend/src/TaxReader.Api/Endpoints/ClassificationEndpoints.cs`
**Commit:** 4f140f7
**Applied fix:** Changed `GetPendingSuggestionsQuery` from a parameterless record to `record GetPendingSuggestionsQuery(int? Year = null)`. Added a `.Where(i => query.Year == null || i.Receipt.PurchaseDate.Year == query.Year)` clause to the EF query so year scoping is pushed to the database. Updated the endpoint to accept an optional `int? year` query parameter and pass it to the query. Existing callers without `?year=` continue to return all years unchanged.

---

### WR-07: ClassifyBatchJob Writes HasSumMismatch After Classification Succeeds But Before SaveChangesAsync on Failure Path

**Files modified:** `Backend/src/TaxReader.Application/Jobs/ClassifyBatchJob.cs`
**Commit:** db9d1fd
**Applied fix:** Added sum-mismatch validation loop inside the `catch` block, after all runs are marked with `terminalStatus` and before `SaveChangesAsync(CancellationToken.None)`. Uses identical logic to the success-path validation (lines 121–126): iterates runs whose `ReceiptFile.Receipt` is non-null, computes `itemsSum`, and sets `HasSumMismatch = true` if the absolute difference exceeds €0.50. The flag is now persisted accurately on both the success and failure path.

---

_Fixed: 2026-05-25_
_Fixer: Claude (gsd-code-fixer)_
_Iteration: 1_
