# Phase 4: Classification Trustworthiness - Context

**Gathered:** 2026-05-22
**Status:** Ready for planning

<domain>
## Phase Boundary

Deliver Core Value — every line item correctly categorized into the right German tax category. Expands the `Category` enum from 8 teacher-specific values to 13 DE tax categories, wires the existing-but-unused `ClassificationRule` entity as a rules-first-then-AI hybrid, enables per-user rule creation from classification overrides, surfaces reasoning prominently without click-to-expand, and adds sum validation to flag AI hallucinations.

In scope: `Category` enum replacement + EF migration + PDF/CSV export updates (CLASS-03, 04-01); `RuleBasedClassifier` + `HybridClassificationService` replacing `AiOnlyClassificationService` as registered `IClassificationService` (CLASS-01, CLASS-02, 04-02); audit/reasoning UX + "Diese Regel speichern" override-to-rule flow + auto-confirm threshold setting (CLASS-04, CLASS-05, CLASS-07, 04-03); sum-validation rule + UI surface (CLASS-06, 04-04).

Out of scope (later phases own these):
- Stripe / payments — Phase 5
- Audit log entries for rule creations — Phase 6 (LEG-08)
- Vitest/Playwright tests for classification flow — Phase 7 (QA-02, QA-03)
- PostgreSQL integration tests — Phase 7 (QA-01)
- Retroactive backfill when a new rule is saved — explicitly deferred (D-11)
- Bulk re-classify by rule UI — v2 backlog (CLASS-V2-01)
- Auto-promotion of N-corrections-into-rule — v2 backlog (CLASS-V2-03)

</domain>

<decisions>
## Implementation Decisions

### Category enum replacement (04-01, CLASS-03)
- **D-01:** Auto-map old category values → new categories on existing `ItemClassification` records. Records that cannot be cleanly mapped reset to `Unbekannt` with `ClassificationStatus.Suggested`, flagging them for manual re-review. No wipe-and-reclassify (would cost tokens and lose existing Confirmed decisions). Migration script handles the remapping in a single SQL UPDATE pass.
  - Mapping guide (for planner): `ConsumablesAndOfficeSupplies` → `WerbungskostenBueromaterial`; `SpecialistLiterature` → `WerbungskostenFachliteratur`; `TeachingMaterials` → `WerbungskostenArbeitsmittel`; `DigitalToolsAndSoftware` → `WerbungskostenArbeitsmittel` (closest fit); `OfficeEquipment` → `WerbungskostenArbeitsmittel`; `TravelAndCommuting` → `WerbungskostenReisekosten`; `ProfessionalDevelopment` → `WerbungskostenFortbildung`; `Unknown` → `Unbekannt`.
- **D-02:** Seed `ClassificationRule` data is migrated to the new categories (existing seed rows remapped; patterns that don't fit any of the 13 new categories are deleted). Result: a smaller, cleaner seed set reflecting DE tax reality. The migration drops rows with stale categories rather than keeping them as inactive noise.
- **D-03:** C# enum value names use the German identifiers **exactly as specified in REQUIREMENTS.md**: `WerbungskostenArbeitsmittel`, `WerbungskostenFachliteratur`, `WerbungskostenBueromaterial`, `WerbungskostenReisekosten`, `WerbungskostenFortbildung`, `WerbungskostenTelekommunikation`, `SonderausgabenSpenden`, `SonderausgabenVorsorgeaufwendungen`, `AussergewoehnlicheBelastungenKrankheit`, `HaushaltsnaheDienstleistung`, `Handwerkerleistung`, `Privat`, `Unbekannt`. `Unbekannt` replaces `Unknown` (consistent German naming). Frontend `categoryLabel()` maps each identifier to a human-readable German display string. PDF/CSV export uses the display strings.

### Rule entity schema (04-02, CLASS-01)
- **D-04:** Two-tier rule scope: add `UserId` (nullable `Guid?`, FK to `users`) to `ClassificationRule`. `UserId = null` = system rule visible to all users. `UserId = {id}` = user-private rule. One table, minimal migration. Index on `(UserId, IsActive, Priority)` for efficient per-user lookup.
- **D-05:** Rule matching schema — replace the single `Pattern` field with three separate nullable fields:
  - `VendorPattern` (`string?`) — case-insensitive substring match against the receipt's vendor/source name
  - `SourceFilePattern` (`string?`) — regex match against the receipt file's original filename or source hint
  - `DescriptionPattern` (`string?`) — regex match against the item's description
  A rule fires when ALL non-null fields match. The old `Pattern` field is renamed to `DescriptionPattern` in the migration (preserving existing seed data's patterns as description-matches). EF configuration updated accordingly.
- **D-06:** Evaluation order — user rules always take priority over system rules. `RuleBasedClassifier` queries user rules first (filtered by `UserId == currentUserId AND IsActive = true`, ordered by `Priority DESC`). If any user rule matches, return immediately. If no user rule matches, evaluate system rules (filtered by `UserId == null AND IsActive = true`, ordered by `Priority DESC`). If a system rule matches, return with `ClassificationMethod.Rule`. If neither matches, return null (triggers AI fallback in `HybridClassificationService`).

### Hybrid classification service (04-02, CLASS-02)
- **D-07:** `HybridClassificationService` replaces `AiOnlyClassificationService` as the registered `IClassificationService` in `DependencyInjection.cs`. The service: (1) runs `RuleBasedClassifier` for each item; (2) collects rule-matched items (skip AI for these — no token cost); (3) passes only unmatched items to `AiOnlyClassificationService.ClassifyItemsAsync` (preserves Phase 3 D-01/D-02 token batching). Rule-matched items are recorded with `ClassificationMethod.Rule` and `ClassificationStatus.Confirmed` (rules are deterministic — no "Suggested" state for rule hits). `AiOnlyClassificationService` is NOT removed from DI — it remains as a named/internal dependency of `HybridClassificationService`.
- **D-08:** Token pre-charge applies only to AI-bound items. Rule-matched items are free. The existing pre-charge logic in `AiOnlyClassificationService.ClassifyItemsAsync` handles this correctly once it only receives unmatched items — no changes needed to the token service.

### Rule-save UX flow (04-03, CLASS-05)
- **D-09:** "Diese Regel speichern" button appears inside `classify-dialog.tsx` only — visible when the user has selected a category that differs from the current classification. No additional entry points on the receipt detail page.
- **D-10:** Clicking the button opens a mini dialog showing the auto-generated rule. The dialog pre-populates:
  - `VendorPattern`: the receipt's vendor/source name (substring match)
  - `DescriptionPattern`: the item's description (or a trimmed version)
  User can toggle which fields to include (checkboxes) and edit the values before saving. One "Speichern" button confirms. Canceling returns to the classify dialog.
- **D-11:** No retroactive backfill when a rule is saved. The rule applies to future `ClassifyBatchJob` runs only. Existing `ItemClassification` records are untouched. Users who want retroactive effect must use the existing "Neu klassifizieren" (reclassify) action per receipt. This avoids unexpected token charges and surprising status changes on already-reviewed items.
- **D-12:** New backend endpoint: `POST /api/v1/receipt-items/{id}/save-rule` — body contains `{ vendorPattern?, descriptionPattern?, sourceFilePattern?, category }`. Returns `201 Created` with the new rule ID. Validates that at least one pattern field is non-empty. Returns `409 Conflict` if a user rule with identical patterns already exists.

### Reasoning display (04-03, CLASS-04)
- **D-13:** Per-classification reasoning is displayed inline on the receipt detail page without click-to-expand. Each receipt item row shows the reasoning text below the category badge with a "Warum wurde das so eingeordnet?" label. Wording from the AI is descriptive ("Diese Position passt zu Werbungskosten weil...") — the classifier prompt must avoid prescriptive language ("Sie können absetzen"). Rule-matched items show "Regel angewendet: [pattern]" as the reason.
- **D-14:** Auto-confirm threshold (CLASS-07) is visible and editable in the user settings page. Default: null (no auto-confirm — requires manual confirmation on every suggestion). When set, shown as a percentage input (0–100). Applied uniformly to both rule matches (always Confirmed) and AI matches (Confirmed if confidence ≥ threshold).

### Sum validation (04-04, CLASS-06)
- **D-15:** `HasSumMismatch` (bool, default false) added to the `Receipt` entity (table `receipts`). This is an informational flag — receipts with `HasSumMismatch = true` still appear in reports and PDF/CSV exports. No status machine changes, no new enum values.
- **D-16:** Sum validation runs at the end of `ClassifyBatchJob`, after all items are classified. The job sums `ReceiptItem.TotalPrice` for all items in the receipt, compares against `Receipt.Total`. If the absolute difference exceeds €0.50, sets `Receipt.HasSumMismatch = true`. Saves as part of the same `dbContext.SaveChangesAsync` call.
- **D-17:** On the receipt detail page, a dismissable `Alert` (shadcn) renders when `HasSumMismatch = true`: "Summe stimmt nicht überein ([€X.XX] Differenz). Bitte prüfen." with an "Als geprüft markieren" button. Clicking it calls `POST /api/v1/receipts/{id}/acknowledge-sum` (sets `HasSumMismatch = false`, returns 204). Alert disappears. Receipt list page shows a warning icon badge on affected receipts.

### Cross-cutting — Claude's Discretion within stated conventions
- Exact SQL UPDATE statement in the category-remapping migration (inline SQL vs EF HasData approach)
- Whether `RuleBasedClassifier` is a separate `IClassificationService`-like interface or an internal class injected into `HybridClassificationService`
- Whether the rule-save mini dialog is a shadcn `Popover` or a `Dialog` (likely `Dialog` for accessibility)
- Exact confidence % format in the auto-confirm threshold setting UI (slider vs number input)
- `ReceiptItem.TotalPrice` vs `UnitPrice * Quantity` for sum computation (use whichever the parsers already populate)
- Exact tolerance comparison: `Math.Abs(itemsSum - receipt.Total) > 0.50m`

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Project context
- `.planning/PROJECT.md` — Core Value (trustworthy classification), "Anyone DE" scope broadening, "Rule + AI hybrid for classification" key decision
- `.planning/REQUIREMENTS.md` — CLASS-01 through CLASS-07 full text with acceptance criteria; traceability table (Phase 4 maps all 7 CLASS requirements)
- `.planning/ROADMAP.md` — Phase 4 entry with 7 success criteria and 4 plan stubs (04-01 through 04-04)

### Codebase intel
- `.planning/codebase/CONCERNS.md` — #16 (ClassificationRule entity exists but unused — this phase activates it)
- `.planning/codebase/ARCHITECTURE.md` — Layer rules (Application defines interfaces, Infrastructure implements); `Result<T>` pattern; per-user data scoping via `ICurrentUser`
- `.planning/codebase/CONVENTIONS.md` — EF migrations pattern, primary-constructor DI, `IOptions<T>` for config, German `Sie`-form for user copy, `Async` suffix on every async method

### Prior-phase context (carries forward)
- `.planning/phases/03-background-pipeline-tesseract-pool/03-CONTEXT.md` — D-01: `ClassifyBatchJob` batches all items in one Anthropic call (hybrid service must preserve this: only unmatched items go to AI batch); D-02: token pre-charge fires at item-count-known time in `ClassifyBatchJob` (rule-matched items excluded from charge); D-21: `UploadErrorCatalog` for German error strings — same pattern applies to rule-save error responses

### Files this phase will touch (read before editing)

#### Backend — Domain
- `Backend/src/TaxReader.Domain/Enums/Category.cs` — Replace 8 old values with 13 German identifiers (D-03)
- `Backend/src/TaxReader.Domain/Entities/ClassificationRule.cs` — Add `UserId` (Guid?), `VendorPattern` (string?), `SourceFilePattern` (string?), `DescriptionPattern` (string?); rename/remove old `Pattern` field (D-04, D-05)
- `Backend/src/TaxReader.Domain/Entities/Receipt.cs` — Add `HasSumMismatch` (bool, default false) (D-15)
- `Backend/src/TaxReader.Domain/Entities/ItemClassification.cs` — No shape change; enum mapping picks up new Category values

#### Backend — Application
- `Backend/src/TaxReader.Application/Interfaces/IClassificationService.cs` — No signature change; `HybridClassificationService` implements it
- `Backend/src/TaxReader.Application/Jobs/ClassifyBatchJob.cs` — Add sum validation at end (D-16); ensure only AI-unmatched items reach `IClassificationService.ClassifyItemsAsync` (D-07)
- `Backend/src/TaxReader.Application/Commands/ReclassifyReceiptHandler.cs` — Already calls `IClassificationService`; no change needed beyond the new registered implementation
- `Backend/src/TaxReader.Application/Commands/SaveClassificationRuleHandler.cs` — NEW; handles D-12 endpoint logic
- `Backend/src/TaxReader.Application/Commands/AcknowledgeSumMismatchHandler.cs` — NEW; handles D-17 dismiss endpoint
- `Backend/src/TaxReader.Application/Queries/GetReceiptDetailHandler.cs` — Ensure `HasSumMismatch` is included in the receipt DTO

#### Backend — Infrastructure
- `Backend/src/TaxReader.Infrastructure/Services/RuleBasedClassifier.cs` — NEW; queries `classification_rules`, applies D-06 evaluation order (user rules first), D-05 field matching
- `Backend/src/TaxReader.Infrastructure/Services/HybridClassificationService.cs` — NEW; composes `RuleBasedClassifier` + `AiOnlyClassificationService` per D-07; registered as `IClassificationService`
- `Backend/src/TaxReader.Infrastructure/Services/AiOnlyClassificationService.cs` — No changes (stays as AI backend, injected into `HybridClassificationService`)
- `Backend/src/TaxReader.Infrastructure/Data/Configurations/ClassificationRuleConfiguration.cs` — Add `UserId` FK mapping, index on `(UserId, IsActive, Priority)`, new seed data (D-04, D-05, D-02)
- `Backend/src/TaxReader.Infrastructure/Data/Configurations/ReceiptConfiguration.cs` — Map `HasSumMismatch` column
- `Backend/src/TaxReader.Infrastructure/DependencyInjection.cs` — Register `HybridClassificationService` as `IClassificationService`; register `RuleBasedClassifier`
- `Backend/src/TaxReader.Infrastructure/Migrations/` — Multiple new migrations: `ExpandCategoryEnum`, `UpdateClassificationRuleSchema`, `AddHasSumMismatchToReceipts`; remapping UPDATE SQL in `ExpandCategoryEnum.Up()`

#### Backend — API
- `Backend/src/TaxReader.Api/Endpoints/ReceiptItemEndpoints.cs` — New `POST /{id}/save-rule` endpoint (D-12)
- `Backend/src/TaxReader.Api/Endpoints/ReceiptEndpoints.cs` — New `POST /{id}/acknowledge-sum` endpoint (D-17)

#### Frontend
- `Frontend/src/types/api.ts` — Update `Category` type union to 13 new values; add `ClassificationRule` type; add `hasReceiptSumMismatch` to `Receipt` type
- `Frontend/src/lib/format.ts` — Update `categoryLabel()` mapping for all 13 new German identifiers
- `Frontend/src/components/receipts/classify-dialog.tsx` — Add "Diese Regel speichern" button; add rule-save mini dialog (D-09, D-10)
- `Frontend/src/components/receipts/save-rule-dialog.tsx` — NEW; mini dialog with VendorPattern + DescriptionPattern editable fields, checkboxes to toggle inclusion (D-10)
- `Frontend/src/app/(authenticated)/receipts/[id]/page.tsx` — Inline reasoning display per item (D-13); dismissable Alert for `HasSumMismatch` (D-17)
- `Frontend/src/app/(authenticated)/settings/page.tsx` — Auto-confirm threshold % input field (D-14)
- `Frontend/src/lib/api-client.ts` — New: `saveClassificationRule(itemId, payload)`, `acknowledgeSumMismatch(receiptId)`
- `Frontend/src/hooks/use-receipt-items.ts` — New `useSaveClassificationRule` mutation hook
- `Frontend/src/hooks/use-receipts.ts` — New `useAcknowledgeSumMismatch` mutation hook

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- **`AiOnlyClassificationService.cs`** — the AI backend is unchanged. `HybridClassificationService` delegates to it for unmatched items. The token pre-charge + per-item-refund-on-Unknown + full-refund-on-failure pattern is preserved verbatim.
- **`ClassificationRuleConfiguration.cs` seed data** — 40+ existing patterns already typed per category. Migration remaps them to new category names (D-02). The `HasData` approach stays; seed IDs are stable UUIDs.
- **`classify-dialog.tsx`** — already shows reasoning (`item.latestClassification.reason`) inline. Already has a category selector. "Diese Regel speichern" is an additive button in the existing dialog footer.
- **`Result<T>` pattern** — new handlers (`SaveClassificationRuleHandler`, `AcknowledgeSumMismatchHandler`) follow the existing `Result<T>.Success` / `Result<T>.Failure` pattern.
- **`ICurrentUser`** — per-user data isolation in `SaveClassificationRuleHandler` and rule queries (filter by `UserId`). Same pattern as `DeleteReceiptFileHandler`.
- **shadcn `Alert` component** — created in Phase 3 (03-04) for upload errors. Reused for sum-mismatch surface (D-17).
- **`categoryLabel()` in `Frontend/src/lib/format.ts`** — already maps Category string → German display label. Extend for 13 new values.

### Established Patterns
- **EF migration + `HasData` seed** (`ClassificationRuleConfiguration`) — add `UserId` column as nullable via `AddColumn` in migration; update seed rows to set `UserId = null` explicitly.
- **Per-user data scoping** — every query adds `WHERE UserId = @userId`; rule queries add `WHERE (UserId = @userId OR UserId IS NULL)`.
- **`IOptions<T>` for config** — auto-confirm threshold already stored on `User.AutoConfirmThreshold` (nullable decimal); no new options class needed for the setting.
- **German user-facing strings in `Result<T>.Failure`** — rule-save error messages follow the same pattern.
- **Hangfire job arguments (serialized by value)** — `ClassifyBatchJob` already receives `userId` as a parameter; `RuleBasedClassifier` receives it as a method argument (not via `ICurrentUser`).

### Integration Points
- **`ClassifyBatchJob`** (Phase 3) — the primary integration point for D-07 (hybrid dispatch) and D-16 (sum validation). The job already has the receipt items and `IClassificationService` reference; hybrid logic and sum check are added at the end of the existing processing loop.
- **`ReceiptItem.TotalPrice`** — used for sum validation (D-16). Confirm that parsers populate `TotalPrice` (not just `UnitPrice × Quantity`); if not, planner must handle the computation.
- **`Receipt.Total`** — the authoritative total from the receipt. Sum validation compares item totals against this field.
- **EF migration commands** (per `CLAUDE.md`): `dotnet ef migrations add <Name> -p Backend/src/TaxReader.Infrastructure -s Backend/src/TaxReader.Api`
- **Frontend `api-client.ts` pattern** — one exported async function per endpoint, typed payload from `@/types/api`. New `saveClassificationRule` and `acknowledgeSumMismatch` follow this.
- **TanStack Query mutations** — `useSaveClassificationRule` and `useAcknowledgeSumMismatch` follow the `useConfirmClassification` pattern in `use-receipt-items.ts`.

</code_context>

<specifics>
## Specific Ideas

- **Token savings from rules are real and meaningful**: if 60% of items match rules, the AI batch size drops by 60% — proportionally cheaper per upload. The Phase 3 D-01 batching design (single Anthropic call per upload) is preserved: `HybridClassificationService` collects all rule-unmatched items FIRST, then makes one AI call for all of them. No per-item AI calls.
- **Unbekannt replaces Unknown in the enum**: The `Category.Unknown = 0` value becomes `Category.Unbekannt = 0`. Frontend, PDF export, and CSV export must all handle the rename. The EF string mapping (`HasConversion<string>()`) means the DB stores "Unbekannt" not "Unknown" after migration.
- **Rule matching is case-insensitive by convention**: `VendorPattern` is a substring match using `StringComparison.OrdinalIgnoreCase`; `DescriptionPattern` and `SourceFilePattern` are regex with `RegexOptions.IgnoreCase`. Planner should document this in the classifier's behavior.
- **The €0.50 tolerance is absolute (gross), not relative**: `Math.Abs(itemsSum - receipt.Total) > 0.50m`. A €1000 receipt is flagged if items sum to €999.49 or less. The planner should note this invariant in the sum-validation test.
- **Rule-save dialog is a separate `Dialog`, not a `Popover`**: Needs focusable fields and a submit button for accessibility. The user returns to (or closes) the classify dialog after saving the rule.
- **Reasoning display for rule-matched items**: The `reason` field in `ItemClassification` for rule matches should read "Regel angewendet: [VendorPattern/DescriptionPattern] → [CategoryLabel]" or similar. The AI sets its own reason text. The `classify-dialog.tsx` already renders `item.latestClassification.reason` — no frontend change needed for the text itself once the backend sets it correctly.

</specifics>

<deferred>
## Deferred Ideas

- **Retroactive backfill when a rule is saved** — explicitly decided against (D-11). Users who want retroactive effect use the per-receipt "Neu klassifizieren" action. Consider adding a "Auf alle vorhandenen Belege anwenden" option in v2 (CLASS-V2-01).
- **Rule management UI** (list, edit, delete rules) — not in Phase 4. The user creates rules implicitly via overrides. An explicit rule management page belongs in a Phase 4 polish pass or v2.
- **System rule editing by admin** — system rules are seed data managed by migrations. No admin UI for rule management in this phase.
- **Auto-promotion: N overrides → suggest a rule** — v2 backlog (CLASS-V2-03). Phase 4 only creates rules on explicit user intent.
- **PdfPig zero-words → Tesseract fallback** (CONCERNS.md #16) — still deferred from Phase 3. Phase 4 classification work may surface more edge-case receipts where this bites. Likely a Phase 7 QA item.
- **Regex validation on rule patterns** — `DescriptionPattern` and `SourceFilePattern` accept arbitrary regex. Invalid regex will throw at classification time. A startup-time or save-time regex validation would be safer; defer to Phase 7 polish.

</deferred>

---

*Phase: 04-classification-trustworthiness*
*Context gathered: 2026-05-22*
