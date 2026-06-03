---
phase: 06-legal-consent-data-export
plan: 02
subsystem: ui
tags: [consent, ttdsg, sentry, cookie-banner, react-context, localstorage, nextjs]

requires:
  - phase: 06-01
    provides: Footer component with CookieSettingsLink placeholder, (authenticated) and (legal) layouts with footer mounted

provides:
  - localStorage-backed ConsentProvider context (taxreader-consent) with acceptAll/acceptNecessary/updateConsent/reopenSettings
  - TTDSG-compliant CookieBanner (fixed bottom, equal-prominence buttons, decided=false trigger)
  - ConsentSettingsDialog with Notwendig (always-on disabled) and Fehleranalyse (opt-in, not pre-ticked)
  - Runtime Sentry gate: hasSentryConsent() in instrumentation-client.ts + Sentry.isInitialized() guard in provider
  - Footer CookieSettingsLink wired to useConsent().reopenSettings()
  - ConsentProvider mounted in app/layout.tsx; CookieBanner mounted in (authenticated) and (legal) layouts

affects: [06-03, 06-04, 06-05, 06-06]

tech-stack:
  added: []
  patterns:
    - ConsentProvider follows auth-provider.tsx "use client" + createContext + localStorage + exported hook pattern
    - hasSentryConsent() reads localStorage inside function body (not module level) — safe before hydration per Next.js 16 docs
    - Sentry double-init prevented by Sentry.isInitialized() guard (Pitfall 3)
    - Sentry.close(2000) fire-and-forget async (void) to avoid blocking re-render (Pitfall 7)

key-files:
  created:
    - Frontend/src/providers/consent-provider.tsx
    - Frontend/src/components/consent/cookie-banner.tsx
    - Frontend/src/components/consent/consent-settings-dialog.tsx
  modified:
    - Frontend/instrumentation-client.ts
    - Frontend/src/components/layout/cookie-settings-link.tsx
    - Frontend/src/app/layout.tsx
    - Frontend/src/app/(authenticated)/layout.tsx
    - Frontend/src/app/(legal)/layout.tsx

key-decisions:
  - "D-07: No page reload on consent change — Sentry.close() handles revoke; Sentry.init() guarded by isInitialized() handles grant"
  - "D-08: Essential auth cookies excluded from consent toggles — Notwendig category disabled/always-on"
  - "hasSentryConsent() in instrumentation-client.ts reads localStorage directly (no React context available pre-hydration)"
  - "CookieBanner mounted in both (authenticated) and (legal) layouts so first-visit consent banner appears regardless of entry point"

patterns-established:
  - "Pattern: ConsentProvider wraps AuthProvider inside TooltipProvider in root layout"
  - "Pattern: consent settings dialog open state uses both local showSettings state AND settingsPanelOpen from context to support both inline (banner) and footer-triggered opens"

requirements-completed: [LEG-05]

duration: 25min
completed: 2026-06-03
---

# Phase 06 Plan 02: Cookie Consent Summary

**TTDSG-compliant cookie consent system with localStorage-backed ConsentProvider, equal-prominence banner, opt-in Sentry gate via hasSentryConsent() in instrumentation-client.ts, and footer revoke entry point**

## Performance

- **Duration:** ~25 min
- **Started:** 2026-06-03T00:00:00Z
- **Completed:** 2026-06-03T00:25:00Z
- **Tasks:** 2 (+ 1 terminal human-verify checkpoint deferred)
- **Files modified:** 8

## Accomplishments

- ConsentProvider with localStorage key `taxreader-consent`, acceptAll/acceptNecessary/updateConsent, Sentry init/close with no page reload (D-07)
- CookieBanner: fixed bottom bar, `role="region"` + `aria-label`, equal-prominence "Alle akzeptieren" / "Nur notwendige" buttons (TTDSG D-06)
- ConsentSettingsDialog: Notwendig always-on disabled checkbox; Fehleranalyse opt-in defaults to `false` (not pre-ticked, TTDSG T-06-21)
- instrumentation-client.ts: compound Sentry guard `NEXT_PUBLIC_SENTRY_ENABLED === "true" && hasSentryConsent()` — consent read from localStorage before React hydration
- Footer "Cookie-Einstellungen" link wired to `useConsent().reopenSettings()` replacing 06-01 no-op
- `npm run build` exits 0

## Task Commits

1. **Task 1: ConsentProvider + instrumentation-client consent gate** - `aa24881` (feat)
2. **Task 2: CookieBanner + ConsentSettingsDialog + layout mounts** - `fc557b4` (feat)

**Plan metadata:** see final metadata commit below

## Files Created/Modified

- `Frontend/src/providers/consent-provider.tsx` — localStorage-backed consent context; grantSentry/revokeSentry helpers; useConsent() hook
- `Frontend/instrumentation-client.ts` — added hasSentryConsent() + compound Sentry.init guard
- `Frontend/src/components/consent/cookie-banner.tsx` — TTDSG fixed-bottom banner; returns null when decided=true
- `Frontend/src/components/consent/consent-settings-dialog.tsx` — shadcn Dialog; Notwendig disabled; Fehleranalyse opt-in
- `Frontend/src/components/layout/cookie-settings-link.tsx` — wired to useConsent().reopenSettings()
- `Frontend/src/app/layout.tsx` — ConsentProvider added wrapping AuthProvider
- `Frontend/src/app/(authenticated)/layout.tsx` — CookieBanner mounted as last child of SidebarProvider
- `Frontend/src/app/(legal)/layout.tsx` — CookieBanner mounted after Footer

## Decisions Made

- Sentry init/close is always fire-and-forget (void Sentry.close(2000)) — avoids blocking the re-render with an async wait, per Pitfall 7 in 06-RESEARCH.md
- hasSentryConsent() wrapped in try/catch to handle malformed localStorage entries without throwing
- Dialog open state uses OR of local `showSettings` state and `settingsPanelOpen` from context so both the banner's "Einstellungen" button and the footer link correctly open the same dialog
- ConsentSettingsDialog syncs local `fehleranalyse` state from `consent.fehleranalyse` on each open (handleOpenChange) — ensures reopened dialog reflects the currently stored consent state

## Deviations from Plan

None — plan executed exactly as written.

## Issues Encountered

None.

## Manual Verification Required (Human UAT)

The final task is a `checkpoint:human-verify` gate (LEG-05 acceptance). Browser interaction is required. Steps to run after `cd Frontend && npm run dev`:

1. In a fresh browser profile (or after `localStorage.clear()`), load the app — the cookie banner must appear at the bottom with "Alle akzeptieren" and "Nur notwendige" rendered at EQUAL size (one filled emerald, one outline — same height/padding).
2. Click "Einstellungen" → the Fehleranalyse checkbox must be UNCHECKED by default; Notwendig must be checked AND disabled (not clickable).
3. Set `NEXT_PUBLIC_SENTRY_ENABLED=true` in env and reload. Click "Alle akzeptieren" → open DevTools, confirm Sentry initializes (network request to ingest endpoint or `Sentry.isInitialized()` true in console). No page reload should occur.
4. From the footer, click "Cookie-Einstellungen" → the settings dialog reopens. Uncheck Fehleranalyse → "Einstellungen speichern" → confirm `Sentry.close()` ran (no further Sentry events) and NO page reload happened.
5. Confirm localStorage key `taxreader-consent` reflects each choice.

**Resume signal:** Type "approved" or describe issues (checkbox pre-ticked, button size mismatch, Sentry fires before consent, page reloads on revoke).

## Next Phase Readiness

- ConsentProvider is available to any component via `useConsent()` — future phases can read consent state without new setup
- Sentry is properly gated; no telemetry fires until user consents
- LEG-05 functional implementation complete; pending human UAT approval

## Self-Check: PASSED

- All 8 implementation files verified present on disk
- Both task commits (aa24881, fc557b4) confirmed in git log
- `npm run build` exits 0

---
*Phase: 06-legal-consent-data-export*
*Completed: 2026-06-03*
