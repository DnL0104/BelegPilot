---
status: partial
phase: 05-commercial-surface-payments
source: [05-VERIFICATION.md]
started: 2026-05-31T00:00:00Z
updated: 2026-05-31T00:00:00Z
---

## Current Test

[awaiting human testing]

## Tests

### 1. End-to-end Stripe checkout → webhook → token grant
expected: User clicks "Credits aufladen" → fills legal gate checkboxes → Kaufen → redirected to Stripe Checkout → completes payment → Stripe fires `checkout.session.completed` webhook → `GrantTokensJob` enqueues → token balance increases → `/billing?payment=success` shows success banner and polls balance every 3s for 15s
result: [pending]

### 2. DE-compliant Stripe invoice PDF
expected: After a successful purchase, the `/billing` page invoice list shows a PDF download link → downloaded PDF contains: vendor name, address, USt-ID (Kleinunternehmer-Hinweis per invoice_data.footer config), sequential invoice number, EUR amount — meets §14 UStG requirements for a Rechnung
result: [pending]

## Summary

total: 2
passed: 0
issues: 0
pending: 2
skipped: 0
blocked: 0

## Gaps
