# Go / No-Go Decision Record

**Phase:** 07-test-depth-launch-qa
**Milestone:** Commercial DE launch
**Rule:** GO only when ALL D-05 rows show status **GREEN**. Any D-05 row that is PENDING or BLOCKED = NO-GO.

---

## Hard Blockers (D-05)

> All three must be GREEN before launch. No exceptions.

| # | Item | Source | Status | Evidence |
|---|------|--------|--------|----------|
| 1 | **All automated CI suites green** — QA-01 Postgres integration tests (`TaxReader.IntegrationTests`) + QA-02 Vitest unit/component tests + QA-03 Playwright E2E happy path all pass on `main`. | D-05 §1 / CONTEXT.md | PENDING | _Link to CI run on `main`:_ |
| 2 | **Lawyer sign-off on AGB + Datenschutzerklärung.** `06-LEGAL-REVIEW.md` shows **Lawyer-reviewed** for all four legal pages. All `[bracketed]` placeholder tokens replaced (06-07 CI guard green). `<DraftWarning />` components removed. | D-05 §2 / QA-07 / 06-LEGAL-REVIEW.md | PENDING | _Lawyer name + sign-off date:_ |
| 3 | **Phase 6 operator items closed.** (a) Real Impressum/legal contact data filled — `hygiene-check` CI placeholder guard is green. (b) All four AVVs/DPAs signed — `06-AVV-TRACKING.md` "Signed" column shows `✓` for Anthropic, Stripe, Sentry, and BetterStack. | D-05 §3 / 06-AVV-TRACKING.md | PENDING | _AVV filing location:_ |

---

## Non-Blocking (D-06)

> Track these items, but do NOT gate launch on them.

| # | Item | Source | Status | Notes |
|---|------|--------|--------|-------|
| A | **Native-speaker DE polish review.** A German native speaker reviews all user-facing copy for `Sie`-form, natural phrasing, and absence of Denglisch. Goes beyond the automated CI guard (QA-04). | D-06 / QA-04 | PENDING | _Reviewer name + date when complete_ |
| B | **Prior-phase manual UAT debt (Phases 2/3/4).** HUMAN-UAT items from earlier phases that were never formally closed: upload edge cases, classification-confirm UX, report export UI. | D-06 | PENDING | _See each phase's HUMAN-UAT.md_ |

---

## Additional Confirmation Items (operator-wired, D-08)

> These support ops readiness. Not a D-05 hard blocker in the launch-gate sense, but must
> be done before going live to avoid monitoring blind spots.

| # | Item | Status | Notes |
|---|------|--------|-------|
| C | BetterStack keyword monitors live and both reporting **Up** (`/health` + `/api/v1/health`). | PENDING | See 07-OPS-SETUP.md |
| D | BetterStack status page created and linked from site footer. | PENDING | See 07-OPS-SETUP.md |
| E | Sentry quiet-hours alert rule set (23:00-07:00 Europe/Berlin, HIGH only, email + push). | PENDING | See 07-OPS-SETUP.md |
| F | PITFALLS.md walked end-to-end — all Section D + E checkboxes ticked. | PENDING | See PITFALLS.md |

---

## Mobile UAT (QA-05)

> Non-blocking. The automated Playwright viewport smoke covers `sm`/`md` — this tracks the
> phone-camera portion that cannot be automated.

| # | Item | Status | Result |
|---|------|--------|--------|
| G | Mobile phone-camera photo-receipt upload end-to-end at `sm` (640 px) and `md` (768 px) on a real device. | PENDING | |

---

## Decision

```
Decision: [ GO / NO-GO ]

Date: ___________
By: ___________

D-05 blockers cleared:
  [1] CI suites green: [ ]
  [2] Lawyer sign-off: [ ]
  [3] Phase 6 operator items: [ ]

Notes:
```

> Fill in GO only when all three D-05 checkboxes above are ticked.
> Fill in NO-GO with reasons (one per D-05 blocker still open) when any remain.

---

_Authored: Phase 7 Plan 07 (07-07)_
_Requirement: QA-07 / D-05 / D-06_
_Last updated: 2026-06-07_
