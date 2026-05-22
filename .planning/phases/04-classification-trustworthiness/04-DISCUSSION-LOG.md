# Phase 4: Classification Trustworthiness — Discussion Log

**Date:** 2026-05-22
**Participants:** DHalling + Claude

---

## Areas Discussed

### 1. Old category data fate

**Question 1:** What happens to existing ItemClassification records when the category enum is replaced?
- Options: Auto-map + reset unknown / Wipe all, re-run AI / Keep old, add migration note
- **Selected:** Auto-map + reset unknown — map each old category to the closest new one; unmappable records reset to Unbekannt/Suggested for manual re-review.

**Question 2:** What happens to the 40+ seeded ClassificationRule rows?
- Options: Migrate seed rules to new categories / Delete all seed rules / Keep as legacy (inactive)
- **Selected:** Migrate seed rules to new categories — remap patterns, drop ones that don't fit the new 13-category set.

**Question 3:** Enum naming convention?
- Options: German identifiers exactly as in REQUIREMENTS.md / Short English keys + German labels / You decide
- **Selected:** German identifiers exactly as specified — WerbungskostenArbeitsmittel, WerbungskostenFachliteratur, etc.

---

### 2. System rules vs user rules

**Question 1:** Should global system rules coexist with per-user rules?
- Options: Two-tier (UserId nullable) / User-only (no seed defaults) / Separate tables
- **Selected:** Two-tier: UserId nullable (null = system, set = user). One table, minimal migration.

**Question 2:** Rule matching schema?
- Options: Three separate nullable fields (VendorPattern, SourceFilePattern, DescriptionPattern) / Single Pattern + MatchType enum / You decide
- **Selected:** Three separate nullable fields — a rule fires when ALL non-null fields match. Old `Pattern` renamed to `DescriptionPattern`.

**Question 3:** Evaluation order — user rules vs system rules?
- Options: User rules always win / Priority int decides across both tiers / You decide
- **Selected:** User rules always win — classifier checks user rules first, falls back to system rules only if no user match.

---

### 3. Rule-save UX depth

**Question 1:** What does the user see when "Diese Regel speichern" is clicked?
- Options: Mini confirmation with editable pattern / Auto-save with undo toast / Same classify dialog extended
- **Selected:** Mini confirmation with editable pattern — a Dialog pre-populated with VendorPattern and DescriptionPattern, user can toggle fields and edit before saving.

**Question 2:** Retroactive backfill when a rule is saved?
- Options: No backfill (future uploads only) / Enqueue backfill job / Show count, let user decide
- **Selected:** No backfill — rule applies to future uploads only. Existing records untouched.

**Question 3:** Where does "Diese Regel speichern" appear?
- Options: Inside classify-dialog.tsx only / Both dialog and receipt detail inline / You decide
- **Selected:** Inside classify-dialog.tsx only.

---

### 4. Sum validation placement

**Question 1:** Where should the "Unverified" flag live?
- Options: New bool on Receipt entity / New ProcessingStatus value / New FileStatus value
- **Selected:** New bool HasSumMismatch on Receipt entity — informational flag, receipt still appears in reports.

**Question 2:** When should sum validation run?
- Options: End of ClassifyBatchJob (same pass) / Separate ValidateReceiptSumJob / On-demand at report time
- **Selected:** End of ClassifyBatchJob — same pass as classification.

**Question 3:** How does the user resolve a sum mismatch?
- Options: Dismiss the warning ("Als geprüft markieren") / Mandatory acknowledgment blocks report / Permanent warning badge, no dismiss
- **Selected:** Dismiss the warning — dismissable Alert with "Als geprüft markieren" button; receipt included in reports regardless.

---

## Deferred Ideas

- Retroactive backfill when saving a rule — explicitly excluded (D-11); v2 enhancement
- Rule management UI (list/edit/delete) — not in Phase 4 scope
- Auto-promotion: N overrides → suggest a rule — v2 backlog (CLASS-V2-03)
- Regex validation on save — defer to Phase 7 polish
- PdfPig zero-words → Tesseract fallback — still deferred from Phase 3

---

*Discussion log for human reference only. Decisions are captured canonically in 04-CONTEXT.md.*
