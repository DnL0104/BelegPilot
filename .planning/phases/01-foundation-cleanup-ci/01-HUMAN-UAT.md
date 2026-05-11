---
status: partial
phase: 01-foundation-cleanup-ci
source:
  - 01-VERIFICATION.md
started: 2026-05-11T13:57:47Z
updated: 2026-05-11T13:57:47Z
---

## Current Test

[awaiting human testing]

## Tests

### 1. Decide on local `build-diag.txt`

expected: |
  Confirm the 1.8MB `build-diag.txt` at the repo root is acceptable as a local-only
  artifact, OR delete it locally. File is gitignored (matched by `.gitignore:16`),
  never tracked, never reaches the CI runner (`actions/checkout@v4` only restores
  tracked files). One must-have truth was literally "Repository working tree contains
  no `build-diag*.txt` or `*.binlog` files" — strict text reading FAILS, behavioural
  reading PASSES. Same precedent as the local empty `storage/` directory documented
  as benign in 01-02-SUMMARY.md.
result: pending

### 2. Enable branch protection on `main` (Phase 1 SC #1 closure)

expected: |
  GitHub → Settings → Branches → Add rule for `main` with three required status checks
  (`Hygiene check (no PII / build artifacts)`, `Backend build + test`, `Frontend lint + build`);
  0 reviewers; signed-commits OFF; linear-history OFF; admin bypass disallowed. Per D-10.
  Until done, CI runs on every PR but does not BLOCK merges.
result: pending

### 3. Sentry dashboard setup (Phase 1 SC #4 closure)

expected: |
  Create Sentry EU organisation (sentry.eu.io), two projects (`taxreader-api`,
  `taxreader-web`), set the two D-15 alert rules (new-error-type 1h cooldown +
  sustained ≥10 events/min for ≥5min), disable the default `Send a notification for
  new issues` rule, confirm Email-only delivery (no Slack/PagerDuty/Discord), and
  record both DSNs in `.env`. Code is dormant by design until DSN is set; backend
  becomes live on first non-empty `Sentry__Dsn`.
result: pending

### 4. GDPR/AVV compliance (PROJECT.md constraint)

expected: |
  Sign Anthropic AVV (Auftragsverarbeitungsvertrag) and Sentry DPA before flipping
  any DSN environment variable to a non-empty value in production. Both contracts
  in operator's records. Compliance prerequisite, not a code artifact.
result: pending

## Summary

total: 4
passed: 0
issues: 0
pending: 4
skipped: 0
blocked: 0

## Gaps
