---
phase: 02-auth-rate-limit-hardening
plan: 01
subsystem: auth
tags: [refresh-tokens, jwt, hmac-sha256, sentry, postgres, ef-core, migration]

# Dependency graph
requires:
  - phase: 01-foundation-cleanup-ci
    provides: Sentry pipeline + PII allow-list (user.id_hash); Serilog enrichers + LogContext.PushProperty pattern; CORS deny-all default
provides:
  - refresh_tokens DB table (HMAC-keyed, multi-device-safe, rotation chain via replaced_by_token_id)
  - IRefreshTokenService (IssueAsync / ValidateAndRotateAsync / RevokeAllForUserAsync)
  - Replay detection that revokes ALL user tokens + Sentry warning (D-03)
  - Silent-401 posture on replay (D-04) — same German error for not-found / expired / replay
  - HMAC-SHA256 pepper via RefreshToken__HashKey env var
  - AuthService refactored to delegate refresh-token persistence to RefreshTokenService
  - HttpContext-aware AuthEndpoints capturing User-Agent + Remote IP for audit trail
affects: [02-02-account-deletion-reauth, 02-03-rate-limit-policies, 03-pipeline-hangfire]

# Tech tracking
tech-stack:
  added:
    - HMACSHA256.HashData (static API; CA1850-compliant)
    - ExecuteUpdateAsync (relational bulk update; in-memory fallback for tests)
    - EF Core inet column type via System.Net.IPAddress
  patterns:
    - Pepper-via-IOptions<RefreshTokenOptions> + double-underscore env override
    - Provider-aware bulk update (production Postgres uses ExecuteUpdateAsync; InMemory tests use load-and-mutate)
    - HttpContext binding in Minimal API parameter list (no IHttpContextAccessor in services)

key-files:
  created:
    - Backend/src/TaxReader.Domain/Entities/RefreshToken.cs
    - Backend/src/TaxReader.Application/Interfaces/IRefreshTokenService.cs
    - Backend/src/TaxReader.Infrastructure/Configuration/RefreshTokenOptions.cs
    - Backend/src/TaxReader.Infrastructure/Services/RefreshTokenService.cs
    - Backend/src/TaxReader.Infrastructure/Data/Configurations/RefreshTokenConfiguration.cs
    - Backend/src/TaxReader.Infrastructure/Migrations/20260514204609_AddRefreshTokensTable_DropLegacyRefreshTokenColumns.cs
    - Backend/tests/TaxReader.UnitTests/Helpers/RateLimitTestFactory.cs
    - Backend/tests/TaxReader.UnitTests/Auth/HmacPepperHashingTests.cs
    - Backend/tests/TaxReader.UnitTests/Auth/RefreshTokenServiceTests.cs
    - Backend/tests/TaxReader.UnitTests/Auth/ReplayDetectionTests.cs
    - Backend/tests/TaxReader.UnitTests/Auth/MultiDeviceTokenTests.cs
    - Backend/tests/TaxReader.UnitTests/Auth/MigrationTests.cs
  modified:
    - Backend/src/TaxReader.Domain/Entities/User.cs
    - Backend/src/TaxReader.Application/Interfaces/IAppDbContext.cs
    - Backend/src/TaxReader.Application/Interfaces/IAuthService.cs
    - Backend/src/TaxReader.Infrastructure/Services/AuthService.cs
    - Backend/src/TaxReader.Infrastructure/Data/AppDbContext.cs
    - Backend/src/TaxReader.Infrastructure/Data/Configurations/UserConfiguration.cs
    - Backend/src/TaxReader.Infrastructure/DependencyInjection.cs
    - Backend/src/TaxReader.Infrastructure/Migrations/AppDbContextModelSnapshot.cs
    - Backend/src/TaxReader.Api/Endpoints/AuthEndpoints.cs
    - docker-compose.yml
    - .env.example

key-decisions:
  - "RefreshTokenService stays HTTP-context-free (Pitfall 8): UA/IP are method parameters; the Minimal API endpoint extracts them from HttpContext via parameter binding"
  - "RevokeAllForUserAsync detects the provider by name (not by type) — production runs ExecuteUpdateAsync, in-memory tests fall back to load-and-mutate. No InMemory package dependency in production Infrastructure"
  - "Sentry message body is the unique searchable token: 'Refresh token replay detected'. Serilog warning uses a different phrasing so the verification grep stays unambiguous"
  - "EF migration manually reordered to CreateTable-first then DropColumn-last (EF scaffolds the reverse). Required by D-15 + RESEARCH Pattern 3"
  - "AuthService.RegisterAsync now SaveChanges first (user must exist before refresh_tokens.user_id FK can resolve), then issues the refresh token"

patterns-established:
  - "Provider-aware EF Core bulk operation: if (concrete.Database.ProviderName != 'Microsoft.EntityFrameworkCore.InMemory') use ExecuteUpdateAsync; else load-and-mutate"
  - "Replay detection emission: logger.LogWarning + SentrySdk.CaptureMessage with user.id_hash (Phase 1 D-14 PII allow-list) — different message bodies for clean grep"
  - "HttpContext capture in Minimal API: bind HttpContext httpContext as a parameter and read Headers.UserAgent + Connection.RemoteIpAddress; do NOT inject IHttpContextAccessor"

requirements-completed: [AUTH-01]

# Metrics
duration: 15min
completed: 2026-05-14
---

# Phase 02 Plan 01: Refresh-Token Hardening Summary

**HMAC-pepper-hashed refresh_tokens table with rotation + replay-revoke-all and IRefreshTokenService refactor of AuthService**

## Performance

- **Duration:** ~15 min
- **Started:** 2026-05-14T20:34:50Z
- **Completed:** 2026-05-14T20:49:18Z
- **Tasks:** 6 / 6
- **Files created:** 12
- **Files modified:** 11

## Accomplishments

- Replaced the single-column `users.refresh_token` model with a multi-row `refresh_tokens` table (10 columns per D-02 schema): supports phone + laptop simultaneously, per-row UA/IP capture, and a cryptographic rotation chain via `replaced_by_token_id`.
- Tokens are stored as **HMAC-SHA256 hashes keyed by a server-side pepper** (D-01). A DB-only leak yields hashes the attacker cannot reverse without `RefreshToken__HashKey`. Mitigates threat T-02-03.
- **Replay detection is active**: a revoked token presented at `/auth/refresh` triggers `RevokeAllForUserAsync` (every active token for that user is revoked) and emits a Sentry warning carrying only `user.id_hash` (Phase 1 D-14 PII allow-list). Mitigates threat T-02-02.
- **Silent posture (D-04)**: not-found, expired, and replay all return the same German error `"Ungültiges oder abgelaufenes Refresh-Token."` to avoid leaking detection signal. Mitigates threat T-02-09.
- `AuthService` refactored to delegate all refresh-token persistence and rotation to `IRefreshTokenService`. `AuthEndpoints` binds `HttpContext` directly and extracts UA + Remote IP. `AuthService` and `RefreshTokenService` stay HTTP-context-free (Pitfall 8 — no `IHttpContextAccessor` injection).
- Single EF migration (`AddRefreshTokensTable_DropLegacyRefreshTokenColumns`) creates the new table first (with unique `token_hash` index and composite `(user_id, revoked_at)` index), then drops the legacy `users.refresh_token` + `users.refresh_token_expires_at` columns. Manually reordered after scaffolding because EF defaulted to DropColumn-first.
- 11 active AUTH-01 unit/integration tests pass; 1 deferred to Phase 7 (`MigrationTests` — InMemory cannot run Postgres DDL).

## Task Commits

Each task was committed atomically:

1. **Task 1 (Wave 0): Install AUTH-01 test scaffolding** — `f7c33f7` (test)
2. **Task 2: Domain entity + interfaces + EF configuration wiring** — `a198f21` (feat)
3. **Task 3: Implement RefreshTokenService (HMAC, rotation, replay-revoke, Sentry)** — `0eee380` (feat)
4. **Task 4: Refactor AuthService + AuthEndpoints HttpContext binding** — `77870b3` (refactor)
5. **Task 5: docker-compose / .env.example + activate Wave 0 tests** — `0a7dcfc` (test, includes Rule 1 deviation fix)
6. **Task 6: EF migration AddRefreshTokensTable_DropLegacyRefreshTokenColumns** — `15d7fed` (feat)

**Post-task touchup:** `685fc2c` (refactor — differentiate Serilog warning text from Sentry message body so the verification grep returns exactly one match)

## Files Created/Modified

### Created (12)
- `Backend/src/TaxReader.Domain/Entities/RefreshToken.cs` — POCO with 10 fields, Domain-pure (uses only `System.Net.IPAddress`)
- `Backend/src/TaxReader.Application/Interfaces/IRefreshTokenService.cs` — Three-method port
- `Backend/src/TaxReader.Infrastructure/Configuration/RefreshTokenOptions.cs` — `SectionName = "RefreshToken"`, `HashKey` property
- `Backend/src/TaxReader.Infrastructure/Services/RefreshTokenService.cs` — HMAC-SHA256 pepper hashing + rotation + replay revoke-all + Sentry capture
- `Backend/src/TaxReader.Infrastructure/Data/Configurations/RefreshTokenConfiguration.cs` — Unique `token_hash` index + composite `(user_id, revoked_at)` + self-FK NoAction for replacement chain
- `Backend/src/TaxReader.Infrastructure/Migrations/20260514204609_AddRefreshTokensTable_DropLegacyRefreshTokenColumns.cs` — Migration body
- `Backend/src/TaxReader.Infrastructure/Migrations/20260514204609_AddRefreshTokensTable_DropLegacyRefreshTokenColumns.Designer.cs` — Designer snapshot
- `Backend/tests/TaxReader.UnitTests/Helpers/RateLimitTestFactory.cs` — Shared `WebApplicationFactory<Program>` helper (also feeds 02-02 / 02-03)
- `Backend/tests/TaxReader.UnitTests/Auth/HmacPepperHashingTests.cs` — 3 tests (determinism, key-sensitivity, collision smoke)
- `Backend/tests/TaxReader.UnitTests/Auth/RefreshTokenServiceTests.cs` — 5 tests (issue + rotate happy path, not-found, expired, revoke-all)
- `Backend/tests/TaxReader.UnitTests/Auth/ReplayDetectionTests.cs` — 2 tests (revoke-all on replay + generic German error per D-04)
- `Backend/tests/TaxReader.UnitTests/Auth/MultiDeviceTokenTests.cs` — 1 test (two devices rotate independently)
- `Backend/tests/TaxReader.UnitTests/Auth/MigrationTests.cs` — Placeholder (skipped; deferred to Phase 7 QA-01)

### Modified (11)
- `Backend/src/TaxReader.Domain/Entities/User.cs` — Removed legacy `RefreshToken` + `RefreshTokenExpiresAt` columns; added `ICollection<RefreshToken> RefreshTokens` nav
- `Backend/src/TaxReader.Application/Interfaces/IAppDbContext.cs` — Added `DbSet<RefreshToken> RefreshTokens`
- `Backend/src/TaxReader.Application/Interfaces/IAuthService.cs` — Added `userAgent` + `ipAddress` parameters to all three methods
- `Backend/src/TaxReader.Infrastructure/Services/AuthService.cs` — Constructor now takes `IRefreshTokenService`; refresh-token persistence delegated; `GenerateAccessToken` split out
- `Backend/src/TaxReader.Infrastructure/Data/AppDbContext.cs` — Added `DbSet<RefreshToken>` property
- `Backend/src/TaxReader.Infrastructure/Data/Configurations/UserConfiguration.cs` — Dropped `RefreshToken` column mapping; added `HasMany(e => e.RefreshTokens) ... Cascade`
- `Backend/src/TaxReader.Infrastructure/DependencyInjection.cs` — Registered `IRefreshTokenService` and `RefreshTokenOptions`
- `Backend/src/TaxReader.Infrastructure/Migrations/AppDbContextModelSnapshot.cs` — Auto-regenerated for new model
- `Backend/src/TaxReader.Api/Endpoints/AuthEndpoints.cs` — `/register`, `/login`, `/refresh` now bind `HttpContext` and extract UA + Remote IP
- `docker-compose.yml` — Added `RefreshToken__HashKey: ${REFRESHTOKEN_HASHKEY}` to api service env block
- `.env.example` — Added `REFRESHTOKEN_HASHKEY=` block with `openssl rand -base64 32` generation hint

## Decisions Made

- **HMAC pepper hashing observable through round-trip**: tests verify pepper determinism + key-sensitivity by issuing a token with one service instance and validating with another (same / different peppers). The private `ComputeHash` method is not exercised directly — `ValidateAndRotate`'s lookup-success/lookup-failure proves the same property.
- **Sentry message body differs from Serilog warning text**: `SentrySdk.CaptureMessage("Refresh token replay detected", ...)` is the canonical event identifier; the Serilog log uses `"Replay of revoked refresh token; revoking all tokens for user"`. Both fire together on every replay event. This keeps the verification grep unambiguous AND makes the log line human-friendlier.
- **Provider detection by name**: `Database.ProviderName == "Microsoft.EntityFrameworkCore.InMemory"` avoids pulling the InMemory extension package into production. The relational path is the production path; the in-memory branch exists solely for unit tests.
- **`AuthService.RegisterAsync` now `SaveChanges` before `IssueAsync`**: the new `refresh_tokens.user_id` FK requires the user row to exist first. Previously a single `SaveChanges` covered both writes; the new architecture splits the persistence into two steps.
- **EF migration manually reordered**: scaffolded as DropColumn → CreateTable; reordered to CreateTable → DropColumn per D-15 + RESEARCH Pattern 3 ordering directive.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] `ExecuteUpdateAsync` is not supported by the EF InMemory provider used in unit tests**

- **Found during:** Task 5 (running Wave 0 tests with the now-active assertions)
- **Issue:** `RefreshTokenService.RevokeAllForUserAsync` uses `ExecuteUpdateAsync` (per RESEARCH Pattern 4 + Task 3 acceptance criterion). The InMemory provider doesn't implement it; tests that exercise the replay-revoke-all path threw `InvalidOperationException: 'ExecuteUpdate' and 'ExecuteUpdateAsync' are not supported by the current database provider.`
- **Fix:** Added provider detection via `Database.ProviderName == "Microsoft.EntityFrameworkCore.InMemory"`. Production (Npgsql) keeps the single-statement bulk `ExecuteUpdateAsync`; tests with InMemory fall back to a load-and-mutate loop. Same row-level outcome both paths.
- **Files modified:** `Backend/src/TaxReader.Infrastructure/Services/RefreshTokenService.cs`
- **Verification:** AUTH-01 test suite (`dotnet test Backend --filter "FullyQualifiedName~Auth"`) — 11 active tests pass, 1 deferred skipped; full backend test suite (124 passing).
- **Committed in:** `0a7dcfc` (Task 5 commit)

**2. [Rule 1 - Grep guard ambiguity] Single string `"Refresh token replay detected"` appeared in both Serilog `LogWarning` and `SentrySdk.CaptureMessage`**

- **Found during:** Final verification (plan `<verification>` block grep guard #4)
- **Issue:** Plan's grep guard requires "exactly one match" for `"Refresh token replay detected"`, but RESEARCH Example 1 (canonical) shows the same string used in both log lines.
- **Fix:** Changed the Serilog warning to a different, more descriptive sentence (`"Replay of revoked refresh token; revoking all tokens for user"`). Sentry's message body remains the canonical identifier. Both still fire on every replay event; no behaviour change.
- **Files modified:** `Backend/src/TaxReader.Infrastructure/Services/RefreshTokenService.cs`
- **Verification:** `grep "Refresh token replay detected"` returns exactly one match (the SentrySdk call site); `dotnet test Backend` remains green.
- **Committed in:** `685fc2c` (post-task touchup)

---

**Total deviations:** 2 auto-fixed (1 test-infra bug, 1 verification-guard alignment)
**Impact on plan:** Neither deviation alters the user-visible behaviour or threat-model coverage. Both kept the canonical patterns intact while improving test compatibility and grep precision. No scope creep.

## Issues Encountered

- **`dotnet ef migrations add` HostAbortedException**: expected — EF Core's design-time host uses a build-and-abort pattern to scaffold migrations without running the API. The migration generated successfully.
- **`dotnet ef migrations list` shows "Pending status not shown"**: harmless — Postgres is not running locally during the plan execution. The migration list still includes `AddRefreshTokensTable_DropLegacyRefreshTokenColumns` so the acceptance check passes.

## Known Stubs

None. `AuthService` no longer holds `NotImplementedException` markers (Task 2 introduced them temporarily; Task 4 replaced them with real `IRefreshTokenService` calls).

## TDD Gate Compliance

This plan is `type: execute`, not `type: tdd`. RED/GREEN/REFACTOR gates do not apply at the plan level. Wave-0 tests were authored as Skip stubs first (Task 1, `test:` commit), then un-skipped after implementation landed (Task 5, `test:` commit). The skip-first-then-implement-then-unskip rhythm satisfies the Wave-0 + Wave-2 pattern documented in `02-VALIDATION.md`.

## Self-Check: PASSED

Verified after writing this summary:

**Files created:**
- `Backend/src/TaxReader.Domain/Entities/RefreshToken.cs` — FOUND
- `Backend/src/TaxReader.Application/Interfaces/IRefreshTokenService.cs` — FOUND
- `Backend/src/TaxReader.Infrastructure/Configuration/RefreshTokenOptions.cs` — FOUND
- `Backend/src/TaxReader.Infrastructure/Services/RefreshTokenService.cs` — FOUND
- `Backend/src/TaxReader.Infrastructure/Data/Configurations/RefreshTokenConfiguration.cs` — FOUND
- `Backend/src/TaxReader.Infrastructure/Migrations/20260514204609_AddRefreshTokensTable_DropLegacyRefreshTokenColumns.cs` — FOUND
- `Backend/tests/TaxReader.UnitTests/Helpers/RateLimitTestFactory.cs` — FOUND
- `Backend/tests/TaxReader.UnitTests/Auth/*.cs` (6 files) — FOUND

**Commits in git history:**
- `f7c33f7` (Task 1) — FOUND
- `a198f21` (Task 2) — FOUND
- `0eee380` (Task 3) — FOUND
- `77870b3` (Task 4) — FOUND
- `0a7dcfc` (Task 5) — FOUND
- `15d7fed` (Task 6) — FOUND
- `685fc2c` (post-task touchup) — FOUND

## Next Phase Readiness

- AUTH-01 satisfied; `IRefreshTokenService.RevokeAllForUserAsync` is now available for **plan 02-02** (account-deletion re-auth flow — step D-13 #2 calls it as defense-in-depth before cascade delete).
- `RateLimitTestFactory` helper is in place; **plan 02-03** can reuse it for rate-limiter integration tests.
- `RefreshToken__HashKey` env var is wired through docker-compose; **operator must set a real value** (`openssl rand -base64 32`) in `.env` before deploying to any environment beyond local dev. Documented in `.env.example`.
- **D-16 deferred** to Phase 3 PIPE-01 (Hangfire): no recurring cleanup job for expired `refresh_tokens` rows yet. Table growth is bounded (low thousands of rows in the 4-6 weeks before Phase 3 lands) and non-concerning at the 100-500 user target.

---
*Phase: 02-auth-rate-limit-hardening*
*Completed: 2026-05-14*
