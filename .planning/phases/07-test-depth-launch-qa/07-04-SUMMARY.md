---
phase: 07-test-depth-launch-qa
plan: 04
subsystem: testing
tags: [vitest, react-testing-library, typescript, frontend, jwt, axios, upload, classification]

requires: []
provides:
  - Vitest 3 + Testing Library configured for the Next.js 16 frontend (first-ever frontend test coverage)
  - format.ts pure functions covered (de-DE currency formatting, 13 category labels, status labels)
  - api-client.ts JWT refresh-promise dedupe test (QA-02: asserts single /auth/refresh call under N concurrent 401s)
  - upload-form.tsx: empty-selection guard and error-restore path covered with German copy assertions
  - classify-dialog.tsx: confirm and quick-confirm flows covered (mutation call + German toast assertions)
affects: [07-05, 07-06, ci-workflow]

tech-stack:
  added:
    - vitest@3.2.6
    - "@vitejs/plugin-react@6.0.2"
    - "@testing-library/react@16.3.2"
    - "@testing-library/dom@10.4.1"
    - "@testing-library/jest-dom@6.9.1"
    - "@testing-library/user-event@14.6.1"
    - jsdom@29.1.1
    - vite-tsconfig-paths@6.1.1
  patterns:
    - "vi.doMock + vi.resetModules() + dynamic import() for testing modules with module-level state (refreshPromise)"
    - "Mock shadcn/ui primitives (Dialog, Select) as simple HTML elements to isolate component logic from Radix/base-ui portals"
    - "Mock hooks (useUploadFiles, useConfirmClassification) at module level, return controllable mutateAsync"
    - "QueryClientProvider wrapper helper for components that use TanStack Query"

key-files:
  created:
    - Frontend/vitest.config.mts
    - Frontend/vitest.setup.ts
    - Frontend/src/lib/format.test.ts
    - Frontend/src/lib/api-client.test.ts
    - Frontend/src/components/upload/upload-form.test.tsx
    - Frontend/src/components/receipts/classify-dialog.test.tsx
  modified:
    - Frontend/package.json (scripts: test, test:run; devDependencies: 7 new packages)
    - Frontend/package-lock.json

key-decisions:
  - "vi.doMock (not vi.mock) inside test body + vi.resetModules() in beforeEach ensures module-level refreshPromise resets between api-client tests"
  - "shadcn/ui Dialog and Select mocked as plain HTML elements (div + native select) — avoids @base-ui/react portal and focus-trap complexity in jsdom while still testing the component's business logic"
  - "FileDropzone mocked as plain <input type=file> — drag-and-drop behavior is unrelated to the upload-form state machine being tested"
  - "QA-02 scope per plan deviation note: login/register unit tests N/A (use plain useState, no RHF/Zod); covered by 07-05 Playwright E2E"

patterns-established:
  - "Pattern 1: Module-level state test isolation — vi.doMock + vi.resetModules() + dynamic import() per test for modules like api-client.ts that hold module-level mutable state"
  - "Pattern 2: Component isolation with shadcn/ui — mock UI primitives as semantic HTML equivalents; test business logic, not UI library internals"
  - "Pattern 3: Hook mocking — vi.mock the hook module, vi.mocked(hook).mockReturnValue({ mutateAsync, isPending }) in beforeEach for controlled async behavior"

requirements-completed: [QA-02]

duration: 35min
completed: 2026-06-07
---

# Phase 07 Plan 04: Vitest Frontend Tests Summary

**Vitest 3 + Testing Library installed for the first time on the frontend, with 19 tests covering de-DE currency/label formatting, the JWT refresh-promise dedupe invariant (QA-02), upload-form error-restore path, and classify-dialog confirm/quick-confirm flows with German copy assertions.**

## Performance

- **Duration:** ~35 min
- **Started:** 2026-06-07T11:25:00Z
- **Completed:** 2026-06-07T11:35:00Z
- **Tasks:** 2
- **Files modified:** 8

## Accomplishments

- Stood up Vitest 3 with jsdom, `@vitejs/plugin-react`, `vite-tsconfig-paths` (@/* alias), and `@testing-library/jest-dom` — zero frontend tests existed before this plan
- Locked the JWT refresh dedupe invariant: api-client.test.ts proves /auth/refresh is called exactly once under 3 concurrent 401s (T-07-12 mitigated)
- Covered the upload-form state machine: disabled guard, error-restore (fallback German message + server error field), and the German success path
- Covered the classify-dialog: both `handleConfirm` ("Klassifizierung bestätigt") and `handleQuickConfirm` ("Vorschlag bestätigt") paths with exact mutation payload assertions

## Task Commits

1. **Task 1: Vitest install + config + format.ts and api-client.ts unit tests** - `2e8fd05` (feat)
2. **Task 2: upload-form + classify-dialog component tests** - `f360091` (feat)

## Files Created/Modified

- `Frontend/vitest.config.mts` - Vitest 3 config: jsdom, tsconfigPaths, e2e excluded
- `Frontend/vitest.setup.ts` - @testing-library/jest-dom import
- `Frontend/package.json` - test/test:run scripts + 7 devDependencies
- `Frontend/package-lock.json` - lockfile updated
- `Frontend/src/lib/format.test.ts` - 12 tests covering formatCurrency (de-DE EUR), categoryLabel (13 categories including "Nicht zugeordnet"), statusLabel
- `Frontend/src/lib/api-client.test.ts` - 1 test: QA-02 refreshPromise dedupe (vi.doMock + vi.resetModules + dynamic import, adapter-level 401 injection)
- `Frontend/src/components/upload/upload-form.test.tsx` - 4 tests: empty-selection guard (button disabled, mutateAsync not called), error-restore (fallback + server error field German messages)
- `Frontend/src/components/receipts/classify-dialog.test.tsx` - 2 tests: confirm path (mutateAsync {itemId,category} + "Klassifizierung bestätigt"), quick-confirm path ("Vorschlag bestätigt")

## Decisions Made

- **vi.doMock vs vi.mock for api-client:** vi.mock is hoisted and runs once; vi.doMock inside the test body + vi.resetModules() in beforeEach properly resets the module-level `refreshPromise = null` state between tests. This is the canonical approach for testing modules with mutable module-level state.
- **shadcn/ui mock strategy:** @base-ui/react Select and Dialog use portals and focus traps that are broken/complex in jsdom. Mocking them as native HTML equivalents (div, select) isolates the component logic from UI library internals without losing coverage of the business paths.
- **QA-02 scope (per plan deviation note):** login/register unit tests explicitly out of scope — those pages use plain useState (no RHF/Zod), and the register→login flow is covered by 07-05 Playwright E2E. Confirmed per user decision 2026-06-06.

## Deviations from Plan

None — plan executed exactly as written. The QA-02 scope deviation was documented in the plan itself (pre-approved by user).

## Issues Encountered

- **api-client.test.ts first attempt (Rule 1):** First draft used top-level `vi.mock('axios')` with hoisted state sharing; the `_instances` array was undefined after `vi.resetModules()`. Fixed by switching to `vi.doMock` inside each test body so the mock factory re-runs after module reset.
- **upload-form.test.tsx:** Initial `act()` import was missing; added to `@testing-library/react` import.

## User Setup Required

None — no external service configuration required.

## Next Phase Readiness

- Frontend test infrastructure is live; `npx vitest run` exits 0 across all 4 test files (19 tests)
- 07-05 (Playwright E2E) can now add an `e2e/` directory — the vitest.config.mts already excludes `**/e2e/**` to prevent Playwright specs from being picked up by Vitest
- CI workflow (07-06) can add `npm run test:run` as a frontend test step

## Known Stubs

None — all tests target real implementations; no placeholder assertions.

## Self-Check: PASSED

- FOUND: Frontend/vitest.config.mts
- FOUND: Frontend/vitest.setup.ts
- FOUND: Frontend/src/lib/format.test.ts
- FOUND: Frontend/src/lib/api-client.test.ts
- FOUND: Frontend/src/components/upload/upload-form.test.tsx
- FOUND: Frontend/src/components/receipts/classify-dialog.test.tsx
- FOUND: .planning/phases/07-test-depth-launch-qa/07-04-SUMMARY.md
- FOUND commit: 2e8fd05 (Task 1)
- FOUND commit: f360091 (Task 2)

---

*Phase: 07-test-depth-launch-qa*
*Completed: 2026-06-07*
