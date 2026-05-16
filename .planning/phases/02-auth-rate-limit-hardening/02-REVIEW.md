---
phase: 02-auth-rate-limit-hardening
reviewed: 2026-05-16T00:00:00Z
depth: standard
files_reviewed: 40
files_reviewed_list:
  - .env.example
  - Backend/src/TaxReader.Api/Endpoints/AuthEndpoints.cs
  - Backend/src/TaxReader.Api/Endpoints/ReceiptFileEndpoints.cs
  - Backend/src/TaxReader.Api/Program.cs
  - Backend/src/TaxReader.Application/Commands/DeleteAccountHandler.cs
  - Backend/src/TaxReader.Application/DTOs/AuthDtos.cs
  - Backend/src/TaxReader.Application/Interfaces/IAppDbContext.cs
  - Backend/src/TaxReader.Application/Interfaces/IAuthService.cs
  - Backend/src/TaxReader.Application/Interfaces/IRefreshTokenService.cs
  - Backend/src/TaxReader.Application/TaxReader.Application.csproj
  - Backend/src/TaxReader.Application/Validators/DeleteAccountValidator.cs
  - Backend/src/TaxReader.Domain/Entities/RefreshToken.cs
  - Backend/src/TaxReader.Domain/Entities/User.cs
  - Backend/src/TaxReader.Infrastructure/Configuration/RefreshTokenOptions.cs
  - Backend/src/TaxReader.Infrastructure/Data/AppDbContext.cs
  - Backend/src/TaxReader.Infrastructure/Data/Configurations/RefreshTokenConfiguration.cs
  - Backend/src/TaxReader.Infrastructure/Data/Configurations/UserConfiguration.cs
  - Backend/src/TaxReader.Infrastructure/DependencyInjection.cs
  - Backend/src/TaxReader.Infrastructure/Migrations/20260514204609_AddRefreshTokensTable_DropLegacyRefreshTokenColumns.cs
  - Backend/src/TaxReader.Infrastructure/Migrations/AppDbContextModelSnapshot.cs
  - Backend/src/TaxReader.Infrastructure/Services/AuthService.cs
  - Backend/src/TaxReader.Infrastructure/Services/RefreshTokenService.cs
  - Backend/tests/TaxReader.UnitTests/Application/Validators/DeleteAccountValidatorTests.cs
  - Backend/tests/TaxReader.UnitTests/Auth/DeleteAccountHandlerTests.cs
  - Backend/tests/TaxReader.UnitTests/Auth/HmacPepperHashingTests.cs
  - Backend/tests/TaxReader.UnitTests/Auth/MigrationTests.cs
  - Backend/tests/TaxReader.UnitTests/Auth/MultiDeviceTokenTests.cs
  - Backend/tests/TaxReader.UnitTests/Auth/RefreshTokenServiceTests.cs
  - Backend/tests/TaxReader.UnitTests/Auth/ReplayDetectionTests.cs
  - Backend/tests/TaxReader.UnitTests/Helpers/RateLimitTestFactory.cs
  - Backend/tests/TaxReader.UnitTests/RateLimiting/AuthRefreshPolicyTests.cs
  - Backend/tests/TaxReader.UnitTests/RateLimiting/AuthStrictPolicyTests.cs
  - Backend/tests/TaxReader.UnitTests/RateLimiting/ForwardedHeadersTests.cs
  - Backend/tests/TaxReader.UnitTests/RateLimiting/ForwardedHeadersWiringTests.cs
  - Backend/tests/TaxReader.UnitTests/RateLimiting/GlobalPolicyTests.cs
  - Backend/tests/TaxReader.UnitTests/RateLimiting/RateLimiterTestCollection.cs
  - Backend/tests/TaxReader.UnitTests/RateLimiting/RejectedResponseShapeTests.cs
  - Backend/tests/TaxReader.UnitTests/RateLimiting/UploadConcurrencyPolicyTests.cs
  - Frontend/src/app/(authenticated)/settings/page.tsx
  - Frontend/src/lib/api-client.ts
findings:
  critical: 2
  warning: 9
  info: 6
  total: 17
status: issues_found
---

# Phase 2: Code Review Report

**Reviewed:** 2026-05-16
**Depth:** standard
**Files Reviewed:** 40
**Status:** issues_found

## Summary

Phase 2 hardens auth with HMAC-pepper refresh-token rotation, replay-revoke-all, password re-auth on account delete, and four rate-limit policies behind ForwardedHeaders. The security primitives (HMAC-SHA256 hashing, BCrypt re-verify, generic replay error per D-04) are correctly implemented. The pipeline ordering for rate-limit + forwarded-headers matches the test guards.

However, the review surfaces two **BLOCKER** correctness defects:

1. The `RefreshTokenService` constructor parses the Base64 pepper at construction time with `Convert.FromBase64String`. When `REFRESHTOKEN_HASHKEY` is unset (default in `.env.example`), the empty string parses to a **zero-length byte array** — every HMAC then collapses to a constant per-input value computed with an empty key. This is silently insecure: the system boots, tokens "work", but the pepper provides zero security and the failure mode is undetectable from operational signals.

2. `DeleteAccountValidator` exists and pins German messages in tests, but **is never invoked**: the `DeleteAccountRequest` is bound by `[FromBody]` and passed straight to the handler. ASP.NET Core Minimal APIs do **not** auto-invoke FluentValidation; `AddValidatorsFromAssemblyContaining<>` only registers the validators in DI. This same defect applies to every other command in the codebase, but it is in scope for this review because the new `DeleteAccountValidator` was authored as part of AUTH-02 and its tests imply it is enforced.

Additional warnings cluster around: English error string leakage on a German-localized surface; brittle endpoint-side string comparison to differentiate 401 vs 404; provider-name string compare for transactional fallback; missing pre-flight validation that `RefreshToken:HashKey` is 32 bytes; an unused `ICollection<RefreshToken>` navigation pinned by `OnDelete(Cascade)`; and rate-limit test cleanup that may flake under WAF/sequential ordering.

## Critical Issues

### CR-01: HMAC pepper silently degrades to empty-key HMAC when `REFRESHTOKEN_HASHKEY` is unset

**File:** `Backend/src/TaxReader.Infrastructure/Services/RefreshTokenService.cs:31`
**Issue:** The constructor pre-computes the pepper bytes via `Convert.FromBase64String(refreshTokenOptions.Value.HashKey)`. `RefreshTokenOptions.HashKey` defaults to `""` (see `RefreshTokenOptions.cs:12`), and `.env.example` ships `REFRESHTOKEN_HASHKEY=` (empty). `Convert.FromBase64String("")` returns a zero-length byte array — this is **not** an error. `HMACSHA256.HashData(_pepper, plaintextBytes)` then runs HMAC with a zero-length key. The resulting hash is fully determined by the plaintext + the public SHA-256 construction, providing zero pepper protection. Service starts, tokens validate, replay detection works in isolated tests — but a DB leak in production fully exposes every plaintext refresh token to anyone with HMAC-SHA256 (i.e., everyone). This nullifies the core premise of D-01.

Worse, the failure is *silent*: the API logs `"Anthropic configuration resolved: ..."` at startup but never logs the pepper byte length or whether it is empty. Operators have no signal that they shipped with an unset pepper.

**Fix:** Fail fast at startup if the pepper is missing or wrong length. Add a guarded resolution in the constructor or, preferably, a startup `IValidateOptions<RefreshTokenOptions>`:

```csharp
public class RefreshTokenService(...) : IRefreshTokenService
{
    private readonly byte[] _pepper = ValidatePepper(refreshTokenOptions.Value.HashKey);

    private static byte[] ValidatePepper(string base64)
    {
        if (string.IsNullOrWhiteSpace(base64))
            throw new InvalidOperationException(
                "RefreshToken:HashKey is not configured. Generate with `openssl rand -base64 32` and set REFRESHTOKEN_HASHKEY.");

        byte[] bytes;
        try { bytes = Convert.FromBase64String(base64); }
        catch (FormatException ex)
        {
            throw new InvalidOperationException(
                "RefreshToken:HashKey is not valid Base64.", ex);
        }

        if (bytes.Length != 32)
            throw new InvalidOperationException(
                $"RefreshToken:HashKey must be exactly 32 bytes (got {bytes.Length}). Generate with `openssl rand -base64 32`.");

        return bytes;
    }
    // ...
}
```

Alternatively, register a `PostConfigure<RefreshTokenOptions>` validator in `DependencyInjection.cs` with `services.AddOptions<RefreshTokenOptions>().Validate(...).ValidateOnStart()`.

### CR-02: `DeleteAccountValidator` is registered but never invoked — the endpoint accepts any payload

**File:** `Backend/src/TaxReader.Api/Endpoints/AuthEndpoints.cs:76-92`
**Issue:** The endpoint signature is:

```csharp
auth.MapDelete("/account", async (
    [FromBody] DeleteAccountRequest request,
    DeleteAccountHandler handler,
    CancellationToken cancellationToken) =>
{
    var result = await handler.HandleAsync(request, cancellationToken);
    // ...
});
```

ASP.NET Core Minimal APIs do **not** run FluentValidation automatically. `AddValidatorsFromAssemblyContaining<UploadReceiptFilesCommand>` in `Program.cs:87` only registers `IValidator<T>` services in DI; nothing actually pulls one and calls `ValidateAsync` for any handler in this codebase (`grep -r "IValidator<" Backend/src` returns zero hits outside the validator definitions). The handler runs first.

Functional impact for this surface:
- An empty password (`""`) bypasses validation, goes to the handler, `BCrypt.Verify("", user.PasswordHash)` returns false, and the user gets the generic `"Ungültiges Passwort."` 401. The user-experience is "fine," but the test `Validate_EmptyPassword_FailsWithGermanMessage` is testing dead code — there is no production path that exercises that validator.
- A `null` password in the JSON body (`{"password": null}`) deserialises into `DeleteAccountRequest("")` is wrong — `request.Password` is annotated non-nullable (NRT), but `[FromBody]` may produce a non-null record with `Password = null!`. `BCrypt.Net.BCrypt.Verify(null, ...)` throws `ArgumentNullException`, which the `ExceptionHandlingMiddleware` translates to **500 Internal Server Error** — not 400 with the German validation message.
- The same defect applies broadly to every other validator in `Application/Validators/`, but only the AUTH-02-authored validator is in scope for this review.

**Fix:** Either (a) invoke validators explicitly inside endpoint handlers, or (b) install a generic validation filter:

Option A (per-endpoint):
```csharp
auth.MapDelete("/account", async (
    [FromBody] DeleteAccountRequest request,
    DeleteAccountHandler handler,
    IValidator<DeleteAccountRequest> validator,
    CancellationToken cancellationToken) =>
{
    var validation = await validator.ValidateAsync(request, cancellationToken);
    if (!validation.IsValid)
        return Results.ValidationProblem(validation.ToDictionary());

    var result = await handler.HandleAsync(request, cancellationToken);
    // ...
});
```

Option B (endpoint filter, applied to a route group):
```csharp
public static RouteHandlerBuilder WithValidation<T>(this RouteHandlerBuilder builder)
    => builder.AddEndpointFilter(async (ctx, next) =>
    {
        var arg = ctx.Arguments.OfType<T>().FirstOrDefault();
        var validator = ctx.HttpContext.RequestServices.GetService<IValidator<T>>();
        if (arg is not null && validator is not null)
        {
            var result = await validator.ValidateAsync(arg, ctx.HttpContext.RequestAborted);
            if (!result.IsValid)
                return Results.ValidationProblem(result.ToDictionary());
        }
        return await next(ctx);
    });

auth.MapDelete("/account", ...).WithValidation<DeleteAccountRequest>();
```

If the intent is to rely on the handler's BCrypt check as the gate (no validator needed), then **delete** `DeleteAccountValidator.cs` and `DeleteAccountValidatorTests.cs` per the CLAUDE.md "no abstractions for single-use code" rule. Either invoke it or remove it — leaving it registered creates a misleading defense-in-depth claim in `02-02-SUMMARY.md`.

## Warnings

### WR-01: English error string leaks to German-localized surface

**File:** `Backend/src/TaxReader.Application/Commands/DeleteAccountHandler.cs:23`
**Issue:** Handler returns `Result<bool>.Failure("User not found.")` — English. The endpoint emits this verbatim in `Results.NotFound(new { error = result.Error })` (`AuthEndpoints.cs:91`). CLAUDE.md and `.planning/codebase/CONVENTIONS.md` both pin user-facing strings as German on this surface (e.g., `"Ungültige E-Mail oder Passwort."`, `"Ein Konto mit dieser E-Mail existiert bereits."`). This violates the localization contract. Practically unreachable on the deletion path (the JWT must have a sub that was valid at issue time), but a deleted-and-deleted-again race (or a stale JWT after manual DB cleanup) would surface this English string in the UI.

**Fix:**
```csharp
if (user is null)
    return Result<bool>.Failure("Benutzer nicht gefunden.");
```

### WR-02: Endpoint discriminates 401 vs 404 via brittle string comparison

**File:** `Backend/src/TaxReader.Api/Endpoints/AuthEndpoints.cs:88-91`
**Issue:**
```csharp
if (result.Error == "Ungültiges Passwort.")
    return Results.Json(new { error = result.Error }, statusCode: 401);

return Results.NotFound(new { error = result.Error });
```

The endpoint reaches into the handler's error string to decide HTTP status. Any future change to the German wording in `DeleteAccountHandler.cs:28` (typo fix, trailing space, capitalization change) silently downgrades 401 → 404, and the frontend will stop surfacing the inline wrong-password message — it will think the dialog is over and the user is gone. This pattern also encourages spreading magic strings across layers and breaks the encapsulation promise of `Result<T>`.

**Fix:** Surface failure kind structurally. Either return a discriminated-union–style `Result<T, FailureKind>`, or model the handler return as a tagged DTO. The minimal change that survives:

```csharp
// In DeleteAccountHandler — return a typed error enum.
public enum DeleteAccountError { UserNotFound, InvalidPassword }

public async Task<Result<DeleteAccountError?>> HandleAsync(...)
{
    if (user is null) return Result<DeleteAccountError?>.Success(DeleteAccountError.UserNotFound);
    if (!BCrypt.Verify(...)) return Result<DeleteAccountError?>.Success(DeleteAccountError.InvalidPassword);
    // ... cascade ...
    return Result<DeleteAccountError?>.Success(null);
}
```

Or, more minimally, introduce a const `DeleteAccountHandler.InvalidPasswordError` exposed by the handler and reference that const from the endpoint instead of duplicating the literal.

### WR-03: `BCrypt.Verify` not guarded against null/empty password

**File:** `Backend/src/TaxReader.Application/Commands/DeleteAccountHandler.cs:27`
**Issue:** `BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash)` — `request.Password` is declared non-null by the record's nullable annotations, but JSON deserialization can produce a null field even when the type is non-nullable (NRTs are compile-time only). `BCrypt.Net.BCrypt.Verify(null, ...)` throws `ArgumentNullException`, which the `ExceptionHandlingMiddleware` returns as 500 Internal Server Error rather than 401. Combined with CR-02 (validator not invoked), this is the actual failure path for a malformed payload.

**Fix:** Add an explicit null/empty guard before the BCrypt call:

```csharp
if (string.IsNullOrEmpty(request.Password))
    return Result<bool>.Failure("Ungültiges Passwort.");

if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
    return Result<bool>.Failure("Ungültiges Passwort.");
```

This is the right answer even if CR-02 is fixed, because a passing FluentValidation pre-check still leaves the in-process handler with no defensive guard.

### WR-04: Provider-name string compare for transactional fallback is fragile

**File:** `Backend/src/TaxReader.Infrastructure/Services/RefreshTokenService.cs:154-155`
**Issue:**
```csharp
private static bool IsInMemoryProvider(DbContext context)
    => context.Database.ProviderName == "Microsoft.EntityFrameworkCore.InMemory";
```

This branches production behavior on an EF-internal magic string. If Microsoft renames or namespaces the provider (e.g., the EF Core 11 InMemory rename) every test starts failing silently. The reverse risk is worse: if a new relational provider lacks `ExecuteUpdateAsync`, the test suite passes but production throws at runtime.

The comment on lines 127-131 explicitly acknowledges this is a test-only escape hatch — putting test-only branching into production code is a CLAUDE.md "Simplicity First" violation ("No abstractions for single-use code", "No flexibility... that wasn't requested"). The simpler approach: have tests use Testcontainers (already planned in Phase 7 QA-01 per `MigrationTests.cs`) or use SQLite-in-memory (which supports `ExecuteUpdateAsync` since EF Core 7). Then the production code can call `ExecuteUpdateAsync` unconditionally.

**Fix:** Migrate refresh-token tests to SQLite-in-memory (one-line change in test factory) and drop the provider-name fork:

```csharp
public async Task<int> RevokeAllForUserAsync(Guid userId, CancellationToken ct = default)
    => await dbContext.RefreshTokens
        .Where(t => t.UserId == userId && t.RevokedAt == null)
        .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, DateTime.UtcNow), ct);
```

If the migration to SQLite is out of scope, at minimum extract the provider name into a `const string` field with a comment pointing to a tracking ticket.

### WR-05: `RefreshToken` ICollection navigation on `User` is unused but pinned by Cascade

**File:** `Backend/src/TaxReader.Domain/Entities/User.cs:21`, `Backend/src/TaxReader.Infrastructure/Data/Configurations/UserConfiguration.cs:27-30`
**Issue:** `User.RefreshTokens` collection is added to the entity and a `HasMany().WithOne().OnDelete(Cascade)` is configured, but the application never loads tokens via this navigation — every access uses `dbContext.RefreshTokens.Where(t => t.UserId == ...)` directly. The navigation only exists to wire cascade delete. That's fine, but the inverse navigation `RefreshToken.User` (`RefreshToken.cs:18`) is also unused and forces every `RefreshToken` query into a join trap if any test author writes `.Include(t => t.User)` unaware of the entity bloat. CLAUDE.md "Simplicity First" — remove unused.

**Fix:** Either:
- Drop the collection navigation and use shadow-FK cascade (`builder.HasOne<User>().WithMany().HasForeignKey(t => t.UserId).OnDelete(Cascade)`), removing both `User.RefreshTokens` and `RefreshToken.User`.
- Keep cascade by leaving the configuration alone, but document why the navigation is intentionally kept and never traversed.

Minor — accept as-is if the team prefers the symmetry with the other User collections.

### WR-06: `ExpiresAt < DateTime.UtcNow` boundary is exclusive — exact-second match is treated as valid

**File:** `Backend/src/TaxReader.Infrastructure/Services/RefreshTokenService.cs:79`
**Issue:**
```csharp
if (existing.ExpiresAt < DateTime.UtcNow)
```

If `ExpiresAt == DateTime.UtcNow` (down to the tick), the token is treated as still valid. Postgres stores `timestamp with time zone` at microsecond precision; UTC clock-skew between API replicas (none today, but possible) plus a 30-day TTL drift can land exactly here. The convention everywhere else in C# auth code is `<=` for "expired or expiring now". Functionally irrelevant given the 30-day TTL granularity, but it is an off-by-one in a security-relevant predicate.

**Fix:**
```csharp
if (existing.ExpiresAt <= DateTime.UtcNow)
```

### WR-07: Rate-limit test factory uses a real Postgres connection string and `Timeout=1`

**File:** `Backend/tests/TaxReader.UnitTests/Helpers/RateLimitTestFactory.cs:32-33`
**Issue:**
```csharp
builder.UseSetting(
    "ConnectionStrings:DefaultConnection",
    "Host=localhost;Port=5432;Database=test;Username=test;Password=test;Timeout=1;Command Timeout=1");
```

`AuthStrictPolicyTests` and `RejectedResponseShapeTests` execute 5+ `/auth/login` requests. Each request:
1. Hits the auth-strict limiter (consumes a permit) — desired.
2. Reaches `AuthService.LoginAsync`, which calls `dbContext.Users.AnyAsync(...)` — undesired.

With no running Postgres on the test agent, Npgsql times out after ~1s and `LoginAsync` either propagates the exception (caught by `ExceptionHandlingMiddleware` → 500) or chokes inside EF Core. The test asserts `BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.BadRequest)` on lines 30-32 of `AuthStrictPolicyTests.cs` — **500 is not in that list**. If the test agent (CI runner) lacks Postgres on localhost:5432, this test fails before reaching the 6th attempt.

Worse, with `Timeout=1` plus 5 retries serialized, total wall time can drift toward the rate-limit window (60s). The risk is timing flakiness, not silent miss.

**Fix:** Either (a) override `IAuthService` with a stub fake in the test factory, or (b) override the `AppDbContext` registration with `UseInMemoryDatabase` for the rate-limit tests. The factory's premise — using a fast-failing connection so requests "fail fast" — is fragile against changes to `AuthService` or `ExceptionHandlingMiddleware` behavior.

### WR-08: `auth-strict` partition for `/login` ends up IP-only because policy reads `httpContext.User` before authentication

**File:** `Backend/src/TaxReader.Api/Program.cs:141-157`, pipeline order in lines 269-278
**Issue:** Comment in `AuthStrictPolicyTests.cs:55-63` flags this as a deferred test. Worth flagging in code review: `app.UseRateLimiter()` (line 276) runs **before** `app.UseAuthentication()` (line 277). Endpoint-attached policies execute after routing, but the JWT validation that populates `httpContext.User` with the `sub` claim happens in `UseAuthentication`. For anonymous endpoints (`/login`, `/register`, `/refresh`), `httpContext.User.FindFirst("sub")?.Value` will always be null — so the partition is always `ip:{...}`. That matches the intended behavior for `/login` and `/register`. But for `/account` (authenticated), the policy resolution depends on the routing+auth sequencing inside `UseRateLimiter`. The "RESEARCH Pitfall 2" comment claims this works because endpoint policies run after routing/auth — verify with an end-to-end test before launch (the skipped `TwoUsersOneIp_BothGetFiveAttempts` test in `AuthStrictPolicyTests.cs:55`).

**Fix:** Un-skip `AuthStrictPolicyTests.TwoUsersOneIp_BothGetFiveAttempts` and verify that two different `sub` claims on `/account` get distinct buckets. If they share a bucket (because `httpContext.User` is empty at policy resolution time), move `UseRateLimiter` AFTER `UseAuthentication` (which would change the global limiter's pre-auth coverage — a behavior trade-off). At minimum, replace the skip with an in-test JWT signing to validate the assertion.

### WR-09: `Sentry.CaptureMessage` runs unconditionally — coupling the Application port to Sentry/observability

**File:** `Backend/src/TaxReader.Infrastructure/Services/RefreshTokenService.cs:89-92`
**Issue:**
```csharp
SentrySdk.CaptureMessage(
    "Refresh token replay detected",
    scope => scope.SetExtra("user.id_hash", HashUserId(existing.UserId)),
    SentryLevel.Warning);
```

The `RefreshTokenService` is an Infrastructure implementation, so a direct Sentry dependency is architecturally OK. But:
- When Sentry is unconfigured (DSN empty per `.env.example`), `SentrySdk.CaptureMessage` is a no-op — fine.
- When Sentry is misconfigured (DSN set, network down), this can block on the SDK's internal HTTP send. The SDK is async-fire-and-forget by default in .NET, so the risk is low, but it adds an uncontrolled side effect to a hot security path.
- The structured log on line 88 already captures the replay event with `UserId` in `LogContext`. Sending the same signal to two sinks (Serilog + Sentry) duplicates work and risks divergent retention/scrubbing.

This is a minor architectural smell. The replay signal needs to page someone — that's the point of going to Sentry — but it should be triggered by an alert rule on the structured log, not a duplicated direct call.

**Fix:** Either drop the explicit `SentrySdk.CaptureMessage` and configure a Sentry alert rule on `logger.LogWarning("Refresh token replay detected")`, or keep the Sentry call but extract it behind an `IReplayAlertService` so the unit test layer can verify it fires without taking a hard dependency on `SentrySdk`. Lower priority — accept-as-is if Phase 6 LEG-08 audit-log work supersedes it.

## Info

### IN-01: `RefreshTokenService.IssueAsync` does not log the issued token's ID

**File:** `Backend/src/TaxReader.Infrastructure/Services/RefreshTokenService.cs:56`
**Issue:** `logger.LogInformation("Refresh token issued for {UserId}", userId);` — useful, but without the token row Id it's hard to correlate an issue → rotate → revoke chain in logs. The structured log on line 88 ("Replay of revoked refresh token") has no token-id either.
**Fix:** Add `{TokenId}` placeholder with the new row's Guid. Same on rotation logs (lines 81, 118) and replay log (88).

### IN-02: `ProcessingRun` cascade comment in `DeleteAccountHandler` is misleading

**File:** `Backend/src/TaxReader.Application/Commands/DeleteAccountHandler.cs:37-42`
**Issue:** Comment claims cascade drops `ProcessingRuns` via `ReceiptFiles → ProcessingRuns`. That cascade is set up in `ReceiptFileConfiguration`, not `UserConfiguration`. The comment block is correct in effect but mis-attributes the cascade origin, which will confuse the next maintainer. Also list-format is incomplete — missing `ItemClassification` and the inverse chain.
**Fix:** Either remove the diagram (the cascade is documented in the entity configs) or anchor it with file:line references to where each cascade is configured.

### IN-03: `RefreshTokenService.IssueAsync` does not verify the user exists

**File:** `Backend/src/TaxReader.Infrastructure/Services/RefreshTokenService.cs:34-58`
**Issue:** `IssueAsync(userId, ...)` writes a `RefreshToken` row with `UserId = userId` and relies on the FK constraint to reject orphan inserts. Postgres will throw `PostgresException` on the SaveChanges call if the user doesn't exist — caught by `ExceptionHandlingMiddleware` as 500. The only caller (`AuthService.RegisterAsync`) does a `SaveChangesAsync` for the user *first* (line 77), then issues — fine in practice. Worth a comment that the FK is load-bearing.
**Fix:** Add a one-line comment above the `dbContext.RefreshTokens.Add(row)` call:
```csharp
// Caller must have persisted the User row first — the FK constraint enforces this.
```

### IN-04: `deleteAccount` in `api-client.ts` uses raw axios but loses the request interceptor's bearer token retry semantic

**File:** `Frontend/src/lib/api-client.ts:126-136`
**Issue:** Comment explains the intent (avoid the 401 → refresh → redirect loop on wrong-password). Correct. But the function calls `getAccessToken()` directly. If the access token is already expired at the time of the click (user opened the dialog 60 minutes ago), the backend returns 401 because the JWT is invalid — not because the password was wrong. The frontend then shows `"Ungültiges Passwort."` (per the dialog's 401 branch) — misleading.
**Fix:** Either (a) trigger a refresh-token roundtrip just before the DELETE to ensure the JWT is fresh, or (b) parse the response body of the 401 — the backend wraps wrong-password 401s with `{"error": "Ungültiges Passwort."}` (per `AuthEndpoints.cs:88-89`), and JWT-expired 401s have no such body. Discriminate on body shape. Low priority but a real UX rough edge.

### IN-05: Magic numbers in rate-limit configuration are not centralized

**File:** `Backend/src/TaxReader.Api/Program.cs:132, 152, 166, 181-183`
**Issue:** `PermitLimit = 60`, `5`, `30`, and `PermitLimit = 2`/`QueueLimit = 4` are scattered as inline magic numbers. The plan document references them (`D-09` "60/min", `D-05` "30/min", `D-07` "concurrency=2 + queue=4"), but a future ops-tuning change has to grep across the file. Tests are pinned to these numbers (`for (var i = 0; i < 60; i++)` in `GlobalPolicyTests.cs:24`) so a config change forces test edits.
**Fix:** Lift to a `RateLimitOptions` POCO bound from `appsettings.json` (already a CLAUDE.md "Configuration" convention pattern). Defer to Phase 6/7 if SCRUM allows.

### IN-06: Tests embed plaintext BCrypt-hashed passwords with insecure work factor

**File:** `Backend/tests/TaxReader.UnitTests/Auth/DeleteAccountHandlerTests.cs:54, 76, 110`
**Issue:** Tests call `BCrypt.Net.BCrypt.HashPassword("mypassword123")` per-test. BCrypt's default work factor is 11; each call burns ~50ms of CPU per hash. With three tests each computing two hashes (`HashPassword` + `Verify`), the suite spends ~300ms on BCrypt alone. For a unit-test suite that should be sub-second per file, this is wasteful.
**Fix:** Hash once in a `[ClassData]` or static field with work factor 4 (`BCrypt.HashPassword(pw, 4)`), or stub `BCrypt.Verify` behind an `IPasswordHasher` port. Minor.

---

_Reviewed: 2026-05-16_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_
