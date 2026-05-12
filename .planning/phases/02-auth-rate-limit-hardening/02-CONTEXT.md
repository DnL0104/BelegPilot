# Phase 2: Auth + Rate-Limit Hardening - Context

**Gathered:** 2026-05-12
**Status:** Ready for planning

<domain>
## Phase Boundary

Replace the single-column `users.refresh_token` model with a `refresh_tokens` table that supports multi-device sessions, rotation, and replay detection that revokes all the user's tokens on collision; add ASP.NET Core `AddRateLimiter` policies (login/register/refresh/upload + global) with German 429 responses; add password re-authentication to the existing account-deletion flow.

In scope: `refresh_tokens` table + `RefreshTokenService` + EF migration that drops the legacy columns; rate-limit pipeline (incl. `UseForwardedHeaders` for behind-Caddy IP resolution); 429 response shape (German body + `Retry-After`); account-deletion password-reverify (DTO + handler + frontend dialog change).

Out of scope (later phases own these):
- Hangfire installation + recurring cleanup of expired refresh tokens — Phase 3 (PIPE-01)
- Active-sessions UI (`GET /auth/sessions`, "log out everywhere" button) — defer to Phase 6/7 or v2
- Audit-log entries for account deletion + refresh-token revocation — Phase 6 (LEG-08)
- Email notification on replay detection — requires SMTP/Resend/SES dependency not in stack
- Stripe / payment-related rate limits (e.g. `/webhooks/stripe`) — Phase 5
- Cookie-banner-gated consent for any new client-side state — Phase 6 (LEG-05)

</domain>

<decisions>
## Implementation Decisions

### Refresh-token storage & rotation (AUTH-01)
- **D-01:** Hash algorithm = **HMAC-SHA256 with server-side pepper**. Add a new `RefreshToken__HashKey` env var (256-bit, generated like `Jwt__Secret`) bound via `RefreshTokenOptions` (same `IOptions<T>` pattern as `JwtOptions`/`AnthropicOptions`). The plaintext token is HMAC-keyed before storage. A DB-only leak does not enable token forgery. Rotating the pepper invalidates all existing sessions — acceptable trade-off documented in operations notes.
- **D-02:** `refresh_tokens` schema columns (snake_case via `EFCore.NamingConventions`):
  - `id` (PK, gen_random_uuid())
  - `user_id` (FK → users, ON DELETE CASCADE)
  - `token_hash` (HMAC-SHA256 result, fixed length, indexed for O(1) lookup)
  - `created_at` (UTC)
  - `expires_at` (UTC, default `now() + JwtOptions.RefreshTokenExpirationDays`)
  - `revoked_at` (nullable UTC)
  - `last_used_at` (nullable UTC, updated on every successful refresh)
  - `user_agent` (varchar(500), nullable, captured from request)
  - `ip_address` (inet, nullable, captured from forwarded-headers-resolved client IP)
  - `replaced_by_token_id` (nullable FK → refresh_tokens.id, set on rotation)
  - Index: `(user_id, revoked_at)` to support "revoke all for user" queries and active-session listing
- **D-03:** Replay detection scope = **revoke ALL of the user's tokens** (per AUTH-01 success-criterion #2). When a refresh attempt resolves to a row where `revoked_at IS NOT NULL` (i.e. an already-rotated token is being replayed), `RefreshTokenService` issues a single `UPDATE refresh_tokens SET revoked_at = now() WHERE user_id = $1 AND revoked_at IS NULL` and returns the failure. Log at `Warning` level with `Sentry.CaptureMessage("Refresh token replay detected", SentryLevel.Warning)` plus an `Extra` of `user.id_hash` (Sentry PII allow-list from Phase 1 D-14 already permits `user.id_hash`).
- **D-04:** Replay surface to user = **silent revoke + generic 401 → /login**. The frontend's existing axios refresh-interceptor (`api-client.ts:41-73`) will see a 401 on `/auth/refresh` and bounce to `/login` like any normal session expiry. No special error code, no German flash, no email notification. Reason: surfacing "replay detected" to the response leaks detection-fired signal to a real attacker; the legitimate user is one re-login away from a working state.

### Rate-limit pipeline (AUTH-03)
- **D-05:** `/auth/refresh` partitioning = **per source IP, 30 req/min fixed-window**. The endpoint is `.AllowAnonymous()` so no JWT identity is available at limiter-time. Per-IP is stateless, doesn't need a DB lookup, and matches the spec's "doesn't lock out legitimate token rotation" intent. Two NAT'd users sharing a bucket is acceptable at the 100–500 paying-user target.
- **D-06:** Real-client-IP resolution = **`UseForwardedHeaders` trusting the Docker subnet**. Add `app.UseForwardedHeaders(new ForwardedHeadersOptions { ForwardedHeaders = ForwardedHeaders.XForwardedFor })` FIRST in the middleware pipeline (before `UseSerilogRequestLogging` and `UseAuthentication`). Configure `KnownNetworks.Add(new IPNetwork(IPAddress.Parse("172.16.0.0"), 12))` to trust only the Docker bridge networks. Caddy already sets `X-Forwarded-For` by default; the Caddyfile does not need changes. Safe because the API port (`api:8080`) is internal-only in `docker-compose.yml` — no host port exposed.
- **D-07:** `/receipt-files` upload concurrency policy = **concurrency=2 + QueueLimit=4 + `QueueProcessingOrder.OldestFirst` + ~30s queue wait, then 429**. Legitimate users double-clicking the upload button get queued rather than rejected; abuse stays bounded. Note: Phase 3 (PIPE-02) will replace the synchronous upload with a Hangfire job + 202 Accepted, retiring this concurrency limiter. This is interim hardening, scoped to disappear in ~6 weeks.
- **D-08:** Rate-limit response shape (applies uniformly across all policies):
  - HTTP 429 with `Content-Type: application/problem+json`
  - `Retry-After` header in seconds (computed from the limiter's `RetryAfter` metadata)
  - Body = `ProblemDetails` JSON with German `title` ("Zu viele Anfragen.") and `detail` (policy-specific German copy: "Bitte versuchen Sie es in {N} Sekunden erneut.")
  - Hooked via `RateLimiterOptions.OnRejected` in `Program.cs`. No `X-RateLimit-Remaining` / `X-RateLimit-Reset` informational headers — `Retry-After` alone is enough for the target scale.
- **D-09 (spec-locked, no choice — surfaced for downstream agents):**
  - `/auth/login`, `/auth/register`: fixed-window 5 req/min per source IP (uses the same forwarded-headers-resolved IP as D-06)
  - Global policy: fixed-window 60 req/min per source IP (catches generic abuse below the per-endpoint quotas)
  - Authenticated endpoints not explicitly listed inherit the global IP-based limit only; per-user concurrency limits are explicit (`/receipt-files`).

### Account-deletion re-auth (AUTH-02)
- **D-10:** Re-auth method = **password reverify in the `DELETE /auth/account` request body**. New `DeleteAccountRequest(string Password)` DTO. Backend `BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash)` before issuing the cascade-delete. Replaces the existing typed-CONFIRM_PHRASE pattern (which the frontend already has the dialog state machine for — we swap the input, not rewrite the dialog).
- **D-11:** Frontend dialog change (`Frontend/src/app/(authenticated)/settings/page.tsx:206-258`):
  - Replace the `Input` bound to `confirmInput` with a password-type input bound to `password` state
  - Replace the "Gib SUPER LÖSCHEN ein" copy with "Geben Sie zur Bestätigung Ihr Passwort ein."
  - Keep the irreversibility-warning paragraph and the destructive-styled "Konto löschen" button
  - Disable button until `password.length >= 1` (no min-length check on the client — the server is the authority)
  - On 401, surface inline German error under the input ("Ungültiges Passwort.") without closing the dialog
- **D-12:** Wrong-password handling = **401 with `{ error: "Ungültiges Passwort." }`** rendered inline; dialog stays open. The endpoint also joins AUTH-03's brute-force-resistant set: same fixed-window 5/min as `/auth/login`, partitioned by the authenticated user's `sub` claim (we have the access token here, unlike `/auth/refresh`). Hand off to `RequireRateLimiting("auth-strict")` policy name.
- **D-13:** Pre-deletion order of operations in `DeleteAccountHandler`:
  1. Verify password (`BCrypt.Verify`); if false → return `Result<bool>.Failure("Ungültiges Passwort.")` mapped to 401
  2. Revoke all of the user's refresh tokens (`UPDATE refresh_tokens SET revoked_at = now() WHERE user_id = $1 AND revoked_at IS NULL`)
  3. `dbContext.Users.Remove(user)` + `SaveChangesAsync` — CASCADE drops `refresh_tokens`, `receipt_files`, etc. The pre-revoke step is defense-in-depth: if the access token is somehow held by a second device, refresh attempts after this point fail. Access tokens still live up to 60 min by design (out of scope to short-circuit).
- **D-14:** Frontend post-success behavior = unchanged. `logout()` (which clears localStorage + redirects to `/login`) fires after the 204; no German flash on `/login`. Reason: user just typed their password to confirm; they know they did it.

### Migration & user-session impact
- **D-15:** Migration shape = **single EF migration**, name `AddRefreshTokensTable_DropLegacyRefreshTokenColumns`:
  - `CREATE TABLE refresh_tokens (...)` per D-02 schema
  - `ALTER TABLE users DROP COLUMN refresh_token, DROP COLUMN refresh_token_expires_at`
  - Update `UserConfiguration.cs` to remove the column mappings; remove `RefreshToken` / `RefreshTokenExpiresAt` properties from `User` entity
  - Pre-launch milestone, the only victims of the forced re-login are dev/test users. Down-migration restores the legacy columns (without data — the plaintext tokens are not preserved through the table-creation step).
- **D-16:** Expired-token cleanup = **defer to Phase 3 (PIPE-01)**. Phase 3's success-criterion already lists "recurring cleanup jobs registered (expired refresh tokens, abandoned Failed jobs)". Table growth between Phase 2 and Phase 3 (~4–6 weeks at 100–500 user target with 30-day TTL) is on the order of low thousands of rows — non-issue for Postgres. CONTEXT.md and PLAN.md both note this dependency so PIPE-01's planner picks it up.

### Claude's Discretion
- Exact `RefreshTokenService` API surface (likely: `IssueAsync(userId, ua, ip, ct)` returning `(string plaintextToken, RefreshToken row)`, `ValidateAndRotateAsync(plaintextToken, ua, ip, ct)`, `RevokeAllForUserAsync(userId, ct)`)
- `OnRejected` callback implementation details (Stream-write vs `Results.Problem` — likely Stream-write since we're inside the limiter middleware, not an endpoint)
- ProblemDetails extension fields beyond `title`/`detail`/`status` (probably mirror `retryAfter` in extensions as a convenience for the frontend toast)
- Whether to bump BCrypt work factor from 10 → 12 — leave at 10 unless a security review flags it
- Whether to use `app.UseRateLimiter()` middleware-attached OR `.RequireRateLimiting("policy-name")` per-endpoint — likely per-endpoint for clarity (auth/login, auth/register, auth/refresh, account-delete, receipt-files; global as default)
- Frontend axios `deleteAccount` signature: pass `password` as request body via the existing axios instance; how to surface the 401 inline without changing the toast pattern
- The exact Caddy KnownNetworks list — `172.16.0.0/12` covers Docker's default bridge ranges; if compose uses a custom network the planner verifies
- Whether to log a structured event on every successful refresh-token rotation (probably yes at Information level so Sentry's sustained-rate alert can baseline)

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Project context
- `.planning/PROJECT.md` — Vision, Core Value, constraints (3-month timeline, solo dev, scale 100–500 users), Out of Scope boundary
- `.planning/REQUIREMENTS.md` — AUTH-01, AUTH-02, AUTH-03 are this phase's deliverables; full text under "Authentication & Rate Limiting"; traceability table at the bottom
- `.planning/ROADMAP.md` — Phase 2 entry with 5 success criteria and 3 plan stubs

### Codebase intel
- `.planning/codebase/CONCERNS.md` — #10 (refresh-token single-column = multi-device-unsafe), #13 (no rate limiting) are the concerns this phase closes
- `.planning/codebase/INTEGRATIONS.md` — JWT bearer config baseline (60-min access, 30-day refresh, HmacSha256), Postgres + EF Core migration history, Caddy reverse-proxy posture
- `.planning/codebase/ARCHITECTURE.md` — Layer rules (Domain has zero deps; Application defines interfaces; Infrastructure implements; API thin); `Result<T>` pattern; `ICurrentUser` abstraction; per-user data scoping idiom
- `.planning/codebase/CONVENTIONS.md` — File-scoped namespaces, primary-constructor DI, `Result<T>` for errors, `Async` suffix, structured-logging named-placeholder rule, German user-facing copy (`Sie`-form)

### Prior-phase context (carries forward)
- `.planning/phases/01-foundation-cleanup-ci/01-CONTEXT.md` — Sentry pipeline (replay-detected events use `Sentry.CaptureMessage` with the existing PII allow-list), Serilog enrichers + `LogContext.PushProperty` pattern (replay-detection logs push `UserId`), CORS deny-all default still applies to new endpoints

### Files this phase will touch (read before editing)
- `Backend/src/TaxReader.Infrastructure/Services/AuthService.cs` — gut the direct `user.RefreshToken =` writes; delegate to `RefreshTokenService`
- `Backend/src/TaxReader.Infrastructure/Services/RefreshTokenService.cs` — NEW; implements `IRefreshTokenService` from Application
- `Backend/src/TaxReader.Application/Interfaces/IRefreshTokenService.cs` — NEW interface; methods per D-03's API surface
- `Backend/src/TaxReader.Domain/Entities/User.cs` — remove `RefreshToken` + `RefreshTokenExpiresAt` properties; add `ICollection<RefreshToken> RefreshTokens` nav
- `Backend/src/TaxReader.Domain/Entities/RefreshToken.cs` — NEW entity per D-02 schema
- `Backend/src/TaxReader.Infrastructure/Data/Configurations/UserConfiguration.cs` — drop column mappings; add nav config
- `Backend/src/TaxReader.Infrastructure/Data/Configurations/RefreshTokenConfiguration.cs` — NEW; index on `(user_id, revoked_at)`; HMAC token_hash column
- `Backend/src/TaxReader.Infrastructure/Data/AppDbContext.cs` — add `DbSet<RefreshToken> RefreshTokens`
- `Backend/src/TaxReader.Application/Interfaces/IAppDbContext.cs` — expose `DbSet<RefreshToken> RefreshTokens`
- `Backend/src/TaxReader.Infrastructure/Migrations/` — NEW migration `AddRefreshTokensTable_DropLegacyRefreshTokenColumns`
- `Backend/src/TaxReader.Infrastructure/Configuration/RefreshTokenOptions.cs` — NEW; `SectionName = "RefreshToken"`, `HashKey` property
- `Backend/src/TaxReader.Infrastructure/Configuration/RateLimitOptions.cs` — NEW; per-policy windows + queue limits (if not hardcoded)
- `Backend/src/TaxReader.Api/Program.cs` — `UseForwardedHeaders` first; `AddRateLimiter` with named policies + `OnRejected`; `app.UseRateLimiter()` in pipeline order; register `IRefreshTokenService`
- `Backend/src/TaxReader.Api/Endpoints/AuthEndpoints.cs` — `.RequireRateLimiting("auth-strict")` on /login + /register + /account-delete; `.RequireRateLimiting("auth-refresh")` on /refresh
- `Backend/src/TaxReader.Api/Endpoints/ReceiptFileEndpoints.cs` — `.RequireRateLimiting("upload-concurrency")` on POST /receipt-files
- `Backend/src/TaxReader.Application/Commands/DeleteAccountHandler.cs` — accept password; verify BCrypt; revoke refresh tokens before delete
- `Backend/src/TaxReader.Application/DTOs/AuthDtos.cs` — add `DeleteAccountRequest(string Password)`
- `Backend/src/TaxReader.Application/Validators/DeleteAccountValidator.cs` — NEW; password non-empty
- `Frontend/src/app/(authenticated)/settings/page.tsx` — swap `confirmInput` for password input; inline 401 error
- `Frontend/src/lib/api-client.ts` — `deleteAccount(password: string)` signature
- `docker-compose.yml` — add `RefreshToken__HashKey` env to `api` service
- `.env.example` — add `REFRESHTOKEN_HASHKEY=` placeholder with generation hint
- `CLAUDE.md` — brief mention under Domain Terms / Architecture (refresh-tokens table; rate-limit policies)

### External docs (read during research)
- `https://learn.microsoft.com/en-us/aspnet/core/performance/rate-limit` — `AddRateLimiter`, `PartitionedRateLimiter.Create`, `OnRejected` callback, named policies
- `https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/proxy-load-balancer` — `UseForwardedHeaders`, `KnownNetworks` / `KnownProxies`, pipeline-order pitfalls
- `https://caddyserver.com/docs/caddyfile/directives/reverse_proxy` — Default forwarded-header set (`X-Forwarded-For`, `X-Forwarded-Proto`, `X-Forwarded-Host`)
- `https://cheatsheetseries.owasp.org/cheatsheets/JSON_Web_Token_for_Java_Cheat_Sheet.html` — Refresh-token rotation patterns, replay detection guidance (language-agnostic recommendations)
- `https://docs.sentry.io/platforms/dotnet/usage/` — `Sentry.CaptureMessage` API (replay-detection events)

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- **`IOptions<T>` config pattern** (`Backend/src/TaxReader.Infrastructure/Configuration/JwtOptions.cs`, `AnthropicOptions.cs`): template for the new `RefreshTokenOptions` (`SectionName = "RefreshToken"`, single `HashKey` property) and `RateLimitOptions` if windows/limits are made configurable rather than hardcoded.
- **`Result<T>` pattern** (`Backend/src/TaxReader.Domain/Common/Result.cs`): every new `RefreshTokenService` method and the updated `DeleteAccountHandler` return `Result<T>`. Endpoints translate `IsSuccess` → 200/204; `IsFailure` → 401 (wrong password, replay detected) / 400.
- **`ICurrentUser`** (`Backend/src/TaxReader.Application/Interfaces/ICurrentUser.cs`): already injected into `DeleteAccountHandler`; provides `UserId` for the password-fetch + token-revoke steps. Account-delete rate-limiter partitions on `currentUser.UserId` since this endpoint is authenticated.
- **`BCrypt.Net.BCrypt.Verify`** (`AuthService.cs:94`): identical reverify call for D-10 — reuse the exact pattern from `LoginAsync`.
- **`AppDbContext` per-entity configuration pattern** (`Backend/src/TaxReader.Infrastructure/Data/Configurations/UserConfiguration.cs`): new `RefreshTokenConfiguration.cs` follows the same `IEntityTypeConfiguration<T>` shape; auto-discovered by `ApplyConfigurationsFromAssembly`.
- **Snake-case naming via `UseSnakeCaseNamingConvention`** (`Backend/src/TaxReader.Infrastructure/DependencyInjection.cs:22`): `RefreshToken` entity → `refresh_tokens` table automatically; no manual `ToTable` needed for case.
- **`LogContext.PushProperty` scope from Phase 1 OBS-02**: wrap `RefreshTokenService.ValidateAndRotateAsync` body with `using (LogContext.PushProperty("UserId", userId))` so replay-detection logs are correlated.
- **Sentry PII allow-list from Phase 1 D-14**: `user.id_hash` is already permitted; replay-detection events use `Sentry.CaptureMessage(..., scope => scope.SetExtra("user.id_hash", HashUserId(userId)))`.
- **Frontend axios refresh-interceptor** (`Frontend/src/lib/api-client.ts:41-73`): existing 401 → in-flight-shared refresh → retry pattern handles replay-revocation transparently; no changes needed (D-04 silent posture).
- **Frontend delete-account dialog state machine** (`Frontend/src/app/(authenticated)/settings/page.tsx:206-258`): keep the open/close logic + irreversibility warning; swap only the input + the call body.

### Established Patterns
- **`__`-nested env vars** (`Anthropic__Model`, `Jwt__Secret`): `RefreshToken__HashKey` follows; loads into `RefreshTokenOptions.HashKey`.
- **Central Package Management** (`Backend/Directory.Packages.props`): no new packages expected — `Microsoft.AspNetCore.RateLimiting` is built into .NET 10; `System.Security.Cryptography.HMACSHA256` is in `System.Security.Cryptography`. Forwarded-headers middleware is built into `Microsoft.AspNetCore.HttpOverrides` (already pulled in by ASP.NET Core).
- **Per-user data scoping in handlers**: `RefreshTokenService` methods always filter by `userId`; never trust client-supplied user identifiers.
- **German user-facing strings in `Result<T>.Failure`**: extend to the rate-limit 429 ProblemDetails body and the wrong-password 401 response.
- **`.RequireAuthorization()` group default + `.AllowAnonymous()` opt-out** (`Program.cs:182`): rate-limit policies attach orthogonally via `.RequireRateLimiting("policy-name")` — both can apply to the same endpoint.

### Integration Points
- **Pipeline order in `Program.cs` (critical)**:
  1. `app.UseForwardedHeaders(...)` ← FIRST (before any IP-reading middleware)
  2. `app.UseMiddleware<ExceptionHandlingMiddleware>()` (existing)
  3. `app.UseCors()` (existing)
  4. `app.UseSerilogRequestLogging()` (existing)
  5. `app.UseRateLimiter()` ← NEW (after request logging so 429s are logged, before auth so unauthenticated rate-limits fire first)
  6. `app.UseAuthentication()` (existing)
  7. `app.UseAuthorization()` (existing)
- **Endpoint policy attachment**: chain `.RequireRateLimiting("policy-name")` on `MapPost`/`MapDelete` after `.AllowAnonymous()` or `.WithName(...)` — order is forgiving.
- **`AuthService` → `RefreshTokenService` refactor**: every `user.RefreshToken = newRefreshToken` line becomes a `await _refreshTokens.IssueAsync(user.Id, ua, ip, ct)` call; `RefreshAsync` becomes `var result = await _refreshTokens.ValidateAndRotateAsync(token, ua, ip, ct)` returning the user_id + new plaintext. `user_agent` and `ip_address` come from `IHttpContextAccessor.HttpContext.Request` — `AuthService` either takes `ICurrentUser`-style context or accepts them as parameters from the endpoint.
- **`DeleteAccountHandler` signature change**: `HandleAsync(DeleteAccountRequest request, CancellationToken ct)`. Wire endpoint to bind the JSON body.
- **`AppDbContext.OnModelCreating`**: no change beyond `ApplyConfigurationsFromAssembly` picking up `RefreshTokenConfiguration` automatically.
- **EF migration command** (from `CLAUDE.md`): `dotnet ef migrations add AddRefreshTokensTable_DropLegacyRefreshTokenColumns -p Backend/src/TaxReader.Infrastructure -s Backend/src/TaxReader.Api`.
- **Frontend `deleteAccount` axios call**: currently `axios.delete('/auth/account')`; change to `axios.delete('/auth/account', { data: { password } })`. Axios serialises `data` on DELETE if explicitly passed.
- **`docker-compose.yml` `api` service**: add `RefreshToken__HashKey: ${REFRESHTOKEN_HASHKEY}` next to `Jwt__Secret`. `.env.example` adds a placeholder with a generation hint (`openssl rand -base64 32`).

</code_context>

<specifics>
## Specific Ideas

- **Pre-launch milestone, force-re-login is free.** The dev's own active session is the only victim of the column-drop migration. No production users exist yet (Phase 5 = payments). This made the "drop legacy columns in same migration" choice cleanly correct.
- **HMAC pepper-not-just-SHA-256 reflects DSGVO posture more than threat realism.** A 64-byte random token is not rainbow-table attackable. But "the DB and the app secrets are stored separately" is a common DPIA / penetration-test talking point that HMAC visibly satisfies. The cost is one env var and one secret to rotate-in-an-emergency.
- **Behind-Caddy IP resolution is the silent-killer concern.** If we don't wire `UseForwardedHeaders`, every IP-partitioned rate limit treats the entire internet as one bucket (the Caddy container's docker-internal IP). The rate limiter would "work" — return 429 sometimes — but be useless. Pipeline-order matters; this goes FIRST.
- **The frontend's existing axios refresh-interceptor already handles the replay-revoke scenario.** A 401 on `/auth/refresh` triggers the same fallback-to-login that a normal expiry triggers. D-04's "silent" posture is essentially "ship the existing UX without changes."
- **Phase 3 (Hangfire) retires the upload-concurrency limiter, not Phase 2.** PIPE-02's 202-Accepted + background job pattern means concurrent uploads become a queue depth concern, not a request-pipeline concern. The concurrency-2 + QueueLimit-4 policy is interim hardening with a known sunset date.
- **The account-deletion endpoint joins `/auth/login` in the brute-force-resistant bucket** even though it's authenticated. Reasoning: an attacker who has stolen an access token can probe the password through this endpoint. Same 5/min fixed-window applies, but partitioned by user_id since the access token is present.

</specifics>

<deferred>
## Deferred Ideas

- **Active-sessions UI** (`GET /auth/sessions` returning the user's non-revoked rows; `DELETE /auth/sessions/{id}` for "log out this device"; `DELETE /auth/sessions` for "log out everywhere") — natural follow-up given we're recording `user_agent` + `ip_address` per token. Defer to Phase 6/7 or v2; not load-bearing for AUTH-01 success criteria.
- **Email notification on replay detection** ("Verdächtige Aktivität — Sie wurden auf allen Geräten abgemeldet") — requires an email-sending dependency (SMTP / Resend / SES) that doesn't exist in the stack. Phase 6 LEG-07's "data export emailed within 24h" forces email infrastructure into the project at that point; revisit the replay-notification surface then.
- **Audit-log entries for account deletion + refresh-token revocation events** — Phase 6 (LEG-08) owns `audit_log` + `AuditLogger`. Phase 2 logs via Serilog + Sentry; Phase 6 retroactively wires `AuditLogger.Log("account.deleted", userId, ...)` and `AuditLogger.Log("refresh_token.revoke_all", userId, reason)` into these flows.
- **Pepper rotation procedure** for `RefreshToken__HashKey` — operational runbook item: "rotating this env var invalidates all existing refresh tokens, forces re-login." Document alongside `Jwt__Secret` in an OPERATIONS.md (out of scope for this phase; no runbook structure exists yet).
- **BCrypt work-factor tuning** (currently library default 10) — a security review may want to bump to 12. Not load-bearing for AUTH-* success criteria; leave at 10 in Phase 2.
- **`/webhooks/stripe` rate limit** — Phase 5 (PAY-01) owns Stripe wiring; the webhook endpoint needs its own policy (likely high-volume per-IP since legitimate webhooks burst).
- **W3C `traceparent` browser → backend trace propagation** — already deferred in Phase 1; revisit when frontend Sentry goes live (Phase 6).
- **CORS deny-all default for new endpoints** — already covered by Phase 1 D-07. Phase 2 endpoints inherit the project policy.
- **Refresh-token pepper stored in a secret manager** (HashiCorp Vault, AWS Secrets Manager, etc.) rather than env var — out of scope for self-hosted Docker Compose; revisit only if the hosting model changes.
- **Per-route concurrency limit on `POST /receipts/{id}/reclassify`** (also AI-call-heavy) — current AUTH-03 scope is `/receipt-files` only. Reclassify is per-receipt and lower-volume; AUTH-03 inheritance via the global 60/min IP limit is adequate. Revisit if reclassify abuse is observed post-launch.
- **Detection of password reuse across users** (e.g. checking against the haveibeenpwned API on register/change-password) — DSGVO complication (sending hashes to a third party), bigger feature; backlog.
- **OAuth / social login (Google, Apple)** — out of scope for v1; PROJECT.md "single user per account" / "email/password" path is locked.

</deferred>

---

*Phase: 02-auth-rate-limit-hardening*
*Context gathered: 2026-05-12*
