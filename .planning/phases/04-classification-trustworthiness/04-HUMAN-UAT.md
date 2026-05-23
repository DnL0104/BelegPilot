---
status: partial
phase: 04-classification-trustworthiness
source: [04-VERIFICATION.md]
started: 2026-05-23T00:00:00Z
updated: 2026-05-23T00:00:00Z
---

## Current Test

[awaiting human testing]

## Tests

### 1. End-to-end rule classification
expected: Upload a receipt whose item description matches a saved ClassificationRule. Verify the item is classified by the rule (ClassificationMethod = Rule, not AI), and that the token balance does NOT decrease for matched items.
result: [pending]

### 2. Inline reasoning visible without click
expected: Navigate to /receipts/{id} for a classified receipt. Each ReceiptItem should show its classification reason inline under a label "Warum wurde das so eingeordnet?" — no click/expand required.
result: [pending]

### 3. Sum-mismatch Alert lifecycle
expected: Process a receipt where the item totals don't add up to the receipt total (> €0.50 gap). The receipt detail page should show a dismissable amber Alert. Clicking "Als geprüft markieren" calls POST /receipts/{id}/acknowledge-sum, which returns 204 and the Alert disappears.
result: [pending]

### 4. Override-to-rule flow
expected: On the receipt detail page, change a classification to a different category via the classify dialog. The "Diese Regel speichern" button appears. Clicking it opens the save-rule-dialog pre-populated with vendor/description. On save, POST /receipt-items/{id}/save-rule returns 201 with a rule ID. A second identical save returns 409 Conflict.
result: [pending]

## Summary

total: 4
passed: 0
issues: 0
pending: 4
skipped: 0
blocked: 0

## Gaps
