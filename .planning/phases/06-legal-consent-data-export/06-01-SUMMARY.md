---
phase: 06-legal-consent-data-export
plan: 01
subsystem: frontend-legal
tags: [legal, footer, public-routes, gdpr, impressum, datenschutz, agb, widerruf]
dependency_graph:
  requires: []
  provides: [footer-component, legal-pages-draft, public-legal-routes, lawyer-review-gate]
  affects: [06-02-consent-banner, 06-03-audit-log]
tech_stack:
  added: []
  patterns: [server-component-legal-page, draft-warning-pattern, footer-server-component, client-sub-component-wrapper]
key_files:
  created:
    - Frontend/src/components/layout/footer.tsx
    - Frontend/src/components/layout/cookie-settings-link.tsx
    - Frontend/src/app/(legal)/agb/page.tsx
    - Frontend/src/app/(legal)/widerruf/page.tsx
    - .planning/phases/06-legal-consent-data-export/06-LEGAL-REVIEW.md
  modified:
    - Frontend/src/app/(legal)/layout.tsx
    - Frontend/src/app/(authenticated)/layout.tsx
    - Frontend/src/providers/auth-provider.tsx
    - Frontend/src/app/(legal)/impressum/page.tsx
    - Frontend/src/app/(legal)/datenschutz/page.tsx
decisions:
  - Footer is a Server Component; CookieSettingsLink is a Client Component wrapper (no-op until 06-02 wires ConsentProvider)
  - Footer placed outside SidebarInset in authenticated layout to avoid overflow-hidden clipping
  - DraftWarning function is inlined per page (not shared import) since all pages are Server Components — avoids cross-boundary client import issues
  - Datenschutz references TADPF/Schrems II for Anthropic/Stripe Drittland-Uebermittlung
  - Widerruf includes §356 Abs.4 BGB waiver text verbatim (locked from Phase 5)
  - AGB uses conservative 5-Werktage support SLA, flagged in 06-LEGAL-REVIEW.md for lawyer review
metrics:
  duration_minutes: 10
  completed_date: 2026-06-03
  tasks_completed: 3
  tasks_total: 4
  files_created: 5
  files_modified: 5
---

# Phase 06 Plan 01: Legal Pages + Footer Summary

**One-liner:** Four German draft legal pages (Impressum, Datenschutz, AGB, Widerruf) with amber DraftWarning markers, a site-wide Footer component linking all five legal/consent items from every page, fixed TaxReader branding, public access for /agb and /widerruf, and a lawyer-review gate doc.

## What Was Built

### Task 1 — Footer + CookieSettingsLink + Layout Fixes + PUBLIC_PATHS (commit c0eea3f)

Created `Footer` as a Server Component at `Frontend/src/components/layout/footer.tsx` with five links: Impressum, Datenschutzerklärung, AGB, Widerrufsbelehrung, and Cookie-Einstellungen. The Cookie-Einstellungen item is a thin Client Component (`cookie-settings-link.tsx`) with a no-op `onClick` placeholder pending 06-02's ConsentProvider.

Fixed `(legal)/layout.tsx`: changed "BelegPilot" and "B" avatar to "TaxReader" / "T", added `<Footer />` after `<main>`.

Updated `(authenticated)/layout.tsx`: added `<Footer />` as a sibling of `<SidebarInset>` inside `<SidebarProvider>` — placed outside SidebarInset to avoid its `overflow-hidden` clip (per RESEARCH Open Question 3 resolution).

Updated `auth-provider.tsx` PUBLIC_PATHS to include `/agb` and `/widerruf`.

### Task 2 — Four Legal Pages with DraftWarning (commit 147c895)

Replaced both placeholder legal pages and created two new ones:

- **Impressum** (`impressum/page.tsx`): TMG §5 fields with operator placeholders, §19 UStG Kleinunternehmer note (no USt-IdNr.), ODR link, StBerG disclaimer.
- **Datenschutz** (`datenschutz/page.tsx`): DSGVO Art.13 (Zwecke + Rechtsgrundlagen), Art.22 (automated classification with human override note), Art.28 sub-processor table (Anthropic/Stripe/Sentry/BetterStack with DPA links), Drittland-Ubermittlung section referencing TADPF/Schrems II + Anthropic DPA, Art.20 self-service export note linking to /settings.
- **AGB** (`agb/page.tsx`): §1 "Vertragsgegenstand ist Strukturierung, keine Steuerberatung" (StBerG-safe), §2 GoBD non-applicability, §3 Widerrufsrecht referencing /widerruf, §4 refund policy, §5 "5 Werktagen" support SLA (flagged for lawyer), §6 VSBG signpost + ODR link.
- **Widerruf** (`widerruf/page.tsx`): 14-Tage-Frist, Widerrufsfolgen, §356 Abs.4 BGB digital-content waiver ("mein Widerrufsrecht verliere"), Muster-Widerrufsformular (BGB Anlage 2 template).

All four pages use the exact DraftWarning amber banner per 06-UI-SPEC.md.

### Task 3 — 06-LEGAL-REVIEW.md Gate Doc (commit 62f0e5a)

Created `.planning/phases/06-legal-consent-data-export/06-LEGAL-REVIEW.md` with a status table for all four legal pages (Drafted → Lawyer-reviewed → Live), explanatory text, per-page removal checklist, and a flag on the AGB row noting the 5-Werktage SLA assumption for lawyer confirmation.

### Task 4 — Human UAT (deferred to human)

The terminal checkpoint requires manually visiting the four legal pages in a browser to verify rendering, draft markers, sub-processor table, and footer presence. See Manual Verification Required section below.

## Commits

| Hash | Message |
|------|---------|
| c0eea3f | feat(06-01): create Footer + CookieSettingsLink + fix (legal) branding + PUBLIC_PATHS |
| 147c895 | feat(06-01): author four German legal pages with draft markers and full DSGVO copy |
| 62f0e5a | docs(06-01): create 06-LEGAL-REVIEW.md lawyer-review gate doc |

## Automated Verification Results

All automated checks passed:

- `cd Frontend && npm run build` — exit 0; /agb and /widerruf appear in route manifest
- Draft marker grep — all four page files contain "Entwurf" (grep -rL returns empty)
- Footer links — href="/agb", href="/widerruf", href="/impressum", href="/datenschutz" all present in footer.tsx
- PUBLIC_PATHS — "/agb" and "/widerruf" present in auth-provider.tsx
- 06-LEGAL-REVIEW.md — exists, contains "Lawyer-reviewed", "Drafted", "Live", all four page names, "5 Werktagen"
- AGB acceptance strings — "Vertragsgegenstand ist Strukturierung, keine Steuerberatung", "GoBD", "StBerG", "VSBG", `<Link href="/widerruf"` all present
- Datenschutz acceptance strings — Anthropic, Stripe, Sentry, BetterStack, anthropic.com/legal/dpa, Art. 22, TADPF all present
- Impressum acceptance strings — §5 TMG, §19 UStG, ec.europa.eu/consumers/odr present; no "USt-IdNr.: DE" (no real USt-ID)
- Widerruf acceptance strings — "Muster-Widerrufsformular" and "mein Widerrufsrecht verliere" (across line break) both present

## Deviations from Plan

None — plan executed exactly as written with no deviations. The CookieSettingsLink no-op placeholder is intentional per the plan's Task 1 action text: "for this plan render the button with an `onClick` that is a no-op placeholder `() => {}` and a `// TODO(06-02): wire reopenSettings` comment."

## Known Stubs

| Stub | File | Line | Reason |
|------|------|------|--------|
| `onClick={() => {}}` no-op on Cookie-Einstellungen button | `Frontend/src/components/layout/cookie-settings-link.tsx` | 9 | ConsentProvider (with `reopenSettings()`) lands in 06-02. TODO(06-02) comment present. The button link is visible and correct in the footer; behavior is wired in the next plan. |

This stub does NOT prevent the plan's goal (footer reachability for LEG-01 is met; the button renders and is present). Plan 06-02 resolves it.

## Threat Flags

No new threat surfaces beyond those covered in the plan's `<threat_model>`. Legal pages expose only operator-supplied public Impressum data (legally mandated under TMG §5). The new public routes /agb and /widerruf serve only static Server Components with no user data.

## Manual Verification Required (Human UAT)

The plan's Task 4 (`checkpoint:human-verify`) requires browser verification. Automated build and grep checks are complete and pass. The following manual steps remain:

1. Run `cd Frontend && npm run dev`.
2. As an **unauthenticated** user, visit each URL and confirm:
   - `http://localhost:3000/impressum` — loads (no redirect to /login), amber "⚠ Entwurf – anwaltliche Prüfung ausstehend" marker at top.
   - `http://localhost:3000/datenschutz` — loads, draft marker present, sub-processor table shows Anthropic/Stripe/Sentry/BetterStack with clickable DPA links.
   - `http://localhost:3000/agb` — loads (no redirect), draft marker present, "Vertragsgegenstand ist Strukturierung, keine Steuerberatung" visible.
   - `http://localhost:3000/widerruf` — loads (no redirect), draft marker present, Muster-Widerrufsformular section visible.
3. On any authenticated page (e.g., `http://localhost:3000/`), confirm the footer appears at the bottom with all five links: Impressum, Datenschutzerklärung, AGB, Widerrufsbelehrung, Cookie-Einstellungen.
4. Confirm the header logo on legal pages reads "TaxReader" (not BelegPilot).
5. NOTE: Lawyer review itself is tracked in 06-LEGAL-REVIEW.md and finalized in Phase 7 (QA-07). Do not remove draft markers yet.

**Resume signal:** Type "approved" or describe issues (e.g., missing field, wrong copy, broken link).

## Self-Check: PASSED

All created files exist on disk. All three task commits exist in git history.

| Check | Result |
|-------|--------|
| `Frontend/src/components/layout/footer.tsx` exists | FOUND |
| `Frontend/src/components/layout/cookie-settings-link.tsx` exists | FOUND |
| `Frontend/src/app/(legal)/agb/page.tsx` exists | FOUND |
| `Frontend/src/app/(legal)/widerruf/page.tsx` exists | FOUND |
| `.planning/phases/06-legal-consent-data-export/06-LEGAL-REVIEW.md` exists | FOUND |
| Commit c0eea3f exists | FOUND |
| Commit 147c895 exists | FOUND |
| Commit 62f0e5a exists | FOUND |
