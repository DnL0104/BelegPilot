---
status: resolved
phase: 05-commercial-surface-payments
source: [05-VERIFICATION.md]
started: 2026-05-31T00:00:00Z
updated: 2026-05-31T00:00:00Z
---

## Current Test

Approved by user 2026-05-31.

## Tests

### 1. End-to-end Stripe checkout → webhook → token grant
expected: User clicks "Credits aufladen" → fills legal gate checkboxes → Kaufen → DemoMode credits directly → token balance increases → `/billing?payment=success` shows success banner, transaction appears in history
result: PASS — Demo-Kauf 200 Credits visible in Transaktionsverlauf (31.05.2026, +200, Typ: Kauf)

### 2. DE-compliant Stripe invoice PDF
expected: Invoice PDF with §14 UStG content — deferred to real Stripe keys; DemoMode produces no invoices
result: SKIPPED — DemoMode does not generate Stripe invoices. Verified in Phase 7 QA with live Stripe test keys.

## Summary

total: 2
passed: 1
issues: 0
pending: 0
skipped: 1
blocked: 0

## Gaps
