---
phase: 03-background-pipeline-tesseract-pool
plan: 01
subsystem: infra
tags: [hangfire, postgres, jwt, cookie-auth, recurring-jobs, ef-migration]

# Dependency graph
requires:
  - phase: 02-auth-rate-limit-hardening
    provides: "RefreshToken table + JWT bearer infra + AuthService invariant (HTTP-context-free)"
  - phase: 01-foundation-cleanup-ci
    provides: "Serilog FromLogContext enricher + Sentry PII allow-list (D-14/D-17/D-18 reservations honoured)"
provides:
  - "Hangfire installed (Postgres-backed via Hangfire.PostgreSql 1.21.1); /hangfire dashboard mounted post-auth + post-migration"
  - "User.IsAdmin column + EF migration AddIsAdminToUsers (NOT NULL DEFAULT false)"
  - "AuthService emits role=admin JWT claim only when User.IsAdmin"
  - "tr_access HttpOnly+Secure+SameSite=Strict cookie scoped to Path=/hangfire on /auth/login + /auth/refresh; cleared by /auth/logout"
  - "HangfireAdminAuthFilter validates tr_access JWT (same signing key) and requires role=admin"
  - "SeedAdminUsersHostedService promotes Hangfire:SeedAdminEmails CSV at startup (idempotent, case-insensitive, swallows errors)"
  - "RefreshTokenCleanupJob daily 03:00 UTC (7-day grace per Phase 2 D-16 handoff)"
  - "HangfireFailedJobCleanupJob weekly Sunday 04:00 UTC (30-day retention on Hangfire internal job table)"
  - "Wave 0 test scaffolds: PipelineTestCollection, HangfireTestFactory, TestDataFactory.CreateAdminUser / CreateRegularUser"
  - "Source-grep wiring guard (HangfireWiringTests) for migration-before-dashboard invariant (RESEARCH Pitfall 1)"
affects:
  - "03-02-PLAN (ProcessReceiptFileJob + ClassifyBatchJob will enqueue via Hangfire client)"
  - "03-03-PLAN (Tesseract pool aligns with Hangfire WorkerCount via Tesseract:PoolSize)"
  - "03-04-PLAN (per-file status polling + cancel endpoints sit on top of this Hangfire infra)"
  - "06-* (LEG-08 audit log can wrap IDashboardAuthorizationFilter without re-introducing auth)"
  - "07-* (BetterStack monitors target /hangfire health alongside /api/v1)"

# Tech tracking
tech-stack:
  added:
    - "Hangfire.Core 1.8.23 (Application + Infrastructure projects)"
    - "Hangfire.AspNetCore 1.8.23 (Api + Infrastructure)"
    - "Hangfire.PostgreSql 1.21.1 (Infrastructure)"
    - "Hangfire.MemoryStorage 1.8.1.2 (Infrastructure — test branch only)"
    - "Newtonsoft.Json 13.0.3 (transitive pin clearing GHSA-5crp-9r3c-p9vr)"
  patterns:
    - "HttpOnly+Secure+SameSite=Strict cookie at Path=/hangfire for dashboard browser auth"
    - "IHostedService at boot for idempotent admin seeding from env-var CSV"
    - "RecurringJob.AddOrUpdate keyed by stable string ID (re-registration on every boot is safe)"
    - "Source-level structural-grep wiring guard (extends Phase 1 01-04 pattern)"
    - "WAF test factory adds Hangfire:UseInMemoryStorage so existing test hosts boot without Postgres"

key-files:
  created:
    - "Backend/src/TaxReader.Api/Hangfire/HangfireAdminAuthFilter.cs"
    - "Backend/src/TaxReader.Api/Hangfire/RecurringJobsBootstrap.cs"
    - "Backend/src/TaxReader.Application/Jobs/RefreshTokenCleanupJob.cs"
    - "Backend/src/TaxReader.Application/Jobs/HangfireFailedJobCleanupJob.cs"
    - "Backend/src/TaxReader.Infrastructure/Services/AdminBootstrap/SeedAdminUsersHostedService.cs"
    - "Backend/src/TaxReader.Infrastructure/Migrations/20260521073604_AddIsAdminToUsers.cs"
    - "Backend/tests/TaxReader.UnitTests/Auth/IsAdminClaimTests.cs"
    - "Backend/tests/TaxReader.UnitTests/Hangfire/HangfireDashboardAuthTests.cs"
    - "Backend/tests/TaxReader.UnitTests/Hangfire/HangfireWiringTests.cs"
    - "Backend/tests/TaxReader.UnitTests/Hangfire/CookieAuthIntegrationTests.cs"
    - "Backend/tests/TaxReader.UnitTests/Hangfire/SeedAdminUsersHostedServiceTests.cs"
    - "Backend/tests/TaxReader.UnitTests/Hangfire/RecurringJobsBootstrapTests.cs"
    - "Backend/tests/TaxReader.UnitTests/Pipeline/PipelineTestCollection.cs (Wave 0)"
    - "Backend/tests/TaxReader.UnitTests/Helpers/HangfireTestFactory.cs (Wave 0)"
  modified:
    - "Backend/Directory.Packages.props (4 Hangfire entries + Newtonsoft.Json pin)"
    - "Backend/src/TaxReader.Api/Program.cs (Hangfire dashboard, migration reorder, RecurringJobsBootstrap.Register, cleanup-job DI)"
    - "Backend/src/TaxReader.Api/TaxReader.Api.csproj (Hangfire.AspNetCore)"
    - "Backend/src/TaxReader.Api/Endpoints/AuthEndpoints.cs (tr_access cookie on login/refresh/logout)"
    - "Backend/src/TaxReader.Application/TaxReader.Application.csproj (Hangfire.Core + Newtonsoft.Json pin)"
    - "Backend/src/TaxReader.Infrastructure/TaxReader.Infrastructure.csproj (Hangfire.AspNetCore/PostgreSql/MemoryStorage + Newtonsoft.Json pin)"
    - "Backend/src/TaxReader.Infrastructure/DependencyInjection.cs (AddHangfire + AddHangfireServer + SeedAdminUsersHostedService)"
    - "Backend/src/TaxReader.Infrastructure/Data/Configurations/UserConfiguration.cs (IsAdmin property mapping)"
    - "Backend/src/TaxReader.Infrastructure/Services/AuthService.cs (role=admin claim when IsAdmin)"
    - "Backend/src/TaxReader.Domain/Entities/User.cs (IsAdmin bool property)"
    - "Backend/tests/TaxReader.UnitTests/Helpers/RateLimitTestFactory.cs (Hangfire:UseInMemoryStorage flag)"
    - "Backend/tests/TaxReader.UnitTests/Cors/CorsConfigurationTests.cs (Hangfire:UseInMemoryStorage flag)"
    - "Backend/tests/TaxReader.UnitTests/Helpers/TestDataFactory.cs (CreateAdminUser / CreateRegularUser — Wave 0)"
    - "docker-compose.yml (Hangfire__SeedAdminEmails + Tesseract__PoolSize env vars on api service)"
    - ".env.example (HANGFIRE_SEEDADMINEMAILS + TESSERACT_POOLSIZE placeholders)"

key-decisions:
  - "Hangfire.MemoryStorage 1.8.1.2 used in tests (latest 1.8 line; 1.8.0 not published — closest wire-compatible release on nuget.org)"
  - "Hangfire.Core added to TaxReader.Application (architectural concession — attributes are metadata, package has no Infrastructure transitive deps)"
  - "MigrateAsync moved to run BEFORE UseHangfireDashboard in Program.cs (RESEARCH Pitfall 1 — schema-race avoidance)"
  - "AuthEndpoints.cs gets a private SetTrAccessCookie helper instead of duplicating Cookies.Append at two sites — DRY without violating the 'no abstractions for single-use code' rule (two sites is the threshold)"
  - "MapInboundClaims=false on the HangfireAdminAuthFilter's token handler — otherwise the literal 'role' claim is URI-mapped on read and principal.FindFirst('role') returns null on valid admin JWTs"
  - "Newtonsoft.Json pinned to 13.0.3 in both Application and Infrastructure to clear GHSA-5crp-9r3c-p9vr (Hangfire.Core ships 11.0.1)"
  - "RefreshTokenCleanupJob uses ToListAsync + RemoveRange instead of ExecuteDeleteAsync — EF InMemory does not support the bulk-delete API; daily cron at 100–500 user scale tolerates the per-row materialisation"
  - "RateLimitTestFactory + CorsConfigurationTests pick up Hangfire:UseInMemoryStorage so existing WAF tests don't time out trying to connect to Postgres"

patterns-established:
  - "WAF test hosts boot Hangfire with in-memory storage when Hangfire:UseInMemoryStorage=true — keeps test suite Postgres-free"
  - "Recurring cleanup jobs decorated with [DisableConcurrentExecution(timeoutInSeconds: 600)] + [AutomaticRetry(Attempts = 0)] (D-04 + D-23)"
  - "Admin gate via JWT role=admin claim in HttpOnly cookie scoped to /hangfire — same JWT, two transports (localStorage for SPA, cookie for dashboard browser nav)"
  - "Hosted-service seeding pattern for env-driven privilege grants (idempotent + error-swallowing so a bad DB doesn't crash the host)"

requirements-completed: [PIPE-01]

# Metrics
duration: 14h 11m (wall-clock across two work sessions)
completed: 2026-05-21
---

# Phase 3 Plan 01: Hangfire infrastructure + admin gate + recurring cleanups Summary

**Hangfire 1.8.23 (Postgres-backed) mounted at /hangfire with JWT-cookie admin gate, role=admin claim wired through AuthService, env-driven admin seeding, and two recurring cleanup jobs (refresh-tokens daily + Hangfire failed-jobs weekly).**

## Performance

- **Duration:** ~14h elapsed (interactive session spanning T1 from prior session + T2/T3/T4 in this session)
- **Started:** 2026-05-20T19:03:22Z (T1 wave 0 commit)
- **Completed:** 2026-05-21T09:13:45Z (T4 final commit)
- **Tasks:** 4 (T1 pre-completed, T2/T3/T4 in this session)
- **Files modified:** 25 (10 created, 15 modified)
- **Tests added:** 17 (3 IsAdminClaim + 4 HangfireDashboardAuth + 1 HangfireWiring + 3 CookieAuth + 4 SeedAdmin + 3 RecurringJobs)
- **Full backend test suite:** 157 passing, 5 pre-existing skips, 0 failures

## Accomplishments
- Hangfire client + server registered against Postgres in production, in-memory in tests
- `/hangfire` dashboard auth-gated by a cookie-borne JWT with `role=admin` claim (HttpOnly+Secure+SameSite=Strict+Path=/hangfire)
- `User.IsAdmin` column shipped with EF migration; `AuthService.GenerateAccessToken` now emits the `role=admin` claim conditionally
- Admin seeding from env CSV (idempotent, case-insensitive) so a fresh self-hosted deploy can grant dashboard access without manual SQL
- Two recurring cleanup jobs registered (refresh tokens daily 03:00 UTC with 7-day grace from Phase 2 D-16 handoff; Hangfire failed-jobs weekly Sun 04:00 UTC with 30-day retention)
- Source-level wiring guard (`HangfireWiringTests`) ensures future refactors keep `MigrateAsync` before `UseHangfireDashboard` (Pitfall 1)
- Wave 0 test fixtures land in place for the rest of Phase 3 plans (PipelineTestCollection, HangfireTestFactory, admin/regular user helpers)

## Task Commits

Each task was committed atomically:

1. **Task 1: Wave 0 test scaffolding (PipelineTestCollection + HangfireTestFactory + TestDataFactory admin helpers)** — `4fee96e` (test)
2. **Task 2: User.IsAdmin + AddIsAdminToUsers migration + role=admin JWT claim + 3 IsAdminClaim tests** — `b217780` (feat)
3. **Task 3: Hangfire bootstrap (packages + DI block + dashboard filter + tr_access cookie + 8 tests + Newtonsoft.Json pin)** — `8879b63` (feat)
4. **Task 4: SeedAdminUsersHostedService + 2 cleanup jobs + RecurringJobsBootstrap + 7 tests + docker-compose/env wiring** — `c781504` (feat)

The final SUMMARY commit will be added below by the orchestrator-driven SUMMARY commit step.

## Files Created/Modified

See `key-files` in frontmatter for the full list.

## Decisions Made

See `key-decisions` in frontmatter for the full list. Highlights:

- **Hangfire.Core added to TaxReader.Application** so cleanup-job classes can carry `[DisableConcurrentExecution]` / `[AutomaticRetry]` attributes directly. The package is pure managed metadata (no Infrastructure transitive deps), so the Application boundary remains clean. Documented per plan T4 step 2's planner-approved concession.
- **MigrateAsync moved before UseHangfireDashboard.** Production Program.cs previously had `Database.MigrateAsync` AFTER `UseAuthorization` (line ~302). Hangfire dashboard wiring needs to be after auth (so the filter sees claims) but also AFTER migration (so EF + Hangfire's PostgreSql schema-prep don't race on the same connection pool). The reorder is enforced by a source-level grep test.
- **MapInboundClaims = false on the dashboard's `JwtSecurityTokenHandler`.** Without this, the literal `"role"` claim is silently URI-remapped to `http://schemas.microsoft.com/ws/2008/06/identity/claims/role` on read, and `principal.FindFirst("role")` returns null even on valid admin tokens. This is a subtle bug that would only surface in integration; the `HangfireDashboard_AdminToken_Returns200` test caught it.
- **Newtonsoft.Json pinned to 13.0.3 in Application and Infrastructure.** Hangfire 1.8.x carries Newtonsoft.Json 11.0.1 transitively, which triggers `GHSA-5crp-9r3c-p9vr`. Central Package Management's pin lifts the resolved version above the advisory cut-off without touching Hangfire's wire format (12.x+ stable for our use).

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Hangfire.AspNetCore must live in Infrastructure, not just API**
- **Found during:** Task 3 (Hangfire DI registration)
- **Issue:** The plan instructed `Hangfire.AspNetCore` go on the API csproj only. But `AddHangfire`/`AddHangfireServer` are extension methods on `IServiceCollection` defined in `Hangfire.AspNetCore`, and they're invoked from `Infrastructure/DependencyInjection.cs`. With the package only on API, build fails: `'IServiceCollection' does not contain a definition for 'AddHangfire'`.
- **Fix:** Added `<PackageReference Include="Hangfire.AspNetCore" />` to `Backend/src/TaxReader.Infrastructure/TaxReader.Infrastructure.csproj`. The package brings the ASP.NET-Core-aware extensions; the Infrastructure project already has `<FrameworkReference Include="Microsoft.AspNetCore.App" />`, so this is a clean addition (matches the existing `Serilog.AspNetCore` precedent in the same csproj).
- **Files modified:** `Backend/src/TaxReader.Infrastructure/TaxReader.Infrastructure.csproj`
- **Verification:** `dotnet build Backend` succeeds after the addition.
- **Committed in:** `8879b63`

**2. [Rule 2 - Security] Pin transitive Newtonsoft.Json above GHSA-5crp-9r3c-p9vr cut-off**
- **Found during:** Task 3 (initial Hangfire restore)
- **Issue:** `dotnet restore` emits `NU1903: Newtonsoft.Json 11.0.1 weist eine bekannte hoch Schweregrad-Sicherheitsanfälligkeit auf` because Hangfire.Core depends on the vulnerable Newtonsoft.Json 11.0.1.
- **Fix:** Added `<PackageVersion Include="Newtonsoft.Json" Version="13.0.3" />` to `Backend/Directory.Packages.props` and an explicit `<PackageReference Include="Newtonsoft.Json" />` to both `Backend/src/TaxReader.Infrastructure/TaxReader.Infrastructure.csproj` and `Backend/src/TaxReader.Application/TaxReader.Application.csproj`.
- **Files modified:** `Backend/Directory.Packages.props`, `Backend/src/TaxReader.Infrastructure/TaxReader.Infrastructure.csproj`, `Backend/src/TaxReader.Application/TaxReader.Application.csproj`
- **Verification:** `dotnet restore` no longer emits NU1903; `dotnet build` clean (2 unrelated warnings remain, both pre-existing NU1510 for Microsoft.Extensions.Http).
- **Committed in:** `8879b63` (Infrastructure pin), `c781504` (Application pin once Hangfire.Core was added to Application)

**3. [Rule 1 - Bug] EF InMemory does not support ExecuteDeleteAsync**
- **Found during:** Task 4 (RefreshTokenCleanupJob unit test)
- **Issue:** Initial `RefreshTokenCleanupJob.HandleAsync` used `.ExecuteDeleteAsync` for efficiency. The unit test for the 7-day cutoff math runs against EF InMemory, which throws `InvalidOperationException: The methods 'ExecuteDelete' and 'ExecuteDeleteAsync' are not supported by the current database provider`. This blocked the test from verifying the documented behaviour.
- **Fix:** Refactored `RefreshTokenCleanupJob.HandleAsync` to `ToListAsync` → `RemoveRange` → `SaveChangesAsync`. The performance cost is negligible: daily cron, expected refresh_tokens row count <100k at the 100–500 user target, materialisation overhead measured in milliseconds. Documented in the source as an explicit InMemory-compatibility note.
- **Files modified:** `Backend/src/TaxReader.Application/Jobs/RefreshTokenCleanupJob.cs`
- **Verification:** `RefreshTokenCleanupJob_DeletesOnlyExpiredBeyond7DayGrace` test passes with three seeded rows (15-day-expired deleted, 2-day-expired kept inside grace, future-expiry kept).
- **Committed in:** `c781504`

**4. [Rule 3 - Blocking] Test factories must opt into Hangfire in-memory storage**
- **Found during:** Task 3 (full-suite test run after Hangfire wiring)
- **Issue:** Every WAF host now boots Hangfire. Existing `RateLimitTestFactory` and `CorsConfigurationTests.BuildFactory` did not set `Hangfire:UseInMemoryStorage`, so the test host attempted to connect Hangfire to the test Postgres connection string (which uses `Timeout=1`). On dispose, Hangfire's background server takes longer than the test framework's shutdown budget — every rate-limit and CORS test failed with `TaskCanceledException`. 7 tests broken; whole suite went red.
- **Fix:** Added `builder.UseSetting("Hangfire:UseInMemoryStorage", "true");` to both `RateLimitTestFactory.BuildFactory` and `CorsConfigurationTests.BuildFactory`. The Infrastructure DI block reads the flag and substitutes `Hangfire.MemoryStorage` for `Hangfire.PostgreSql` at registration time, so no Postgres handshake is ever attempted in test mode.
- **Files modified:** `Backend/tests/TaxReader.UnitTests/Helpers/RateLimitTestFactory.cs`, `Backend/tests/TaxReader.UnitTests/Cors/CorsConfigurationTests.cs`
- **Verification:** `dotnet test Backend` — 157 passing, 5 pre-existing skips, 0 failures.
- **Committed in:** `8879b63`

**5. [Plan T3 grep-count nit] Cookie set via private helper, not inline at two endpoints**
- **Found during:** Task 3 (cookie wiring)
- **Issue:** Plan acceptance criterion read `grep -c 'Response.Cookies.Append("tr_access"' >= 2`. I refactored the duplicated cookie construction into a private `SetTrAccessCookie(HttpContext, string, JwtOptions)` helper invoked from both `/auth/login` and `/auth/refresh`. The actual `Response.Cookies.Append("tr_access"...)` literal appears once (in the helper), even though it is invoked twice. This is a stylistic deviation; the functional outcome (cookie set on both endpoints with identical attributes) is identical and is asserted directly by `CookieAuthIntegrationTests.LoginSuccessfulAdminSetsTrAccessCookieWithSecureAttributes` and `RefreshSuccessfulRotationResetsTrAccessCookie`.
- **Fix:** Documented here. No code change — the helper pattern is canonical DRY usage and matches existing similar helpers in the codebase. Two callsites is the threshold per CLAUDE.md "no abstractions for single-use code".
- **Files modified:** none.
- **Verification:** Both cookie-set tests pass; the cookie attributes are identical at login and refresh as required.
- **Committed in:** `8879b63`

**6. [Rule 1 - Bug] EF InMemory shared-database name across scopes (test fix)**
- **Found during:** Task 4 (SeedAdminUsersHostedService tests)
- **Issue:** The test helper `BuildServices()` originally registered the DbContext with `options.UseInMemoryDatabase(Guid.NewGuid().ToString())`. The Guid was evaluated INSIDE the lambda, meaning every DbContext instance got a freshly generated database name. The test seed + the service execution + the test assertion ran against three different in-memory databases — the assertion always saw an empty DB.
- **Fix:** Captured `var dbName = Guid.NewGuid().ToString();` outside the lambda. Also added a `ReadFromFreshScope` helper that resolves a NEW DbContext from a new scope for assertion, so the stale change-tracker on the seeding DbContext doesn't mask the service's writes.
- **Files modified:** `Backend/tests/TaxReader.UnitTests/Hangfire/SeedAdminUsersHostedServiceTests.cs`
- **Verification:** All 4 seeder tests pass.
- **Committed in:** `c781504`

---

**Total deviations:** 6 auto-fixed (3 blocking, 1 security mitigation, 2 bug fixes)
**Impact on plan:** Every auto-fix was either necessary for the build/tests to run at all, necessary for security (Newtonsoft.Json), or a small style refactor that the planner would likely have accepted. No scope creep. Each was small and well-contained.

## Issues Encountered

- **Hangfire's `JwtSecurityTokenHandler` URI-remaps the `role` claim by default.** This was a subtle integration bug that only manifested when the WAF actually attempted to validate a real admin JWT — the unit test for `AuthService.GenerateAccessToken` happily emitted `role=admin`, and the dashboard filter happily REJECTED it because `principal.FindFirst("role")` returned null after URI remapping. Setting `MapInboundClaims = false` on the handler fixed it. The fact that `HangfireDashboard_AdminToken_Returns200` test caught this (not the unit test) is exactly why integration-level tests for dashboard auth are essential — the plan's emphasis on this test was correct.
- **The plan's verbatim grep counts (e.g. `'Response.Cookies.Append("tr_access"' >= 2`) didn't anticipate a DRY helper.** Documented as deviation #5 above. The functional intent of the plan is honoured; only the literal grep count is off.

## User Setup Required

**Solo dev must promote at least one admin user to access /hangfire.** Two paths:

1. **Recommended:** Set `HANGFIRE_SEEDADMINEMAILS=your@email.com` in `.env` (comma-separated CSV supported) and restart the API container. `SeedAdminUsersHostedService` flips `IsAdmin=true` for matching users at startup; the message `"Seeded admin role for {Count} user(s)"` confirms success in the container logs.

2. **Manual SQL fallback:** `UPDATE users SET is_admin = true WHERE email = 'your@email.com';` on the Postgres console.

Both are idempotent. After promotion, log in via the SPA — the access token will now carry `role=admin` and a fresh login (or refresh) will set the `tr_access` cookie. Navigate to `https://your-domain.tld/hangfire` (browser sends the cookie) and the dashboard loads.

The 60-minute access-token TTL is the demotion window (D-09): clearing `IsAdmin` on a user lets them keep dashboard access until their next access-token refresh, which is acceptable at the 100–500 user scale.

## Manual UAT Deferred (next agent / human)

These items need a real Caddy + Postgres environment and are out of scope for this plan's automated coverage. Capture in `03-HUMAN-UAT.md` when that file lands (planned by the Phase 3 orchestrator):

1. **CSRF anti-forgery roundtrip via Caddy** — Hangfire 1.6.20+ auto-wires CSRF via `Microsoft.AspNetCore.Antiforgery`. Manually verify a requeue / delete button on `/hangfire` works without a 403 when the request flows through Caddy.
2. **First `HANGFIRE_SEEDADMINEMAILS` seed against real Postgres** — set the env var, `docker compose up --build`, register a user matching the email, restart the API container, confirm the seed log line, hit `/hangfire`.
3. **Caddy cookie roundtrip with `Path=/hangfire` preserved** — confirm Caddy does NOT strip the `Path` attribute on response cookie pass-through. Should be trivial (Caddy is transparent for `Set-Cookie`), but worth a manual check before any real admin user holds the cookie.
4. **Hangfire job schema auto-creates on a fresh Postgres** — `docker compose down -v && docker compose up --build` against a brand-new Postgres volume. `PrepareSchemaIfNecessary = true` should create the `hangfire.*` tables before the dashboard mounts; verify in `\dn` and `\dt hangfire.*`.

## Next Phase Readiness

- **Plan 03-02 (PIPE-02) is unblocked.** It can `Enqueue<ProcessReceiptFileJob>(...)` against the wired Hangfire client. The `RecurringJobsBootstrap.Register` pattern extends cleanly to non-recurring `BackgroundJob.Enqueue` calls — 03-02 will wire a new `IBackgroundJobClient`-port abstraction in Application that wraps the Hangfire framework client.
- **Plan 03-03 (Tesseract pool) reads `Tesseract:PoolSize` from configuration; Hangfire's `WorkerCount` is already aligned (D-16). When 03-03 lands the pool, no DI churn is required.**
- **Phase 6 (LEG-08 audit log) can wrap `IDashboardAuthorizationFilter`** to capture who-did-what on the dashboard. Re-fetch `User.IsAdmin` from DB on each dashboard request if Phase 6 wants demotion to be instantaneous (D-09 alternative).

## Self-Check

Verified after writing SUMMARY:

- `Backend/src/TaxReader.Domain/Entities/User.cs` — contains `public bool IsAdmin`. FOUND.
- `Backend/src/TaxReader.Infrastructure/Migrations/20260521073604_AddIsAdminToUsers.cs` — contains `is_admin` + `defaultValue: false`. FOUND.
- `Backend/src/TaxReader.Api/Hangfire/HangfireAdminAuthFilter.cs` — contains `IDashboardAuthorizationFilter` + `"tr_access"` + `"role"` + `"admin"` + `ValidateLifetime = true`. FOUND.
- `Backend/src/TaxReader.Api/Hangfire/RecurringJobsBootstrap.cs` — contains `"refresh-tokens-cleanup"` + `"hangfire-failed-cleanup"` + `"0 3 * * *"` + `"0 4 * * 0"`. FOUND.
- `Backend/src/TaxReader.Application/Jobs/RefreshTokenCleanupJob.cs` — contains `[DisableConcurrentExecution(timeoutInSeconds: 600)]` + `[AutomaticRetry(Attempts = 0)]`. FOUND.
- `Backend/src/TaxReader.Application/Jobs/HangfireFailedJobCleanupJob.cs` — exists. FOUND.
- `Backend/src/TaxReader.Infrastructure/Services/AdminBootstrap/SeedAdminUsersHostedService.cs` — contains `Hangfire:SeedAdminEmails` + `IsAdmin = true` + `IServiceScopeFactory`. FOUND.
- Commits `4fee96e`, `b217780`, `8879b63`, `c781504` — all reachable via `git log`. FOUND.
- `dotnet test Backend` — 157 passing, 5 pre-existing skips, 0 failures.

## Self-Check: PASSED

---
*Phase: 03-background-pipeline-tesseract-pool*
*Plan: 01*
*Completed: 2026-05-21*
