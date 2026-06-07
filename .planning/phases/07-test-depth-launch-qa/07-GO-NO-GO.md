# Go / No-Go Decision Record

**Phase:** 07-test-depth-launch-qa
**Milestone:** Commercial DE launch
**Rule:** GO only when ALL D-05 rows show status **GREEN**. Any D-05 row that is PENDING or BLOCKED = NO-GO.

---

## Hard Blockers (D-05)

> All three must be GREEN before launch. No exceptions.
>
> **Operator checkpoint resolution (2026-06-07):** The operator replied "approved — tracked".
> All D-05 rows remain OPEN/PENDING. GO is withheld until every D-05 row is individually
> confirmed GREEN. These items are tracked pre-launch obligations, not yet cleared.

| # | Item | Source | Status | Evidence |
|---|------|--------|--------|----------|
| 1 | **All automated CI suites green** — QA-01 Postgres integration tests (`TaxReader.IntegrationTests`) + QA-02 Vitest unit/component tests + QA-03 Playwright E2E happy path all pass on `main`. The heavy-suite gated job (07-06) must be green on the `main` branch — not just on a feature branch. | D-05 §1 / CONTEXT.md | **OPEN — PENDING** (CI heavy suite has not yet confirmed green on `main`; this must be verified before GO) | _Link to CI run on `main`:_ |
| 2 | **Lawyer sign-off on AGB + Datenschutzerklärung.** `06-LEGAL-REVIEW.md` shows **Lawyer-reviewed** for all four legal pages. All `[bracketed]` placeholder tokens replaced (06-07 CI guard green). `<DraftWarning />` components removed. | D-05 §2 / QA-07 / 06-LEGAL-REVIEW.md | **OPEN — PENDING** (lawyer review not yet commissioned; legal placeholder CI guard still red; DraftWarning components still in place) | _Lawyer name + sign-off date:_ |
| 3 | **Phase 6 operator items closed.** (a) Real Impressum/legal contact data filled — `hygiene-check` CI placeholder guard is green. (b) All four AVVs/DPAs signed — `06-AVV-TRACKING.md` "Signed" column shows `✓` for Anthropic, Stripe, Sentry, and BetterStack. | D-05 §3 / 06-AVV-TRACKING.md | **OPEN — PENDING** (legal data not yet filled; all four AVVs unconfirmed — Anthropic, Stripe, Sentry, BetterStack all outstanding) | _AVV filing location:_ |

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
Decision: PENDING — operator approved/tracked 2026-06-07;
          GO withheld until all D-05 hard blockers are confirmed GREEN.

Date: 2026-06-07 (checkpoint resolution — not a GO)
By: Operator (DHalling) — "approved — tracked"

D-05 blockers cleared:
  [1] CI suites green on main:                    [ ] OPEN
  [2] Lawyer sign-off on AGB + Datenschutz:       [ ] OPEN
  [3] Phase 6 operator items (legal data + AVVs): [ ] OPEN

Notes:
  Operator acknowledged all three D-05 blockers as tracked, open, pre-launch obligations.
  The external monitoring setup (BetterStack / Sentry) is likewise outstanding (see D-08 rows).
  This record remains PENDING until each D-05 row is individually confirmed and evidenced.
  Re-open this file, fill in the evidence column for each row, and flip the decision to GO
  only when all three checkboxes are ticked.
```

> **Rule:** GO only when all three D-05 checkboxes above are ticked AND evidence is filled in.
> **Current state:** NO-GO — three D-05 blockers open (CI heavy suite, lawyer sign-off, legal data + four AVVs).

---

_Authored: Phase 7 Plan 07 (07-07)_
_Requirement: QA-07 / D-05 / D-06_
_Last updated: 2026-06-07 (checkpoint resolution: "approved — tracked")_
