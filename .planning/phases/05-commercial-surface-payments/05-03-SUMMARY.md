---
phase: 05-commercial-surface-payments
plan: 03
subsystem: frontend-payments
tags: [frontend, payments, billing, stripe, tanstack-query, react]

# Dependency graph
requires:
  - plan: 05-01
    provides: "GET /payments/invoices, POST /payments/portal endpoints"
  - plan: 05-02
    provides: "useInvoices, useCreatePortalSession, useCreateCheckoutSession billing hooks; TopUpDialog with legal gate; DemoMode redirect"
  - plan: 05-04
    provides: "Stripe webhook + refund flow wired"
provides:
  - "/billing page with all 6 UI-SPEC sections"
  - "Balance polling on ?payment=success (3s for 15s)"
  - "DemoMode banner (reads ?demo=true set by TopUpDialog)"
  - "Negative balance destructive styling + Konto gesperrt label"
  - "Invoice list with PDF download links"
  - "Stripe Customer Portal redirect via POST /payments/portal"
affects: []

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "useQuery directly in page (not useTokenBalance hook) to allow dynamic refetchInterval for polling"
    - "Plain <a> tag with button classes for PDF download (Button has no asChild — uses @base-ui/react/button)"
    - "transactionTypeBadge helper function mapping string type to German Badge"

key-files:
  created:
    - Frontend/src/app/(authenticated)/billing/page.tsx

key-decisions:
  - "Used useQuery directly with queryKeys.tokens.balance instead of useTokenBalance hook — hook has fixed refetchInterval=30s; billing page needs dynamic polling (3s for 15s on payment=success)"
  - "PDF download uses plain <a> styled with Tailwind button classes — Button component uses @base-ui/react/button without asChild support"
  - "Invoice amountPaid divided by 100 for EUR display — Stripe stores amounts in cents"

# Metrics
duration: 8min
completed: 2026-05-31
---

# Phase 05 Plan 03: /billing Page Summary

**Full billing page with balance, transactions, invoices, and Stripe Customer Portal redirect**

## Performance

- **Duration:** ~8 min
- **Tasks:** 1
- **Files modified:** 1 (1 created)

## Accomplishments

- Created `Frontend/src/app/(authenticated)/billing/page.tsx` ("use client")
- All 6 UI-SPEC sections implemented:
  1. DemoMode banner (amber, `FlaskConical` icon, reads `?demo=true` set by TopUpDialog)
  2. Payment success banner (`CheckCircle` icon, `?payment=success`)
  3. Balance card with `CardAction` "Credits aufladen" button (opens TopUpDialog), negative balance destructive styling + "Konto gesperrt" label + CardFooter warning
  4. Transaction history table (last 20, German dates, +/- Credits, type badges: Kauf/Rückerstattung/Verbrauch)
  5. Invoice list table (German EUR formatting, PDF download link or "Ausstehend")
  6. Payment method card with "Zahlungsmethode verwalten" → Stripe Customer Portal redirect
- Balance polling: `useQuery` with `refetchInterval: isPolling ? 3000 : 30_000`; `isPolling=true` for 15s on `?payment=success`
- `npm run build` exits 0; `/billing` route present in build output

## Task Commits

1. Task 1 - Create /billing page: f7a25fa

## Verification Results

All checks passed:
1. file exists: True
2. Credits & Abrechnung: 2 (≥1 required)
3. payment=success: 1 (≥1 required)
4. isDemoMode|demo.*true: 2 (≥1 required)
5. useInvoices: 2 (≥1 required)
6. useCreatePortalSession: 2 (≥1 required)
7. refetchInterval: 1 (≥1 required)
8. Zahlungsmethode verwalten: 1 (≥1 required)
9. Rechnungen: 2 (≥1 required)
10. Transaktionsverlauf: 1 (≥1 required)
11. npm run build: exit 0
12. top-up-dialog.tsx NOT modified

## Decisions Made

- `useQuery` directly (not `useTokenBalance`) — needed dynamic `refetchInterval` for polling. Shared `queryKeys.tokens.balance` key ensures cache consistency.
- Plain `<a>` with Tailwind button classes for PDF download — `Button` from `@base-ui/react/button` has no `asChild`; direct anchor is correct pattern.
- `invoice.amountPaid / 100` — Stripe reports amounts in cents; `formatCurrency` expects EUR decimal.

## Deviations from Plan

**1. [Rule 3 - Blocking] Used plain `<a>` instead of `<Button asChild>`**
- `Button` component wraps `@base-ui/react/button` which does not accept `asChild` prop
- Used plain `<a>` with equivalent Tailwind classes — identical visual output
- Impact: None

## Known Stubs

None — all hooks wire to real backend endpoints from Plans 05-01 and 05-02.

## Threat Flags

- T-05-13: `?payment=success` manipulation accepted (query param shows banner only — does not grant tokens; token grant is backend-only via webhook)

## Self-Check: PASSED

- billing/page.tsx: FOUND
- npm run build: exit 0
- Commit f7a25fa: verified
- top-up-dialog.tsx: NOT modified
