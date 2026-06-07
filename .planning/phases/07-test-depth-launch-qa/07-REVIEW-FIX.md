---
phase: 07-test-depth-launch-qa
fixed_at: 2026-06-07T13:42:00Z
review_path: .planning/phases/07-test-depth-launch-qa/07-REVIEW.md
iteration: 1
findings_in_scope: 4
fixed: 4
skipped: 0
status: all_fixed
---

# Phase 07: Code Review Fix Report

**Fixed at:** 2026-06-07T13:42:00Z
**Source review:** .planning/phases/07-test-depth-launch-qa/07-REVIEW.md
**Iteration:** 1

**Summary:**
- Findings in scope: 4 (WR-01, WR-02, WR-03, WR-04 — fix_scope = critical_warning; IN-01/IN-02 skipped per scope)
- Fixed: 4
- Skipped: 0

---

## Fixed Issues

### WR-01: `beforeEach` mock-setup wiped by `vi.clearAllMocks()` called immediately after

**File modified:** `Frontend/src/components/upload/upload-form.test.tsx`
**Commit:** `a1035b7`
**Applied fix:** Moved `vi.clearAllMocks()` to the top of `beforeEach` (line 47), before `mutateAsyncMock = vi.fn()` and the `mockReturnValue(...)` setup. The mock is now configured after the clear, so it is always valid when each test renders `<UploadForm />`. The `error-restore` tests that rely on the mock's behavior now correctly exercise an initialized mock rather than an `undefined` one.

---

### WR-02: `classify-dialog.test.tsx` `beforeEach` double-initializes mock, masking the `clearAllMocks` order bug

**File modified:** `Frontend/src/components/receipts/classify-dialog.test.tsx`
**Commit:** `8218642`
**Applied fix:** Consolidated `beforeEach` to the canonical clear-then-setup order: `vi.clearAllMocks()` first, then fresh `mutateAsyncMock = vi.fn().mockResolvedValue({})`, then `onOpenChangeMock = vi.fn()`, then a single `useConfirmClassification` mock setup. Removed the redundant second `mockReturnValue` call (lines 97–100) and the explanatory comment that papered over the ordering issue. The `mutateAsyncMock` `.mockResolvedValue({})` is now set on a fresh `vi.fn()` that `clearAllMocks` never touches.

---

### WR-03: CI integration-test job sets non-standard env var names that ASP.NET Core never reads

**File modified:** `.github/workflows/ci.yml`
**Commit:** `9081da9`
**Applied fix:** Renamed the two env vars in the `Run Postgres integration suite (QA-01)` step from `JWT_SECRET` / `REFRESHTOKEN_HASHKEY` to `Jwt__Secret` / `RefreshToken__HashKey`, matching ASP.NET Core's `__`-delimited section:key binding convention. Confirmed against `JwtOptions.SectionName = "Jwt"` + `property Secret` and `RefreshTokenOptions.SectionName = "RefreshToken"` + `property HashKey`. The E2E step already used the correct names; this aligns the integration-test step to the same standard. YAML validated via `js-yaml` load — no parse errors.

---

### WR-04: E2E CSV export assertion accepts failure toast as passing

**File modified:** `Frontend/e2e/happy-path.spec.ts`
**Commit:** `f8100fb`
**Applied fix:** Removed `.catch(() => null)` from `page.waitForEvent('download', ...)` so a missing download now throws and fails the test. Removed the `exportFeedback` fallback variable and the `expect(download !== null || exportFeedback)` assertion that accepted `"Export fehlgeschlagen"` as a passing outcome. Added an explicit `await expect(failureToast).not.toBeVisible({ timeout: 5_000 })` guard, and a direct `expect(download).not.toBeNull()` to make intent clear. A classification was confirmed in step 6, so data is always present — a download event is the only valid success signal. `npx playwright test --list` confirms the spec still parses (3 viewport projects, 1 test each).

---

## Skipped Issues

None — all in-scope findings were fixed.

---

## Verification Results

### Vitest (WR-01 / WR-02)

Run after both frontend test fixes:

```
Test Files  4 passed (4)
      Tests  19 passed (19)
   Duration  2.70s
```

All 19 tests green. The `upload-form` and `classify-dialog` test suites pass with the corrected mock ordering, confirming mocks are properly initialized before each test.

### YAML lint (WR-03)

Validated via `js-yaml.load()` on the modified `.github/workflows/ci.yml` — no parse errors.

### Playwright list (WR-04)

```
[desktop] › happy-path.spec.ts:30:5 › happy path: ...
[md]      › happy-path.spec.ts:30:5 › happy path: ...
[sm]      › happy-path.spec.ts:30:5 › happy path: ...
Total: 3 tests in 1 file
```

Spec parses cleanly across all three viewport projects.

---

_Fixed: 2026-06-07T13:42:00Z_
_Fixer: Claude (gsd-code-fixer)_
_Iteration: 1_
