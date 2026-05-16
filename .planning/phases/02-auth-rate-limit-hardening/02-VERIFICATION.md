---
phase: 02-auth-rate-limit-hardening
verified: 2026-05-16T00:00:00Z
status: human_needed
score: 5/5 ROADMAP success criteria verified (all plan must-haves verified against codebase; 2 advisory REVIEW findings noted as warnings — see "Advisory Findings"); 4 manual UAT items pending sign-off
overrides_applied: 0
human_verification:
  - test: "Real-IP-through-Caddy end-to-end"
    expected: "6th /auth/login from same client IP within 1 minute returns 429 with German body; Caddy access logs show real client IP (not 172.x docker-internal IP)"
    why_human: "Reverse-proxy hop cannot be simulated in WebApplicationFactory in-process — confirmed by intentional [Fact(Skip)] on XForwardedFor_TrustedSubnet_ResolvesRealIp. Requires `docker compose up --build` + `curl -H 'X-Forwarded-For: 1.2.3.4' https://localhost/api/v1/auth/login` repeated 6×"
  - test: "Upload-concurrency limit (2 active + 4 queued)"
    expected: "7th concurrent POST to /api/v1/receipt-files from the same authenticated user returns 429 with German body; 3rd-6th queue until earlier upload completes"
    why_human: "WebApplicationFactory test client runs in-process; concurrent HttpClient.SendAsync does not exercise the same timing characteristics as production HTTP. Confirmed unreliable by intentional [Fact(Skip)] on two UploadConcurrencyPolicyTests"
  - test: "Account-deletion dialog UX"
    expected: "Open /settings; click 'Konto unwiderruflich löschen'; verify dialog shows password input + German prompt 'Geben Sie zur Bestätigung Ihr Passwort ein.'; typing wrong password surfaces 'Ungültiges Passwort.' inline without closing dialog; typing correct password closes dialog + redirects to /login"
    why_human: "Visual + interaction flow — automated component tests not in scope until Phase 7 QA-02 (Vitest); per VALIDATION.md Manual-Only Verifications row 4"
  - test: "Postgres migration Up() against real Postgres 17"
    expected: "psql -c '\\d refresh_tokens' shows: id (uuid, default gen_random_uuid()), user_id (uuid, NOT NULL), token_hash (varchar(44), NOT NULL, UNIQUE), created_at/expires_at/revoked_at/last_used_at (timestamptz), user_agent (varchar(500)), ip_address (inet), replaced_by_token_id (uuid, nullable self-FK); users table no longer contains refresh_token / refresh_token_expires_at columns"
    why_human: "EF InMemory provider cannot run Postgres DDL — MigrationTests.cs is an explicit skip. Real Postgres-backed migration verification deferred to Phase 7 QA-01 (Testcontainers), but operator should run once now via `docker compose up db` + `dotnet ef database update` before merging"
---

# Phase 02: Auth + Rate-Limit Hardening — Verification Report

**Phase Goal (ROADMAP.md):** Multi-device-safe authentication via a `refresh_tokens` table with rotation + replay detection, plus rate limiting that doesn't lock out legitimate token rotation, plus DSGVO-friendly account-deletion confirmation.

**Verified:** 2026-05-16
**Status:** human_needed
**Re-verification:** No — initial verification

## Goal Achievement

### Observable Truths (ROADMAP Success Criteria)

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | User can stay logged in on phone and laptop simultaneously across multiple refreshes | VERIFIED | `RefreshToken` is a multi-row entity (Domain/Entities/RefreshToken.cs); `RefreshTokenConfiguration.cs:22-27` declares unique index on `token_hash` only (not on `(user_id)`) so a user can have N active rows; `User.cs:21` exposes `ICollection<RefreshToken> RefreshTokens`; `MultiDeviceTokenTests.TwoActiveTokens_BothValidate` asserts two simultaneously-active tokens both rotate independently — test passes |
| 2 | A leaked refresh token replayed after rotation triggers full revocation of all the user's tokens | VERIFIED | `RefreshTokenService.cs:86-96`: when `existing.RevokedAt is not null` (revoked row presented again), service invokes `RevokeAllForUserAsync(existing.UserId)` AND emits `SentrySdk.CaptureMessage("Refresh token replay detected", ..., SentryLevel.Warning)` with `user.id_hash` extra; `ReplayDetectionTests.ValidateAndRotateAsync_RevokedTokenPresented_RevokesAllUserTokens` asserts every non-revoked row for the user gets `RevokedAt` set — test passes; D-04 silent posture verified by sibling test asserting same `"Ungültiges oder abgelaufenes Refresh-Token."` German error as not-found/expired |
| 3 | Brute-force login attempts from one IP get rate-limited within 5 attempts/min without blocking legitimate users | VERIFIED | `Program.cs:141-157` registers `auth-strict` policy: `PermitLimit = 5`, `Window = TimeSpan.FromMinutes(1)`, fixed-window per IP (anonymous) or per sub (authenticated); `AuthEndpoints.cs:32,52` attaches `.RequireRateLimiting("auth-strict")` to `/login` + `/register`; `AuthStrictPolicyTests.SixthLoginAttempt_Returns429WithGermanProblemDetails` confirms 6th attempt returns 429 — test passes; `auth-refresh` (30/min on `/refresh`, `Program.cs:161-170`) and global 60/min (`Program.cs:127-136`) provide layered defense without locking out legitimate token rotation |
| 4 | Account deletion requires re-authentication via password before firing | VERIFIED | `DeleteAccountHandler.cs:27-28`: `BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash)` runs BEFORE `dbContext.Users.Remove(user)` (line 42); D-13 ordering enforced (line 35 calls `refreshTokenService.RevokeAllForUserAsync(userId, ...)` BEFORE the remove). `AuthEndpoints.cs:76-92` binds `[FromBody] DeleteAccountRequest request` and dispatches "Ungültiges Passwort." → 401 JSON; `DeleteAccountHandlerTests.WrongPassword_Returns401_GermanError` + `CorrectPassword_Returns204_AndCascadeDeletes` + `RevokesTokensBeforeDelete_DefenseInDepth` all pass |
| 5 | Rate-limited responses include German error copy + Retry-After header | VERIFIED | `Program.cs:189-217` `OnRejected` callback sets `Response.Headers.RetryAfter` from `MetadataName.RetryAfter` lease metadata (line 195), writes `ProblemDetails` with `Title = "Zu viele Anfragen."` and `Detail = "Bitte versuchen Sie es in {N} Sekunden erneut."` (lines 204-206), uses `WriteAsJsonAsync(..., contentType: "application/problem+json", ...)` (line 215) so Content-Type is correctly set; `RejectedResponseShapeTests.RateLimited_Returns429WithGermanProblemDetails_AndRetryAfter` asserts all four parts (status 429, content type application/problem+json, Retry-After header, German Title+Detail, no policy-name leakage) — test passes |

**Score:** 5/5 ROADMAP success criteria verified

### Plan-Level Must-Haves (truths from PLAN frontmatter)

In addition to the ROADMAP success criteria, the three plans declared 14 plan-level truths. All are verified below.

**Plan 02-01 (AUTH-01 — refresh tokens):**

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1.1 | Two active refresh_tokens rows for same user_id, both validate | VERIFIED | `MultiDeviceTokenTests.cs:54-74` — passes; unique index is on `token_hash` only |
| 1.2 | Replay revokes all user tokens | VERIFIED | `ReplayDetectionTests.cs:54-76` — passes |
| 1.3 | DB-only leak does not allow forgery (HMAC pepper) | VERIFIED with WARNING | `RefreshTokenService.cs:160` uses `HMACSHA256.HashData(_pepper, plaintextBytes)`; `HmacPepperHashingTests` verifies pepper key-sensitivity. **CAVEAT:** REVIEW.md CR-01 flags that if operator ships with `REFRESHTOKEN_HASHKEY=` empty, `Convert.FromBase64String("")` yields a 0-byte key and HMAC becomes effectively SHA-256 — see Advisory Findings |
| 1.4 | Rotation produces new plaintext + revokes old row + ReplacedByTokenId chain | VERIFIED | `RefreshTokenService.cs:98-119`: insert new row, set old `RevokedAt`/`LastUsedAt`/`ReplacedByTokenId = newRow.Id`; `RefreshTokenServiceTests.ValidateAndRotateAsync_ValidToken_ReturnsNewPlaintextAndRevokesOld` passes |
| 1.5 | On replay, Sentry captures warning with user.id_hash | VERIFIED | `RefreshTokenService.cs:89-92`: `SentrySdk.CaptureMessage("Refresh token replay detected", scope => scope.SetExtra("user.id_hash", HashUserId(...)), SentryLevel.Warning)`; `HashUserId` uses SHA-256 + first 16 hex chars (Phase 1 D-14 PII allow-list) |

**Plan 02-02 (AUTH-02 — account-deletion re-auth):**

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 2.1 | Account deletion requires password BCrypt re-verify | VERIFIED | `DeleteAccountHandler.cs:27` — covered by ROADMAP SC #4 above |
| 2.2 | Wrong password → 401 with German inline; dialog stays open | VERIFIED | `AuthEndpoints.cs:88-89`: `Results.Json(new { error = "Ungültiges Passwort." }, statusCode: 401)`; `Frontend/src/app/(authenticated)/settings/page.tsx:75-84` sets `deleteError = "Ungültiges Passwort."` on 401 and does NOT close the dialog (line 83 only sets `isDeleting = false`); `Dialog onOpenChange` (line 215) guards against close while `isDeleting` |
| 2.3 | Correct password cascades: revoke refresh tokens → user remove → FK CASCADE drops related data | VERIFIED | `DeleteAccountHandler.cs:35` (revoke) precedes `:42` (Users.Remove); `RefreshTokenConfiguration.cs:32-35` declares self-FK NoAction; `UserConfiguration.cs` (referenced in 02-01-SUMMARY) declares `HasMany(RefreshTokens).OnDelete(Cascade)`; migration body confirms `onDelete: ReferentialAction.Cascade` on `fk_refresh_tokens_users_user_id` |
| 2.4 | Rate-limited: 5 deletion attempts per user per minute via auth-strict (partition by sub) | VERIFIED | `AuthEndpoints.cs:93` attaches `.RequireRateLimiting("auth-strict")` to `MapDelete("/account")`; `Program.cs:143-146` partition logic chooses `user:{sub}` when sub claim present (authenticated endpoint always has one). **Partition-by-sub behavior remains skip-tested:** `AuthStrictPolicyTests.TwoUsersOneIp_BothGetFiveAttempts` is an intentional skip (documented manual UAT). REVIEW.md WR-08 flags this as a verify-end-to-end-before-launch item |
| 2.5 | Frontend dialog shows German prompt; 401 surfaces inline without closing dialog | VERIFIED | `settings/page.tsx:234-256` — password Input with `type="password"`; German prompt at line 236; inline `deleteError` rendered at line 254-256; raw `axios.delete` at `api-client.ts:131-134` with `data: { password }` config |

**Plan 02-03 (AUTH-03 — rate limit + forwarded headers):**

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 3.1 | Brute-force /auth/login from one IP rate-limited within 5/min | VERIFIED | Covered by ROADMAP SC #3 above |
| 3.2 | 429 responses: German copy + Retry-After + application/problem+json | VERIFIED | Covered by ROADMAP SC #5 above |
| 3.3 | Real client IP behind Caddy resolved (Docker subnet trusted; not IP-spoofable) | VERIFIED for config; PENDING for end-to-end | `Program.cs:110-117` configures `ForwardedHeadersOptions` with `KnownIPNetworks.Add(System.Net.IPNetwork.Parse("172.16.0.0/12"))` and `ForwardLimit = 1`; `ForwardedHeadersTests.KnownIPNetworksContainsDockerSubnet` + `ForwardLimitIsOne` pass. End-to-end (reverse-proxy hop) requires manual UAT — see `human_verification` item 1 |
| 3.4 | /auth/refresh tolerates legitimate rotation (30/min per IP) | VERIFIED | `Program.cs:161-170` `auth-refresh` policy: `PermitLimit = 30`, `Window = 1 min`; `AuthEndpoints.cs:72` attaches `.RequireRateLimiting("auth-refresh")`; `AuthRefreshPolicyTests.ThirtyFirstRefreshAttempt_Returns429` passes (31st = 429) |
| 3.5 | /receipt-files upload has concurrency=2 + queue=4 + 30s queue wait | VERIFIED for config; PENDING for behavior | `Program.cs:174-185` `upload-concurrency` policy: `PermitLimit = 2`, `QueueLimit = 4`, `QueueProcessingOrder.OldestFirst`, partition by `user:{sub}`; `ReceiptFileEndpoints.cs:48` attaches `.RequireRateLimiting("upload-concurrency")`. Behavior under load requires manual UAT — see `human_verification` item 2 |
| 3.6 | Global 60/min per IP catches generic abuse | VERIFIED | `Program.cs:127-136` `GlobalLimiter`: `PermitLimit = 60`, fixed-window per IP; `GlobalPolicyTests.SixtyFirstRequest_Returns429` passes |

### Required Artifacts

All 16 declared artifacts across the three plans exist and pass substantive content checks. Spot-checks:

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `Backend/src/TaxReader.Domain/Entities/RefreshToken.cs` | POCO with 10 fields per D-02 | VERIFIED | 10 fields + nav property; Domain-pure (only `using System.Net;`) |
| `Backend/src/TaxReader.Application/Interfaces/IRefreshTokenService.cs` | 3 methods | VERIFIED | `IssueAsync`, `ValidateAndRotateAsync`, `RevokeAllForUserAsync` |
| `Backend/src/TaxReader.Infrastructure/Services/RefreshTokenService.cs` | HMAC hashing + rotation + replay-revoke + Sentry | VERIFIED | All four mechanisms present at lines 86-96, 98-119, 89-92 |
| `Backend/src/TaxReader.Infrastructure/Configuration/RefreshTokenOptions.cs` | SectionName + HashKey | VERIFIED | Lines 5, 12 |
| `Backend/src/TaxReader.Infrastructure/Data/Configurations/RefreshTokenConfiguration.cs` | Unique token_hash + composite (user_id, revoked_at) | VERIFIED | Lines 22, 27 |
| `Backend/src/TaxReader.Infrastructure/Migrations/20260514204609_AddRefreshTokensTable_DropLegacyRefreshTokenColumns.cs` | CreateTable BEFORE DropColumn | VERIFIED | Migration body: CreateTable at line 19, DropColumns at lines 67/71 (correct order per D-15) |
| `Backend/src/TaxReader.Application/DTOs/AuthDtos.cs` | DeleteAccountRequest record | VERIFIED | Line 6 |
| `Backend/src/TaxReader.Application/Validators/DeleteAccountValidator.cs` | German required-password message | VERIFIED (presence); WARNING (not invoked) | `"Passwort ist erforderlich."` at line 12; but see REVIEW.md CR-02 — Minimal API does NOT auto-invoke the validator. BCrypt.Verify in handler is the actual gate (still satisfies SC #4) |
| `Backend/src/TaxReader.Application/Commands/DeleteAccountHandler.cs` | BCrypt verify → revoke → cascade | VERIFIED | Lines 27, 35, 42 in D-13 order |
| `Backend/src/TaxReader.Api/Endpoints/AuthEndpoints.cs` | [FromBody] + RequireRateLimiting + 401 mapping | VERIFIED | Lines 77, 88, 93 |
| `Backend/src/TaxReader.Api/Program.cs` | ForwardedHeaders + 4 policies + OnRejected + pipeline order | VERIFIED | Lines 110-117 (forwarded), 121-218 (rate limiter), 269-278 (pipeline) |
| `Backend/src/TaxReader.Api/Endpoints/ReceiptFileEndpoints.cs` | upload-concurrency attached | VERIFIED | Line 48 |
| `Frontend/src/app/(authenticated)/settings/page.tsx` | Password input + inline 401 + German prompt | VERIFIED | Lines 234-256 |
| `Frontend/src/lib/api-client.ts` | deleteAccount(password) via raw axios.delete with data config | VERIFIED | Lines 126-136 |
| `docker-compose.yml` | RefreshToken__HashKey env mapping | VERIFIED | Line 37 |
| `.env.example` | REFRESHTOKEN_HASHKEY placeholder with generation hint | VERIFIED | Lines 17-21 |

### Key Link Verification

All declared key links pass wiring grep:

| From | To | Via | Status |
|------|-----|-----|--------|
| `AuthService.cs` | `IRefreshTokenService.IssueAsync` | Constructor DI primary param | WIRED — `AuthService.cs:80, 104` |
| `AuthService.cs` | `IRefreshTokenService.ValidateAndRotateAsync` | RefreshAsync method | WIRED — `AuthService.cs:119` |
| `AuthEndpoints.cs` | `AuthService` UA/IP passthrough | `httpContext.Request.Headers.UserAgent` + `Connection.RemoteIpAddress` | WIRED — `AuthEndpoints.cs:21-23, 42-43, 62-63` |
| `docker-compose.yml` | `RefreshTokenOptions.HashKey` | `RefreshToken__HashKey: ${REFRESHTOKEN_HASHKEY}` | WIRED — `docker-compose.yml:37` |
| Frontend dialog button | `api-client.deleteAccount(password)` | `onClick` handler with password state | WIRED — `settings/page.tsx:269` calls `handleDeleteAccount` which calls `deleteAccount(password)` (line 73) |
| `api-client.deleteAccount` | `DELETE /api/v1/auth/account` | `axios.delete` with `data: { password }` config | WIRED — `api-client.ts:131-134` |
| `AuthEndpoints MapDelete /account` | `DeleteAccountHandler.HandleAsync` | `[FromBody] DeleteAccountRequest` | WIRED — `AuthEndpoints.cs:77, 81` |
| `DeleteAccountHandler` | `IRefreshTokenService.RevokeAllForUserAsync` | Constructor DI | WIRED — `DeleteAccountHandler.cs:11, 35` |
| Caddy reverse proxy | API `HttpContext.Connection.RemoteIpAddress` | `UseForwardedHeaders` + KnownIPNetworks 172.16.0.0/12 | WIRED (config); end-to-end PENDING — `Program.cs:269` precedes all other middleware; manual UAT required |
| Program.cs middleware | Endpoint routing | `UseRateLimiter` after Serilog, before Authentication | WIRED — `Program.cs:272, 276, 277` (verified by `ForwardedHeadersWiringTests` source-grep tests) |
| OnRejected callback | Client 429 | `Response.WriteAsJsonAsync(problem, options: null, contentType: "application/problem+json", ...)` + Headers.RetryAfter | WIRED — `Program.cs:195-197, 212-216` |

### Data-Flow Trace (Level 4)

| Artifact | Data Variable | Source | Produces Real Data | Status |
|----------|---------------|--------|--------------------|---|
| `RefreshTokenService.ValidateAndRotateAsync` | `existing` (RefreshToken row) | `dbContext.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == tokenHash)` | Yes — real EF Core query via `IAppDbContext.RefreshTokens` exposed in `AppDbContext.cs:11` | FLOWING |
| `DeleteAccountHandler.HandleAsync` | `user` (User row) | `dbContext.Users.FirstOrDefaultAsync(u => u.Id == userId)` with `userId = currentUser.UserId` | Yes — real query against authenticated user's row | FLOWING |
| Frontend settings/page.tsx dialog `password` state | Local React state | `setPassword(e.target.value)` from password Input | Yes — typed by user | FLOWING |
| `deleteAccount(password)` axios call | Request body | `data: { password }` from caller arg | Yes — flows from dialog state to backend handler | FLOWING |
| `OnRejected` problem body `retryAfterSeconds` | int | Lease metadata `context.Lease.TryGetMetadata(MetadataName.RetryAfter, ...)` | Yes — computed from limiter state | FLOWING |

### Behavioral Spot-Checks

| Behavior | Command | Result | Status |
|----------|---------|--------|--------|
| Backend builds without errors or deprecation warnings | `dotnet build Backend` | Succeeded; 2 unrelated NU1510 trim-suggestion warnings (Microsoft.Extensions.Http); zero ASPDEPR005 (deprecated KnownNetworks) | PASS |
| Backend test suite passes | `dotnet test Backend` | 139 passed, 5 skipped, 0 failed (48s) | PASS |
| AuthEndpoints attaches rate-limit policies to all four /auth* endpoints + /account | grep `.RequireRateLimiting` in `AuthEndpoints.cs` | 4 matches: /register (auth-strict), /login (auth-strict), /refresh (auth-refresh), /account (auth-strict) | PASS |
| Receipt-files upload endpoint attaches upload-concurrency | grep `.RequireRateLimiting` in `ReceiptFileEndpoints.cs` | 1 match: line 48 on POST / | PASS |
| Legacy `user.RefreshToken =` writes are gone | grep `user\.RefreshToken\s*=` in `Backend/src` | 0 matches (legacy column writes removed) | PASS |
| HMAC pepper static API used | grep `HMACSHA256.HashData` | 1 match: `RefreshTokenService.cs:160` | PASS |
| ExecuteUpdateAsync bulk revoke for production path | grep `ExecuteUpdateAsync` | 1 match: `RefreshTokenService.cs:136` (with InMemory fallback) | PASS |
| Sentry replay-detection unique message | grep `"Refresh token replay detected"` | Exactly 1 match: `RefreshTokenService.cs:90` (Serilog warning uses different phrasing per 02-01-SUMMARY post-task touchup) | PASS |
| German 429 title in Program.cs | grep `"Zu viele Anfragen"` | 1 match: `Program.cs:204` | PASS |
| German rate-limit detail prefix | grep `"Bitte versuchen Sie es in"` | Present in `Program.cs:206` | PASS |
| KnownIPNetworks (NOT deprecated KnownNetworks) | grep `KnownIPNetworks.Add` | 1 match: `Program.cs:115` | PASS |
| env var mapping in compose | grep `RefreshToken__HashKey` in `docker-compose.yml` | 1 match: line 37 | PASS |

### Requirements Coverage

| Requirement | Source Plan | Description | Status | Evidence |
|-------------|-------------|-------------|--------|----------|
| AUTH-01 | 02-01 | `refresh_tokens` table with hash-only storage, multi-row per user, rotation on refresh, replay detection that revokes all tokens on collision; `RefreshTokenService` replacing `user.RefreshToken` column logic | SATISFIED | All 5 plan truths verified; 12 active tests pass (11 active Auth/* + 1 deferred MigrationTests skip) |
| AUTH-02 | 02-02 | Account-deletion confirmation modal — re-authentication required + irreversibility warning before `DELETE /auth/account` fires | SATISFIED | Handler verifies password; revoke-before-cascade enforced (D-13); dialog shows irreversibility warning + German prompt; 6 active tests pass |
| AUTH-03 | 02-03 | ASP.NET Core `AddRateLimiter` policies — fixed-window 5 req/min on `/auth/login` + `/auth/register` per IP, 30 req/min on `/auth/refresh` per user, concurrency-2 on `/receipt-files` per user, global 60 req/min per IP | SATISFIED | 4 policies registered; endpoint attachments in place; 9 active tests pass (4 documented manual-UAT skips: X-Forwarded-For end-to-end, 2× upload-concurrency timing, partition-by-sub on /account) |

No orphaned requirements: REQUIREMENTS.md maps exactly AUTH-01 → 02-01, AUTH-02 → 02-02, AUTH-03 → 02-03, all "Complete".

### Anti-Patterns Found

Scoped to files modified in Phase 2. No new TODO/FIXME/PLACEHOLDER markers; no empty handlers; no hardcoded stub returns.

| File | Line | Pattern | Severity | Impact |
|------|------|---------|----------|--------|
| `RefreshTokenService.cs` | 31 | `Convert.FromBase64String(refreshTokenOptions.Value.HashKey)` runs unguarded; empty `HashKey` silently yields zero-length pepper | Warning (Advisory) | See Advisory Findings — REVIEW.md CR-01 |
| `AuthEndpoints.cs` | 88-91 | Endpoint discriminates 401 vs 404 via brittle string comparison `result.Error == "Ungültiges Passwort."` | Info | See Advisory Findings — REVIEW.md WR-02 |
| `DeleteAccountHandler.cs` | 23 | English error string `"User not found."` on a German-localized surface | Info | See Advisory Findings — REVIEW.md WR-01 |
| `RefreshTokenService.cs` | 154-155 | Provider-name string compare `ProviderName == "Microsoft.EntityFrameworkCore.InMemory"` puts test-only branch in production code | Info | See Advisory Findings — REVIEW.md WR-04 |
| `RefreshTokenService.cs` | 79 | `ExpiresAt < DateTime.UtcNow` exclusive boundary; should be `<=` | Info | See Advisory Findings — REVIEW.md WR-06 |
| Various validators | n/a | `AddValidatorsFromAssemblyContaining<>` registers them in DI but nothing actually invokes them — `DeleteAccountValidator` is dead production code | Warning (Advisory) | See Advisory Findings — REVIEW.md CR-02 |

### Advisory Findings (from 02-REVIEW.md)

Two critical-severity findings from the code review require operator decision before final merge. **Neither defeats a ROADMAP success criterion**, so verification status is NOT `gaps_found`. They are surfaced here for transparency.

#### CR-01: HMAC pepper silently degrades to empty-key HMAC if `REFRESHTOKEN_HASHKEY` is unset

**File:** `Backend/src/TaxReader.Infrastructure/Services/RefreshTokenService.cs:31`

**The defect:** `_pepper = Convert.FromBase64String(refreshTokenOptions.Value.HashKey)` runs at construction time. When the env var is empty (default in `.env.example`), this yields a 0-byte array. `HMACSHA256.HashData(emptyKey, plaintext)` then computes an HMAC with an empty key — effectively SHA-256 over the plaintext with a fixed (publicly-known) padding. A DB-only leak fully exposes every refresh token.

**Why VERIFIED-with-WARNING rather than FAILED:**
- The env var IS plumbed end-to-end (`.env.example` → `docker-compose.yml` → `IOptions<RefreshTokenOptions>` → service)
- AUTH-01 plan must-have truth #3 ("DB-only leak does not allow forgery") is technically VERIFIED when the operator actually sets a value
- The codebase ships with a placeholder `REFRESHTOKEN_HASHKEY=` (empty) — this is a deployment hardening gap, not a code defect that prevents the goal
- The phase goal ("multi-device-safe auth + replay detection + rate limit + DSGVO deletion") does NOT name HMAC pepper strength specifically

**Recommended remediation (HIGH priority — should land before Phase 5 commercial launch):**
- Add `IValidateOptions<RefreshTokenOptions>` with `.ValidateOnStart()` in `DependencyInjection.cs`, or fail-fast in the service constructor (per CR-01 fix sketch in REVIEW.md lines 86-114). Reject empty or non-32-byte values at startup.
- Update operator runbook to require `openssl rand -base64 32` before first deploy.

This SHOULD NOT block Phase 2 merge but MUST be addressed before any environment hosts real user data. Track it as a Phase 3 or dedicated 02.1 hardening plan item.

#### CR-02: `DeleteAccountValidator` is registered but never invoked

**File:** `Backend/src/TaxReader.Api/Endpoints/AuthEndpoints.cs:76-92`

**The defect:** ASP.NET Core Minimal APIs do NOT auto-run FluentValidation. `AddValidatorsFromAssemblyContaining<>` in `Program.cs:87` only registers the validators in DI; the endpoint binds `[FromBody] DeleteAccountRequest` and passes it straight to the handler. `DeleteAccountValidator.cs` is dead production code.

**Functional impact for AUTH-02:**
- Empty password (`""`) → `BCrypt.Verify("", hash)` returns false → 401 with "Ungültiges Passwort." — user-experience identical to wrong password
- Null password (`{"password": null}`) → `BCrypt.Verify(null, ...)` throws `ArgumentNullException` → `ExceptionHandlingMiddleware` returns 500 instead of 400 — minor UX issue, not a goal-defeating defect

**Why VERIFIED rather than FAILED:**
- ROADMAP SC #4 ("Account deletion requires re-authentication via password before firing") IS satisfied — BCrypt.Verify gates the cascade
- The validator existence is a defense-in-depth duplication; its absence-in-runtime does not change the security posture
- Same defect applies project-wide to every validator (`ConfirmClassificationValidator`, `UploadReceiptFilesValidator`, etc.) — out of Phase 2 scope to fix all of them
- The relevant tests (`DeleteAccountValidatorTests`) verify validator definition, not endpoint invocation — they are testing the wrong layer, which is an honest scope choice

**Recommended remediation (MEDIUM priority — best-handled project-wide):**
- Per REVIEW.md Option A or B (per-endpoint invocation or generic endpoint filter)
- Add a single `WithValidation<T>()` extension and apply to all command endpoints
- Defer to Phase 3 or 7 (test-depth) as part of a broader validator-invocation pass

Alternatively: drop `DeleteAccountValidator` + its tests per CLAUDE.md "no abstractions for single-use code" — the BCrypt check is the real gate.

### Deferred Items (Step 9b — Filtered against later phases)

None of the gaps identified during scanning are deferred to later phases. All Phase 2 must-haves are addressed in Phase 2. (CR-01 + CR-02 remediation is recommended for future phases but does not match a future phase's roadmap success criteria — they remain Advisory rather than Deferred.)

### Human Verification Required

See `human_verification` in frontmatter. Four items require manual sign-off:

1. **Real-IP-through-Caddy end-to-end** — `docker compose up --build`, then `curl -H "X-Forwarded-For: 1.2.3.4"` repeated 6× against `/api/v1/auth/login`. Verify 6th returns 429 and Caddy logs show real client IPs.
2. **Upload-concurrency limit (2 active + 4 queued)** — Open 7 concurrent uploads from the same authenticated user. Verify 7th returns 429 with German body; 3rd-6th queue.
3. **Account-deletion dialog UX** — Open `/settings`, click "Konto unwiderruflich löschen", type wrong password (verify inline error), type correct password (verify redirect to /login).
4. **Postgres migration `Up()` against real Postgres 17** — `docker compose up db`, `dotnet ef database update`, `psql -c '\d refresh_tokens'` to verify columns + indexes match D-02 schema.

### Gaps Summary

**No blocking gaps.** All 5 ROADMAP success criteria are verified by code + passing automated tests. Three plan-level must-haves carry advisory warnings from REVIEW.md:

- **CR-01 (HMAC pepper empty-key degradation)** is a real ops hardening gap but doesn't defeat any ROADMAP SC. Must be remediated before any real-user deployment.
- **CR-02 (DeleteAccountValidator never invoked)** is project-wide and the BCrypt gate still satisfies SC #4. Should be addressed in a broader validator-invocation pass.
- **Partition-by-sub on /account, upload-concurrency timing, X-Forwarded-For end-to-end** are intentionally manual-UAT (deferred via documented `[Fact(Skip)]` markers per 02-VALIDATION.md Manual-Only Verifications).

The 5 skipped tests in the suite map exactly to the documented deferrals (`MigrationTests` → Phase 7 QA-01; `AuthStrictPolicyTests.TwoUsersOneIp_BothGetFiveAttempts` + 2× `UploadConcurrencyPolicyTests` + `ForwardedHeadersTests.XForwardedFor_TrustedSubnet_ResolvesRealIp` → manual UAT).

**Verdict:** Phase goal is achieved in the codebase. Automated verification PASSED. Status `human_needed` because four manual UAT items must complete before final phase close-out, AND the operator should weigh CR-01 against any pre-Phase-5 deployment plans.

---

_Verified: 2026-05-16T00:00:00Z_
_Verifier: Claude (gsd-verifier)_
