---
phase: 07-test-depth-launch-qa
fixed_at: 2026-06-09T00:00:00Z
review_path: .planning/phases/07-test-depth-launch-qa/07-REVIEW.md
iteration: 2
findings_in_scope: 6
fixed: 6
skipped: 0
status: all_fixed
---

# Phase 07: Code Review Fix Report

**Fixed at:** 2026-06-09T00:00:00Z
**Source review:** .planning/phases/07-test-depth-launch-qa/07-REVIEW.md
**Iteration:** 2

**Summary:**
- Findings in scope: 6 (WR-01, WR-02, WR-03, WR-04, IN-01, IN-02 — fix_scope = all)
- Fixed: 6
- Skipped: 0

> Iteration 2 note: WR-01..WR-04 were fixed and committed in iteration 1 (fix_scope = critical_warning). This `--all` re-run applied the two previously out-of-scope INFO findings (IN-01, IN-02). The WR sections below are preserved as already-fixed; IN sections record the new commits.

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

### IN-01: Health secret-leak test lacks Anthropic key prefix and JWT fragment assertions

**File modified:** `Backend/tests/TaxReader.UnitTests/Health/HealthEndpointTests.cs`
**Commit:** `5275367`
**Applied fix:** Added two negative assertions to the per-body loop in `HealthBody_DoesNotContainSecretsOrConnectionString`: `body.Should().NotContainEquivalentOf("sk-ant-", ...)` for the Anthropic API key prefix and `body.Should().NotContainEquivalentOf("eyJ", ...)` for Base64-encoded JWT header fragments. Both carry the existing `T-07-09` reasoning string and match the established FluentAssertions style of the surrounding checks. This is belt-and-suspenders coverage that will catch a future regression that accidentally serializes a secret into the health response.

---

### IN-02: `PostgresContainerFixture` opens a fresh `NpgsqlConnection` on every `ResetAsync`

**File modified:** `Backend/tests/TaxReader.IntegrationTests/Fixtures/PostgresContainerFixture.cs`
**Commit:** `8b04e89`
**Applied fix:** Introduced a cached `private NpgsqlConnection _respawnConnection` field. The connection is opened once in `InitializeAsync` (immediately before `Respawner.CreateAsync`, replacing the previous `await using var conn`) and reused directly by `ResetAsync`, which is now a one-liner `=> await _respawner.ResetAsync(_respawnConnection)`. `DisposeAsync` became `async`, disposing `_respawnConnection` before the container. This removes the per-test-class cold-connection cost across the five integration test classes while preserving the single-snapshot Respawn semantics.

---

## Skipped Issues

None — all in-scope findings were fixed.

---

## Verification Results

### Vitest (WR-01 / WR-02)

Run after both frontend test fixes (iteration 1):

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

### .NET build + xUnit (IN-01)

- `dotnet build Backend/tests/TaxReader.UnitTests/TaxReader.UnitTests.csproj` → 0 errors.
- `dotnet test --filter "FullyQualifiedName~HealthEndpointTests"` → 4 passed, 0 failed. The updated `HealthBody_DoesNotContainSecretsOrConnectionString` test passes with the two new `sk-ant-` / `eyJ` assertions.

### .NET build (IN-02)

- `dotnet build Backend/tests/TaxReader.IntegrationTests/TaxReader.IntegrationTests.csproj` → 0 errors.
- Integration tests themselves require Docker/Testcontainers and were not executed in this environment; the build verifies the refactored fixture compiles and the `IAsyncLifetime` contract (now `async DisposeAsync`) is satisfied.

---

_Fixed: 2026-06-09T00:00:00Z_
_Fixer: Claude (gsd-code-fixer)_
_Iteration: 2_
