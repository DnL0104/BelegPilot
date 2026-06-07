---
phase: 07-test-depth-launch-qa
plan: 05
subsystem: frontend-e2e
tags: [playwright, e2e, de-locale, qa-03, qa-05, responsive]
dependency_graph:
  requires: [07-04]
  provides: [QA-03, QA-05-automated]
  affects: [frontend, ci-heavy-job]
tech_stack:
  added:
    - "@playwright/test@^1.60.0 (Next.js 16 peer requires ^1.51.1)"
  patterns:
    - "Playwright webServer: npm run build && npm run start (standalone production, not next dev)"
    - "testDir: ./e2e (Vitest exclude: **/e2e/** already present from 07-04)"
    - "locale: de-DE + timezoneId: Europe/Berlin for German copy + EUR formatting"
    - "sm(640)/md(768) viewport projects for responsive smoke (QA-05 automated)"
key_files:
  created:
    - Frontend/playwright.config.ts
    - Frontend/e2e/happy-path.spec.ts
    - Frontend/e2e/fixtures/sample-receipt.pdf
  modified:
    - Frontend/package.json (added test:e2e script, @playwright/test devDep)
    - Frontend/package-lock.json
    - Frontend/.gitignore (added /test-results/, /playwright-report/, /playwright/.cache/)
decisions:
  - "Playwright 1.60.0 installed (not 1.50): Next.js 16.2.2 requires peerOptional @playwright/test@^1.51.1; 1.50 causes ERESOLVE — resolved by installing latest satisfying version (^1.51.1 → 1.60.0)"
  - "Backend prerequisite documented in spec header comment rather than auto-starting: full docker-compose stack is the heavy CI job (07-06); locally the operator runs docker compose up db api first"
  - "Export step uses Promise.all([waitForEvent('download'), click]) with .catch(() => null) fallback to toast-text check — the export triggers a blob URL anchor-click, not a Playwright-native download in all cases; spec validates either download event OR success/error toast"
metrics:
  duration: "~18 min"
  completed: "2026-06-07"
  tasks_completed: 2
  tasks_total: 2
  files_modified: 5
  files_created: 3
---

# Phase 07 Plan 05: Playwright E2E Happy Path Summary

**One-liner:** Playwright 1.60 (DE locale, standalone production server, sm/md viewports) with a single register-to-export happy-path spec exercising the real stack — no API mocks.

## Tasks Completed

| Task | Name | Commit | Files |
|------|------|--------|-------|
| 1 | Playwright install + config | f62d7b1 | playwright.config.ts, package.json, package-lock.json, .gitignore |
| 2 | e2e/happy-path.spec.ts | 89839ce | e2e/happy-path.spec.ts, e2e/fixtures/sample-receipt.pdf |

## Verification Result

```
npx playwright test --list e2e/happy-path.spec.ts

Listing tests:
  [desktop] › happy-path.spec.ts:30:5 › happy path: register → login → upload → classify → confirm → report → export
  [md] › happy-path.spec.ts:30:5 › happy path: register → login → upload → classify → confirm → report → export
  [sm] › happy-path.spec.ts:30:5 › happy path: register → login → upload → classify → confirm → report → export
Total: 3 tests in 1 file
```

## Spec Coverage

The single `test(...)` in `e2e/happy-path.spec.ts` covers:

1. `/register` — fill Name, E-Mail, Passwort, Passwort bestätigen; submit; assert redirect
2. `/login` — conditional (only if registration redirects there); fill E-Mail + Passwort; assert redirect away from login
3. `/upload` — attach `e2e/fixtures/sample-receipt.pdf` via `input[type=file]`; click upload button; assert "Beleg(e) wird/werden verarbeitet" success toast
4. `/receipts` — wait for receipt row to appear; click to navigate to detail
5. `/receipts/[id]` — assert "Artikel (" heading + item row; open classify dialog; assert "Artikel klassifizieren" heading; quick-confirm ("Vorschlag bestätigt") or manual confirm ("Klassifizierung bestätigt")
6. `/reports` — assert "Jahresbericht" heading; assert EUR de-DE pattern `/\d+[.,]\d{2}\s*€/` if data present
7. Export — click CSV button; assert download event OR toast feedback

**German copy asserted:** "Konto erstellen", "Belege hochladen", "Artikel klassifizieren", "Vorschlag bestätigt" / "Klassifizierung bestätigt", "Jahresbericht"

**EUR de-DE pattern:** `/\d+[.,]\d{2}\s*€/`

**No /api/v1 mocks:** real stack end-to-end (QA-03 ✓)

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Playwright 1.50 peer conflict with Next.js 16**
- **Found during:** Task 1 (npm install @playwright/test@1.50)
- **Issue:** Next.js 16.2.2 declares `peerOptional @playwright/test@"^1.51.1"`; npm ERESOLVE rejects 1.50
- **Fix:** Installed `@playwright/test@^1.60.0` (latest satisfying `^1.51.1`); version pinned in package.json as `"^1.60.0"`
- **Files modified:** Frontend/package.json, Frontend/package-lock.json
- **Commit:** f62d7b1

## Known Stubs

None — all spec steps target real routes with real backend calls.

## Backend Prerequisite (not a stub)

The spec header documents: a running BelegPilot backend + PostgreSQL must be reachable at `BACKEND_API_URL` (default `http://localhost:5190`). This is expected per the plan's `<dependency_note>` — the live green run is the heavy CI job (07-06) and local `docker compose up`.

## Threat Flags

None — no new network endpoints, auth paths, or schema changes introduced.

## Self-Check: PASSED

- Frontend/playwright.config.ts: FOUND
- Frontend/e2e/happy-path.spec.ts: FOUND
- Frontend/e2e/fixtures/sample-receipt.pdf: FOUND
- Commit f62d7b1: FOUND (git log)
- Commit 89839ce: FOUND (git log)
- `npx playwright test --list` output: 3 tests in 1 file (desktop/md/sm) — CONFIRMED
