---
status: partial
phase: 06-legal-consent-data-export
source: [06-VERIFICATION.md]
started: 2026-06-05T00:00:00.000Z
updated: 2026-06-05T00:00:00.000Z
---

## Current Test

[awaiting human testing]

## Tests

### 1. Operator: Replace placeholder contact details in all four (legal) pages (CR-04 — pre-launch blocker, now CI-guarded)
expected: [Name], [Anschrift], [PLZ Ort], [kontakt@taxreader.de] (and any other [bracket] token) replaced with real legal-entity data in impressum/datenschutz/agb/widerruf; mailto link is a valid email. CI hygiene-check currently FAILS by design until this is done.
result: [pending]

### 2. Operator: AVV/DPA signing — Anthropic, Stripe, Sentry, BetterStack (LEG-06)
expected: All four AVVs/DPAs signed/accepted, filed, and checked off in 06-AVV-TRACKING.md; DPA URLs match those in datenschutz/page.tsx
result: [pending]

### 3. Operator: DPMA + EUIPO Marken search for 'TaxReader' classes 9 + 42 (LEG-09)
expected: Search results recorded in 06-MARKEN-SEARCH.md as Clear/Conflicted; decision set to proceed/rename/register; rename decided before launch if conflicted
result: [pending]

### 4. Lawyer review of AGB + Datenschutzerklärung (LEG-02/LEG-03 — deferred to Phase 7 QA-07 by design D-02)
expected: 06-LEGAL-REVIEW.md rows reach Lawyer-reviewed for all four pages; draft markers removed after sign-off
result: [pending]

### 5. Legal pages unauthenticated access + footer links + draft markers (UI behavior)
expected: /impressum, /datenschutz, /agb, /widerruf load without redirect for unauthenticated users; amber draft marker visible; footer shows all five links; header reads 'TaxReader'
result: [pending]

### 6. Cookie banner TTDSG compliance + Sentry consent gating (LEG-05 — UI behavior)
expected: Banner appears on first visit with equally prominent Alle akzeptieren / Nur notwendige; Fehleranalyse unchecked by default; Sentry init on grant, Sentry.close() on revoke with no page reload; footer Cookie-Einstellungen reopens dialog
result: [pending]

### 7. DSGVO export end-to-end — bundle now includes parsed_receipts, IDOR check, one-time token, failure recovery (LEG-07 — integration)
expected: Settings -> Daten exportieren shows Wird erstellt... then Export bereit; downloaded zip contains receipts, parsed_receipts (vendor/date/amount), items, classifications, token_transactions, audit_log, README; IDOR: second account gets 403; second download attempt 404/expired; a failed/stuck job surfaces as Expired with a re-trigger button
result: [pending]

## Summary

total: 7
passed: 0
issues: 0
pending: 7
skipped: 0
blocked: 0

## Gaps
