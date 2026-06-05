---
phase: 06-legal-consent-data-export
plan: 07
subsystem: ci-legal-compliance
tags: [ci, legal, gdpr, tmg, gap-closure, cr-04]
requires:
  - "Existing hygiene-check job in .github/workflows/ci.yml (bash + set -e + exit 1 pattern)"
  - "Frontend/src/app/(legal) route-group pages with bracketed placeholder tokens"
provides:
  - "CI guard that fails the build on any [bracket] placeholder in (legal) pages"
  - "Tracked operator action for real legal-entity contact-data fill-in"
affects:
  - ".github/workflows/ci.yml (hygiene-check job)"
  - "Future deploys of Frontend/src/app/(legal)/**"
tech-stack:
  added: []
  patterns:
    - "Bash hygiene step (set -e, grep-inside-if to survive no-match) reused from existing hygiene-check job"
key-files:
  created: []
  modified:
    - ".github/workflows/ci.yml"
    - ".planning/phases/06-legal-consent-data-export/06-LEGAL-REVIEW.md"
decisions:
  - "Treat ANY bracketed token in (legal) pages as a placeholder — there are no legitimate bracketed strings in those pages, so no allowlist is needed."
  - "Keep grep inside the if-condition so a no-match (grep exit 1) does not abort the step under set -e; invert so matches-found triggers exit 1."
  - "Do NOT invent real contact data — the guard blocks deploy until the operator fills it in; fill-in stays a tracked operator action."
metrics:
  duration: "~10 min"
  completed: "2026-06-05"
  tasks: 1
  files: 2
---

# Phase 06 Plan 07: CI Legal-Placeholder Guard Summary

A CI guard added to the existing `hygiene-check` job fails the build (exit 1) if any `[bracket]` placeholder token remains in `Frontend/src/app/(legal)/`, making it impossible to deploy an Impressum/Datenschutz/AGB/Widerruf with unfilled `[Name]`/`[Anschrift]`/`[PLZ Ort]`/`[kontakt@taxreader.de]` contact data (TMG §5 / DSGVO Art. 13).

## What Was Built

- **CI guard step** "Verify legal pages contain no placeholder tokens (CR-04 / TMG §5)" appended to the existing `hygiene-check` job in `.github/workflows/ci.yml`. It reuses the established `shell: bash` + `set -e` + `exit 1` pattern, greps `Frontend/src/app/(legal)` for `\[[^]]+\]`, prints each `file:line:match` to the CI log, and exits nonzero when any placeholder is found. The grep sits inside the `if` condition so a clean (no-match) run does not abort under `set -e`.
- **Operator-action note** appended to `06-LEGAL-REVIEW.md` under a new "Operator Action: Placeholder Replacement (CR-04)" section, documenting that filling in real legal-entity data is a blocking, tracked operator step gated by this CI guard.

No new tooling, npm scripts, GitHub Actions, or dependencies were introduced. No other CI job (`backend-build-test`, `frontend-lint-build`) or the existing hygiene step was modified — the change is a pure 14-line insertion after the existing hygiene step (`git diff` hunk `@@ -46,0 +47,14 @@`).

## Verification Results

- `grep -F '(legal)' .github/workflows/ci.yml` → match (guard references the legal dir).
- `grep -F 'Legal placeholder check' .github/workflows/ci.yml` → match.
- `grep -cF "exit 1" .github/workflows/ci.yml` → 3 (existing hygiene + 2 lines of new guard; ≥2 satisfied).
- `bash -c 'grep -rqE "\[[^]]+\]" "Frontend/src/app/(legal)"'` → exit 0: 16 placeholder occurrences across all four pages (impressum 5, datenschutz 7, agb 2, widerruf 2). CI would FAIL today, proving the guard catches the real CR-04 violation.
- YAML parsed successfully via js-yaml (syntactically valid).
- Operator-action note present in `06-LEGAL-REVIEW.md` referencing placeholder replacement.

## Deviations from Plan

None - plan executed exactly as written.

## Open Operator Action

Real legal-entity contact data must still be supplied. Replacing `[Name]`/`[Anschrift]`/`[PLZ Ort]`/`[kontakt@taxreader.de]` (and any other bracketed token) in all four `(legal)` pages with real data remains an open, tracked operator action — it is intentionally NOT invented here. The CI guard blocks merge/deploy until that fill-in is complete; once the operator replaces all placeholders the same grep returns no matches and CI passes with no further code change.

## Known Stubs

The four `(legal)` pages still contain literal `[bracket]` placeholder tokens by design. These are not code stubs to be silently completed — they are deliberately left for the operator and are now guarded by CI so they cannot be deployed. Tracked in `06-LEGAL-REVIEW.md`.

## Self-Check: PASSED

- FOUND: .github/workflows/ci.yml (modified, guard step present)
- FOUND: .planning/phases/06-legal-consent-data-export/06-LEGAL-REVIEW.md (operator note present)
- FOUND commit: f792254 (ci(06-07): add legal-placeholder guard to hygiene-check)
