---
phase: 05-commercial-surface-payments
plan: 02
subsystem: frontend-payments
tags: [frontend, payments, stripe, legal, widerrufsrecht, shadcn, react]

# Dependency graph
requires:
  - plan: 05-01
    provides: "POST /payments/checkout, GET /payments/invoices, POST /payments/portal endpoints"
provides:
  - "TopUpDialog with AGB + Widerrufsrecht legal gate (D-05) and DemoMode redirect (D-14)"
  - "useCreateCheckoutSession, useInvoices, useCreatePortalSession billing hooks"
  - "createCheckoutSession, createPortalSession, getInvoices API functions"
  - "CheckoutSession, Invoice, PortalSession, CreateCheckoutSessionRequest types"
  - "shadcn Checkbox component from @base-ui/react"
  - "TokenBalanceBadge destructive red styling for balance < 0 (D-11)"
  - "Credits & Abrechnung sidebar nav item with CreditCard icon (D-09)"
affects: [05-03]

# Tech tracking
tech-stack:
  added: ["shadcn Checkbox via @base-ui/react/checkbox (official registry)"]
  patterns:
    - "useCreateCheckoutSession useMutation hook follows use-tokens.ts convention"
    - "DemoMode redirect in top-up-dialog.tsx - canonical DemoMode owner"
    - "isNegative check takes precedence over isLow in TokenBalanceBadge"
    - "Plain label HTML used instead of shadcn Label (not installed); identical semantics"

key-files:
  created:
    - Frontend/src/hooks/use-billing.ts
    - Frontend/src/components/ui/checkbox.tsx
  modified:
    - Frontend/src/types/api.ts
    - Frontend/src/lib/api-client.ts
    - Frontend/src/components/tokens/top-up-dialog.tsx
    - Frontend/src/components/tokens/token-balance-badge.tsx
    - Frontend/src/components/layout/app-sidebar.tsx

key-decisions:
  - "Plain label element instead of shadcn Label - identical semantics, Simplicity First"
  - "base-ui Checkbox onCheckedChange takes boolean directly (not CheckedState union)"
  - "DemoMode redirect owned by top-up-dialog.tsx; 05-03 reads demo=true query param"

# Metrics
duration: 4min
completed: 2026-05-28
---

# Phase 05 Plan 02: Frontend Legal Gate and Billing Plumbing Summary

**Widerrufsrecht + AGB legal gate wired to real Stripe checkout, DemoMode redirect, negative balance destructive badge, and Credits & Abrechnung sidebar nav**

## Performance

- **Duration:** ~4 min
- **Tasks:** 2
- **Files modified:** 7 (2 created, 5 modified)

## Accomplishments

- shadcn Checkbox installed from official registry (uses @base-ui/react/checkbox)
- CheckoutSession, Invoice, PortalSession, CreateCheckoutSessionRequest in api.ts
- createCheckoutSession, createPortalSession, getInvoices in api-client.ts
- useCreateCheckoutSession, useInvoices, useCreatePortalSession in use-billing.ts
- TopUpDialog rewritten: usePurchaseTokens removed, useCreateCheckoutSession wired, AGB + Widerrufsrecht checkboxes (D-05), Kaufen disabled until both checked, DemoMode redirect (D-14), checkboxes reset on close
- TokenBalanceBadge updated: isNegative = balance < 0 with destructive red styling
- Sidebar updated: Credits & Abrechnung nav item with CreditCard icon after Einstellungen (D-09)
- npm run build passes, 0 errors

## Task Commits

1. Task 1 - Install Checkbox, add payment types, API functions, billing hooks: 61a1665
2. Task 2 - Rewrite TopUpDialog, update TokenBalanceBadge, add billing nav item: 7af2202

## Verification Results

All 10 checks passed:
1. widerrufsrecht-checkbox in top-up-dialog.tsx: 2 (>= 1 required)
2. hierdurch in top-up-dialog.tsx: 1 (exact statutory text per S356 Abs 4 BGB)
3. Ich verlange in top-up-dialog.tsx: 1
4. usePurchaseTokens in top-up-dialog.tsx: 0 (removed)
5. useCreateCheckoutSession in top-up-dialog.tsx: 2
6. isDemoMode in top-up-dialog.tsx: 1
7. demo=true in top-up-dialog.tsx: 1
8. isNegative in token-balance-badge.tsx: 2
9. Credits & Abrechnung in app-sidebar.tsx: 1
10. checkbox.tsx: FOUND

## Decisions Made

- Plain label instead of shadcn Label: label.tsx not installed. Plain HTML label has identical htmlFor/id pairing and WCAG accessibility. Simplicity First.
- base-ui Checkbox API: onCheckedChange takes (checked: boolean, eventDetails). First arg is direct boolean.
- DemoMode redirect ownership: isDemoMode check and window.location.href redirect live in top-up-dialog.tsx. Plan 05-03 reads demo=true param.

## Deviations from Plan

**1. [Rule 3 - Blocking] Used plain label instead of shadcn Label (not installed)**
- label.tsx not in component inventory
- Used native label element - identical HTML semantics, zero behavior difference
- Impact: None

## Known Stubs

None - all hooks and API functions wired to real backend endpoints from Plan 05-01.

## Threat Flags

None - no new network endpoints, auth paths, or schema changes in this plan.

## Self-Check: PASSED

- use-billing.ts: FOUND
- checkbox.tsx: FOUND
- Commit 61a1665: verified
- Commit 7af2202: verified
- npm run build: exit 0
