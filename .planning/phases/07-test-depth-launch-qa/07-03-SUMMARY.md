---
phase: 07-test-depth-launch-qa
plan: 03
subsystem: api
tags: [health, monitoring, betterstack, obs, efcore, waf-tests]

requires:
  - phase: 02-rate-limiting-auth-hardening
    provides: RateLimiterTestCollection + RateLimitTestFactory WAF patterns used by health tests

provides:
  - GET /health — anonymous liveness probe (DB ping, 200/503 JSON)
  - GET /api/v1/health — anonymous readiness probe (DB ping + Anthropic-configured check, 200/503 JSON)
  - DatabaseFacade Database on IAppDbContext (enables CanConnectAsync from Application-facing interface)
  - WAF tests proving anonymity (T-07-11) and no-secret-leak (T-07-09) for both endpoints

affects: [07-07-betterstack-wiring, monitoring, ops]

tech-stack:
  added: []
  patterns:
    - "Health endpoints registered on app (WebApplication) directly — not inside the /api/v1 RequireAuthorization group — so .AllowAnonymous() is sufficient and unambiguous"
    - "IAppDbContext.Database (DatabaseFacade) added to interface so endpoint handlers in API layer can call CanConnectAsync without taking a concrete AppDbContext dependency"

key-files:
  created:
    - Backend/src/TaxReader.Api/Endpoints/HealthEndpoints.cs
    - Backend/tests/TaxReader.UnitTests/Health/HealthEndpointTests.cs
  modified:
    - Backend/src/TaxReader.Api/Program.cs
    - Backend/src/TaxReader.Application/Interfaces/IAppDbContext.cs

key-decisions:
  - "IAppDbContext.Database (DatabaseFacade) added to the Application interface — Application already depends on Microsoft.EntityFrameworkCore (for DbSet<T>), so adding DatabaseFacade maintains the existing architectural concession and avoids injecting AppDbContext concrete in endpoint handlers"
  - "Anthropic not-configured is reported as 'unconfigured' in the body but does NOT make /api/v1/health return 503 — classification degrades but the service is still alive; status = db-health-only"
  - "Both endpoints registered via app.MapHealthEndpoints() on WebApplication (not inside api group) — consistent with MapStripeWebhookEndpoint pattern, both endpoints are clearly anonymous"

patterns-established:
  - "Health endpoint anonymity regression test pattern: [Fact] asserts StatusCode != 401 AND == 200 with no Authorization header"
  - "Secret-leak assertion pattern: NotContainEquivalentOf (case-insensitive FluentAssertions) on response body for 'connectionstring', 'host=', 'password', 'sk_live', 'whsec', 'secret'"

requirements-completed: [OBS-03]

duration: 18min
completed: 2026-06-06
---

# Phase 7 Plan 03: Health Endpoints (OBS-03) Summary

**Anonymous /health and /api/v1/health probes with DB ping + Anthropic-configured reporting, WAF-tested for anonymity (T-07-11) and no-secret-leak (T-07-09), ready for BetterStack keyword monitoring**

## Performance

- **Duration:** ~18 min
- **Started:** 2026-06-06T00:00:00Z
- **Completed:** 2026-06-06T00:18:00Z
- **Tasks:** 2
- **Files modified:** 4

## Accomplishments

- Created two anonymous health endpoints: `/health` (DB liveness) and `/api/v1/health` (DB + Anthropic-configured readiness)
- Both return minimal JSON (`status`/`db`/`anthropic`) with 200 on healthy, 503 on unhealthy DB; no secrets, connection strings, or stack traces ever reach the body
- Four WAF tests prove anonymity (no 401) and negative-assert six secret patterns (T-07-09 / T-07-11 mitigations)

## Task Commits

1. **Task 1: HealthEndpoints.cs + Program.cs wiring** - `b3510dc` (feat)
2. **Task 2: HealthEndpointTests WAF** - `94117d4` (test)

## Files Created/Modified

- `Backend/src/TaxReader.Api/Endpoints/HealthEndpoints.cs` — Two anonymous GET endpoints; DB ping via `CanConnectAsync`; Anthropic config via `IAiClassifier.IsConfigured`; no exception echo
- `Backend/src/TaxReader.Api/Program.cs` — Added `app.MapHealthEndpoints()` after MapStripeWebhookEndpoint (OBS-03 comment)
- `Backend/src/TaxReader.Application/Interfaces/IAppDbContext.cs` — Added `DatabaseFacade Database { get; }` (EF Core already a dependency of Application)
- `Backend/tests/TaxReader.UnitTests/Health/HealthEndpointTests.cs` — 4 WAF tests in RateLimiterTestCollection; uses in-memory DB (CanConnectAsync → true → "healthy")

## Decisions Made

- `DatabaseFacade Database` added to `IAppDbContext`: Application already references `Microsoft.EntityFrameworkCore` (for `DbSet<T>`), so this is not a new dependency. Avoids injecting the concrete `AppDbContext` in the API endpoint handler, which would break the layering intent.
- Anthropic not-configured reports `"unconfigured"` in body but does NOT cause 503: the service is alive even if AI classification is down; the operator needs to see the misconfiguration but BetterStack should not page for it.
- Both endpoints registered outside the `RequireAuthorization` group — same pattern as `MapStripeWebhookEndpoint` — making the anonymous intent unambiguous rather than relying on per-endpoint `.AllowAnonymous()` inside the auth group.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] IAppDbContext.Database missing from interface**
- **Found during:** Task 1 (build verification)
- **Issue:** Plan specified `IAppDbContext.Database.CanConnectAsync(ct)` but `IAppDbContext` did not expose `DatabaseFacade Database`. Build error CS1061.
- **Fix:** Added `DatabaseFacade Database { get; }` and `using Microsoft.EntityFrameworkCore.Infrastructure;` to `IAppDbContext.cs`. `AppDbContext` inherits `Database` from `DbContext`, so no change to the concrete class was needed.
- **Files modified:** `Backend/src/TaxReader.Application/Interfaces/IAppDbContext.cs`
- **Verification:** `dotnet build` exits 0 after the addition.
- **Committed in:** `b3510dc` (Task 1 commit)

---

**Total deviations:** 1 auto-fixed (Rule 1 — build-blocking bug)
**Impact on plan:** Required to make the plan's `CanConnectAsync` approach compile. No scope creep; the `DatabaseFacade` type is already within Application's existing EF Core dependency.

## Issues Encountered

None beyond the IAppDbContext.Database deviation above.

## User Setup Required

None — no external service configuration required. BetterStack wiring is handled in plan 07-07.

## Next Phase Readiness

- `/health` and `/api/v1/health` are live and anonymous; BetterStack can probe them immediately after 07-07 wiring
- WAF tests are in the standard `RateLimiterTestCollection` — CI will catch any future regression to RequireAuthorization
- Readiness probe reports Anthropic misconfiguration — useful for ops debugging without exposing the key

---
*Phase: 07-test-depth-launch-qa*
*Completed: 2026-06-06*
