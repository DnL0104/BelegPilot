---
phase: 07-test-depth-launch-qa
reviewed: 2026-06-07T00:00:00Z
depth: standard
files_reviewed: 20
files_reviewed_list:
  - Backend/src/TaxReader.Api/Endpoints/HealthEndpoints.cs
  - Backend/src/TaxReader.Api/Program.cs
  - Backend/src/TaxReader.Application/Interfaces/IAppDbContext.cs
  - Backend/tests/TaxReader.UnitTests/Health/HealthEndpointTests.cs
  - Backend/tests/TaxReader.UnitTests/Services/AuthServiceTests.cs
  - Backend/tests/TaxReader.UnitTests/Services/TokenServiceTests.cs
  - Backend/tests/TaxReader.UnitTests/Services/AiOnlyClassificationServiceTests.cs
  - Backend/tests/TaxReader.IntegrationTests/Fixtures/PostgresContainerFixture.cs
  - Backend/tests/TaxReader.IntegrationTests/Fixtures/IntegrationTestCollection.cs
  - Backend/tests/TaxReader.IntegrationTests/IntegrationTestWebAppFactory.cs
  - Backend/tests/TaxReader.IntegrationTests/MigrationSmokeTests.cs
  - Backend/tests/TaxReader.IntegrationTests/CascadeDeleteTests.cs
  - Backend/tests/TaxReader.IntegrationTests/DuplicateDetectionTests.cs
  - Backend/tests/TaxReader.IntegrationTests/RefreshTokenRotationReplayTests.cs
  - Backend/tests/TaxReader.IntegrationTests/PaymentIdempotencyTests.cs
  - Frontend/vitest.config.mts
  - Frontend/vitest.setup.ts
  - Frontend/src/lib/format.test.ts
  - Frontend/src/lib/api-client.test.ts
  - Frontend/src/components/receipts/classify-dialog.test.tsx
  - Frontend/src/components/upload/upload-form.test.tsx
  - Frontend/playwright.config.ts
  - Frontend/e2e/happy-path.spec.ts
  - .github/workflows/ci.yml
findings:
  critical: 0
  warning: 4
  info: 2
  total: 6
status: issues_found
---

# Phase 07: Code Review Report

**Reviewed:** 2026-06-07
**Depth:** standard
**Files Reviewed:** 24
**Status:** issues_found

## Summary

Phase 07 adds a Testcontainers-based integration test suite, service-layer unit tests, frontend Vitest + component tests, a Playwright E2E happy-path spec, a CI workflow, and the only production code in the phase: anonymous health endpoints (`/health`, `/api/v1/health`).

The production health endpoint code is secure: no secrets or connection strings leak, both endpoints call `.AllowAnonymous()`, and DB-down returns 503. `IAppDbContext.Database` exposure is limited to the health-check use case.

The test code is generally well-structured with real-constraint coverage (cascade FK, UNIQUE index, token replay). However, four quality/reliability issues were found that risk false-confidence or test flakiness in CI.

---

## Critical Issues

None found.

---

## Warnings

### WR-01: `beforeEach` mock-setup is wiped by `vi.clearAllMocks()` called immediately after — mocks are silently undefined during first render

**File:** `Frontend/src/components/upload/upload-form.test.tsx:47-53`

**Issue:** The `beforeEach` block in `UploadForm` tests calls `vi.mocked(useUploadFiles).mockReturnValue(...)` to configure the mock, then immediately calls `vi.clearAllMocks()`. `clearAllMocks` resets all mock implementations and return values, including the `mockReturnValue` just set. The mock is therefore undefined when the test's component renders, meaning `useUploadFiles()` returns `undefined` instead of `{ mutateAsync, isPending: false }`. The `empty-selection guard` tests pass by accident (they never call `mutateAsync`), but any test that relies on the mock's return value after `beforeEach` runs is actually calling an uninitialized mock.

The same pattern exists in `classify-dialog.test.tsx:88-101` where the author recognized the problem and worked around it by calling `mockReturnValue` a second time after `clearAllMocks`. The upload form tests have no such re-setup.

**Fix:** Move `vi.clearAllMocks()` to the top of `beforeEach` (before any setup), or use `vi.resetAllMocks()` + an `afterEach` teardown, so the mock configuration is not clobbered:
```typescript
beforeEach(() => {
  vi.clearAllMocks()                          // clear first
  mutateAsyncMock = vi.fn()
  vi.mocked(useUploadFiles).mockReturnValue({ // then configure
    mutateAsync: mutateAsyncMock,
    isPending: false,
  } as ReturnType<typeof useUploadFiles>)
})
```

---

### WR-02: `classify-dialog.test.tsx` `beforeEach` double-initializes mock unnecessarily, masking the `clearAllMocks` order bug

**File:** `Frontend/src/components/receipts/classify-dialog.test.tsx:88-101`

**Issue:** The `beforeEach` sets up `useConfirmClassification` mock, calls `vi.clearAllMocks()` (which wipes it), then re-sets it. This works but is fragile: it relies on `mutateAsyncMock` still being set in the closure after `clearAllMocks` (closures capture the variable, not the mock state). If any future developer reorders the lines or adds another mock in between, the pattern silently breaks. The real fix is to clear before setting up, not after.

Additionally, `mutateAsyncMock` is created with `.mockResolvedValue({})` at line 89, then `vi.clearAllMocks()` wipes that resolved value, then line 97 re-sets `useConfirmClassification` but does NOT re-attach `.mockResolvedValue({})` to the re-created mock — the `mutateAsyncMock` variable still points to the original `vi.fn()` from line 89, whose resolved value was cleared. In the `confirm` path test, `mutateAsyncMock` is called and `await vi.waitFor(...)` asserts on it — if `clearAllMocks` has cleared the resolved value, the mock now returns `undefined` by default (which is `Promise<undefined>`, still awaitable), so the test happens to pass, but the assertion on `mutateAsyncMock` call count is correct only because the reference is the same object.

**Fix:** Consolidate to a single clean setup pattern:
```typescript
beforeEach(() => {
  vi.clearAllMocks()
  mutateAsyncMock = vi.fn().mockResolvedValue({})
  onOpenChangeMock = vi.fn()
  vi.mocked(useConfirmClassification).mockReturnValue({
    mutateAsync: mutateAsyncMock,
    isPending: false,
  } as ReturnType<typeof useConfirmClassification>)
})
```

---

### WR-03: CI integration-test job sets non-standard env var names (`JWT_SECRET`, `REFRESHTOKEN_HASHKEY`) that ASP.NET Core never reads

**File:** `.github/workflows/ci.yml:145-146`

**Issue:** The `Run Postgres integration suite (QA-01)` step sets:
```yaml
JWT_SECRET: test-secret-test-secret-test-secret-1234
REFRESHTOKEN_HASHKEY: AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=
```

ASP.NET Core maps environment variables to configuration using `__` as the section separator (e.g. `Jwt__Secret`). `JWT_SECRET` and `REFRESHTOKEN_HASHKEY` do not match any configuration key, so these env vars are silently ignored at runtime. The values they contain are never loaded into `JwtOptions.Secret` or `RefreshTokenOptions.HashKey`.

The integration tests do not fail because `IntegrationTestWebAppFactory.ConfigureWebHost` calls `builder.UseSetting("Jwt:Secret", ...)` and `builder.UseSetting("RefreshToken:HashKey", ...)` directly, which overrides environment variable configuration. The stale env vars are effectively dead configuration — the tests boot correctly without them.

The risk is that a future change that removes or refactors `IntegrationTestWebAppFactory` (e.g. adding a non-WAF integration test class that expects these env vars to configure the host) would fail silently in CI with a cryptographic key error that looks like an unrelated boot failure.

**Fix:** Either remove the dead env vars entirely (they are redundant given `IntegrationTestWebAppFactory`), or correct the names to match ASP.NET Core convention so they are meaningful if the factory changes:
```yaml
Jwt__Secret: test-secret-test-secret-test-secret-1234
RefreshToken__HashKey: AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=
```

---

### WR-04: E2E test uses `Promise.all` with a `download` event that is conditionally `null`, then asserts `download !== null || exportFeedback` — the assertion does not fail if the export silently errors

**File:** `Frontend/e2e/happy-path.spec.ts:161-175`

**Issue:** The CSV export step is:
```typescript
const [download] = await Promise.all([
  page.waitForEvent('download', { timeout: 20_000 }).catch(() => null),
  csvButton.click(),
])
// ...
expect(download !== null || exportFeedback).toBe(true)
```

The `waitForEvent('download')` is swallowed with `.catch(() => null)` so a missing download silently sets `download = null`. The fallback asserts `exportFeedback`, which looks for either `"Export heruntergeladen"` or `"Export fehlgeschlagen"`. This means a visible `"Export fehlgeschlagen"` toast satisfies the assertion — the test passes even when the export endpoint returns an error. This gives no confidence that the happy-path export actually works in CI.

**Fix:** Remove the `.catch(() => null)` and instead make the export assertion meaningful. If the classification in step 6 is confirmed, there should be data for export and the download should always occur. If the export endpoint can legitimately be empty (no data yet), assert on the success toast only — not on the failure toast:
```typescript
// Expect either a download (data present) OR the success toast, but NOT the failure toast
const exportToast = page.getByText('CSV-Export heruntergeladen')
const failureToast = page.getByText('Export fehlgeschlagen')
await expect(failureToast).not.toBeVisible({ timeout: 5_000 }).catch(() => {})
// At least one of: download event or success toast
```

---

## Info

### IN-01: `HealthBody_DoesNotContainSecretsOrConnectionString` test does not check for `"anthropic"` API key format or bearer token patterns

**File:** `Backend/tests/TaxReader.UnitTests/Health/HealthEndpointTests.cs:62-86`

**Issue:** The secret-leak test checks for `"connectionstring"`, `"host="`, `"password"`, `"sk_live"`, `"whsec"`, and `"secret"` — but does not check for the Anthropic API key prefix (`"sk-ant-"`) or JWT Bearer token fragments (`"eyJ"` — Base64-encoded JWT header). These are not a present risk (the health endpoint only emits `{ status, db, anthropic }` and neither value includes those strings), but the test would not catch a future regression that accidentally includes them.

**Fix:** Add assertions for the two additional patterns if belt-and-suspenders coverage is desired:
```csharp
body.Should().NotContainEquivalentOf("sk-ant-",
    "T-07-09: Anthropic API key prefix must not appear");
body.Should().NotContainEquivalentOf("eyJ",
    "T-07-09: JWT fragments (Base64 header) must not appear");
```

---

### IN-02: `PostgresContainerFixture` creates a new `NpgsqlConnection` on every `ResetAsync` call rather than reusing a pooled connection

**File:** `Backend/tests/TaxReader.IntegrationTests/Fixtures/PostgresContainerFixture.cs:45-50`

**Issue:** Each `ResetAsync()` call opens a fresh `NpgsqlConnection` (no connection pool reuse because the fixture manages its own lifecycle outside EF's pool), then disposes it. With five integration test classes each calling `ResetAsync` in `InitializeAsync`, this opens five sequential cold connections to the containerized Postgres. This is not a correctness issue but adds latency to a sequential test suite.

**Fix:** Cache the `NpgsqlConnection` used for Respawn and reuse it across `ResetAsync` calls, closing it in `DisposeAsync`:
```csharp
private NpgsqlConnection _respawnConnection = default!;

public async Task InitializeAsync()
{
    await Container.StartAsync();
    // ... migrations ...
    _respawnConnection = new NpgsqlConnection(ConnectionString);
    await _respawnConnection.OpenAsync();
    _respawner = await Respawner.CreateAsync(_respawnConnection, ...);
}

public async Task ResetAsync() => await _respawner.ResetAsync(_respawnConnection);

public async Task DisposeAsync()
{
    await _respawnConnection.DisposeAsync();
    await Container.DisposeAsync();
}
```

---

_Reviewed: 2026-06-07_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_
