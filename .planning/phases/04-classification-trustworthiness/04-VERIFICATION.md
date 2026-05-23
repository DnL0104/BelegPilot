---
phase: 04-classification-trustworthiness
verified: 2026-05-23T00:00:00Z
status: human_needed
score: 13/13 must-haves verified
overrides_applied: 0
human_verification:
  - test: "Run a receipt through the full pipeline with at least one matching system rule"
    expected: "That item's ItemClassification has Method=Rule, Status=Confirmed, Reason starts with 'Regel angewendet:'; the remaining items go to AI and get AI-generated reasons. No tokens charged for rule-matched items."
    why_human: "Requires a running backend + database + Anthropic API key; cannot verify rule-fires-before-AI dispatch at runtime with static code inspection."
  - test: "Navigate to a receipt detail page with a classified item; verify reasoning text is visible inline below the category badge without any click or expand action"
    expected: "Text starting with 'Warum wurde das so eingeordnet?' visible immediately below the classification badge for each item that has a reason."
    why_human: "Rendering is conditional on latestClassification.reason being non-empty; requires a real processed receipt to observe the UI state."
  - test: "On a receipt with hasSumMismatch=true, verify the dismissable amber Alert renders; click 'Als geprüft markieren' and verify the Alert disappears and receipt.hasSumMismatch becomes false"
    expected: "Alert visible → button click → 204 from API → Alert gone (hasSumMismatch = false). Receipt list page also loses the AlertTriangle badge for that receipt."
    why_human: "Requires setting HasSumMismatch=true on a real receipt row and observing the UI mutation cycle end-to-end."
  - test: "Override a classification in the classify-dialog to a different category, then click 'Diese Regel speichern'; fill in the save-rule-dialog and submit; verify the rule is saved and reused on next classification run"
    expected: "201 Created returned. Rule appears in classification_rules table with UserId = current user. Next ClassifyBatchJob run for an item matching the rule produces Method=Rule classification."
    why_human: "Requires multi-step authenticated user flow and background job execution."
---

# Phase 4: Classification Trustworthiness — Verification Report

**Phase Goal:** Move classification from a two-value black-box (ConsumablesAndOfficeSupplies / Unknown) to a 13-category DE-tax system with rule-based + AI hybrid, inline reasoning the user can audit and override, and sum validation that flags AI hallucinations.
**Verified:** 2026-05-23T00:00:00Z
**Status:** human_needed
**Re-verification:** No — initial verification

## Goal Achievement

### Observable Truths

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | All 13 German category identifiers exist in Category.cs with correct integer values (Unbekannt=0 … Privat=12) | VERIFIED | `Backend/src/TaxReader.Domain/Enums/Category.cs` read directly; 13 values, correct assignments confirmed. |
| 2 | ExpandCategoryEnum EF migration remaps all existing item_classifications rows via inline SQL UPDATE | VERIFIED | `20260522183745_ExpandCategoryEnum.cs` lines 303-310 contain all 8 UPDATE statements mapping old → new category strings; Down() has reverse SQL. |
| 3 | PDF export CategoryLabels map contains all 13 new German identifiers with German display strings | VERIFIED | `PdfExportService.cs` line 16 contains `WerbungskostenArbeitsmittel = "Werbungskosten – Arbeitsmittel"`; 13 entries confirmed. |
| 4 | CSV export uses the same 13 German display strings | VERIFIED | `CsvExportService.cs` line 26 contains `WerbungskostenArbeitsmittel`. |
| 5 | Frontend Category type union contains exactly the 13 new identifiers | VERIFIED | `Frontend/src/types/api.ts` lines 128-141: 13-value union counted by grep (13 matches). The "Unknown" at line 161 is in ReceiptFileErrorCode — a distinct, unrelated type. |
| 6 | categoryLabel() in format.ts returns German display strings for all 13 identifiers | VERIFIED | `Frontend/src/lib/format.ts` line 25 has `WerbungskostenArbeitsmittel: "Werbungskosten – Arbeitsmittel"`; grep count returns 13. |
| 7 | AiOnlyClassificationService references Category.Unbekannt (not Category.Unknown) | VERIFIED | `AiOnlyClassificationService.cs` lines 90 and 131: both use `Category.Unbekannt`. |
| 8 | ClassificationRule entity has UserId (Guid?), VendorPattern, SourceFilePattern, DescriptionPattern; old Pattern field gone | VERIFIED | `ClassificationRule.cs` read directly; all 4 fields present, no `Pattern` field. |
| 9 | UpdateClassificationRuleSchema migration adds three new columns, renames Pattern → DescriptionPattern, adds index on (UserId, IsActive, Priority) | VERIFIED | `20260522185722_UpdateClassificationRuleSchema.cs` uses `RenameColumn("pattern" → "description_pattern")` and `AddColumn` for user_id/vendor_pattern/source_file_pattern; new index confirmed. |
| 10 | RuleBasedClassifier queries user rules first (UserId == userId), then system rules (UserId == null); returns null if no match; rule fires when ALL non-null fields match | VERIFIED | `RuleBasedClassifier.cs` read in full: user rules queried with `UserId == userId`, system with `UserId == null`, Matches() checks all non-null fields with OrdinalIgnoreCase and IgnoreCase regex. Returns null on no match. |
| 11 | HybridClassificationService collects all rule-unmatched items first, then makes one batch AI call; DI registers it as IClassificationService | VERIFIED | `HybridClassificationService.cs` collects `aiItems` list, then one `aiClassifier.ClassifyItemsAsync(aiItems, ...)` call. `DependencyInjection.cs` lines 129-131: `AddScoped<IClassificationService, HybridClassificationService>()`. AiOnlyClassificationService NOT registered as IClassificationService (grep returns 0). |
| 12 | RuleBasedClassifier unit tests: 6 required test methods all exist and pass | VERIFIED | `RuleBasedClassifierTests.cs` has 6+ test methods including `UserRuleMatchesBeforeSystemRule`, `SystemRuleFallback`, `NoRuleMatches_ReturnsNull`, `VendorPatternOnly_MatchesCaseInsensitive`, `AllFieldsMustMatch_PartialMatchDoesNotFire`, `RuleMatchedResult_HasCorrectMethodAndStatus`. `dotnet test Backend` — 233 passed, 0 failed. |
| 13 | Sum validation: ClassifyBatchJob sets HasSumMismatch=true when Math.Abs(itemsSum - receipt.TotalAmount) > 0.50m; saved in same SaveChangesAsync as Completed status; ReceiptDto includes HasSumMismatch; frontend receipt detail page and receipts list surface the flag | VERIFIED | `ClassifyBatchJob.cs` lines 119-127: D-16 validation block before Finalize block (line 133). `ReceiptDto.cs` line 16: `bool HasSumMismatch`. `DtoMappingExtensions.cs` line 50: `entity.HasSumMismatch`. `receipts-table.tsx` lines 216-220: AlertTriangle conditional. `receipts/[id]/page.tsx` lines 148-179: dismissable Alert with `Als geprüft markieren` button. |

**Score:** 13/13 truths verified

### Required Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `Backend/src/TaxReader.Domain/Enums/Category.cs` | 13 German category enum values | VERIFIED | 13 values, Unbekannt=0 through Privat=12 |
| `Backend/src/TaxReader.Infrastructure/Migrations/20260522183745_ExpandCategoryEnum.cs` | ExpandCategoryEnum migration with UPDATE remapping SQL | VERIFIED | 8 UPDATE statements in Up(), 6 reverse in Down() |
| `Backend/src/TaxReader.Infrastructure/Migrations/20260522185722_UpdateClassificationRuleSchema.cs` | UpdateClassificationRuleSchema migration | VERIFIED | RenameColumn + AddColumn + FK + index |
| `Backend/src/TaxReader.Infrastructure/Migrations/20260523165841_AddHasSumMismatchToReceipts.cs` | AddHasSumMismatchToReceipts migration | VERIFIED | AddColumn has_sum_mismatch with defaultValue: false |
| `Backend/src/TaxReader.Infrastructure/Services/PdfExportService.cs` | 13-entry German CategoryLabels dictionary | VERIFIED | Contains `WerbungskostenArbeitsmittel` and `Außergewöhnliche Belastungen – Krankheit` |
| `Backend/src/TaxReader.Infrastructure/Services/RuleBasedClassifier.cs` | Three-field rule matcher with user/system priority | VERIFIED | Exists, contains ClassifyItemAsync, Regex.IsMatch, OrdinalIgnoreCase |
| `Backend/src/TaxReader.Infrastructure/Services/HybridClassificationService.cs` | IClassificationService implementation combining rules and AI | VERIFIED | Implements IClassificationService, injects AiOnlyClassificationService |
| `Backend/src/TaxReader.Domain/Entities/ClassificationRule.cs` | Updated entity with UserId + three pattern fields | VERIFIED | DescriptionPattern, VendorPattern, SourceFilePattern, UserId present; no Pattern field |
| `Backend/src/TaxReader.Domain/Entities/Receipt.cs` | HasSumMismatch property | VERIFIED | `public bool HasSumMismatch { get; set; } = false;` at line 15 |
| `Backend/src/TaxReader.Application/Jobs/ClassifyBatchJob.cs` | Sum validation logic before Completed status | VERIFIED | Lines 119-127 (before line 133 Completed assignment) |
| `Backend/src/TaxReader.Application/DTOs/ReceiptDto.cs` | HasSumMismatch field in DTO record | VERIFIED | Line 16: `bool HasSumMismatch` |
| `Backend/src/TaxReader.Application/Commands/SaveClassificationRuleHandler.cs` | save-rule handler with ownership guard and 409 check | VERIFIED | Contains "Eine identische Regel existiert bereits." and "Artikel mit id" |
| `Backend/src/TaxReader.Application/Commands/AcknowledgeSumMismatchHandler.cs` | acknowledge-sum handler with ownership guard | VERIFIED | Contains `HasSumMismatch = false` |
| `Backend/tests/TaxReader.UnitTests/Services/RuleBasedClassifierTests.cs` | Unit tests for rule matching logic | VERIFIED | 6 test methods, all pass |
| `Backend/tests/TaxReader.UnitTests/Pipeline/SumValidationTests.cs` | Sum validation tests (TDD RED then GREEN) | VERIFIED | Exists; 8 tests all pass |
| `Frontend/src/types/api.ts` | 13-value Category type union; hasSumMismatch on Receipt | VERIFIED | Lines 128-141 (Category), line 37 (hasSumMismatch); ClassificationRule interface present |
| `Frontend/src/lib/format.ts` | 13-entry categoryLabel() map | VERIFIED | All 13 German display strings present |
| `Frontend/src/components/receipts/save-rule-dialog.tsx` | SaveRuleDialog component | VERIFIED | Contains SaveRuleDialog, vendorPattern state, descPattern state, Speichern button |
| `Frontend/src/components/receipts/classify-dialog.tsx` | "Diese Regel speichern" button and saveRuleOpen state | VERIFIED | Line 57: saveRuleOpen; line 203: "Diese Regel speichern"; condition line 196 enforces category changed |
| `Frontend/src/app/(authenticated)/receipts/[id]/page.tsx` | Inline reasoning + dismissable sum-mismatch Alert | VERIFIED | "Warum wurde das so eingeordnet?" at line 179; "Als geprüft markieren" at line 167; hasSumMismatch Alert at lines 148-179 |
| `Frontend/src/app/(authenticated)/settings/page.tsx` | autoConfirmThreshold (CLASS-07 pre-existing) | VERIFIED | 5 occurrences of autoConfirmThreshold — read/write fully wired |

### Key Link Verification

| From | To | Via | Status | Details |
|------|----|-----|--------|---------|
| Category.cs enum | EF HasConversion<string>() | ClassificationRuleConfiguration + ItemClassificationConfiguration | WIRED | Both configurations have `HasConversion<string>()` for Category property |
| ExpandCategoryEnum.Up() | item_classifications table | inline SQL UPDATE | WIRED | 8 UPDATE statements confirmed in migration file |
| DependencyInjection.cs | HybridClassificationService as IClassificationService | `services.AddScoped<IClassificationService, HybridClassificationService>()` | WIRED | Line 131 of DependencyInjection.cs; AiOnlyClassificationService NOT registered as interface |
| HybridClassificationService | AiOnlyClassificationService | direct injection of concrete type | WIRED | Constructor parameter `AiOnlyClassificationService aiClassifier`; registered at line 129 |
| RuleBasedClassifier | classification_rules table | IAppDbContext.ClassificationRules DbSet | WIRED | Lines 22-25, 37-40: `dbContext.ClassificationRules.Where(...)` |
| ClassifyBatchJob sum validation | receipt.HasSumMismatch | direct property assignment before SaveChangesAsync | WIRED | Line 126: `receipt.HasSumMismatch = true`; SaveChangesAsync at line 137 |
| GetReceiptByIdHandler | ReceiptDto.HasSumMismatch | receipt.ToDto() mapping in DtoMappingExtensions.cs | WIRED | `receipt.ToDto(includeRawText: true)` call; DtoMappingExtensions.cs line 50 passes `entity.HasSumMismatch` |
| Frontend receipts-table.tsx | receipt.hasSumMismatch | conditional AlertTriangle render | WIRED | Lines 216-220: `{receipt.hasSumMismatch && <AlertTriangle ...>}` |
| Frontend classify-dialog.tsx | SaveRuleDialog | setSaveRuleOpen(true) on button click | WIRED | Line 57: `saveRuleOpen` state; line 209: `setSaveRuleOpen(true)` |
| SaveRuleDialog | useSaveClassificationRule → POST /receipt-items/{id}/save-rule | TanStack Query mutation | WIRED | `useSaveClassificationRule()` imported in save-rule-dialog.tsx; mutationFn calls saveClassificationRule from api-client |
| receipt detail page Alert | useAcknowledgeSumMismatch → POST /receipts/{id}/acknowledge-sum | TanStack Query mutation | WIRED | `acknowledgeMutation.mutate(id)` at line 161; `useAcknowledgeSumMismatch` hook imported at line 13 |

### Data-Flow Trace (Level 4)

| Artifact | Data Variable | Source | Produces Real Data | Status |
|----------|---------------|--------|--------------------|--------|
| `RuleBasedClassifier.cs` | userRules / systemRules | `dbContext.ClassificationRules.Where(...)` EF query | Yes — real DB query | FLOWING |
| `HybridClassificationService.cs` | results / aiItems | RuleBasedClassifier + AiOnlyClassificationService | Yes — rule DB + AI batch | FLOWING |
| `ClassifyBatchJob.cs` | HasSumMismatch | `receipt.Items.Sum(i => i.TotalPrice)` vs `receipt.TotalAmount` | Yes — real entity property | FLOWING |
| `ReceiptDto.cs` | HasSumMismatch | `entity.HasSumMismatch` via `DtoMappingExtensions.ToDto()` | Yes — mapped from domain entity | FLOWING |
| `receipts-table.tsx` | receipt.hasSumMismatch | API response via TanStack Query | Yes — flows from ReceiptDto | FLOWING |
| `save-rule-dialog.tsx` | vendorPattern / descPattern | Props `vendor` and `item.description` | Yes — receipt data passed via props | FLOWING |
| `receipt detail page` | acknowledgeMutation | `useAcknowledgeSumMismatch()` → POST /receipts/{id}/acknowledge-sum | Yes — real API mutation | FLOWING |

### Behavioral Spot-Checks

| Behavior | Command | Result | Status |
|----------|---------|--------|--------|
| Backend compiles clean | `dotnet build Backend --no-incremental` | 0 errors, 2 pre-existing NU1510 warnings | PASS |
| All backend tests pass | `dotnet test Backend` | 233 passed, 0 failed, 5 skipped | PASS |
| Frontend build passes | `cd Frontend && npm run build` | "Compiled successfully in 2.7s" | PASS |
| Category enum has 13 values, no old English names | `grep -c "WerbungskostenArbeitsmittel" Category.cs` | 1 match; no `ConsumablesAndOfficeSupplies` in source | PASS |
| HybridClassificationService registered as IClassificationService | `grep DependencyInjection.cs` | Line 131: `AddScoped<IClassificationService, HybridClassificationService>()` | PASS |
| Sum validation precedes Completed status | Line numbers in ClassifyBatchJob.cs | Line 126 (HasSumMismatch=true) before line 133 (Completed) | PASS |

### Requirements Coverage

| Requirement | Source Plan | Description | Status | Evidence |
|-------------|-------------|-------------|--------|---------|
| CLASS-01 | 04-02 | RuleBasedClassifier (DB-backed) wired against ClassificationRule entity | SATISFIED | RuleBasedClassifier.cs queries classification_rules; user/system scoped; all three field match types implemented |
| CLASS-02 | 04-02 | HybridClassificationService composing rules-first-then-AI replaces AiOnlyClassificationService as registered IClassificationService | SATISFIED | DependencyInjection.cs registers HybridClassificationService; AiOnlyClassificationService not registered as interface |
| CLASS-03 | 04-01 | Category enum expanded to 13 values with EF migration and export updates | SATISFIED | Category.cs has 13 values; ExpandCategoryEnum migration with SQL remapping; PDF/CSV exports updated |
| CLASS-04 | 04-03 | Per-classification reasoning visible without click-to-expand; "Warum wurde das so eingeordnet?" label | SATISFIED | receipt-items-table.tsx lines 211-214 render reason inline in both mobile and desktop layouts |
| CLASS-05 | 04-03 | "Diese Regel speichern" button on classification override creates user-scoped ClassificationRule | SATISFIED | classify-dialog.tsx button visible when category changed; save-rule-dialog.tsx with vendor/description pre-population; SaveClassificationRuleHandler with ownership guard |
| CLASS-06 | 04-04 | Sum-validation line-item totals vs receipt total within €0.50; mismatch flags receipt and surfaces audit prompt | SATISFIED | ClassifyBatchJob.cs D-16 logic; HasSumMismatch flows through DTO to Alert on receipt detail page; receipts list warning badge |
| CLASS-07 | 04-03 | Auto-confirm threshold visible and user-settable in settings | SATISFIED (pre-existing) | settings/page.tsx has 5 occurrences of autoConfirmThreshold — read/write fully wired from prior phase |

### Anti-Patterns Found

| File | Line | Pattern | Severity | Impact |
|------|------|---------|----------|--------|
| `Frontend/src/components/receipts/save-rule-dialog.tsx` | 88, 109 | `placeholder="z. B. Amazon"` / `placeholder="z. B. Buch"` | Info | HTML input placeholder attributes — legitimate UX, not code stubs |
| `Frontend/src/components/receipts/classify-dialog.tsx` | 182 | `placeholder="Kategorie wählen..."` | Info | HTML select placeholder — legitimate UX |
| `Backend/src/TaxReader.Infrastructure/Migrations/20260406153622_InitialCreate.cs` | 154-157 | Old English category strings (`ConsumablesAndOfficeSupplies`) | Info | Pre-existing migration snapshot — immutable historical record; subsequent migrations remap these values. Not a code stub. |

No blockers found. All placeholder occurrences are HTML UI placeholders, not implementation stubs.

### Human Verification Required

#### 1. End-to-End Rule Classification Execution

**Test:** Process a receipt from a vendor matching an existing system rule (e.g., a vendor name containing "Amazon" or a description matching a known keyword). Observe the ItemClassification record created.
**Expected:** ItemClassification.Method = "Rule", ItemClassification.Status = "Confirmed", Reason starts with "Regel angewendet:". Tokens NOT charged for rule-matched items (check UserTokenBalance before vs after).
**Why human:** Requires running backend + database + Anthropic API connection. Cannot verify rule-dispatch branching at runtime with static analysis.

#### 2. Inline Reasoning Visible Without Click

**Test:** Navigate to a receipt detail page (`/receipts/[id]`) for a receipt that has been classified. Observe item rows.
**Expected:** Each item row shows reasoning text immediately below the classification badge, labeled "Warum wurde das so eingeordnet?" — no accordion, no hover, no click required.
**Why human:** Text renders only when `latestClassification.reason` is non-empty; requires a real classified receipt in the database.

#### 3. Sum-Mismatch Alert Lifecycle

**Test:** Artificially or naturally produce a receipt where `Math.Abs(itemsSum - totalAmount) > 0.50`. Navigate to the receipt detail page. Observe the amber Alert. Click "Als geprüft markieren".
**Expected:** Alert visible on load. Button click fires POST /receipts/{id}/acknowledge-sum, returns 204. Alert disappears. AlertTriangle badge also disappears from the receipts list for that receipt.
**Why human:** Requires a receipt with genuine sum mismatch — either a real edge-case receipt or manual DB manipulation.

#### 4. Override-to-Rule Flow

**Test:** On a classified item, open classify-dialog, select a different category. Confirm "Diese Regel speichern" button appears. Click it. Fill in save-rule-dialog (pre-populated vendor + description). Submit.
**Expected:** POST /receipt-items/{id}/save-rule returns 201 Created. Rule appears in classification_rules table with correct UserId, patterns, and category. Submitting the same rule again returns 409 Conflict.
**Why human:** Requires authenticated user session and database inspection.

### Gaps Summary

No gaps found. All 13 must-have truths are verified against the codebase. All 7 CLASS requirements (CLASS-01 through CLASS-07) are satisfied. Backend builds clean, 233 tests pass, frontend builds clean. Four items require human verification with a running system to observe runtime behavior — these cannot be verified with static analysis.

---

_Verified: 2026-05-23T00:00:00Z_
_Verifier: Claude (gsd-verifier)_
