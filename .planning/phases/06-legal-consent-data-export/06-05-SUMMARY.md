---
phase: 06-legal-consent-data-export
plan: "05"
subsystem: compliance-tracking
tags: [legal, avv, dpa, marken, trademark, gdpr, operator-tracked]
dependency_graph:
  requires: []
  provides:
    - 06-AVV-TRACKING.md (AVV/DPA sign-off checklist for Anthropic, Stripe, Sentry, BetterStack)
    - 06-MARKEN-SEARCH.md (DPMA/EUIPO trademark search record for "TaxReader" classes 9+42)
  affects:
    - LEG-06 (AVVs/DPAs tracked; operator completes signing before launch)
    - LEG-09 (Marken clearance tracked; operator completes search before launch)
tech_stack:
  added: []
  patterns:
    - Operator-tracked compliance checklist pattern (markdown tables + sign-off gates)
key_files:
  created:
    - .planning/phases/06-legal-consent-data-export/06-AVV-TRACKING.md
    - .planning/phases/06-legal-consent-data-export/06-MARKEN-SEARCH.md
  modified: []
decisions:
  - AVV-TRACKING URLs coupled to Datenschutz sub-processor table (same four DPA URLs verified against datenschutz/page.tsx)
  - Drittland note covers Anthropic (USA/TADPF) and Stripe (USA/TADPF) with SCCs reference
  - Marken Decision field has three options: proceed / rename / register; conflict forces rename before launch
  - BetterStack privacy policy basis flagged for operator to verify whether a separate DPA form is available for paid accounts
metrics:
  duration_minutes: 2
  completed_date: "2026-06-03"
  tasks_completed: 2
  tasks_total: 3
  files_created: 2
  files_modified: 0
---

# Phase 06 Plan 05: AVV/DPA and Marken Tracking Summary

**One-liner:** Operator-tracked compliance docs for DSGVO Art. 28 AVV sign-off (four sub-processors with DPA URLs coupled to Datenschutz) and DPMA/EUIPO trademark clearance ("TaxReader" Nizza classes 9+42) with rename-forcing conflict gate.

---

## Tasks Completed

| Task | Name | Commit | Key Files |
|---|---|---|---|
| 1 | Create 06-AVV-TRACKING.md | d54358b | `.planning/phases/06-legal-consent-data-export/06-AVV-TRACKING.md` |
| 2 | Create 06-MARKEN-SEARCH.md | d330baa | `.planning/phases/06-legal-consent-data-export/06-MARKEN-SEARCH.md` |
| 3 | Operator sign-off checkpoint | (deferred to operator) | — |

---

## What Was Built

### 06-AVV-TRACKING.md
A sign-off checklist for DSGVO Art. 28 Auftragsverarbeitungsverträge (AVV) / Data Processing Agreements. Contains:
- Sub-processor table: Anthropic (`https://www.anthropic.com/legal/dpa`), Stripe (`https://stripe.com/de/legal/dpa`), Sentry (`https://sentry.io/legal/dpa/`), BetterStack (`https://betterstack.com/privacy`)
- DPA URLs are **identical** to those in the Datenschutz sub-processor table (`Frontend/src/app/(legal)/datenschutz/page.tsx`) — coupling rule satisfied
- Drittland-Übermittlung note: Anthropic (USA) and Stripe (USA) covered by TADPF + Schrems II reference + SCCs
- Operator action guide: step-by-step DPA sign-off + URL verification + DPF participant status check
- Sign-off gate with seven checkboxes — all must be checked before commercial launch

### 06-MARKEN-SEARCH.md
A DPMA + EUIPO trademark clearance search record for the mark "TaxReader". Contains:
- Nizza class definitions: class 9 (Software) and class 42 (SaaS/IT-Dienstleistungen)
- Results table: 4 rows (DPMA class 9, DPMA class 42, EUIPO class 9, EUIPO class 42) — all "Pending"
- Result legend: Clear / Conflicted / Already registered by us / Pending
- Decision section: proceed / rename / register (Pending until operator completes search)
- Risk assessment table
- Operator step-by-step search guide with direct links to register.dpma.de and euipo.europa.eu/eSearch
- Sign-off gate: conflict forces a rename decision document before launch

---

## Coupling Verification

The DPA URLs in 06-AVV-TRACKING.md were read from `Frontend/src/app/(legal)/datenschutz/page.tsx` (lines 144, 159, 173, 188) and are identical:

| Sub-processor | 06-AVV-TRACKING.md URL | Datenschutz page URL | Match |
|---|---|---|---|
| Anthropic | `https://www.anthropic.com/legal/dpa` | `https://www.anthropic.com/legal/dpa` | ✓ |
| Stripe | `https://stripe.com/de/legal/dpa` | `https://stripe.com/de/legal/dpa` | ✓ |
| Sentry | `https://sentry.io/legal/dpa/` | `https://sentry.io/legal/dpa/` | ✓ |
| BetterStack | `https://betterstack.com/privacy` | `https://betterstack.com/privacy` | ✓ |

---

## Deviations from Plan

None — plan executed exactly as written.

---

## Manual Verification Required (Human UAT — Operator Actions)

Task 3 of the plan is a `checkpoint:human-action` gate. Per the plan instructions, this is deferred to the operator. The executor has created the tracking artifacts; the operator must complete the following before commercial launch.

### LEG-06: AVV/DPA Sign-Off (06-AVV-TRACKING.md)

1. **Sign Anthropic DPA:** Visit https://www.anthropic.com/legal/dpa — accept the Data Processing Addendum. File the confirmation (PDF/email).
2. **Sign Stripe DPA:** Visit https://stripe.com/de/legal/dpa — confirm the DPA is active on your Stripe account. Download the confirmation.
3. **Sign Sentry DPA:** Visit https://sentry.io/legal/dpa/ — sign the DPA in Sentry organization settings. Download the signed PDF.
4. **Confirm BetterStack basis:** Visit https://betterstack.com/privacy — verify whether a separate DPA form is available for paid accounts; if so, sign it. If privacy policy is the Art. 28 basis, document this explicitly.
5. **Update 06-AVV-TRACKING.md:** Mark each row "Signed" = `✓ YYYY-MM-DD` and "Link in Datenschutz" = `✓` after URL verification.
6. **Check DPF participant status:** Verify Anthropic and Stripe appear at https://www.dataprivacyframework.gov/s/participant-search.

### LEG-09: DPMA + EUIPO Trademark Search (06-MARKEN-SEARCH.md)

1. **DPMA search:** Visit https://register.dpma.de/DPMAregister/marke/einsteiger — search "TaxReader" in classes 9 and 42. Screenshot results.
2. **EUIPO search:** Visit https://euipo.europa.eu/eSearch/ — search "TaxReader" in classes 9 and 42. Screenshot results.
3. **Record results:** Update the results table in 06-MARKEN-SEARCH.md with Search Date, Result, and evidence file references.
4. **Make decision:** Set Decision to `proceed` / `rename` / `register`. If `Conflicted` → create a rename decision document BEFORE commercial launch.

**Resume signal (from plan):** Type "tracked" to acknowledge the operator tasks are recorded (actual completion may occur before launch), or "blocked" with details.

---

## Known Stubs

None — this plan creates documentation-only artifacts with no code stubs.

---

## Threat Surface Scan

No new network endpoints, auth paths, file access patterns, or schema changes. Both files are planning documentation with no code surface.

---

## Self-Check: PASSED

| Item | Status |
|---|---|
| FOUND: `.planning/phases/06-legal-consent-data-export/06-AVV-TRACKING.md` | ✓ |
| FOUND: `.planning/phases/06-legal-consent-data-export/06-MARKEN-SEARCH.md` | ✓ |
| FOUND: commit `d54358b` (AVV-TRACKING) | ✓ |
| FOUND: commit `d330baa` (MARKEN-SEARCH) | ✓ |
