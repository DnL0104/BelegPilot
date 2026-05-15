---
phase: 02-auth-rate-limit-hardening
plan: 02
subsystem: auth
tags: [account-deletion, bcrypt, refresh-tokens, rate-limit, minimal-api, frombody, react, axios]

# Dependency graph
requires:
  - phase: 02-auth-rate-limit-hardening
    provides: |
      IRefreshTokenService.RevokeAllForUserAsync (plan 02-01) — the D-13 step 2
      defence-in-depth revoke before cascade delete; `auth-strict` rate-limit
      policy (plan 02-03) — mixed-partition limiter that automatically partitions
      by `user:{sub}` on authenticated /account; RateLimitTestFactory.BuildFactory
      pattern (plans 02-01 + 02-03) — fast-fail Npgsql timeout for WAF tests
provides:
  - DeleteAccountRequest(string Password) DTO bound from DELETE body
  - DeleteAccountValidator (FluentValidation, German password-required message)
  - DeleteAccountHandler refactor with D-13 ordered cascade
    (BCrypt.Verify → RevokeAllForUserAsync → Users.Remove + SaveChanges)
  - DELETE /auth/account endpoint binding via [FromBody] +
    .RequireRateLimiting("auth-strict") + 401 mapping for wrong password
  - Frontend dialog rebind from CONFIRM_PHRASE typed-input to password input
    with inline 401 surface (German "Ungültiges Passwort.")
  - Frontend api-client.deleteAccount(password) using raw axios.delete with
    `data: { password }` config so 401 surfaces inline instead of triggering
    the shared refresh-interceptor's logout flow
affects: [03-pipeline-hangfire (audit-log integration), 06-leg-08]

# Tech tracking
tech-stack:
  added:
    - BCrypt.Net-Next 4.0.3 reference on TaxReader.Application project
      (handler-side password verify — previously Infrastructure-only)
    - "[FromBody] attribute on DELETE body-binding to disambiguate Minimal API
      parameter source (Microsoft.AspNetCore.Mvc)"
  patterns:
    - "Body-bound DELETE in Minimal API requires [FromBody] explicit attribute —
      without it, the host short-circuits under WebApplicationFactory<Program>
      with ObjectDisposedException at ConfigureHostBuilder time"
    - "axios.delete second arg is a CONFIG object, not a body — only
      `{ data: payload }` actually sends content"
    - "Raw `axios` (not the shared `api` instance) for endpoints that need a
      401 to surface inline rather than trigger the refresh-interceptor's
      logout-and-redirect flow (Pattern 6 from research)"
    - "Mock.Callback to capture intermediate state mid-handler — cleaner than
      Mock.Sequence for asserting call ordering against EF in-memory provider"

key-files:
  created:
    - Backend/src/TaxReader.Application/Validators/DeleteAccountValidator.cs
    - Backend/src/TaxReader.Application/Commands/DeleteAccountHandler.cs
      (file existed on disk previously, but was untracked; this plan rewrites
      it and adds it to git tracking)
    - Backend/src/TaxReader.Application/DTOs/AuthDtos.cs (newly tracked;
      DeleteAccountRequest record appended)
    - Backend/tests/TaxReader.UnitTests/Auth/DeleteAccountHandlerTests.cs
    - Backend/tests/TaxReader.UnitTests/Application/Validators/DeleteAccountValidatorTests.cs
    - Frontend/src/app/(authenticated)/settings/page.tsx (newly tracked;
      dialog rewired for password input)
    - Frontend/src/lib/api-client.ts (newly tracked; deleteAccount signature
      change)
  modified:
    - Backend/src/TaxReader.Api/Endpoints/AuthEndpoints.cs (DTO binding +
      [FromBody] + RequireRateLimiting("auth-strict") + 401 mapping)
    - Backend/src/TaxReader.Application/TaxReader.Application.csproj
      (added BCrypt.Net-Next PackageReference)

key-decisions:
  - "DELETE /auth/account requires [FromBody] on the DeleteAccountRequest
    parameter — without it, Minimal API's implicit body binder breaks
    WebApplicationFactory<Program> host startup with ObjectDisposedException.
    Plan's task 3 did NOT specify this; Rule 1 auto-fix added it during Task 5
    verification."
  - "BCrypt.Net-Next is on the Application project's PackageReference set
    (not Infrastructure-only). The handler verifies passwords in the
    Application layer because that's where existing handlers live; the
    architecture rule 'Infrastructure implements external concerns' is not
    violated because BCrypt is a pure library (no IO/network)."
  - "D-13 ordering (revoke → delete) is asserted via Mock.Callback that
    captures the user count at the moment RevokeAllForUserAsync fires —
    cleaner than Mock.Sequence + compatible with EF in-memory which doesn't
    intercept SaveChanges. The test still proves the same invariant:
    revoke runs while user is still in the DB, delete fires afterward."
  - "Frontend dialog uses raw `axios.delete` (NOT the shared `api` instance)
    so a wrong-password 401 surfaces inline instead of triggering the
    shared refresh-interceptor's logout flow. This matches RESEARCH Pitfall 6 +
    Pattern 6."

patterns-established:
  - "Minimal API DELETE-with-body requires [FromBody]: `MapDelete(path, async
    ([FromBody] TRequest req, …handler, ct) => …)`. Future endpoints that take
    a body on a verb other than POST/PUT must follow this."
  - "Application-layer password verification: BCrypt.Net.BCrypt.Verify usable
    from any handler. The fully-qualified namespace matches existing usage in
    AuthService.cs:100."
  - "Frontend: when an endpoint's 401 represents a USER ERROR (wrong password,
    invalid input) rather than session expiry, call the endpoint via raw
    `axios` not the shared `api` instance — the refresh-interceptor's blanket
    401 handling would otherwise force a logout."

requirements-completed: [AUTH-02]

# Metrics
duration: ~30min
completed: 2026-05-15
---

# Phase 02 Plan 02: Account-Deletion Password Re-Auth Summary

**Password re-verify via `BCrypt.Verify` on DELETE /auth/account body, defence-in-depth refresh-token revoke before cascade, German inline 401 surface, [FromBody] disambiguation fix**

## Performance

- **Duration:** ~30 min (active work; investigation of the [FromBody] WAF
  regression dominated the wall-clock time)
- **Started:** 2026-05-15T08:30:28Z
- **Completed:** 2026-05-15T16:05:07Z (wall-clock includes idle periods)
- **Tasks:** 5 / 5
- **Files created:** 7 (5 source + 2 test)
- **Files modified:** 2 (AuthEndpoints.cs + Application.csproj)

## Accomplishments

- **DTO + Validator landed** — `DeleteAccountRequest(string Password)`
  appended to `AuthDtos.cs`; `DeleteAccountValidator` enforces non-empty
  password with German message `"Passwort ist erforderlich."` and is
  auto-discovered by `AddValidatorsFromAssemblyContaining<UploadReceiptFilesCommand>()`.
- **Handler refactored to D-13 ordering** — `DeleteAccountHandler.HandleAsync`
  accepts a `DeleteAccountRequest` and runs the three-step pipeline:
  (1) `BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash)` —
  fails with `Result<bool>.Failure("Ungültiges Passwort.")` on mismatch;
  (2) `refreshTokenService.RevokeAllForUserAsync(userId, ct)` — runs BEFORE
  the cascade delete, providing audit-log clarity for Phase 6 LEG-08 and
  closing a small race window where an in-flight `/auth/refresh` could
  re-issue against a user mid-delete;
  (3) `dbContext.Users.Remove(user); SaveChangesAsync` — FK CASCADE drops
  refresh_tokens, receipt_files, token balance, transactions.
- **Endpoint wired with [FromBody] + auth-strict rate-limit** — DELETE
  `/auth/account` binds `[FromBody] DeleteAccountRequest`, dispatches the
  `"Ungültiges Passwort."` failure to a 401 JSON body, attaches
  `.RequireRateLimiting("auth-strict")` (auto-partitioned by `user:{sub}`
  because the endpoint is authenticated). T-02-04 mitigation: stolen access
  token cannot delete the account without the password; 5/min per-user
  budget makes brute-force probing infeasible.
- **Frontend dialog rebuilt for password input** — Removed the
  `CONFIRM_PHRASE = "LÖSCHEN"` typed-confirm pattern in favor of a
  `type="password"` Input. Prompt copy: "Geben Sie zur Bestätigung Ihr
  Passwort ein." On 401, sets inline `deleteError = "Ungültiges Passwort."`
  without closing the dialog (D-11 + D-12). Dialog open/close handler
  resets both `password` and `deleteError` so re-open starts clean.
- **`deleteAccount(password)` API client rewired** — Uses raw `axios.delete`
  (not the shared `api` instance) so wrong-password 401 surfaces inline
  instead of triggering the refresh-interceptor's silent retry + logout
  flow. Body goes via the `data` config slot (RESEARCH Pitfall 6:
  `axios.delete(url, body)` does NOT send a body — only
  `axios.delete(url, { data: body })` does).
- **6 active AUTH-02 tests pass** — 4 handler tests (correct password
  204 + cascade; wrong password 401 + German error; revoke-before-delete
  ordering; missing-user failure) + 2 validator tests (empty password →
  German error; non-empty → pass). Full backend suite: 139 passed / 5
  skipped / 0 failed (the 5 skipped are intentional manual-UAT deferrals
  from plan 02-03).

## Task Commits

Each task was committed atomically (within the constraints of
inter-task signature coupling):

1. **Task 1 (Wave 0): Install AUTH-02 test scaffolding** — `9d173fd` (test)
2. **Task 2: Add DeleteAccountRequest DTO + Validator + handler refactor** — `267d8b4` (feat)
3. **Task 3: Bind DeleteAccountRequest body + attach auth-strict rate-limit** — `96bfe7c` (feat)
4. **Task 4: Swap CONFIRM_PHRASE for password input + inline 401 surface** — `1279bd2` (feat)
5. **Task 5: Un-skip tests + [FromBody] body-binding Rule 1 fix** — `7de3dcc` (test, includes Rule 1 deviation)

## Files Created/Modified

### Created (7)
- `Backend/src/TaxReader.Application/Validators/DeleteAccountValidator.cs` —
  Single-rule FluentValidation: Password NotEmpty with German message
- `Backend/src/TaxReader.Application/Commands/DeleteAccountHandler.cs` —
  Rewritten with `IRefreshTokenService` injection + D-13 ordered cascade
- `Backend/src/TaxReader.Application/DTOs/AuthDtos.cs` — Newly tracked;
  contains the new `DeleteAccountRequest(string Password)` record
- `Backend/tests/TaxReader.UnitTests/Auth/DeleteAccountHandlerTests.cs` —
  4 active tests (in-memory EF + Moq pattern from
  `ConfirmClassificationHandlerTests`)
- `Backend/tests/TaxReader.UnitTests/Application/Validators/DeleteAccountValidatorTests.cs` —
  2 active tests (FluentValidation TestHelper)
- `Frontend/src/app/(authenticated)/settings/page.tsx` — Newly tracked;
  dialog rewired from CONFIRM_PHRASE typed-input to password input with
  inline 401 surface
- `Frontend/src/lib/api-client.ts` — Newly tracked; deleteAccount signature
  change + raw axios.delete with data config

### Modified (2)
- `Backend/src/TaxReader.Api/Endpoints/AuthEndpoints.cs` — DTO binding +
  [FromBody] + RequireRateLimiting("auth-strict") + 401 mapping for the
  wrong-password failure. /login, /register, /refresh chains preserved
  unchanged.
- `Backend/src/TaxReader.Application/TaxReader.Application.csproj` —
  Added `<PackageReference Include="BCrypt.Net-Next" />` so the
  Application-layer handler can call `BCrypt.Net.BCrypt.Verify`.

## Decisions Made

- **[FromBody] is mandatory on the DELETE body-binding** — without it,
  Minimal API treats the implicit body parameter as ambiguous and the
  host short-circuits at WebApplicationFactory bootstrap time. The fix
  costs one attribute + one using directive; the alternative (using
  POST or PATCH for delete-with-body) would break REST semantics for
  no real benefit.
- **BCrypt.Net-Next on the Application project** — the existing
  convention places handlers in `Application/Commands/`, so the password
  verify naturally lives there. BCrypt is a pure-library dependency
  (no IO, no platform calls), so allowing it in Application does not
  violate the "Infrastructure implements external concerns" architecture
  rule.
- **Mock.Callback for ordering assertion** — capturing the user count
  at the moment `RevokeAllForUserAsync` fires is cleaner than
  `MockSequence` (which doesn't play well with EF's deferred SaveChanges)
  and proves the same D-13 invariant: revoke runs while the user is
  still in the DB, delete fires afterward.
- **Raw axios for `deleteAccount`** — the shared `api` instance's
  refresh-interceptor would catch 401s and silently bounce to /login,
  which is correct for session-expiry 401s but wrong for user-error
  401s (wrong password). Using raw axios lets the caller see the 401
  and render inline error feedback. Documented in RESEARCH Pitfall 6 +
  Pattern 6.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] BCrypt.Net-Next package not available to Application project**

- **Found during:** Task 2 verify (`dotnet build Backend`)
- **Issue:** The plan instructed the handler to call `BCrypt.Net.BCrypt.Verify`
  in `Backend/src/TaxReader.Application/Commands/DeleteAccountHandler.cs`,
  but `BCrypt.Net-Next` was only registered on `TaxReader.Infrastructure`
  (where `AuthService.cs:100` lives). Application couldn't see the namespace.
- **Fix:** Added `<PackageReference Include="BCrypt.Net-Next" />` to
  `Backend/src/TaxReader.Application/TaxReader.Application.csproj`. Central
  Package Management (`Directory.Packages.props`) already pins the version
  at 4.0.3, so no version specification needed.
- **Files modified:** `Backend/src/TaxReader.Application/TaxReader.Application.csproj`
- **Committed in:** `267d8b4` (Task 2)

**2. [Rule 1 - Bug] DELETE-with-record-body broke WebApplicationFactory host startup**

- **Found during:** Task 5 (running the full backend test suite)
- **Issue:** After Task 3 attached `DeleteAccountRequest request` as the
  first lambda parameter on `MapDelete("/account", …)`, EVERY
  `WebApplicationFactory<Program>` test in the suite —
  `CorsConfigurationTests`, all `RateLimiting` tests — failed in
  isolation with `System.ObjectDisposedException: Cannot access a
  disposed object. Object name: 'IServiceProvider'.` thrown from
  `WebApplicationFactory.ConfigureHostBuilder` during host bootstrap.
- **Diagnosis path:**
  1. Confirmed the 8 failures were NEW — `dotnet test` at fb69636 passed.
  2. Confirmed the failure was isolated, not parallel-test collisions
     (single-test runs failed too — so the `RateLimiterTestCollection`
     serialization helper from plan 02-03 was not enough).
  3. Reverted to fb69636 state on disk + re-applied my plan changes
     piecewise. The breaking change isolated cleanly to adding
     `DeleteAccountRequest request` to the MapDelete lambda.
  4. Confirmed `.RequireRateLimiting("auth-strict")` alone did NOT
     cause the issue; the body-binding parameter did.
- **Root cause:** Minimal API treated the new record-type parameter on
  `MapDelete` as ambiguous (DELETE rarely body-binds; the implicit
  source inference picked the wrong slot), and the host's DI bootstrap
  silently aborted before WAF could capture services.
- **Fix:** Annotate the parameter with
  `[Microsoft.AspNetCore.Mvc.FromBody]` to explicitly mark it as a body
  binding. Added `using Microsoft.AspNetCore.Mvc;` so the call site
  reads `[FromBody] DeleteAccountRequest request`. Matches the
  axios-side `data: { password }` config one-to-one.
- **Files modified:** `Backend/src/TaxReader.Api/Endpoints/AuthEndpoints.cs`
- **Verification:** Full backend suite (`dotnet test Backend`) reports
  139 passed / 5 skipped / 0 failed (was 125 passed / 8 failed / 5
  skipped before the fix). The 5 skipped tests are intentional
  manual-UAT deferrals from plan 02-03 and are unrelated.
- **Committed in:** `7de3dcc` (Task 5)

---

**Total deviations:** 2 auto-fixed
- 1 Rule 3 (blocking — missing dependency)
- 1 Rule 1 (bug — DELETE body-binding host-startup regression)

**Impact on plan:** Neither deviation altered the user-visible behaviour
or threat-model coverage. Rule 3 was a build-time package addition; Rule
1 was a single-line attribute correction. The original behavioural intent
(password verify → token revoke → cascade delete, with German 401 surface)
is delivered as specified.

## Issues Encountered

- **Frontend lint pre-existing errors** — `cd Frontend && npm run lint`
  reports 6 errors + 2 warnings, ALL in files outside my plan's diff
  scope (`auth-provider.tsx`, `classify-dialog.tsx`, `impressum/page.tsx`,
  `category-breakdown.tsx`, `export-buttons.tsx`, and a pre-existing
  `setState-in-effect` on settings/page.tsx:48 that pre-dates my changes).
  Per the SCOPE BOUNDARY rule, these are out of scope. Frontend build
  succeeds. Lint stays informational; the build is the gating signal.
- **Settings page pre-existing `setState-in-effect`** — line 48 has a
  `setThreshold(settings.autoConfirmThreshold)` call inside a useEffect
  that pre-dates plan 02-02. Not touched by my changes. Logged in
  out-of-scope deferred items (no action required by this plan).

## Known Stubs

None. The DeleteAccountHandler returns real values; the DTO has a single
required property; the validator enforces it; the endpoint binds the
body; the frontend dialog has a working password input + inline error
surface. No `NotImplementedException` markers; no hardcoded empties; no
placeholders.

## TDD Gate Compliance

This plan is `type: execute`, not `type: tdd`. RED/GREEN/REFACTOR gates
do not apply at the plan level. Wave-0 tests were authored as Skip stubs
first (Task 1, `test:` commit), then un-skipped after implementation
landed (Task 5, `test:` commit). The skip-first-then-implement-then-unskip
rhythm satisfies the Wave-0 + Wave-2 pattern documented in
`02-VALIDATION.md`.

## Self-Check: PASSED

Verified after writing this summary:

**Files created:**
- `Backend/src/TaxReader.Application/Validators/DeleteAccountValidator.cs` — FOUND
- `Backend/src/TaxReader.Application/Commands/DeleteAccountHandler.cs` — FOUND
- `Backend/src/TaxReader.Application/DTOs/AuthDtos.cs` — FOUND
- `Backend/tests/TaxReader.UnitTests/Auth/DeleteAccountHandlerTests.cs` — FOUND
- `Backend/tests/TaxReader.UnitTests/Application/Validators/DeleteAccountValidatorTests.cs` — FOUND
- `Frontend/src/app/(authenticated)/settings/page.tsx` — FOUND
- `Frontend/src/lib/api-client.ts` — FOUND

**Commits in git history:**
- `9d173fd` (Task 1) — FOUND
- `267d8b4` (Task 2) — FOUND
- `96bfe7c` (Task 3) — FOUND
- `1279bd2` (Task 4) — FOUND
- `7de3dcc` (Task 5 + [FromBody] fix) — FOUND

## Next Phase Readiness

- **Phase 2 fully satisfied.** AUTH-01, AUTH-02, AUTH-03 are all green:
  refresh tokens are HMAC-keyed in a multi-row table with rotation +
  replay-revoke-all, the four rate-limit policies are wired with a
  German `application/problem+json` 429 shape, and account deletion is
  password-gated with defence-in-depth revoke-before-cascade.
- **D-16 deferred to Phase 3 PIPE-01:** still no recurring cleanup job
  for expired `refresh_tokens` rows. Table growth bounded; non-concerning
  at the 100–500 user target.
- **D-23 deferred to Phase 6 LEG-08:** still no audit-log entries on
  account deletion or refresh-token revocation. The Serilog warning
  from `RefreshTokenService.RevokeAllForUserAsync` fires on every revoke
  (replay-detection log), and the account-deletion path will gain its
  own audit entry when LEG-08 adds the `audit_log` table + `AuditLogger`.
- **Operator (manual UAT) pending:** Phase 2 verifier (`/gsd-verify-phase`)
  should validate via `docker compose up --build`:
  1. Open `/settings`, click "Konto löschen"
  2. Verify dialog shows German password prompt
  3. Type wrong password → verify inline "Ungültiges Passwort." appears
     without closing dialog
  4. Type correct password → verify dialog closes and redirect to /login
  5. Verify 6th deletion attempt within a minute is rate-limited (429)

---
*Phase: 02-auth-rate-limit-hardening*
*Completed: 2026-05-15*
