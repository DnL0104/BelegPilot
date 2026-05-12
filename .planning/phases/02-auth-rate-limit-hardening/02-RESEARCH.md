# Phase 2: Auth + Rate-Limit Hardening - Research

**Researched:** 2026-05-12
**Domain:** ASP.NET Core 10 auth hardening — refresh-token rotation, rate limiting behind a reverse proxy, account-deletion re-auth
**Confidence:** HIGH

## Summary

This phase replaces a single-column refresh-token model with a hashed-row `refresh_tokens` table, adds `Microsoft.AspNetCore.RateLimiting` policies that survive being behind Caddy, and gates account deletion behind password re-verification. All sixteen implementation decisions in `02-CONTEXT.md` are LOCKED — research focused on the specific .NET-10 API shapes the planner needs.

Three implementation traps surfaced that the planner must address:

1. **`KnownNetworks` is OBSOLETE in .NET 10.** `02-CONTEXT.md` D-06 refers to `ForwardedHeadersOptions.KnownNetworks` — that property generates `ASPDEPR005` at compile time. The correct .NET 10 property is `KnownIPNetworks`, and the type used is `System.Net.IPNetwork` (not `Microsoft.AspNetCore.HttpOverrides.IPNetwork`). `System.Net.IPNetwork.Parse("172.16.0.0/12")` is the idiomatic call. [VERIFIED: learn.microsoft.com/en-us/aspnet/core/breaking-changes/10/ipnetwork-knownnetworks-obsolete]
2. **Caddy `trusted_proxies` is not configured in `Caddyfile`** but the current setup is fine because Caddy is the FIRST hop from the public internet — it sees the real client IP at the TCP layer and writes it to `X-Forwarded-For` automatically. `trusted_proxies` only matters when another proxy sits IN FRONT of Caddy. The Caddyfile needs no changes. [VERIFIED: caddyserver.com/docs/caddyfile/directives/reverse_proxy]
3. **Refresh token rotation requires `IHttpContextAccessor`** to capture user-agent and resolved IP. `AuthService` already uses constructor DI for `IOptions<JwtOptions>` — the planner should let `AuthEndpoints.cs` extract the values from `HttpContext` and pass them as method parameters into `RefreshTokenService.IssueAsync(userId, userAgent, ipAddress, ct)` to keep `AuthService` HTTP-context-free.

**Primary recommendation:** Use built-in `Microsoft.AspNetCore.RateLimiting` (zero new packages), `HMACSHA256.HashData(key, plaintext)` static API for token hashing (zero allocations), `ExecuteUpdateAsync` for the replay-revoke `UPDATE` (single round-trip, no entity tracking), and a single EF migration that issues `CREATE TABLE refresh_tokens` BEFORE `ALTER TABLE users DROP COLUMN`. The pipeline-order critical-path is `UseForwardedHeaders` → existing middleware → `UseRateLimiter` (inserted between `UseSerilogRequestLogging` and `UseAuthentication`).

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

**Refresh-token storage & rotation (AUTH-01)**

- **D-01:** Hash algorithm = HMAC-SHA256 with server-side pepper. Add `RefreshToken__HashKey` env var (256-bit), bound via `RefreshTokenOptions` (same `IOptions<T>` pattern as `JwtOptions`/`AnthropicOptions`). Plaintext token is HMAC-keyed before storage. Rotating the pepper invalidates all sessions — documented in operations notes.
- **D-02:** `refresh_tokens` schema columns (snake_case via `EFCore.NamingConventions`):
  - `id` (PK, `gen_random_uuid()`)
  - `user_id` (FK → users, ON DELETE CASCADE)
  - `token_hash` (HMAC-SHA256 result, fixed length, indexed for O(1) lookup)
  - `created_at` (UTC)
  - `expires_at` (UTC, default `now() + JwtOptions.RefreshTokenExpirationDays`)
  - `revoked_at` (nullable UTC)
  - `last_used_at` (nullable UTC, updated on every successful refresh)
  - `user_agent` (varchar(500), nullable)
  - `ip_address` (inet, nullable)
  - `replaced_by_token_id` (nullable FK → refresh_tokens.id)
  - Index: `(user_id, revoked_at)` to support "revoke all for user" queries
- **D-03:** Replay detection scope = revoke ALL of the user's tokens. When a refresh attempt resolves to a row where `revoked_at IS NOT NULL`, `RefreshTokenService` issues `UPDATE refresh_tokens SET revoked_at = now() WHERE user_id = $1 AND revoked_at IS NULL`. Log at `Warning` with `Sentry.CaptureMessage("Refresh token replay detected", SentryLevel.Warning)` plus `Extra` of `user.id_hash`.
- **D-04:** Replay surface to user = silent revoke + generic 401 → /login. Frontend's existing axios refresh-interceptor handles this.

**Rate-limit pipeline (AUTH-03)**

- **D-05:** `/auth/refresh` = per source IP, 30 req/min fixed-window. Endpoint is `.AllowAnonymous()` so no JWT identity available.
- **D-06:** Real-client-IP resolution = `UseForwardedHeaders` trusting Docker subnet. Add `app.UseForwardedHeaders(...)` FIRST in the middleware pipeline. Configure to trust the Docker bridge networks (172.16.0.0/12). Caddy already sets `X-Forwarded-For` by default.
- **D-07:** `/receipt-files` upload concurrency = concurrency=2 + QueueLimit=4 + `QueueProcessingOrder.OldestFirst` + ~30s queue wait, then 429. Will be retired in Phase 3 (PIPE-02 = 202 Accepted + Hangfire).
- **D-08:** Rate-limit response shape (applies uniformly):
  - HTTP 429 with `Content-Type: application/problem+json`
  - `Retry-After` header in seconds (from limiter's `RetryAfter` metadata)
  - Body = `ProblemDetails` JSON with German `title` ("Zu viele Anfragen.") and `detail` ("Bitte versuchen Sie es in {N} Sekunden erneut.")
  - Hooked via `RateLimiterOptions.OnRejected` in `Program.cs`.
- **D-09:**
  - `/auth/login`, `/auth/register`: fixed-window 5 req/min per source IP
  - Global: fixed-window 60 req/min per source IP
  - Authenticated endpoints not explicitly listed inherit the global IP limit only; per-user concurrency is explicit (`/receipt-files`).

**Account-deletion re-auth (AUTH-02)**

- **D-10:** Re-auth method = password reverify in `DELETE /auth/account` request body. New `DeleteAccountRequest(string Password)` DTO. Backend `BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash)` before cascade-delete.
- **D-11:** Frontend dialog change in `Frontend/src/app/(authenticated)/settings/page.tsx:206-258`:
  - Replace `Input` bound to `confirmInput` with password-type input bound to `password` state
  - Replace "Gib SUPER LÖSCHEN ein" copy with "Geben Sie zur Bestätigung Ihr Passwort ein."
  - Keep irreversibility warning + destructive button styling
  - Disable button until `password.length >= 1`
  - On 401, surface inline German error ("Ungültiges Passwort.") without closing dialog
- **D-12:** Wrong-password = 401 with `{ error: "Ungültiges Passwort." }` rendered inline. Endpoint joins AUTH-03 brute-force-resistant set: 5/min fixed-window, partitioned by authenticated user's `sub` claim. Use `.RequireRateLimiting("auth-strict")` policy.
- **D-13:** Order of operations in `DeleteAccountHandler`:
  1. Verify password; if false → `Result<bool>.Failure("Ungültiges Passwort.")` → 401
  2. Revoke all refresh tokens (`ExecuteUpdateAsync`)
  3. `dbContext.Users.Remove(user)` + `SaveChangesAsync` — CASCADE drops everything else
- **D-14:** Frontend post-success = unchanged. `logout()` fires after 204; no German flash.

**Migration & user-session impact**

- **D-15:** Single EF migration `AddRefreshTokensTable_DropLegacyRefreshTokenColumns`:
  - `CREATE TABLE refresh_tokens (...)` per D-02
  - `ALTER TABLE users DROP COLUMN refresh_token, DROP COLUMN refresh_token_expires_at`
  - Update `UserConfiguration.cs` to remove column mappings; remove properties from `User` entity
  - Pre-launch: only dev/test users affected. Down-migration restores legacy columns (data not preserved).
- **D-16:** Expired-token cleanup deferred to Phase 3 (PIPE-01) Hangfire recurring job.

### Claude's Discretion

- Exact `RefreshTokenService` API surface (likely: `IssueAsync(userId, ua, ip, ct)` returning `(string plaintextToken, RefreshToken row)`, `ValidateAndRotateAsync(plaintextToken, ua, ip, ct)`, `RevokeAllForUserAsync(userId, ct)`)
- `OnRejected` callback implementation details (Stream-write vs `Results.Problem`)
- ProblemDetails extension fields beyond `title`/`detail`/`status` (probably mirror `retryAfter` in extensions)
- Whether to bump BCrypt work factor from 10 → 12 — leave at 10 unless flagged
- Whether to use `app.UseRateLimiter()` middleware-attached OR `.RequireRateLimiting("policy-name")` per-endpoint — likely per-endpoint for clarity
- Frontend axios `deleteAccount` signature
- Exact Caddy KnownNetworks list — `172.16.0.0/12` covers Docker's default bridge ranges
- Whether to log a structured event on every successful refresh-token rotation (probably yes at Information level)

### Deferred Ideas (OUT OF SCOPE)

- Active-sessions UI (`GET /auth/sessions`, "log out everywhere" button) — Phase 6/7 or v2
- Email notification on replay detection — requires SMTP/Resend/SES not in stack
- Audit-log entries for account deletion + refresh-token revocation — Phase 6 (LEG-08)
- Pepper rotation runbook — out of scope (no OPERATIONS.md exists yet)
- BCrypt work-factor tuning (10 → 12)
- `/webhooks/stripe` rate limit — Phase 5 (PAY-01)
- W3C `traceparent` browser → backend trace propagation — Phase 6/7
- Refresh-token pepper in secret manager (Vault, AWS Secrets Manager)
- Per-route concurrency on `POST /receipts/{id}/reclassify`
- haveibeenpwned password check on register/change-password
- OAuth / social login (Google, Apple)
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| AUTH-01 | `refresh_tokens` table with hash-only storage, multi-row per user, rotation on refresh, replay detection that revokes all tokens on collision; `RefreshTokenService` replacing `user.RefreshToken` column logic | HMAC-SHA256 pattern (Don't Hand-Roll table), EF migration combo pattern, `ExecuteUpdateAsync` for revoke-all (Code Examples), `IHttpContextAccessor` for ua/ip capture (Architecture Patterns) |
| AUTH-02 | Account-deletion confirmation modal — re-authentication required + irreversibility warning before `DELETE /auth/account` fires | BCrypt.Verify reuse from `AuthService.cs:94` (Reusable Assets), axios DELETE with body via `data` config (Code Examples), German error copy convention (`Ungültiges Passwort.`) |
| AUTH-03 | ASP.NET Core `AddRateLimiter` policies — fixed-window 5 req/min `/auth/login` + `/auth/register` per IP, 30 req/min `/auth/refresh`, concurrency-2 `/receipt-files`, global 60 req/min | `AddRateLimiter` syntax with named policies + `PartitionedRateLimiter.Create` (Code Examples), `OnRejected` callback writing `application/problem+json` + Retry-After header, `UseForwardedHeaders` with KnownIPNetworks (.NET 10 API), `WebApplicationFactory<Program>` test pattern from `CorsConfigurationTests` (Validation Architecture) |
</phase_requirements>

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Refresh-token persistence + hashing | API / Backend (Infrastructure) | — | Tokens are HMAC-keyed server-side with pepper from env; storage is Postgres; never touched by client |
| Refresh-token rotation logic | API / Backend (Application + Infrastructure) | — | `IRefreshTokenService` interface in Application; concrete `RefreshTokenService` in Infrastructure; `AuthService` orchestrates |
| Replay detection | API / Backend (Infrastructure) | — | DB-level invariant ("if you present a revoked token, all your tokens die") — must be server-side |
| Rate-limit enforcement | API / Backend (`Program.cs` pipeline) | — | Caddy does not rate-limit; only the API knows policy names and partition keys |
| Real client IP resolution | API / Backend (`UseForwardedHeaders`) | Edge (Caddy auto-injects `X-Forwarded-For`) | Caddy sets the header; the API trusts it because the Docker subnet is known |
| Account-deletion password gate | API / Backend (`DeleteAccountHandler`) | Browser / Client (dialog captures password) | Server is the authority on the password (BCrypt.Verify); UI only collects the input |
| Account-deletion dialog UX | Browser / Client (Next.js settings page) | — | Pure UI state — disabled-until-non-empty, inline 401 surface |
| 429 ProblemDetails localization | API / Backend (`OnRejected` callback) | — | German copy lives in the rate-limit middleware; ProblemDetails contract is server-emitted |

## Standard Stack

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| `Microsoft.AspNetCore.RateLimiting` | Built into .NET 10 ASP.NET Core (no PackageReference needed) | Rate-limit policies, partitioned limiters, `OnRejected` hook | First-party MS implementation; ships with the framework; supports fixed-window + concurrency + sliding-window + token-bucket out of the box [VERIFIED: learn.microsoft.com/en-us/aspnet/core/performance/rate-limit] |
| `Microsoft.AspNetCore.HttpOverrides` | Built into .NET 10 ASP.NET Core | `UseForwardedHeaders`, `ForwardedHeadersOptions`, `KnownIPNetworks` | First-party; required for trusting `X-Forwarded-For` from Caddy [VERIFIED: learn.microsoft.com/en-us/aspnet/core/host-and-deploy/proxy-load-balancer] |
| `System.Net.IPNetwork` | Built into .NET 10 `System.Net.Primitives` | CIDR representation for `KnownIPNetworks` | .NET 10 replacement for the obsolete `Microsoft.AspNetCore.HttpOverrides.IPNetwork`; supports `IPNetwork.Parse("172.16.0.0/12")` [VERIFIED: learn.microsoft.com/en-us/dotnet/api/system.net.ipnetwork] |
| `System.Security.Cryptography.HMACSHA256` | Built into .NET 10 | Pepper-keyed token hashing | Static `HashData(key, source)` API is zero-allocation, no instance to dispose, CA1850-compliant [VERIFIED: learn.microsoft.com/en-us/dotnet/api/system.security.cryptography.hmacsha256.hashdata] |
| `BCrypt.Net-Next` 4.0.3 | Already in `Directory.Packages.props:10` | Account-deletion password re-verify | Already used in `AuthService.cs:44, :94` — reuse the exact pattern [VERIFIED: codebase grep] |
| `Microsoft.EntityFrameworkCore` 10.0.4 | Already in `Directory.Packages.props:15` | `refresh_tokens` table, `ExecuteUpdateAsync` for revoke-all | Already wired into `IAppDbContext` pattern [VERIFIED: codebase grep] |
| `Npgsql.EntityFrameworkCore.PostgreSQL` 10.0.1 | Already in `Directory.Packages.props:20` | Postgres `inet` column type for `ip_address`, snake_case via `EFCore.NamingConventions` | Already configured [VERIFIED: codebase grep `DependencyInjection.cs:22`] |
| `FluentValidation` 12.0.0 | Already in `Directory.Packages.props:7` | `DeleteAccountValidator` for password non-empty | Existing pattern (e.g. `ConfirmClassificationValidator`) [VERIFIED: codebase grep] |

### Supporting
| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| `Sentry.AspNetCore` 6.4.1 | Already in `Directory.Packages.props:27` | `Sentry.CaptureMessage("Refresh token replay detected", SentryLevel.Warning)` on replay | Per D-03 + Phase 1 D-14 PII allow-list (`user.id_hash`) |
| `Serilog` 4.2.0 | Already in `Directory.Packages.props:28` | `LogContext.PushProperty("UserId", userId)` correlation scope on `ValidateAndRotateAsync` | Per Phase 1 OBS-02 pattern |
| `Microsoft.AspNetCore.Mvc.Testing` 10.0.4 | Already in `Directory.Packages.props:39` | `WebApplicationFactory<Program>` integration tests for rate-limit policies and 429 ProblemDetails | Established pattern in `CorsConfigurationTests.cs` |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| `Microsoft.AspNetCore.RateLimiting` | `AspNetCoreRateLimit` NuGet (Stefan Prodan) | The community package is well-known but unmaintained; .NET 7+ built-in middleware is now the standard. Skip. |
| `HMACSHA256.HashData` static | `new HMACSHA256(key); .ComputeHash(plaintext)` | Instance version requires `using` block, allocates, and CA1850 analyzer flags it [VERIFIED: learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/quality-rules/ca1850] |
| `ExecuteUpdateAsync` for revoke-all | Load entities + `SaveChangesAsync` | Loading rows + tracking + per-row UPDATE is wasteful when we just want one bulk `UPDATE`. The replay path runs once per attack — performance matters less than the operational simplicity of one SQL statement [VERIFIED: learn.microsoft.com/en-us/ef/core/saving/execute-insert-update-delete] |
| `IHttpContextAccessor` injected into `AuthService` | Pass `userAgent` + `ipAddress` as method parameters from endpoint | Keeping `AuthService` HTTP-context-free is the existing convention; the endpoint already has `HttpContext` and can extract values trivially |
| Per-endpoint rate-limit policy attachment | Single global limiter + endpoint metadata | Named policies + `.RequireRateLimiting("name")` make the policy/endpoint mapping inspectable in code (per D-09 vagueness around clarity); CONTEXT lists it as Claude's Discretion but the established pattern (`.AllowAnonymous()` chained per endpoint) strongly suggests per-endpoint attachment |

**Installation:**

No new PackageReferences needed — every primary library is already in `Directory.Packages.props`.

**Version verification (verified 2026-05-12):**

- `Microsoft.AspNetCore.RateLimiting` and `Microsoft.AspNetCore.HttpOverrides` are framework-resolved via `Microsoft.AspNetCore.App.Ref v10.0.0` — no explicit PackageReference needed in any project. [VERIFIED: dotnet --version → 10.0.201]
- `System.Net.IPNetwork` ships in `System.Net.Primitives.dll`, available since .NET 8. [VERIFIED: learn.microsoft.com/en-us/dotnet/api/system.net.ipnetwork]
- `BCrypt.Net-Next` 4.0.3 — already used at `AuthService.cs:44, :94`. No version bump needed. [VERIFIED: codebase grep]
- `Sentry.AspNetCore` 6.4.1 — already wired in `Program.cs:36-41` (Phase 1). [VERIFIED: codebase grep]

## Architecture Patterns

### System Architecture Diagram

```
                            ┌─────────────────┐
   Public Internet ─────────│ Caddy (TLS)     │  Sets X-Forwarded-For
                            │ on :443         │  with real client IP
                            └────────┬────────┘  (it's the first hop)
                                     │
                          ┌──────────▼──────────┐
                          │ Docker bridge       │  172.16.0.0/12 — trusted
                          │ network             │  by ForwardedHeaders middleware
                          └──────────┬──────────┘
                                     │
                          ┌──────────▼──────────────────────────────┐
                          │ Next.js (web:3000) — proxies /api/v1/*  │
                          │ to api:8080 via next.config.ts rewrites │
                          └──────────┬──────────────────────────────┘
                                     │
                                     ▼
   ╔═══════════════════ TaxReader.Api ═══════════════════════════════════╗
   ║                                                                     ║
   ║  1. UseForwardedHeaders ──► HttpContext.Connection.RemoteIpAddress  ║
   ║     (KnownIPNetworks={172.16.0.0/12}) is now the REAL client IP     ║
   ║                                                                     ║
   ║  2. UseMiddleware<ExceptionHandlingMiddleware> (existing)           ║
   ║  3. UseCors (existing)                                              ║
   ║  4. UseSerilogRequestLogging (existing)                             ║
   ║                                                                     ║
   ║  5. UseRateLimiter ──► policies (D-08 OnRejected returns 429+JSON): ║
   ║     • "auth-strict"      (5/min, partition: IP for /login,/register)║
   ║                          (partition: sub  for /account)             ║
   ║     • "auth-refresh"     (30/min, partition: IP)                    ║
   ║     • "upload-concurrency" (concurrency=2, queue=4)                 ║
   ║     • Global             (60/min, partition: IP)                    ║
   ║                                                                     ║
   ║  6. UseAuthentication (existing — JWT bearer)                       ║
   ║  7. UseAuthorization  (existing)                                    ║
   ║                                                                     ║
   ║  Endpoints (.RequireRateLimiting("policy-name") attached):          ║
   ║                                                                     ║
   ║    POST /auth/login           ──► "auth-strict"     ┌─────────────┐ ║
   ║    POST /auth/register        ──► "auth-strict"     │   Anthropic │ ║
   ║    POST /auth/refresh         ──► "auth-refresh"    │   Claude    │ ║
   ║    DELETE /auth/account       ──► "auth-strict"     │  (existing) │ ║
   ║    POST /receipt-files        ──► "upload-conc..."  └─────────────┘ ║
   ║                                                                     ║
   ║                       │                                             ║
   ║                       ▼                                             ║
   ║  IRefreshTokenService.{IssueAsync, ValidateAndRotateAsync,          ║
   ║                        RevokeAllForUserAsync}                       ║
   ║                       │                                             ║
   ║                       ▼                                             ║
   ║   HMACSHA256.HashData(pepper, plaintextToken) ───► token_hash       ║
   ║                       │                                             ║
   ║                       ▼                                             ║
   ║                  Postgres                                           ║
   ║   ┌─────────────────────────────────────────────────────────────┐   ║
   ║   │ refresh_tokens                                              │   ║
   ║   │   id PK | user_id FK→users | token_hash IDX | created_at    │   ║
   ║   │   expires_at | revoked_at | last_used_at | user_agent       │   ║
   ║   │   ip_address (inet) | replaced_by_token_id (self-FK)        │   ║
   ║   │   IDX: (user_id, revoked_at)                                │   ║
   ║   └─────────────────────────────────────────────────────────────┘   ║
   ║                                                                     ║
   ║   ┌─────────────────────────────────────────────────────────────┐   ║
   ║   │ users (after migration)                                     │   ║
   ║   │   refresh_token COLUMN DROPPED                              │   ║
   ║   │   refresh_token_expires_at COLUMN DROPPED                   │   ║
   ║   └─────────────────────────────────────────────────────────────┘   ║
   ║                                                                     ║
   ╚═════════════════════════════════════════════════════════════════════╝
```

### Component Responsibilities

| Component (file) | Responsibility |
|------------------|----------------|
| `RefreshTokenOptions.cs` (NEW, Infrastructure/Configuration) | `IOptions<T>` POCO; `SectionName = "RefreshToken"`; single `HashKey` property |
| `RateLimitOptions.cs` (NEW, Infrastructure/Configuration — optional) | `IOptions<T>` POCO if windows/limits become configurable instead of hardcoded |
| `RefreshToken.cs` (NEW, Domain/Entities) | Plain POCO per D-02 schema; `ICollection<RefreshToken> RefreshTokens` nav on `User` |
| `IRefreshTokenService.cs` (NEW, Application/Interfaces) | `IssueAsync`, `ValidateAndRotateAsync`, `RevokeAllForUserAsync` |
| `RefreshTokenService.cs` (NEW, Infrastructure/Services) | HMAC-hash plaintext, INSERT, rotation, ExecuteUpdateAsync revoke-all, Sentry on replay |
| `RefreshTokenConfiguration.cs` (NEW, Infrastructure/Data/Configurations) | `IEntityTypeConfiguration<RefreshToken>`; index `(user_id, revoked_at)`; auto-discovered by `ApplyConfigurationsFromAssembly` |
| `UserConfiguration.cs` (UPDATE, Infrastructure/Data/Configurations) | Drop column mappings for `refresh_token` / `refresh_token_expires_at`; add nav config for `RefreshTokens` |
| `User.cs` (UPDATE, Domain/Entities) | Remove `RefreshToken`, `RefreshTokenExpiresAt`; add `ICollection<RefreshToken> RefreshTokens` |
| `AppDbContext.cs` (UPDATE, Infrastructure/Data) | Add `DbSet<RefreshToken> RefreshTokens` |
| `IAppDbContext.cs` (UPDATE, Application/Interfaces) | Expose `DbSet<RefreshToken> RefreshTokens` |
| `AuthService.cs` (UPDATE, Infrastructure/Services) | Delegate `RefreshToken =` writes to `IRefreshTokenService`; remove direct `user.RefreshToken =` lines (74, 98, 120) |
| `AuthEndpoints.cs` (UPDATE, Api/Endpoints) | Extract `userAgent` + `ipAddress` from `HttpContext`; pass to `AuthService`; `.RequireRateLimiting("auth-strict")` on /login, /register; `.RequireRateLimiting("auth-refresh")` on /refresh; `.RequireRateLimiting("auth-strict")` on /account |
| `ReceiptFileEndpoints.cs` (UPDATE) | `.RequireRateLimiting("upload-concurrency")` on POST / |
| `DeleteAccountRequest` (NEW, Application/DTOs/AuthDtos.cs) | Record `DeleteAccountRequest(string Password)` |
| `DeleteAccountValidator.cs` (NEW, Application/Validators) | `RuleFor(x => x.Password).NotEmpty()` |
| `DeleteAccountHandler.cs` (UPDATE) | Accept `DeleteAccountRequest`; BCrypt.Verify; revoke refresh tokens; cascade delete |
| `AuthEndpoints.cs` MapDelete (UPDATE) | Bind JSON body to `DeleteAccountRequest`; map BCrypt-fail Result.Failure → 401 with `{ error: "Ungültiges Passwort." }` |
| `Migrations/AddRefreshTokensTable_DropLegacyRefreshTokenColumns.cs` (NEW) | CREATE TABLE first, then ALTER TABLE DROP COLUMNs |
| `Program.cs` (UPDATE) | `UseForwardedHeaders` FIRST in pipeline; `AddRateLimiter` config; `app.UseRateLimiter()` AFTER `UseSerilogRequestLogging`, BEFORE `UseAuthentication`; register `IRefreshTokenService` (Scoped); call `services.AddHttpContextAccessor()` (already there from Phase 1 D-15) |
| `Frontend/src/lib/api-client.ts` (UPDATE) | `deleteAccount(password: string)` signature; pass body via `axios.delete('/auth/account', { data: { password } })` |
| `Frontend/src/app/(authenticated)/settings/page.tsx` (UPDATE) | Swap CONFIRM_PHRASE input for password input; inline 401 error |
| `docker-compose.yml` (UPDATE) | Add `RefreshToken__HashKey: ${REFRESHTOKEN_HASHKEY}` next to `Jwt__Secret` |
| `.env.example` (UPDATE) | `REFRESHTOKEN_HASHKEY=` placeholder with `openssl rand -base64 32` hint |

### Recommended Project Structure

No structural change. New files follow existing conventions:

```
Backend/src/
├── TaxReader.Domain/Entities/
│   ├── User.cs                       # UPDATE (remove 2 props, add nav)
│   └── RefreshToken.cs               # NEW
├── TaxReader.Application/
│   ├── Interfaces/
│   │   ├── IAppDbContext.cs          # UPDATE (add DbSet)
│   │   └── IRefreshTokenService.cs   # NEW
│   ├── DTOs/AuthDtos.cs              # UPDATE (add DeleteAccountRequest)
│   ├── Commands/DeleteAccountHandler.cs  # UPDATE
│   └── Validators/DeleteAccountValidator.cs  # NEW
├── TaxReader.Infrastructure/
│   ├── Configuration/
│   │   ├── RefreshTokenOptions.cs    # NEW
│   │   └── RateLimitOptions.cs       # NEW (optional)
│   ├── Data/
│   │   ├── AppDbContext.cs           # UPDATE (DbSet)
│   │   └── Configurations/
│   │       ├── UserConfiguration.cs  # UPDATE
│   │       └── RefreshTokenConfiguration.cs  # NEW
│   ├── Migrations/
│   │   └── 2026...AddRefreshTokensTable_DropLegacyRefreshTokenColumns.cs  # NEW
│   └── Services/
│       ├── AuthService.cs            # UPDATE (delegate to RefreshTokenService)
│       └── RefreshTokenService.cs    # NEW
└── TaxReader.Api/
    ├── Endpoints/
    │   ├── AuthEndpoints.cs          # UPDATE
    │   └── ReceiptFileEndpoints.cs   # UPDATE
    └── Program.cs                    # UPDATE (forwarded headers + rate limiter)

Backend/tests/TaxReader.UnitTests/
├── RateLimiting/                     # NEW
│   ├── AuthStrictPolicyTests.cs      # NEW
│   ├── AuthRefreshPolicyTests.cs     # NEW
│   ├── UploadConcurrencyPolicyTests.cs # NEW
│   ├── GlobalPolicyTests.cs          # NEW
│   ├── RejectedResponseShapeTests.cs # NEW (German body + Retry-After)
│   └── ForwardedHeadersTests.cs      # NEW
├── Auth/                             # NEW
│   ├── RefreshTokenServiceTests.cs   # NEW
│   ├── ReplayDetectionTests.cs       # NEW
│   ├── DeleteAccountHandlerTests.cs  # NEW
│   └── HmacPepperHashingTests.cs     # NEW
```

### Pattern 1: `AddRateLimiter` with named policies + `OnRejected` ProblemDetails

**What:** Register fixed-window + concurrency policies by name; attach via `.RequireRateLimiting("name")` per-endpoint; emit German `application/problem+json` on 429.

**When to use:** Every endpoint listed in D-05/D-07/D-09/D-12.

**Example:**

```csharp
// Source: learn.microsoft.com/en-us/aspnet/core/performance/rate-limit?view=aspnetcore-10.0
// Adapted to D-08 German ProblemDetails shape + D-05/D-06 IP partitioning after UseForwardedHeaders
using System.Globalization;
using System.Net;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;

// ── 1. ForwardedHeaders FIRST (before anything that reads RemoteIpAddress) ──
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor
                             | ForwardedHeaders.XForwardedProto
                             | ForwardedHeaders.XForwardedHost;
    // .NET 10: KnownNetworks is OBSOLETE — use KnownIPNetworks with System.Net.IPNetwork
    options.KnownIPNetworks.Add(IPNetwork.Parse("172.16.0.0/12")); // Docker bridge
    options.ForwardLimit = 1; // Caddy is the ONLY hop; no chain
});

// ── 2. AddRateLimiter with named policies ──
builder.Services.AddRateLimiter(options =>
{
    // Default rejection: 429 (not 503). Custom body is set in OnRejected.
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // ── Global IP-partitioned limiter (D-09: 60 req/min) ──
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 60,
                Window = TimeSpan.FromMinutes(1),
                AutoReplenishment = true,
                QueueLimit = 0
            }));

    // ── auth-strict: 5/min, partitioned by IP for anon endpoints,
    //    by sub for /account (the endpoint chooses the key via metadata) ──
    options.AddPolicy("auth-strict", httpContext =>
    {
        // /account is authenticated → partition by user sub. Anon endpoints → partition by IP.
        var sub = httpContext.User.FindFirst("sub")?.Value;
        var partitionKey = !string.IsNullOrEmpty(sub)
            ? $"user:{sub}"
            : $"ip:{httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown"}";

        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: partitionKey,
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(1),
                AutoReplenishment = true,
                QueueLimit = 0
            });
    });

    // ── auth-refresh: 30/min per IP (D-05) ──
    options.AddPolicy("auth-refresh", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: $"ip:{httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown"}",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 30,
                Window = TimeSpan.FromMinutes(1),
                AutoReplenishment = true,
                QueueLimit = 0
            }));

    // ── upload-concurrency: 2 + queue 4, partitioned by authenticated user (D-07) ──
    options.AddPolicy("upload-concurrency", httpContext =>
    {
        var sub = httpContext.User.FindFirst("sub")?.Value ?? "anonymous";
        return RateLimitPartition.GetConcurrencyLimiter(
            partitionKey: $"user:{sub}",
            factory: _ => new ConcurrencyLimiterOptions
            {
                PermitLimit = 2,
                QueueLimit = 4,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst
            });
    });

    // ── OnRejected: German ProblemDetails + Retry-After ──
    options.OnRejected = async (context, cancellationToken) =>
    {
        var retryAfterSeconds = 60; // fallback
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            retryAfterSeconds = (int)retryAfter.TotalSeconds;
            context.HttpContext.Response.Headers.RetryAfter =
                retryAfterSeconds.ToString(NumberFormatInfo.InvariantInfo);
        }

        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        context.HttpContext.Response.ContentType = "application/problem+json";

        var problem = new ProblemDetails
        {
            Type = "https://tools.ietf.org/html/rfc6585#section-4",
            Title = "Zu viele Anfragen.",
            Status = StatusCodes.Status429TooManyRequests,
            Detail = $"Bitte versuchen Sie es in {retryAfterSeconds} Sekunden erneut."
        };
        problem.Extensions["retryAfterSeconds"] = retryAfterSeconds;

        await context.HttpContext.Response.WriteAsJsonAsync(problem, cancellationToken);
    };
});

// ── 3. Build app and order the pipeline ──
var app = builder.Build();

app.UseForwardedHeaders();                  // FIRST (D-06)
app.UseMiddleware<ExceptionHandlingMiddleware>();  // existing
app.UseCors();                              // existing
app.UseSerilogRequestLogging();             // existing
app.UseRateLimiter();                       // NEW — after request logging so 429s are logged
app.UseAuthentication();                    // existing
app.UseAuthorization();                     // existing

// ── 4. Endpoint policy attachment ──
auth.MapPost("/login", ...)
    .AllowAnonymous()
    .RequireRateLimiting("auth-strict");

auth.MapPost("/register", ...)
    .AllowAnonymous()
    .RequireRateLimiting("auth-strict");

auth.MapPost("/refresh", ...)
    .AllowAnonymous()
    .RequireRateLimiting("auth-refresh");

auth.MapDelete("/account", ...)
    .RequireRateLimiting("auth-strict");   // authenticated, partition by sub

receiptFiles.MapPost("/", ...)
    .RequireRateLimiting("upload-concurrency");
```

### Pattern 2: HMAC-SHA256 keyed-hash with static `HashData`

**What:** Compute HMAC-SHA256 of the plaintext refresh token using the server pepper.

**When to use:** Inside `RefreshTokenService.IssueAsync` (before INSERT) and `ValidateAndRotateAsync` (before SELECT lookup).

**Example:**

```csharp
// Source: learn.microsoft.com/en-us/dotnet/api/system.security.cryptography.hmacsha256.hashdata
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using TaxReader.Infrastructure.Configuration;

public class RefreshTokenService(
    IAppDbContext dbContext,
    IOptions<RefreshTokenOptions> options) : IRefreshTokenService
{
    private readonly byte[] _pepper = Convert.FromBase64String(options.Value.HashKey);

    public string ComputeHash(string plaintextToken)
    {
        // Static HashData: zero allocations, no instance to dispose, CA1850-compliant
        var plaintextBytes = Encoding.UTF8.GetBytes(plaintextToken);
        var hash = HMACSHA256.HashData(_pepper, plaintextBytes);
        return Convert.ToBase64String(hash);  // 44 chars, fixed-length, indexable
    }
}
```

**Storage choice — Base64 vs hex:** Base64 is 44 chars vs hex 64; both are fixed-length (one HMAC-SHA256 output = 32 bytes). Base64 is the project convention (`AuthService.cs:152`: `Convert.ToBase64String(RandomNumberGenerator.GetBytes(64))`). Use Base64 with `HasMaxLength(44)` on the column.

**Pepper format:** 32-byte (256-bit) random value, Base64-encoded in env var. `.env.example` hint: `openssl rand -base64 32`. Stored as a single `HashKey` string property on `RefreshTokenOptions` — same pattern as `JwtOptions.Secret`.

### Pattern 3: EF migration combining CREATE TABLE + ALTER TABLE DROP COLUMN

**What:** A single migration `Up()` method that creates the new table before dropping columns from an existing one. Postgres handles both DDL operations in a transaction.

**When to use:** D-15 explicitly requires a single migration.

**Example:**

```csharp
// Pattern derived from existing 20260412095923_AddAuthAndUserScoping.cs
// (which already combines AddColumn + CreateTable + CreateIndex + AddForeignKey
//  in one Up() — same shape).
// Source: learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/managing

public partial class AddRefreshTokensTable_DropLegacyRefreshTokenColumns : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // 1. CREATE the new table FIRST.
        migrationBuilder.CreateTable(
            name: "refresh_tokens",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false,
                    defaultValueSql: "gen_random_uuid()"),
                user_id = table.Column<Guid>(type: "uuid", nullable: false),
                token_hash = table.Column<string>(type: "character varying(44)",
                    maxLength: 44, nullable: false),
                created_at = table.Column<DateTime>(type: "timestamp with time zone",
                    nullable: false),
                expires_at = table.Column<DateTime>(type: "timestamp with time zone",
                    nullable: false),
                revoked_at = table.Column<DateTime>(type: "timestamp with time zone",
                    nullable: true),
                last_used_at = table.Column<DateTime>(type: "timestamp with time zone",
                    nullable: true),
                user_agent = table.Column<string>(type: "character varying(500)",
                    maxLength: 500, nullable: true),
                ip_address = table.Column<IPAddress>(type: "inet", nullable: true),
                replaced_by_token_id = table.Column<Guid>(type: "uuid", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_refresh_tokens", x => x.id);
                table.ForeignKey(
                    name: "fk_refresh_tokens_users_user_id",
                    column: x => x.user_id,
                    principalTable: "users",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "fk_refresh_tokens_refresh_tokens_replaced_by_token_id",
                    column: x => x.replaced_by_token_id,
                    principalTable: "refresh_tokens",
                    principalColumn: "id");
            });

        // Indexes for token_hash lookup and (user_id, revoked_at) revoke-all query (D-02).
        migrationBuilder.CreateIndex(
            name: "ix_refresh_tokens_token_hash",
            table: "refresh_tokens",
            column: "token_hash",
            unique: true);
        migrationBuilder.CreateIndex(
            name: "ix_refresh_tokens_user_id_revoked_at",
            table: "refresh_tokens",
            columns: new[] { "user_id", "revoked_at" });

        // 2. THEN drop the legacy columns. Order doesn't strictly matter for Postgres
        //    (no FKs reference these columns), but "create first, drop last" is
        //    safest and matches the existing AddAuthAndUserScoping migration pattern.
        migrationBuilder.DropColumn(
            name: "refresh_token",
            table: "users");
        migrationBuilder.DropColumn(
            name: "refresh_token_expires_at",
            table: "users");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Re-add legacy columns. Plaintext refresh tokens cannot be restored — they
        // were never persisted in the new table (only HMAC hashes are stored).
        // Down() therefore restores the columns empty; downstream consequence is
        // forced re-login for every user. Acceptable per D-15 pre-launch context.
        migrationBuilder.AddColumn<string>(
            name: "refresh_token",
            table: "users",
            type: "character varying(500)",
            maxLength: 500,
            nullable: true);
        migrationBuilder.AddColumn<DateTime>(
            name: "refresh_token_expires_at",
            table: "users",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.DropTable(name: "refresh_tokens");
    }
}
```

**Important:** EF's migration scaffolder generates `Up()` operations in a sensible order automatically when you run `dotnet ef migrations add ...`. Verify the generated migration matches the create-first-drop-last shape; if EF inverts it, manually reorder before committing. [VERIFIED: learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/managing — "operations execute in the order they're added to the builder"]

### Pattern 4: ExecuteUpdateAsync for bulk revoke-all

**What:** Issue a single SQL UPDATE without loading entities — used in both replay-detection (D-03) and account-deletion pre-step (D-13).

**When to use:** `RefreshTokenService.RevokeAllForUserAsync(userId)`.

**Example:**

```csharp
// Source: learn.microsoft.com/en-us/ef/core/saving/execute-insert-update-delete
public async Task<int> RevokeAllForUserAsync(Guid userId, CancellationToken ct)
{
    return await dbContext.RefreshTokens
        .Where(t => t.UserId == userId && t.RevokedAt == null)
        .ExecuteUpdateAsync(
            setters => setters.SetProperty(t => t.RevokedAt, DateTime.UtcNow),
            ct);
}
```

**Why not load + SaveChanges:** Untracked bulk UPDATE is one round-trip, no materialization, no change-tracker overhead. Returns the row count for logging ("revoked N tokens"). The trade-off (change tracker out of sync) is irrelevant because we don't read these entities elsewhere in the same scope.

### Pattern 5: `IRefreshTokenService` API surface (Claude's Discretion)

**What:** Three-method interface so `AuthService` doesn't know about hashing/peppers/replays.

**When to use:** Wherever `AuthService.cs:74`, `:98`, `:120` currently write `user.RefreshToken =`.

**Example:**

```csharp
namespace TaxReader.Application.Interfaces;

public interface IRefreshTokenService
{
    /// <summary>
    /// Issues a new refresh token for the user. Returns the PLAINTEXT token
    /// (caller embeds in HTTP response) and a void Task once persisted.
    /// </summary>
    Task<string> IssueAsync(
        Guid userId,
        string? userAgent,
        string? ipAddress,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates a plaintext refresh token, rotates it (revokes the old row,
    /// inserts a new one linked via replaced_by_token_id), and returns the
    /// new plaintext token + the user_id for JWT issuance.
    ///
    /// On REPLAY (presented token is already revoked):
    ///   - Revokes all of the user's non-revoked tokens (D-03)
    ///   - Logs a Sentry warning with user.id_hash
    ///   - Returns Failure
    /// </summary>
    Task<Result<(Guid UserId, string PlaintextToken)>> ValidateAndRotateAsync(
        string plaintextToken,
        string? userAgent,
        string? ipAddress,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Defense-in-depth: revoke all tokens for a user.
    /// Called before user deletion (D-13) and from replay detection (D-03).
    /// </summary>
    Task<int> RevokeAllForUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}
```

### Pattern 6: Axios DELETE with body

**What:** Send a JSON body on a DELETE request via the existing axios instance.

**When to use:** `Frontend/src/lib/api-client.ts` `deleteAccount(password)`.

**Example:**

```typescript
// Source: github.com/axios/axios/issues/897 (resolved — supported via config.data)
// Note: unlike axios.post/put, the body for DELETE must be nested in `data` of
// the 2nd config arg.
export async function deleteAccount(password: string): Promise<void> {
  await api.delete("/auth/account", { data: { password } });
  clearAuthStorage();
}
```

The existing axios instance carries the Bearer-token interceptor (`api-client.ts:33-38`), so the call automatically includes the access token. The 401 path on wrong password is unique: the axios refresh-interceptor (`api-client.ts:43-73`) will attempt one refresh and retry. The DELETE call inside the password-reverify flow should NOT trigger that path — we want the 401 to reach the caller. Two options:

1. Set `originalRequest._retry = true` manually before the call to bypass the refresh-interceptor. **Brittle — depends on interceptor internals.**
2. Recommended: Read the response status in the caller and surface inline. The refresh-interceptor only retries if the refresh succeeds; if the user just typed a wrong password, the access token is still valid, so refresh succeeds, the call retries, gets 401 again — and falls through to `clearAuthStorage()` + redirect. That's wrong UX (we wanted inline error, not logout).

**Resolution:** Issue a one-off axios call WITHOUT the shared interceptors, or add a config flag `{ headers: { 'X-Skip-Auth-Refresh': '1' } }` and short-circuit `_retry = true` in the interceptor when present. The simpler approach is to use a top-level `axios` (not `api`) for this single endpoint:

```typescript
// Bypass the refresh-interceptor for this call so a wrong-password 401 surfaces
// inline instead of triggering logout.
import axios from "axios";

export async function deleteAccount(password: string): Promise<void> {
  await axios.delete("/api/v1/auth/account", {
    headers: { Authorization: `Bearer ${getAccessToken()}` },
    data: { password },
  });
  clearAuthStorage();
}
```

This is identical to how `register` and `login` already use raw `axios` (`api-client.ts:106, :115`) — the pattern is established.

### Anti-Patterns to Avoid

- **❌ Storing plaintext refresh tokens.** Storing only the HMAC hash + pepper means a DB-only leak doesn't enable forgery (D-01's stated rationale).
- **❌ Calling `UseRateLimiter` BEFORE `UseForwardedHeaders`.** The partition key would be the Caddy container's IP — every request shares one bucket. D-06.
- **❌ Using `Microsoft.AspNetCore.HttpOverrides.IPNetwork` in .NET 10.** Generates `ASPDEPR005` warning. Use `System.Net.IPNetwork.Parse(...)` with `KnownIPNetworks` instead.
- **❌ Per-row revoke in a loop.** Use `ExecuteUpdateAsync` for the bulk revoke-all path (D-03).
- **❌ Throwing exceptions from `RefreshTokenService.ValidateAndRotateAsync`.** Project convention is `Result<T>.Failure` (CLAUDE.md "Patterns We DON'T Use: Exceptions for control flow").
- **❌ Surfacing "replay detected" to the client.** D-04 — generic 401 only.
- **❌ Letting `AuthService` know about `IHttpContextAccessor`.** Keep HTTP context plumbing at the endpoint layer (matches existing `ICurrentUser` pattern in `CurrentUser.cs`).
- **❌ Reading `httpContext.Connection.RemoteIpAddress` BEFORE `UseForwardedHeaders` runs.** The middleware mutates `RemoteIpAddress` based on the trusted `X-Forwarded-For` header — anything earlier sees the Caddy container's docker-internal IP.
- **❌ Returning `503 Service Unavailable` on rate limit.** Default behavior without `RejectionStatusCode = 429`. Set it explicitly.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Refresh-token rotation logic | Custom one-row-per-user replacement with "is this newer than that" comparisons | Multi-row `refresh_tokens` table with `replaced_by_token_id` self-FK | Multi-device support and replay detection are first-class invariants of the schema — hand-rolling means O(N) edge cases [CITED: codesignal.com/learn/courses/preventing-refresh-token-abuse, copyprogramming.com/howto/how-to-allow-users-to-connect-from-multiple-devices-with-refresh-tokens] |
| Rate limiting / throttling | Counter dictionary keyed on IP with sliding window manually computed in middleware | `Microsoft.AspNetCore.RateLimiting` with `AddFixedWindowLimiter` / `AddConcurrencyLimiter` | Per-partition reset timing, queue-overflow semantics, distributed-friendly metadata (Retry-After), `[EnableRateLimiting]` attribute, OpenTelemetry metrics — all built in [VERIFIED: learn.microsoft.com/en-us/aspnet/core/performance/rate-limit] |
| Client-IP-from-proxy resolution | Parse `X-Forwarded-For` manually in handlers | `UseForwardedHeaders` with `ForwardedHeadersOptions` | Hand-parsing misses XFF ordering rules, IPv6, trust-chain validation, the `X-Original-For` mirror, and `ForwardLimit` per-hop check [VERIFIED: learn.microsoft.com/en-us/aspnet/core/host-and-deploy/proxy-load-balancer] |
| Password hashing / verification | Roll BCrypt, scrypt, or Argon2 yourself | `BCrypt.Net.BCrypt.HashPassword` / `Verify` (already in project) | Already wired at `AuthService.cs:44, :94`. CLAUDE.md's "Don't hand-roll cryptography" applies universally |
| HMAC keyed-hash | Manual `byte[]` XOR + SHA-256 + ipad/opad | `HMACSHA256.HashData(key, data)` static | One method call, zero allocation, handles 32-byte key automatically; alternative `new HMACSHA256(key); ComputeHash(...)` triggers CA1850 [VERIFIED: learn.microsoft.com/en-us/dotnet/api/system.security.cryptography.hmacsha256.hashdata] |
| Bulk UPDATE / DELETE | `await foreach { context.Remove(...) }` + `SaveChangesAsync` | `ExecuteUpdateAsync` / `ExecuteDeleteAsync` | EF Core 7+ standard for bulk operations [VERIFIED: learn.microsoft.com/en-us/ef/core/saving/execute-insert-update-delete] |
| ProblemDetails JSON serialization | Hand-write JSON in `Response.WriteAsync` | `Response.WriteAsJsonAsync(problem)` with `ProblemDetails` from `Microsoft.AspNetCore.Mvc` | Type-safe; respects content negotiation; emits `application/problem+json` correctly |
| CIDR parsing | Bitmask arithmetic on `IPAddress.GetAddressBytes()` | `System.Net.IPNetwork.Parse("172.16.0.0/12")` | First-class API since .NET 8 [VERIFIED: learn.microsoft.com/en-us/dotnet/api/system.net.ipnetwork] |

**Key insight:** Every component in this phase has a first-party .NET 10 implementation. The phase is mostly "wire up framework features correctly" rather than "build new mechanisms." The risk surface is integration order (pipeline middleware ordering, migration `Up()` ordering) and contract (German copy in ProblemDetails, exactly-matching policy names between `AddPolicy` and `.RequireRateLimiting`).

## Runtime State Inventory

This is a refactor phase (replacing `users.refresh_token` with `refresh_tokens` table), so the runtime state audit applies.

| Category | Items Found | Action Required |
|----------|-------------|------------------|
| Stored data | **`users.refresh_token` + `users.refresh_token_expires_at` columns** contain in-flight refresh tokens for any currently logged-in user. Solo dev's own session is the only victim per D-15. | D-15: Drop columns in the migration; data migration is intentional re-login. **NO data preservation step.** |
| Stored data | **No ChromaDB / Mem0 / external datastore** keyed on refresh tokens — only Postgres holds refresh-token state. Verified by codebase grep on `RefreshToken` references — only `User.cs`, `AuthService.cs`, and `UserConfiguration.cs`. | None |
| Live service config | **None.** No n8n workflows, no Datadog dashboards, no Tailscale ACL tags reference refresh tokens. Phase 2 introduces Sentry events on replay detection, but the Sentry config does not need pre-staging. | None |
| OS-registered state | **None.** No Windows Task Scheduler tasks, pm2 saved processes, launchd plists, or systemd unit names reference auth state. | None |
| Secrets/env vars | **New env var `RefreshToken__HashKey`** must be added to `docker-compose.yml` `api` service block and to `.env.example` (placeholder with `openssl rand -base64 32` hint per D-01). Existing `Jwt__Secret` unchanged. | Add `RefreshToken__HashKey: ${REFRESHTOKEN_HASHKEY}` next to `Jwt__Secret`; add `.env.example` line; operator must set the variable in their `.env` file before deploy. |
| Build artifacts / installed packages | **None.** No package renames, no compiled binaries, no Docker image tags carrying the old auth state. | None |

**Migration data preservation:** D-15 explicitly accepts "down-migration restores the legacy columns (without data — the plaintext tokens are not preserved through the table-creation step)." Pre-launch milestone, force-re-login is free. The dev's own active session is the only victim.

**Frontend localStorage:** `Frontend/src/lib/api-client.ts:87` stores `refreshToken` in `localStorage`. After the migration, the value held in browser storage is the OLD random Base64 token. On next refresh attempt, the backend hashes it with HMAC-SHA256 + new pepper, looks it up in the NEW empty `refresh_tokens` table, doesn't find it, returns 401, axios refresh-interceptor bounces to `/login`. The localStorage value gets overwritten on next login. **No explicit migration step needed.**

## Common Pitfalls

### Pitfall 1: KnownNetworks deprecation in .NET 10
**What goes wrong:** Compilation produces `ASPDEPR005` warning; if `<TreatWarningsAsErrors>` is set, build fails. `Directory.Build.props` does NOT set `TreatWarningsAsErrors`, but the warning still pollutes CI output and should be fixed at first write.
**Why it happens:** `02-CONTEXT.md` D-06 references the deprecated API by name.
**How to avoid:** Use `KnownIPNetworks` (note the `IP` prefix) and `System.Net.IPNetwork.Parse("172.16.0.0/12")`.
**Warning signs:** `warning ASPDEPR005: Please use KnownIPNetworks instead` in build output.

### Pitfall 2: Pipeline ordering — `UseRateLimiter` before `UseAuthentication`
**What goes wrong:** Authenticated rate-limit policies (`auth-strict` partition by `sub`, `upload-concurrency` partition by `sub`) can't read the JWT claim because authentication hasn't run yet. The partition falls back to "anonymous" or empty string.
**Why it happens:** Intuition says "rate-limit first to reject abuse before doing expensive auth," but JWT verification is cheap (HMAC check) and the limiter middleware needs `httpContext.User.FindFirst("sub")` to be populated.
**How to avoid:** Order: `UseForwardedHeaders → UseMiddleware<ExceptionHandlingMiddleware> → UseCors → UseSerilogRequestLogging → UseRateLimiter → UseAuthentication → UseAuthorization`. Wait — this is wrong. The limiter runs BEFORE auth, so `sub` is NOT yet available on the global limiter. **Correct resolution:** for `auth-strict` and `upload-concurrency` policies that need `sub`, attach the policy via `.RequireRateLimiting("policy-name")` per-endpoint. Per-endpoint policies run AFTER auth/authz middleware because they're tied to the endpoint, not the global pipeline. The global limiter only sees pre-auth requests and partitions on IP only.

**Authoritative:** "[EnableRateLimiting] is applied to a routable component... UseRateLimiter must be called after UseRouting" — endpoint-scoped policies run at the endpoint layer, after routing, where JWT claims are present. [VERIFIED: learn.microsoft.com/en-us/aspnet/core/performance/rate-limit]

**Practical implication for D-12:** `/auth/account` is authenticated and uses `auth-strict`. Because it's attached via `.RequireRateLimiting("auth-strict")` and rate-limiter middleware runs after routing/auth for endpoint policies, the `sub` claim IS available when the policy runs. The global IP-partitioned limiter ALSO runs and adds an additional gate. Both fire; both must permit.

### Pitfall 3: HMAC pepper rotation invalidates all sessions
**What goes wrong:** Rotating `RefreshToken__HashKey` for emergency response invalidates every stored hash because new plaintext → different HMAC. Every user is logged out.
**Why it happens:** Single-pepper design, no key versioning.
**How to avoid:** D-01 documents the trade-off as acceptable: "Rotating the pepper invalidates all existing sessions — acceptable trade-off documented in operations notes." Don't try to engineer around it in Phase 2. The "documented in operations notes" piece is in the **Deferred Ideas** of CONTEXT.md as a backlog runbook item.
**Warning signs:** Operator complains "we rotated the env var, now everyone is logged out." Direct them to D-01.

### Pitfall 4: Caddy must be the first hop
**What goes wrong:** If a public proxy (CDN like Cloudflare, AWS ALB) is added in front of Caddy without configuring Caddy's `trusted_proxies`, Caddy will OVERWRITE the public proxy's `X-Forwarded-For` with the proxy's IP (because Caddy doesn't trust it). The API receives the proxy's IP as the "real" client, and IP-partitioned rate limits bucket every public request together.
**Why it happens:** Defense-in-depth: Caddy refuses to trust headers from untrusted upstream hops.
**How to avoid:** Phase 2 doesn't add a CDN. Document in code comments: "When a CDN is added in front of Caddy, BOTH Caddy and ASP.NET Core forwarded-headers configs must update to trust the new hop." Out of scope here. [VERIFIED: caddyserver.com/docs/caddyfile/directives/reverse_proxy]
**Warning signs:** All IP-partitioned 429s fire against the same partition key (the upstream proxy's IP).

### Pitfall 5: Forgetting `RejectionStatusCode`
**What goes wrong:** Without `options.RejectionStatusCode = StatusCodes.Status429TooManyRequests`, the rate limiter returns `503 Service Unavailable`. Frontend's axios refresh-interceptor doesn't recognize 503, monitoring tools think the API is down, and load balancers may evict the instance.
**Why it happens:** ASP.NET Core's default is 503 (legacy compatibility).
**How to avoid:** Set `RejectionStatusCode` explicitly as the FIRST line inside `AddRateLimiter`. [CITED: oneuptime.com/blog/post/2025-12-23-aspnet-core-rate-limiting/view]
**Warning signs:** 503 responses in browser dev tools, not 429.

### Pitfall 6: `axios.delete()` second arg is config, not body
**What goes wrong:** `axios.delete("/auth/account", { password })` sends the body as `{}` because the second positional arg is the config object, not the data. Backend gets an empty `Password` and returns 400/401 even with the correct password.
**Why it happens:** axios DELETE / GET use `{ params, data, headers }` config, not body-first.
**How to avoid:** `axios.delete("/auth/account", { data: { password } })`. Always nest in `data`. [VERIFIED: github.com/axios/axios/issues/897 (resolved)]
**Warning signs:** Backend logs show `request.Password` is empty/null on /account DELETE attempts.

### Pitfall 7: Replay detection log floods Sentry
**What goes wrong:** A buggy client retrying with the same revoked token every 30s creates one Sentry event per attempt. Quota burn + alert fatigue.
**Why it happens:** D-03 says log at Warning, but says nothing about deduplication.
**How to avoid:** Phase 1 D-15 already specifies alert rules: "new error type with 1h cooldown" + "sustained rate ≥ 10 events/min for ≥ 5 min." The first-event fires once per hour per error type; the rate alert catches the storm. Don't add per-call deduplication in code — the alert config already handles it.
**Warning signs:** Sentry events page showing many entries for "Refresh token replay detected" from the same user.

### Pitfall 8: `IHttpContextAccessor` is null in test scenarios
**What goes wrong:** Unit tests for `RefreshTokenService` instantiate the service without a fake `IHttpContextAccessor`; tests fail with `NullReferenceException` when the service reads `HttpContext.Request.Headers.UserAgent`.
**Why it happens:** The "Claude's Discretion" section names `IHttpContextAccessor` as the obvious approach to capture UA/IP.
**How to avoid:** Don't inject `IHttpContextAccessor` into `RefreshTokenService` at all. Accept `userAgent` + `ipAddress` as method parameters. The endpoint (`AuthEndpoints.cs`) is the one place where `HttpContext` is already in scope, so it extracts the values and passes them in. This matches the existing convention — `AuthService` doesn't know about `HttpContext`, `ICurrentUser` is the only HttpContext-aware abstraction.
**Warning signs:** Test bootstrap requires `Mock<IHttpContextAccessor>` + nested mocks for `HttpContext.Request.Headers`.

### Pitfall 9: `ForwardLimit` set too high lets attackers spoof IP
**What goes wrong:** With `ForwardLimit = 5`, an attacker prepending fake IPs to `X-Forwarded-For` ("attacker_fake1, attacker_fake2, attacker_real, caddy") causes the middleware to consume entries right-to-left, possibly stopping at `attacker_real` because the chain length matches.
**Why it happens:** Default `ForwardLimit = 1` means "trust only the rightmost (last proxy) entry." Raising it without adding more hops to `KnownIPNetworks` creates a spoofing window.
**How to avoid:** Set `ForwardLimit = 1` explicitly. Caddy is the only hop; one entry to consume. [VERIFIED: learn.microsoft.com/en-us/aspnet/core/host-and-deploy/proxy-load-balancer "The default is 1"]

## Code Examples

### Example 1: Full `RefreshTokenService.ValidateAndRotateAsync` happy + replay paths

```csharp
// Source: composed from D-01 (HMAC pepper), D-03 (replay revoke), D-04 (silent 401),
// and the project's Result<T> + Sentry conventions
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sentry;
using Serilog.Context;
using System.Security.Cryptography;
using System.Text;

public class RefreshTokenService(
    IAppDbContext dbContext,
    IOptions<RefreshTokenOptions> refreshTokenOptions,
    IOptions<JwtOptions> jwtOptions,
    ILogger<RefreshTokenService> logger) : IRefreshTokenService
{
    private readonly byte[] _pepper = Convert.FromBase64String(refreshTokenOptions.Value.HashKey);
    private readonly JwtOptions _jwt = jwtOptions.Value;

    public async Task<Result<(Guid UserId, string PlaintextToken)>> ValidateAndRotateAsync(
        string plaintextToken,
        string? userAgent,
        string? ipAddress,
        CancellationToken cancellationToken = default)
    {
        var tokenHash = ComputeHash(plaintextToken);

        var existing = await dbContext.RefreshTokens
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);

        if (existing is null)
        {
            // Either never issued or rotated long ago. Generic 401.
            return Result<(Guid, string)>.Failure("Ungültiges oder abgelaufenes Refresh-Token.");
        }

        using (LogContext.PushProperty("UserId", existing.UserId))
        {
            // Expiry check
            if (existing.ExpiresAt < DateTime.UtcNow)
            {
                logger.LogInformation("Refresh token expired");
                return Result<(Guid, string)>.Failure("Ungültiges oder abgelaufenes Refresh-Token.");
            }

            // REPLAY DETECTION — token was already rotated (D-03)
            if (existing.RevokedAt is not null)
            {
                logger.LogWarning("Refresh token replay detected");
                SentrySdk.CaptureMessage(
                    "Refresh token replay detected",
                    scope => scope.SetExtra("user.id_hash", HashUserId(existing.UserId)),
                    SentryLevel.Warning);

                await RevokeAllForUserAsync(existing.UserId, cancellationToken);
                return Result<(Guid, string)>.Failure("Ungültiges oder abgelaufenes Refresh-Token.");
            }

            // Happy path: rotate
            var (newPlaintext, newHash) = GenerateAndHash();
            var newRow = new RefreshToken
            {
                Id = Guid.NewGuid(),
                UserId = existing.UserId,
                TokenHash = newHash,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddDays(_jwt.RefreshTokenExpirationDays),
                UserAgent = userAgent?.Length > 500 ? userAgent[..500] : userAgent,
                IpAddress = ParseIpOrNull(ipAddress)
            };
            dbContext.RefreshTokens.Add(newRow);

            existing.RevokedAt = DateTime.UtcNow;
            existing.LastUsedAt = DateTime.UtcNow;
            existing.ReplacedByTokenId = newRow.Id;

            await dbContext.SaveChangesAsync(cancellationToken);

            logger.LogInformation("Refresh token rotated successfully");
            return Result<(Guid, string)>.Success((existing.UserId, newPlaintext));
        }
    }

    public async Task<int> RevokeAllForUserAsync(Guid userId, CancellationToken ct)
    {
        return await dbContext.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null)
            .ExecuteUpdateAsync(
                s => s.SetProperty(t => t.RevokedAt, DateTime.UtcNow),
                ct);
    }

    private string ComputeHash(string plaintextToken)
    {
        var plaintextBytes = Encoding.UTF8.GetBytes(plaintextToken);
        var hash = HMACSHA256.HashData(_pepper, plaintextBytes);
        return Convert.ToBase64String(hash);
    }

    private (string Plaintext, string Hash) GenerateAndHash()
    {
        var plaintext = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        var hash = ComputeHash(plaintext);
        return (plaintext, hash);
    }

    private static string HashUserId(Guid userId)
    {
        // Phase 1 D-14 PII allow-list — `user.id_hash` is permitted.
        // Use a non-keyed SHA256 since we're hashing for correlation, not auth.
        var bytes = SHA256.HashData(userId.ToByteArray());
        return Convert.ToHexString(bytes)[..16];
    }

    private static System.Net.IPAddress? ParseIpOrNull(string? ip)
        => System.Net.IPAddress.TryParse(ip, out var addr) ? addr : null;
}
```

### Example 2: AuthEndpoints capture UA/IP and pass through

```csharp
// Source: composed from existing AuthEndpoints.cs + Pitfall 8 (don't inject
// IHttpContextAccessor into AuthService)
auth.MapPost("/login", async (
    LoginRequest request,
    IAuthService authService,
    HttpContext httpContext,            // ← bind HttpContext directly
    CancellationToken cancellationToken) =>
{
    var userAgent = httpContext.Request.Headers.UserAgent.ToString();
    // After UseForwardedHeaders runs, RemoteIpAddress is the real client IP.
    var ipAddress = httpContext.Connection.RemoteIpAddress?.ToString();

    var result = await authService.LoginAsync(request, userAgent, ipAddress, cancellationToken);

    return result.IsSuccess
        ? Results.Ok(result.Value)
        : Results.Unauthorized();
})
.AllowAnonymous()
.RequireRateLimiting("auth-strict")
.WithName("Login");
```

### Example 3: DeleteAccountHandler with password re-verify

```csharp
// Source: composed from D-10 + D-13 + existing DeleteAccountHandler.cs
public class DeleteAccountHandler(
    IAppDbContext dbContext,
    ICurrentUser currentUser,
    IRefreshTokenService refreshTokenService) // NEW dependency
{
    public async Task<Result<bool>> HandleAsync(
        DeleteAccountRequest request,
        CancellationToken cancellationToken = default)
    {
        var userId = currentUser.UserId;

        var user = await dbContext.Users
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user is null)
            return Result<bool>.Failure("User not found.");

        // D-13 step 1: password re-verify
        if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            return Result<bool>.Failure("Ungültiges Passwort.");

        // D-13 step 2: revoke all refresh tokens (defense-in-depth)
        await refreshTokenService.RevokeAllForUserAsync(userId, cancellationToken);

        // D-13 step 3: cascade delete
        dbContext.Users.Remove(user);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result<bool>.Success(true);
    }
}

// AuthEndpoints.cs MapDelete change:
auth.MapDelete("/account", async (
    DeleteAccountRequest request,           // ← NEW: bind from JSON body
    DeleteAccountHandler handler,
    CancellationToken cancellationToken) =>
{
    var result = await handler.HandleAsync(request, cancellationToken);

    if (result.IsSuccess)
        return Results.NoContent();

    // D-12: wrong password = 401 with German error
    if (result.Error == "Ungültiges Passwort.")
        return Results.Json(new { error = result.Error }, statusCode: 401);

    return Results.NotFound(new { error = result.Error });
})
.RequireRateLimiting("auth-strict")    // D-12
.WithName("DeleteAccount");
```

**Note:** Minimal API binding from JSON body for DELETE requires `DeleteAccountRequest` to be a record (already a convention) and the request to send `Content-Type: application/json`. axios sets that automatically when `data: { password }` is passed.

### Example 4: WebApplicationFactory integration test for rate-limit 429 shape

```csharp
// Source: pattern from CorsConfigurationTests.cs:42-57, extended for rate-limit testing.
// Uses a short window so the test doesn't wait 60 seconds for a reset.
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;

public class AuthStrictPolicyTests
{
    [Fact]
    public async Task SixthLoginAttempt_Returns429WithGermanProblemDetails()
    {
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseEnvironment("Production");
            b.UseSetting("Jwt:Secret", "test-secret-test-secret-test-secret-1234");
            b.UseSetting("Jwt:Issuer", "test");
            b.UseSetting("Jwt:Audience", "test");
            b.UseSetting("RefreshToken:HashKey", Convert.ToBase64String(new byte[32]));
            b.UseSetting("ConnectionStrings:DefaultConnection",
                "Host=localhost;Port=5432;Database=test;Username=test;Password=test");
        });

        var client = factory.CreateClient();

        // Burn the 5/min budget with intentionally-bad credentials (avoids DB)
        for (var i = 0; i < 5; i++)
        {
            var ok = await client.PostAsJsonAsync("/api/v1/auth/login",
                new { Email = "x@x.de", Password = "bad" });
            ok.StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized,
                                          HttpStatusCode.BadRequest);
        }

        var response = await client.PostAsJsonAsync("/api/v1/auth/login",
            new { Email = "x@x.de", Password = "bad" });

        response.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        response.Content.Headers.ContentType?.MediaType
            .Should().Be("application/problem+json");
        response.Headers.RetryAfter.Should().NotBeNull();

        var body = await response.Content.ReadFromJsonAsync<ProblemResponse>();
        body!.Title.Should().Be("Zu viele Anfragen.");
        body.Detail.Should().Contain("Sekunden erneut");
        body.Status.Should().Be(429);
    }

    private record ProblemResponse(string Title, string Detail, int Status);
}
```

For tests that need to verify partitioning (per-IP), inject a custom `X-Forwarded-For` header per request — but only after wiring the test environment to trust the test host's loopback. For pure unit testing of policy registration, prefer the option-resolution shape used in `CorsConfigurationTests.cs:59-66` (`scope.ServiceProvider.GetRequiredService<IOptions<RateLimiterOptions>>()`).

### Example 5: Frontend settings dialog (D-11)

```typescript
// Source: composed from D-11 + existing Frontend/src/app/(authenticated)/settings/page.tsx
"use client";

// ...existing imports...
import { useState } from "react";
import axios from "axios";

// REPLACE confirmInput with password state
const [password, setPassword] = useState("");
const [deleteError, setDeleteError] = useState<string | null>(null);
const [isDeleting, setIsDeleting] = useState(false);

const handleDeleteAccount = async () => {
  if (password.length === 0) return;
  setIsDeleting(true);
  setDeleteError(null);
  try {
    await deleteAccount(password);       // see updated api-client below
    logout();                             // clears tokens + navigates to /login
  } catch (err) {
    const status = (err as { response?: { status?: number } }).response?.status;
    if (status === 401) {
      setDeleteError("Ungültiges Passwort.");
    } else {
      toast.error("Konto konnte nicht gelöscht werden. Bitte erneut versuchen.");
    }
    setIsDeleting(false);
  }
};

// In the dialog JSX:
<DialogDescription>
  Diese Aktion kann nicht rückgängig gemacht werden. Alle Belege, Artikel,
  Klassifizierungen und dein Token-Guthaben werden dauerhaft gelöscht.
</DialogDescription>

<div className="space-y-3 py-2">
  <p className="text-sm text-muted-foreground">
    Geben Sie zur Bestätigung Ihr Passwort ein.
  </p>
  <Input
    type="password"
    value={password}
    onChange={(e) => { setPassword(e.target.value); setDeleteError(null); }}
    placeholder="Passwort"
    disabled={isDeleting}
    autoComplete="current-password"
    onKeyDown={(e) => {
      if (e.key === "Enter" && password.length > 0) handleDeleteAccount();
    }}
  />
  {deleteError && (
    <p className="text-sm text-destructive">{deleteError}</p>
  )}
</div>

<DialogFooter>
  <Button variant="outline" onClick={() => setDeleteDialogOpen(false)} disabled={isDeleting}>
    Abbrechen
  </Button>
  <Button
    variant="destructive"
    onClick={handleDeleteAccount}
    disabled={password.length === 0 || isDeleting}
  >
    {isDeleting && <Loader2 className="mr-1.5 h-3.5 w-3.5 animate-spin" />}
    Konto löschen
  </Button>
</DialogFooter>
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| `Microsoft.AspNetCore.HttpOverrides.IPNetwork` + `KnownNetworks` | `System.Net.IPNetwork` + `KnownIPNetworks` | .NET 10 Preview 7 (2025) | Source-compat break; warning `ASPDEPR005` [VERIFIED: learn.microsoft.com/en-us/aspnet/core/breaking-changes/10/ipnetwork-knownnetworks-obsolete] |
| `new HMACSHA256(key).ComputeHash(data)` instance pattern | `HMACSHA256.HashData(key, data)` static | .NET 5 (2020) | Zero-allocation; CA1850 analyzer warning if not migrated [VERIFIED: learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/quality-rules/ca1850] |
| Single `users.refresh_token` column | Multi-row `refresh_tokens` table with hash + rotation + replay detection | OAuth 2.1 mandate (2024+) | "Storing one refresh token per user creates session conflicts when users log in from multiple devices" [CITED: copyprogramming.com/howto/how-to-allow-users-to-connect-from-multiple-devices-with-refresh-tokens] |
| Load entities + `Remove` + `SaveChangesAsync` for bulk delete/update | `ExecuteUpdateAsync` / `ExecuteDeleteAsync` | EF Core 7 (2022) | One round-trip, no tracker overhead [VERIFIED: learn.microsoft.com/en-us/ef/core/saving/execute-insert-update-delete] |
| Custom IP-throttling middleware or `AspNetCoreRateLimit` NuGet | Built-in `Microsoft.AspNetCore.RateLimiting` | .NET 7 (2022) | First-party; partitioning + queue + RetryAfter metadata standard [VERIFIED: learn.microsoft.com/en-us/aspnet/core/performance/rate-limit] |

**Deprecated/outdated:**

- `Microsoft.AspNetCore.HttpOverrides.IPNetwork` — DO NOT USE. CONTEXT.md D-06 mentions the API name only generically; planner must translate to `System.Net.IPNetwork` + `KnownIPNetworks` when writing actual code.
- `new HMACSHA256(key).ComputeHash(plaintext)` — use static `HashData` instead.

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | Docker bridge subnet for this compose stack is in `172.16.0.0/12` | D-06 implementation, Pattern 1, Pitfall 9 | If compose uses a custom-named network with explicit IPAM, the trusted range may differ. Verify with `docker network inspect taxreader_default` (or whatever the network is named) before locking the CIDR. CONTEXT D-06 flagged this as Claude's Discretion. |
| A2 | Postgres `inet` column type maps cleanly to `IPAddress?` via Npgsql 10.0.1 | RefreshToken schema, Pattern 3 migration | Standard Npgsql mapping since v3.x. Verify with a single integration test that `IpAddress = IPAddress.Parse("192.0.2.1")` round-trips. [CITED: Npgsql docs but not re-verified here] |
| A3 | `IHttpContextAccessor` is already registered (it is — `Program.cs:77`) and `HttpContext` is available in endpoint signatures via parameter binding | Pattern 1 endpoint code | If not, we'd need to inject `IHttpContextAccessor` into endpoint classes manually. Verified via `Program.cs:77` grep. |
| A4 | `Sentry.CaptureMessage` is the correct API on `Sentry.AspNetCore` 6.4.1 (vs. older `SentrySdk.CaptureMessage`) | RefreshTokenService replay-detection log | Confirmed by checking the existing Sentry call in `SentryScrubbing.cs`. Both `SentrySdk.CaptureMessage` and `SentrySdk.CaptureException` are documented in Sentry 6.x. |
| A5 | Per-endpoint rate-limit policies have access to JWT `sub` claim because they run AFTER authentication middleware in the routed endpoint pipeline | Pitfall 2, Pattern 1 `auth-strict` for `/account` | Documented behavior — `[EnableRateLimiting]` runs after `UseRouting + UseAuthentication` when applied to a routable endpoint. If false, `auth-strict` for `/account` would partition by IP only (anonymous fallback). Acceptable degradation. [CITED: learn.microsoft.com/en-us/aspnet/core/performance/rate-limit "UseRateLimiter must be called after UseRouting"] |
| A6 | The frontend axios refresh-interceptor's `_retry` short-circuit handles a 401 on `/auth/account` correctly without triggering a refresh-loop | Pattern 6 (Axios DELETE) | Behavior verified by reading `api-client.ts:48`: interceptor sets `originalRequest._retry = true` BEFORE the refresh attempt; on a successful refresh, the request is re-fired with the new token. Wrong-password 401 is a fresh request, so the interceptor will try one refresh. If the access token is still valid, the refresh succeeds, the call retries, gets 401 again, refresh-interceptor returns the 401 (because `_retry` is already true). That's the correct surface — the caller gets the 401. BUT: the recommended pattern in Example 5 uses raw `axios` to bypass the interceptor entirely, eliminating ambiguity. Verify the chosen pattern in user testing. |

**If this table is empty:** Not empty — A1 and A6 should be validated during execution.

## Open Questions

1. **Should the rate-limit windows/limits be configurable via env vars, or hardcoded?**
   - What we know: CONTEXT.md D-09 calls out the values explicitly (5/min, 30/min, 60/min) but Claude's Discretion includes "RateLimitOptions.cs (NEW... if windows/limits are made configurable rather than hardcoded)."
   - What's unclear: Whether the operator needs to tune these without redeploy.
   - Recommendation: **Hardcoded constants** in `Program.cs` for Phase 2. The values are tied to Success Criterion #3 ("within 5 attempts/min"); making them runtime-configurable invites accidental over-permissive settings. Phase 7 (QA-06) re-tunes against real traffic and can introduce config at that point.

2. **Should `IpAddress` be stored as `inet` (Postgres native) or `varchar(45)` (max IPv6 textual length)?**
   - What we know: `inet` is more space-efficient, indexable, and supports CIDR queries.
   - What's unclear: Npgsql round-trip behavior for `IPAddress?`.
   - Recommendation: Use `inet`. The existing `EFCore.NamingConventions` + Npgsql 10.0.1 stack handles `IPAddress` → `inet` natively. Add a unit-test round-trip in the test plan. If issues, fall back to `varchar(45)` in the migration.

3. **What happens to in-flight refresh attempts during the migration?**
   - What we know: D-15 accepts "the only victims of the forced re-login are dev/test users." Pre-launch.
   - What's unclear: Whether `RUN_MIGRATIONS=true` causes a small window where the new table exists, the old columns are dropped, but no rows are in `refresh_tokens`.
   - Recommendation: Migration runs at API startup; the period between "Up() executes" and "first user logs in again" is functional. Any /auth/refresh during the migration window fails with 401 — frontend bounces to /login — user re-logs — done. Document this as "migration window: any active session forced to re-login." No mitigation needed.

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| .NET 10 SDK | Backend build | ✓ | 10.0.201 | — |
| `Microsoft.AspNetCore.RateLimiting` | Rate limiter middleware | ✓ (framework-resolved) | 10.0.0 | — |
| `Microsoft.AspNetCore.HttpOverrides` | Forwarded headers | ✓ (framework-resolved) | 10.0.0 | — |
| `System.Net.IPNetwork` | CIDR parsing for KnownIPNetworks | ✓ (BCL since .NET 8) | 10.0.0 | — |
| PostgreSQL 17 (with `inet` type, `gen_random_uuid()`) | Migration + storage | ✓ | 17-alpine | — |
| `BCrypt.Net-Next` | Password re-verify | ✓ | 4.0.3 | — |
| `Sentry.AspNetCore` | Replay-detection event | ✓ | 6.4.1 (Phase 1) | — |
| `Microsoft.AspNetCore.Mvc.Testing` | Rate-limit integration tests | ✓ | 10.0.4 | — |
| Docker Compose | Stack orchestration | ✓ | v2 | — |

**Missing dependencies with no fallback:** None.
**Missing dependencies with fallback:** None.

All Phase 2 dependencies are already in the build graph or part of the .NET 10 framework.

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xUnit 2.9.2 + FluentAssertions 7.0.0 + Moq 4.20.72 + Microsoft.AspNetCore.Mvc.Testing 10.0.4 (already wired) |
| Config file | `Backend/tests/TaxReader.UnitTests/TaxReader.UnitTests.csproj` |
| Quick run command | `dotnet test Backend/tests/TaxReader.UnitTests --filter "FullyQualifiedName~RateLimiting\|FullyQualifiedName~Auth" -c Debug` |
| Full suite command | `dotnet test Backend` |

### Phase Requirements → Test Map

| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| AUTH-01 | Refresh-token rotation: issue → use → new token works → old token fails | integration | `dotnet test Backend --filter "RefreshTokenRotationTests.HappyPath_OldTokenRejected_NewTokenAccepted"` | ❌ Wave 0 |
| AUTH-01 | Replay detection: revoked token presented → all user's tokens revoked + Sentry event | integration | `dotnet test Backend --filter "ReplayDetectionTests.RevokedTokenPresented_RevokesAllTokens"` | ❌ Wave 0 |
| AUTH-01 | HMAC pepper round-trip: `ComputeHash(t)` is deterministic for same key + plaintext, differs across keys | unit | `dotnet test Backend --filter "HmacPepperHashingTests"` | ❌ Wave 0 |
| AUTH-01 | Multi-device: same user can hold ≥2 active rows in `refresh_tokens` simultaneously | integration | `dotnet test Backend --filter "MultiDeviceTokenTests.TwoActiveTokens_BothValidate"` | ❌ Wave 0 |
| AUTH-01 | Migration: `Up()` then `Down()` round-trips on an in-memory schema | integration | `dotnet test Backend --filter "MigrationTests.Add_RefreshTokens_AndDropLegacy"` (manual smoke; in-memory EF doesn't run Postgres DDL) | ❌ Wave 0 — defer real verification to Phase 7 QA-01 (Testcontainers) |
| AUTH-02 | Account-deletion: correct password → 204 No Content + cascade | integration | `dotnet test Backend --filter "DeleteAccountTests.CorrectPassword_Returns204"` | ❌ Wave 0 |
| AUTH-02 | Account-deletion: wrong password → 401 + German error | integration | `dotnet test Backend --filter "DeleteAccountTests.WrongPassword_Returns401_GermanError"` | ❌ Wave 0 |
| AUTH-02 | Account-deletion: refresh tokens revoked BEFORE user delete | integration | `dotnet test Backend --filter "DeleteAccountTests.RevokesTokensBeforeDelete"` | ❌ Wave 0 |
| AUTH-03 | `auth-strict` 5/min on /login from one IP | integration | `dotnet test Backend --filter "AuthStrictPolicyTests.SixthAttempt_Returns429"` | ❌ Wave 0 |
| AUTH-03 | `auth-strict` 5/min on /account partitioned by `sub` (two users from same IP both get 5) | integration | `dotnet test Backend --filter "AuthStrictPolicyTests.TwoUsersOneIp_BothGetFiveAttempts"` | ❌ Wave 0 |
| AUTH-03 | `auth-refresh` 30/min on /refresh | integration | `dotnet test Backend --filter "AuthRefreshPolicyTests"` | ❌ Wave 0 |
| AUTH-03 | `upload-concurrency`: 3rd concurrent upload queued; 7th rejected | integration | `dotnet test Backend --filter "UploadConcurrencyPolicyTests"` | ❌ Wave 0 |
| AUTH-03 | Global 60/min from one IP | integration | `dotnet test Backend --filter "GlobalPolicyTests"` | ❌ Wave 0 |
| AUTH-03 | 429 response: German title "Zu viele Anfragen.", German detail, `Retry-After` header, `application/problem+json` content type | integration | `dotnet test Backend --filter "RejectedResponseShapeTests"` | ❌ Wave 0 |
| AUTH-03 | `UseForwardedHeaders` registered FIRST in pipeline (source-level structural-grep) | unit | `dotnet test Backend --filter "ForwardedHeadersWiringTests"` | ❌ Wave 0 — pattern from existing `SerilogEnrichmentTests` |
| AUTH-03 | `KnownIPNetworks` configured with `172.16.0.0/12` (option resolution) | unit | `dotnet test Backend --filter "ForwardedHeadersTests.KnownIPNetworksContainsDockerSubnet"` | ❌ Wave 0 |

### Sampling Rate
- **Per task commit:** `dotnet test Backend/tests/TaxReader.UnitTests --filter "FullyQualifiedName~RateLimiting\|FullyQualifiedName~Auth\|FullyQualifiedName~RefreshToken\|FullyQualifiedName~DeleteAccount"` (subsumes the Phase 2 surface)
- **Per wave merge:** `dotnet test Backend`
- **Phase gate:** Full suite green before `/gsd-verify-work`

### Wave 0 Gaps
- [ ] `Backend/tests/TaxReader.UnitTests/RateLimiting/AuthStrictPolicyTests.cs` — covers AUTH-03 (login/register/account 5/min)
- [ ] `Backend/tests/TaxReader.UnitTests/RateLimiting/AuthRefreshPolicyTests.cs` — covers AUTH-03 (refresh 30/min)
- [ ] `Backend/tests/TaxReader.UnitTests/RateLimiting/UploadConcurrencyPolicyTests.cs` — covers AUTH-03 (concurrency=2+queue=4)
- [ ] `Backend/tests/TaxReader.UnitTests/RateLimiting/GlobalPolicyTests.cs` — covers AUTH-03 (60/min)
- [ ] `Backend/tests/TaxReader.UnitTests/RateLimiting/RejectedResponseShapeTests.cs` — covers AUTH-03 (German 429 + Retry-After)
- [ ] `Backend/tests/TaxReader.UnitTests/RateLimiting/ForwardedHeadersTests.cs` — covers AUTH-03 (KnownIPNetworks resolution)
- [ ] `Backend/tests/TaxReader.UnitTests/RateLimiting/ForwardedHeadersWiringTests.cs` — source-level structural grep
- [ ] `Backend/tests/TaxReader.UnitTests/Auth/RefreshTokenServiceTests.cs` — covers AUTH-01 (issue, validate, rotate happy path)
- [ ] `Backend/tests/TaxReader.UnitTests/Auth/ReplayDetectionTests.cs` — covers AUTH-01 (replay → revoke-all + Sentry capture)
- [ ] `Backend/tests/TaxReader.UnitTests/Auth/MultiDeviceTokenTests.cs` — covers AUTH-01 (two tokens, same user)
- [ ] `Backend/tests/TaxReader.UnitTests/Auth/HmacPepperHashingTests.cs` — covers AUTH-01 (deterministic, pepper-sensitive)
- [ ] `Backend/tests/TaxReader.UnitTests/Auth/DeleteAccountHandlerTests.cs` — covers AUTH-02 (BCrypt verify, token revoke, cascade)
- [ ] Shared `Backend/tests/TaxReader.UnitTests/Helpers/RateLimitTestFactory.cs` — `WebApplicationFactory<Program>` extension with seed user + access token issuance + short test windows
- [ ] **No new framework install required** — xUnit + WebApplicationFactory + InMemory DB are already in the test project. Tests follow `CorsConfigurationTests.cs` shape.

## Security Domain

### Applicable ASVS Categories (Level 1)

| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V2 Authentication | yes | BCrypt for password hash (existing); refresh-token rotation + replay detection (AUTH-01); password re-verify on destructive operation (AUTH-02) |
| V3 Session Management | yes | Hash-only token storage (AUTH-01 D-01); rotation on every refresh (D-03); revoke-all on replay (D-03); revoke-all on account delete (D-13); access-token expiry 60min, refresh-token expiry 30d |
| V4 Access Control | yes | Per-user data scoping via `ICurrentUser.UserId` filter (existing); `[Authorize]` global on `/api/v1/*` (existing); `.AllowAnonymous()` opt-out only for `/auth/login`, `/auth/register`, `/auth/refresh` |
| V5 Input Validation | yes | FluentValidation for `DeleteAccountRequest.Password` non-empty; `RefreshRequest.RefreshToken` non-empty (existing pattern via `ConfirmClassificationValidator`) |
| V6 Cryptography | yes | HMACSHA256 from BCL (D-01); BCrypt.Net-Next 4.0.3 (existing); JWT HS256 (existing); pepper from env var (256-bit) — NEVER hand-roll |
| V7 Error Handling | yes | German error copy in `Result<T>.Failure`; D-04 generic 401 on replay (no information disclosure); D-12 inline "Ungültiges Passwort." (no enumeration risk because the request is already authenticated) |
| V8 Data Protection | partial | PII (`user.id_hash` only) in Sentry events per Phase 1 D-14; rate-limit responses do not echo `Authorization` or `X-Forwarded-For`; user-agent / IP captured in DB serve session-audit purpose |
| V13 API and Web Service | yes | 429 ProblemDetails with `Retry-After`; per-endpoint rate-limit policies (V13.4 "rate limiting and resource consumption controls") |

### Known Threat Patterns for ASP.NET Core 10 + JWT + Postgres

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| Brute-force login (credential stuffing) | Tampering / EoP | Fixed-window 5/min per IP via `auth-strict` policy (D-09); BCrypt work factor 10 slows per-attempt |
| Refresh-token theft + replay | Spoofing / EoP | Rotation on every use + replay detection that revokes all of user's tokens (D-03) |
| Refresh-token DB leak | Information Disclosure | HMAC-SHA256 with server-side pepper stored separately from DB (D-01); DB-only leak cannot forge a valid token |
| Account-delete via stolen access token | Tampering | Password re-verify required (AUTH-02); rate-limited 5/min per user (D-12) |
| IP-spoofing for rate-limit evasion | Tampering | `UseForwardedHeaders` with `ForwardLimit=1` + `KnownIPNetworks` restricted to Docker bridge; only Caddy can forge `X-Forwarded-For` and Caddy is the trusted single hop |
| SQL injection via refresh token plaintext | Tampering | EF Core parameterized queries via LINQ (project convention "no FromSqlRaw"); LINQ where-clause on `TokenHash` is fully parameterized |
| Timing attack on `BCrypt.Verify` | Information Disclosure | BCrypt.Verify is constant-time by design [VERIFIED: BCrypt-Net-Next 4.0.3 docs] |
| Timing attack on `token_hash == ?` lookup | Information Disclosure | DB index lookup is not constant-time, but the security model is that the attacker doesn't already possess a valid token — they're attacking with arbitrary input. Hash comparison is byte-equality on a hashed value, not on the secret. Acceptable. |
| Session fixation | Spoofing | Rotation means each refresh produces a brand-new token; the attacker can't pre-fix a value |
| CSRF on `DELETE /auth/account` | Spoofing | Bearer JWT (not cookie) means no implicit-credentials surface; same-origin requirement enforced by CORS deny-all default (Phase 1 D-07) |
| Rate-limit DoS by spoofing partition keys | Availability | API is internal-only; only Caddy reaches it. Without spoofable `X-Forwarded-For` (limited by `ForwardLimit=1` + `KnownIPNetworks`), attacker cannot fill the partition table. Mitigated by design. |
| Pepper compromise | Confidentiality | D-01 acceptance: rotating the pepper invalidates all sessions. Backup recovery: `RefreshToken__HashKey` must be backed up alongside `Jwt__Secret`. Out of scope for Phase 2 runbook. |

## Sources

### Primary (HIGH confidence)
- **learn.microsoft.com/en-us/aspnet/core/performance/rate-limit?view=aspnetcore-10.0** — `AddRateLimiter`, `PartitionedRateLimiter.Create`, named policies, `OnRejected`, `RetryAfter` metadata, `.RequireRateLimiting`
- **learn.microsoft.com/en-us/aspnet/core/host-and-deploy/proxy-load-balancer?view=aspnetcore-10.0** — `UseForwardedHeaders`, `ForwardedHeadersOptions`, `ForwardLimit`, pipeline order
- **learn.microsoft.com/en-us/aspnet/core/breaking-changes/10/ipnetwork-knownnetworks-obsolete?view=aspnetcore-10.0** — `KnownNetworks` deprecation, `KnownIPNetworks` replacement, `System.Net.IPNetwork`
- **learn.microsoft.com/en-us/dotnet/api/system.net.ipnetwork** — `IPNetwork.Parse(string)` CIDR syntax
- **learn.microsoft.com/en-us/dotnet/api/system.security.cryptography.hmacsha256.hashdata** — `HMACSHA256.HashData(byte[], byte[])` static API
- **learn.microsoft.com/en-us/ef/core/saving/execute-insert-update-delete** — `ExecuteUpdateAsync`, `ExecuteDeleteAsync`, `SetProperty`
- **caddyserver.com/docs/caddyfile/directives/reverse_proxy** — Default `X-Forwarded-*` header behavior, `trusted_proxies` semantics
- **Codebase** — `Backend/src/TaxReader.Infrastructure/Services/AuthService.cs`, `Program.cs`, `User.cs`, `UserConfiguration.cs`, `AuthEndpoints.cs`, `ReceiptFileEndpoints.cs`, `api-client.ts`, `settings/page.tsx`, `CorsConfigurationTests.cs`, `Directory.Packages.props`

### Secondary (MEDIUM confidence)
- **learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/quality-rules/ca1850** — Static `HashData` preferred over instance `ComputeHash`
- **github.com/axios/axios/issues/897** (resolved) — axios DELETE with body via `data` config
- **learn.microsoft.com/en-us/aspnet/core/performance/rate-limit-samples?view=aspnetcore-10.0** — Reference samples

### Tertiary (LOW confidence — informational)
- **copyprogramming.com/howto/how-to-allow-users-to-connect-from-multiple-devices-with-refresh-tokens** — Multi-device refresh-token patterns (corroborates D-02 schema choice)
- **codesignal.com/learn/courses/preventing-refresh-token-abuse** — General OAuth 2.1 rotation rationale
- **oneuptime.com/blog/post/2025-12-23-aspnet-core-rate-limiting/view** — `RejectionStatusCode` default-503 pitfall (corroborated by MS docs)

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — every dependency is first-party .NET 10 or already in `Directory.Packages.props`. Verified versions via `dotnet --version` and `Directory.Packages.props` grep.
- Architecture: HIGH — pipeline order, named policies, and `IRefreshTokenService` shape are direct applications of MS docs to the locked decisions in CONTEXT.md.
- Pitfalls: HIGH — Pitfalls 1 (KnownNetworks deprecation), 2 (rate limiter order vs auth), 5 (RejectionStatusCode default), and 6 (axios DELETE body) are confirmed against authoritative docs.
- Validation: HIGH — test pattern (`WebApplicationFactory<Program>`) is established at `Backend/tests/TaxReader.UnitTests/Cors/CorsConfigurationTests.cs`.
- Security: MEDIUM — ASVS L1 mapping is straightforward; the pepper-rotation runbook is explicitly deferred (D-01 note).

**Research date:** 2026-05-12
**Valid until:** 2026-06-11 (30 days — .NET 10 + Caddy + Sentry stack are stable LTS surface)

---
*Phase: 02-auth-rate-limit-hardening*
*Researched: 2026-05-12*
