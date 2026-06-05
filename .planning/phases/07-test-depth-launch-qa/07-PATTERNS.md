# Phase 7: Test Depth + Launch QA - Pattern Map

**Mapped:** 2026-06-05
**Files analyzed:** 18 new/modified (5 backend integration, 3 backend unit, 2 backend src/config, 5 frontend test config/spec, 1 CI, 2 docs)
**Analogs found:** 14 / 18 (4 have no in-repo analog — frontend tests are greenfield, but real signatures excerpted below)

This phase is almost entirely **test + config authoring**. There are no new product features. The job of the planner/executor is to *copy existing harness shapes* and *match real signatures of the code under test* — not invent abstractions. Every excerpt below is load-bearing for that.

---

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|-------------------|------|-----------|----------------|---------------|
| `Backend/tests/TaxReader.IntegrationTests/TaxReader.IntegrationTests.csproj` | config (test project) | n/a | `Backend/tests/TaxReader.UnitTests/TaxReader.UnitTests.csproj` + `Directory.Packages.props` | exact |
| `…/IntegrationTests/Fixtures/PostgresContainerFixture.cs` + `IntegrationTestCollection.cs` | test fixture | n/a (lifecycle) | `RateLimiterTestCollection.cs` (collection-def) + `RateLimitTestFactory.cs` (WAF build) | role-match |
| `…/IntegrationTests/IntegrationTestWebAppFactory.cs` | test factory | request-response | `RateLimitTestFactory.cs` / `CookieAuthIntegrationTests.CreateFactoryWithInMemoryDb` | exact (invert: keep Npgsql, point at container) |
| `…/IntegrationTests/PaymentIdempotencyTests.cs` | test (integration) | CRUD / constraint | `Webhooks/StripeWebhookHandlerTests.cs` (the in-memory variant being upgraded) | role-match |
| `…/IntegrationTests/RefreshTokenRotationReplayTests.cs` | test (integration) | CRUD / constraint | `Auth/RefreshTokenServiceTests.cs` + `Auth/ReplayDetectionTests.cs` | exact (port to real DB) |
| `…/IntegrationTests/{DuplicateDetection,CascadeDelete,MigrationSmoke}Tests.cs` | test (integration) | CRUD / DDL | `Auth/MigrationTests.cs` (the `[Skip]`ed placeholder this fulfills) | role-match |
| `…/UnitTests/Services/AuthServiceTests.cs` (D-01) | test (unit) | request-response | `Auth/RefreshTokenServiceTests.cs` (constructor-as-setup, in-memory DB) | exact |
| `…/UnitTests/Services/TokenServiceTests.cs` (D-01) | test (unit) | CRUD (ledger) | `Auth/RefreshTokenServiceTests.cs` | exact |
| `…/UnitTests/Services/AiOnlyClassificationServiceTests.cs` (D-01) | test (unit) | event-driven (batch) | `Services/RuleBasedClassifierTests.cs` / `RefreshTokenServiceTests.cs` (Moq for `IAiClassifier`/`ITokenService`) | role-match |
| `Backend/src/TaxReader.Api/Endpoints/HealthEndpoints.cs` (OBS-03) | endpoint | request-response | `Endpoints/TokenEndpoints.cs` + `AuthEndpoints.cs` `.AllowAnonymous()` | exact |
| `Backend/src/TaxReader.Infrastructure/Services/*HealthCheck.cs` (OBS-03) | service (health) | request-response | `Services/AuthService.cs` (primary-ctor DI, `IAppDbContext`) | role-match |
| `Frontend/vitest.config.mts` + `vitest.setup.ts` | config | n/a | **NO analog** — use bundled Next.js 16 `vitest.md` + RESEARCH Pattern 3 | none |
| `Frontend/playwright.config.ts` + `e2e/happy-path.spec.ts` | config + test (e2e) | request-response | **NO analog** — use bundled Next.js 16 `playwright.md` + RESEARCH Pattern 5 | none |
| `Frontend/src/**/*.test.tsx` (Vitest unit/component) | test (unit) | n/a | **NO test analog** — match real signatures of `api-client.ts`, `format.ts`, `classify-dialog.tsx`, `upload-form.tsx` (excerpted below) | none (signatures provided) |
| `.github/workflows/ci.yml` (heavy job + DE guard) | config (CI) | n/a | existing `ci.yml` 3 jobs + 06-07 legal-placeholder bash guard | exact |
| `PITFALLS.md` (QA-07) | docs | n/a | **NO analog** — author fresh per CONTEXT D + RESEARCH "Common Pitfalls" | none |

---

## Pattern Assignments

### Backend Integration Project — csproj + CPM (QA-01)

**Analog:** `Backend/tests/TaxReader.UnitTests/TaxReader.UnitTests.csproj` + `Backend/Directory.Packages.props`.

**Central Package Management rule (load-bearing):** new NuGet versions go in `Backend/Directory.Packages.props` as `<PackageVersion>`; the csproj references them **version-less**. Add these two lines to `Directory.Packages.props` ItemGroup (Testcontainers/Respawn pinned per REQUIREMENTS):
```xml
<PackageVersion Include="Testcontainers.PostgreSql" Version="4.12.0" />
<PackageVersion Include="Respawn" Version="6.2.1" />
```
The new `.csproj` then carries version-less `<PackageReference Include="Testcontainers.PostgreSql" />`, `<PackageReference Include="Respawn" />`, plus the already-central `Microsoft.AspNetCore.Mvc.Testing`, `Npgsql.EntityFrameworkCore.PostgreSQL`, `xunit`, `FluentAssertions`, `Moq`. Add a `<ProjectReference>` to `TaxReader.Api` (so `WebApplicationFactory<Program>` resolves `Program`) and `TaxReader.Infrastructure`.

---

### `PostgresContainerFixture.cs` + `IntegrationTestCollection.cs` (QA-01 harness)

**Analog:** `Backend/tests/TaxReader.UnitTests/RateLimiting/RateLimiterTestCollection.cs` — the proven serialization pattern. **Copy this verbatim shape** for the integration collection (the WAF-parallelism crash it documents applies identically here — RESEARCH Pitfall 2):
```csharp
// RateLimiterTestCollection.cs:11-15 — the exact attribute to replicate
[CollectionDefinition(Name, DisableParallelization = true)]
public class RateLimiterTestCollection
{
    public const string Name = "RateLimiter integration tests (sequential)";
}
```
Replicate as `[CollectionDefinition(Name, DisableParallelization = true)] ICollectionFixture<PostgresContainerFixture>`. The `DisableParallelization = true` is non-negotiable — top-level `Program.cs` (`await app.RunAsync()` at `Program.cs:374`) crashes under parallel WAF boot.

The fixture body itself follows RESEARCH Pattern 1 (Testcontainers + Respawn `IAsyncLifetime`). No in-repo analog for the container wiring — use RESEARCH 07-RESEARCH.md lines 182-219 as the literal template. Key: pin `postgres:17-alpine` (matches `docker-compose.yml`), Respawn `TablesToIgnore = ["__EFMigrationsHistory", <hangfire tables>]` (RESEARCH Pitfall 3), migrate once before the Respawn checkpoint.

---

### `IntegrationTestWebAppFactory.cs` (QA-01)

**Analog:** `Backend/tests/TaxReader.UnitTests/Helpers/RateLimitTestFactory.cs` — but **inverted**. RateLimitTestFactory *strips Npgsql and swaps to in-memory*; the integration factory does the **opposite**: it keeps Npgsql and points `ConnectionStrings:DefaultConnection` at the container. Copy the `UseSetting` boot-config block (the API refuses to boot without these — RESEARCH Pitfall + STATE 02-CR-01):

```csharp
// RateLimitTestFactory.cs:32-54 — the required boot settings to carry over
builder.UseEnvironment("Production");                       // exercise prod paths
builder.UseSetting("Jwt:Secret", "test-secret-test-secret-test-secret-1234");
builder.UseSetting("Jwt:Issuer", "test");
builder.UseSetting("Jwt:Audience", "test");
// 32 zero bytes Base64 — RefreshTokenOptionsValidator.ValidateOnStart() rejects an invalid pepper
builder.UseSetting("RefreshToken:HashKey", Convert.ToBase64String(new byte[32]));
// StripeOptionsValidator.ValidateOnStart() requires these; sk_live_ bypasses the D-13 Production guard
builder.UseSetting("Stripe:SecretKey", "sk_live_test_placeholder_for_unit_tests");
builder.UseSetting("Stripe:PublishableKey", "pk_live_test_placeholder_for_unit_tests");
builder.UseSetting("Stripe:WebhookSecret", "whsec_placeholder_for_unit_tests");
```
**The one new line vs the analog:** `builder.UseSetting("ConnectionStrings:DefaultConnection", containerConnectionString);` — this single override redirects both `AppDbContext` AND Hangfire (both read that key; RESEARCH Pitfall 1). Do **NOT** set `Hangfire:UseInMemoryStorage` (the analog does — the integration host wants real Postgres). Decide Hangfire handling per RESEARCH Pitfall 1 (let it create its schema + add to Respawn ignore-list, OR strip its registration).

---

### `PaymentIdempotencyTests.cs` (QA-01 — the canonical "why this phase exists" test)

**Analog:** `Backend/tests/TaxReader.UnitTests/Webhooks/StripeWebhookHandlerTests.cs` — the **in-memory variant being superseded**. RESEARCH Pitfall 7: the existing test exercises the handler's `AnyAsync` guard but NOT the `payments.stripe_event_id` UNIQUE index (in-memory ignores it). The new integration test must assert the **real** constraint fires:
```csharp
// Insert the same Payment.StripeEventId twice; the 2nd SaveChanges must throw
// DbUpdateException wrapping Postgres 23505 unique_violation.
var act = async () => { /* add+save duplicate Payment */ };
await act.Should().ThrowAsync<DbUpdateException>();
```
Per-test reset via `fixture.ResetAsync()` in `InitializeAsync` (RESEARCH Pattern 2). Note `Method_Scenario_Result` naming (`SecondInsert_SameStripeEventId_ViolatesUniqueConstraint`).

---

### `RefreshTokenRotationReplayTests.cs` (QA-01 — V2/V3 ASVS)

**Analog:** `Backend/tests/TaxReader.UnitTests/Auth/RefreshTokenServiceTests.cs` (rotation logic) + `Auth/ReplayDetectionTests.cs` (replay-revokes-all). These already prove the *logic* on in-memory; the integration port proves the **real `refresh_tokens.token_hash` UNIQUE** anchors replay detection. Reuse the service construction and assertion shape from the analog:
```csharp
// RefreshTokenServiceTests.cs:90-118 — rotation assertion shape to port to real Postgres
var rotation = await _service.ValidateAndRotateAsync(original, "ua-B", "10.0.0.2");
rotation.IsSuccess.Should().BeTrue();
rotation.Value.PlaintextToken.Should().NotBe(original, "rotation mints a brand-new plaintext");
// old row: RevokedAt != null, ReplacedByTokenId == newRow.Id  (chain pointer)
```
Construct `RefreshTokenService` with `Options.Create(new RefreshTokenOptions { HashKey = Convert.ToBase64String(new byte[32]) })` and `Mock<IAuditLogger>` exactly as `RefreshTokenServiceTests.cs:42-63`.

---

### `DuplicateDetectionTests.cs`, `CascadeDeleteTests.cs`, `MigrationSmokeTests.cs` (QA-01)

**Analog:** `Backend/tests/TaxReader.UnitTests/Auth/MigrationTests.cs` — this is the **literal `[Skip]`ed placeholder** these fulfill:
```csharp
// MigrationTests.cs:12 — the deferral this phase closes
[Fact(Skip = "Deferred to Phase 7 QA-01 (Testcontainers) — EF InMemory cannot run Postgres DDL")]
```
- Duplicate detection: assert `receipt_files (user_id, content_hash)` UNIQUE rejects a 2nd insert (same `DbUpdateException` shape as PaymentIdempotency).
- Cascade delete: delete a `ReceiptFile`, assert child `Receipt`/`ReceiptItem`/`ProcessingRun` rows gone (real FK `ON DELETE CASCADE` — in-memory only approximates).
- Migration smoke: `dbContext.Database.MigrateAsync()` against the fresh container, seed via `TestDataFactory`, assert schema. Seed with `Backend/tests/TaxReader.UnitTests/Helpers/TestDataFactory.cs` (`CreateReceiptFile`/`CreateReceipt`/`CreateReceiptItem`) — **reference, do not duplicate** (consider linking the file or a shared seed helper).

---

### `AuthServiceTests.cs` (D-01 backfill, unit)

**Analog:** `Backend/tests/TaxReader.UnitTests/Auth/RefreshTokenServiceTests.cs` — copy its constructor-as-setup + in-memory DB + `IDisposable` shape exactly:
```csharp
// RefreshTokenServiceTests.cs:26-64 — the setup shape to replicate
public AuthServiceTests()
{
    var options = new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;     // fresh DB per test instance
    _dbContext = new AppDbContext(options);
    // seed user(s) + SaveChanges()
    _service = new AuthService(_dbContext, Options.Create(new JwtOptions { … }), refreshTokenServiceMock);
}
public void Dispose() => _dbContext.Dispose();
```
**Real signature of code under test** (`AuthService.cs`): `RegisterAsync(RegisterRequest, userAgent, ipAddress, ct)`, `LoginAsync(LoginRequest, …)`, `RefreshAsync(refreshToken, …)` — all return `Result<AuthResponse>`. Assert on `Result.IsSuccess`/`.Error`, never catch exceptions. **German error strings are exact** (`AuthService.cs:36,39,101`):
- `"Ein Konto mit dieser E-Mail existiert bereits."`
- `"Das Passwort muss mindestens 8 Zeichen lang sein."`
- `"Ungültige E-Mail oder Passwort."`

Mock `IRefreshTokenService` (Moq) so register/login don't hit the rotation path. Test the BCrypt verify branch by seeding a user with `BCrypt.Net.BCrypt.HashPassword(...)` (see `TestDataFactory.CreateRegularUser`).

---

### `TokenServiceTests.cs` (D-01 backfill, unit)

**Analog:** `RefreshTokenServiceTests.cs` (same in-memory shape). **Real signatures** (`TokenService.cs`): primary ctor `TokenService(IAppDbContext dbContext, ICurrentUser currentUser)` — so **mock `ICurrentUser`** to return a fixed `UserId`. Methods to cover:
- `TryConsumeManyAsync(IReadOnlyList<TokenLedgerEntry>, ct)` → `bool`; returns `false` when `balance.Balance < total` (atomic pre-charge — `TokenService.cs:63`).
- `RefundManyAsync(entries, ct)` → `UserTokenBalance` (`TokenService.cs:94`).
- `AddTokensAsync(amount, type, description, ct)` — throws `ArgumentOutOfRangeException` when `amount <= 0` (`TokenService.cs:137-138`).
- `GetOrCreateBalanceAsync` seeds `InitialFreeTokens = 10` + a "Welcome bonus" `TokenTransaction` (`TokenService.cs:27-47`).
Assert ledger rows: each consume writes a `Consumption` row with `Amount = -entry.Amount` and running `BalanceAfter`; refund writes `Refund` rows.

---

### `AiOnlyClassificationServiceTests.cs` (D-01 backfill, unit)

**Analog:** `Backend/tests/TaxReader.UnitTests/Services/RuleBasedClassifierTests.cs` (classifier test shape) + Moq usage from `RefreshTokenServiceTests`. **Real signature** (`AiOnlyClassificationService.cs:21-27`): primary ctor takes `IAiClassifier`, `ITokenService`, `IAppDbContext`, `IOptions<AnthropicOptions>`, `ILogger<…>`. Method: `ClassifyItemsAsync(IEnumerable<ReceiptItem>, Guid userId, ct)`.

The four named behaviors (D-01) map to these exact branches — **mock `IAiClassifier` + `ITokenService`**:
- **Token pre-charge / insufficient:** `tokenService.TryConsumeManyAsync` returns `false` → every item `Unknown` with reason `"Keine Tokens verfügbar – bitte Credits aufladen."` (`AiOnlyClassificationService.cs:57-65`).
- **Refund-on-Unknown:** AI returns `Category.Unbekannt` for an item → that item's entry added to `refunds`, `RefundManyAsync` called (`:90-98,121-122`).
- **Refund-on-failure:** `ClassifyBatchAsync` throws → `RefundManyAsync(ledgerEntries)` + all `Unknown` with `"AI-Fehler: …"` (`:73-78`).
- **Auto-confirm threshold:** seed `User.AutoConfirmThreshold`; when `result.Confidence >= threshold` → `ClassificationStatus.Confirmed` + reason prefix `"Auto-bestätigt (… ≥ …)"`; else `Suggested` (`:100-118`).
Use `NullLogger<AiOnlyClassificationService>.Instance` (as `RefreshTokenServiceTests.cs:62`). `IsConfigured=false` path returns `Unknown` with `"AI-Klassifizierung nicht konfiguriert."` (`:38-42`).

---

### `HealthEndpoints.cs` + health-check services (OBS-03)

**Analog (endpoint shape):** `Backend/src/TaxReader.Api/Endpoints/TokenEndpoints.cs` — the `MapXxxEndpoints` static extension on `RouteGroupBuilder` + `.WithName`/`.WithSummary`. **Analog (anonymous opt-out):** `AuthEndpoints.cs:34` uses `.AllowAnonymous()`. Both health endpoints MUST be anonymous (BetterStack probes unauthenticated — RESEARCH Open Question 2).

**Registration point** — `Program.cs:352-368`. The `/api/v1` group is `RequireAuthorization()` by default (`Program.cs:354`). `/health` (top-level, no version prefix) registers on `app` directly like the Stripe webhook (`Program.cs:368` `app.MapStripeWebhookEndpoint()` — anonymous, outside the auth group). `/api/v1/health` registers inside the group but with `.AllowAnonymous()`. Excerpt of the insertion context:
```csharp
// Program.cs:354-368 — where to wire health endpoints
var api = app.MapGroup("/api/v1").RequireAuthorization();
api.MapAuthEndpoints();
// … other groups …
app.MapStripeWebhookEndpoint();   // ← pattern for an anonymous top-level endpoint; add app.MapHealthEndpoints() near here
```
**Health-check service shape (Infrastructure):** follow `AuthService.cs` primary-ctor DI — a small `IHealthCheck` (or endpoint handler) taking `IAppDbContext` and doing `await dbContext.Database.CanConnectAsync(ct)`; `/api/v1/health` additionally asserts `IAiClassifier.IsConfigured` (the property used in `AiOnlyClassificationService.cs:38`). Return a JSON body containing `"healthy"` so BetterStack keyword-monitors on it; return 503 when unhealthy (RESEARCH Pitfall 6). **Security:** leak NO connection strings / secrets / Anthropic key (RESEARCH Security Domain) — minimal status only.

---

### Frontend Vitest config + setup (QA-02, Wave 0) — NO analog

No frontend tests exist. **Source of truth = bundled Next.js 16 docs**, NOT training data (`Frontend/AGENTS.md`: "This is NOT the Next.js you know"). Read `Frontend/node_modules/next/dist/docs/01-app/02-guides/testing/vitest.md` before authoring. Use RESEARCH Pattern 3 (`vitest.config.mts`) literally: `tsconfigPaths()` resolves the `@/*` alias (project uses `paths: { "@/*": ["./src/*"] }`), `environment: 'jsdom'`, `setupFiles: ['./vitest.setup.ts']` importing `@testing-library/jest-dom`, and `exclude: ['**/e2e/**']` to keep Playwright specs out (RESEARCH anti-pattern). Add `"test": "vitest"` to `Frontend/package.json` scripts and the devDeps from RESEARCH Standard Stack.

---

### Frontend Vitest unit/component tests (QA-02) — match these REAL signatures

No test analog; the executor must match the actual code under test. Excerpted shapes:

**`api-client.ts` shared-refresh-promise (QA-02 named target):** module-level `let refreshPromise` (`api-client.ts:47`) dedupes concurrent 401s — only the first 401 calls `tryRefreshToken()`, others await the same promise (`api-client.ts:54-68`). Test: fire N concurrent requests that 401, assert `/auth/refresh` called exactly once. **Critical:** `refreshPromise` is module-level state → reset with `vi.resetModules()` between tests (RESEARCH Pattern 4). The refresh POST uses bare `axios` (not the `api` instance) at `api-client.ts:89`.

**`format.ts` pure-fn targets (QA-04/QA-02):** `formatCurrency` IS the canonical money path — `new Intl.NumberFormat("de-DE", { style: "currency", currency: "EUR" })` (`format.ts:1-6`). `formatDate` → `de-DE`. `categoryLabel`/`statusLabel` map to German strings. These are trivially unit-testable and anchor the DE-localization assertions.

**`classify-dialog.tsx` classification-confirm/override (QA-02 named target):** `useConfirmClassification()` mutation; `handleConfirm` calls `mutateAsync({ itemId, category })` → toast `"Klassifizierung bestätigt"`; `handleQuickConfirm` (the suggestion accept path) → `"Vorschlag bestätigt"` (`classify-dialog.tsx:75-98`). The category `Select` and the 13-category list (`classify-dialog.tsx:34-48`) are the override surface. Render with `@testing-library/react`, drive with `@testing-library/user-event`, mock the hook.

**`upload-form.tsx` upload state machine (QA-02 named target):** `useState<File[]>` + `useUploadFiles()` mutation; `handleUpload` clears files optimistically, calls `mutateAsync`, on error restores files + toasts the server `error` field or `"Upload fehlgeschlagen. Bitte erneut versuchen."` (`upload-form.tsx:18-43`). Test the empty-selection guard (`"Bitte mindestens eine Datei auswählen"`) and the error-restore path.

**`login/page.tsx` form (QA-02 RHF+Zod note):** NOTE — `login/page.tsx` uses plain `useState` + native `required`/`minLength`, **not** RHF+Zod. **There are NO RHF+Zod forms in `Frontend/src`** — `zodResolver`/`@hookform`/`useForm` have ZERO matches despite the deps being installed (verified). The earlier mention of `save-rule-dialog.tsx`/settings/register as `zodResolver` users was incorrect — none exist. QA-02 form coverage targets the actual implementations (login/register plain `useState` + native validation); the login/register flows are otherwise covered end-to-end by the 07-05 Playwright happy path. Test authors must check each form's actual implementation before asserting any RHF behavior.

---

### Frontend Playwright config + happy-path spec (QA-03/QA-05) — NO analog

Read `Frontend/node_modules/next/dist/docs/01-app/02-guides/testing/playwright.md` first. Use RESEARCH Pattern 5 (`playwright.config.ts`) literally: `testDir: './e2e'`, `use: { locale: 'de-DE', timezoneId: 'Europe/Berlin' }`, `webServer: { command: 'npm run build && npm run start' }` (standalone production server, NOT `next dev` — RESEARCH anti-pattern), and `sm`(640)/`md`(768) viewport projects for QA-05. **Happy path = the real routes** (verified present): `/register` → `/login` → `/(authenticated)/upload` → `/(authenticated)/receipts/[id]` (see-classification + confirm via `classify-dialog`) → `/(authenticated)/reports` → export (`export-buttons.tsx`). All copy is German.

---

### CI heavy job + DE-localization guard (D-03, D-07)

**Analog:** existing `.github/workflows/ci.yml` — keep the 3 lightweight jobs unchanged. The 06-07 legal-placeholder guard is the **exact template** for the D-07 DE guard (`ci.yml:47-60`): `shell: bash` + `set -e` + **grep-inside-`if`** (so a no-match exit-1 doesn't abort under `set -e`) + `exit 1` on violation. Copy this skeleton:
```yaml
# ci.yml:47-60 — the bash-guard template to clone for the DE-localization guard
- name: Verify legal pages contain no placeholder tokens (CR-04 / TMG §5)
  shell: bash
  run: |
    set -e
    legal_dir="Frontend/src/app/(legal)"
    if grep -rnE '\[[^]]+\]' "$legal_dir"; then        # grep INSIDE if — no-match exit-1 won't abort set -e
      printf 'Legal placeholder check FAILED: …\n'
      exit 1
    fi
    printf 'Legal placeholder check passed.\n'
```
DE guard (RESEARCH Code Examples + Pitfall 4): scope to `Frontend/src`, flag bare `toLocaleString()` without `de-DE`, assert money goes through `formatCurrency`/`Intl.NumberFormat('de-DE'…)`. Scope tightly to avoid false positives on code identifiers.

**Heavy job** (new, D-03): triggers `on: push: branches:[main]` + a `run-heavy` PR label. `ubuntu-latest` provides Docker for Testcontainers (no `services: postgres` block — Testcontainers manages its own; RESEARCH Open Q 1). Add `npx playwright install --with-deps` + cache `~/.cache/ms-playwright` (RESEARCH Pitfall 5). Reuse the `actions/setup-dotnet@v4` + CPM cache-key (`cache-dependency-path: Backend/Directory.Packages.props`) and `actions/setup-node@v4` blocks from the existing jobs (`ci.yml:67-74, 91-94`). Run `dotnet test Backend/tests/TaxReader.IntegrationTests` + `npx playwright test`.

---

### `PITFALLS.md` (QA-07) — NO analog

Does not exist. Author as the canonical "Looks done but isn't" pre-launch checklist (CONTEXT Claude's Discretion). Seed it from RESEARCH "Common Pitfalls" (the 7 documented traps) + the D-05 hard-blocker list. Likely lives in plan 07-05.

---

## Shared Patterns

### WAF serialization (applies to ALL `WebApplicationFactory<Program>` test classes)
**Source:** `Backend/tests/TaxReader.UnitTests/RateLimiting/RateLimiterTestCollection.cs:11-15`
**Apply to:** every QA-01 integration class.
`[CollectionDefinition(Name, DisableParallelization = true)]` — top-level `Program.cs` (`await app.RunAsync()`, `Program.cs:374`) crashes under parallel WAF boot ("entry point exited without ever building an IHost").

### In-memory test-DB construction (applies to ALL D-01 unit tests)
**Source:** `Backend/tests/TaxReader.UnitTests/Auth/RefreshTokenServiceTests.cs:26-64`
**Apply to:** AuthService / TokenService / AiOnlyClassificationService unit tests.
Constructor-as-setup, `UseInMemoryDatabase(Guid.NewGuid().ToString())` (fresh per instance), seed + `SaveChanges()`, `Moq` for collaborators, `NullLogger<T>.Instance`, `IDisposable.Dispose() => _dbContext.Dispose()`.

### Test seeding factory
**Source:** `Backend/tests/TaxReader.UnitTests/Helpers/TestDataFactory.cs`
**Apply to:** integration fixtures + service tests needing entities. `CreateAdminUser`/`CreateRegularUser` (BCrypt hash for `"test-password-1234"`), `CreateReceiptFile`/`CreateReceipt`/`CreateReceiptItem`/`CreateClassification`/`CreateRule`. Reuse, don't re-author.

### Result<T> + German strings (applies to all backend assertions)
**Source:** `Backend/src/TaxReader.Infrastructure/Services/AuthService.cs` (German error literals), CLAUDE.md conventions.
**Apply to:** every backend test asserting service outcomes. Assert `Result.IsSuccess`/`.Error` — never catch exceptions for control flow (DB-constraint `DbUpdateException` is the one legitimate throw). User-facing strings are German and exact.

### CPM version-less references
**Source:** `Backend/Directory.Packages.props` + any `*.csproj`.
**Apply to:** the new integration `.csproj`. Versions ONLY in `Directory.Packages.props`; `<PackageReference>` carries no `Version`.

### Anonymous endpoint opt-out
**Source:** `Backend/src/TaxReader.Api/Endpoints/AuthEndpoints.cs:34` (`.AllowAnonymous()`); `Program.cs:354` (`/api/v1` is `RequireAuthorization()` by default).
**Apply to:** both health endpoints — they must opt out of the global auth requirement.

### Bash CI guard skeleton
**Source:** `.github/workflows/ci.yml:47-60`.
**Apply to:** the D-07 DE-localization guard. `set -e` + grep-inside-`if` + `exit 1`.

### Bundled-docs-first for frontend
**Source:** `Frontend/AGENTS.md` + `Frontend/node_modules/next/dist/docs/01-app/02-guides/testing/{vitest,playwright}.md`.
**Apply to:** all Vitest + Playwright config authoring — bundled Next.js 16 docs override training data.

---

## No Analog Found

| File | Role | Data Flow | Reason / Mitigation |
|------|------|-----------|---------------------|
| `Frontend/vitest.config.mts` + `vitest.setup.ts` | config | n/a | First frontend test config. Use bundled Next.js 16 `vitest.md` + RESEARCH Pattern 3. |
| `Frontend/playwright.config.ts` + `e2e/*.spec.ts` | config + e2e | request-response | First E2E. Use bundled `playwright.md` + RESEARCH Pattern 5; real routes excerpted above. |
| `Frontend/src/**/*.test.tsx` | test (unit) | n/a | Zero frontend tests today. Real signatures of `api-client.ts` / `format.ts` / `classify-dialog.tsx` / `upload-form.tsx` excerpted above to match. |
| `PITFALLS.md` | docs | n/a | New checklist; seed from RESEARCH "Common Pitfalls" + D-05 blockers. |
| `PostgresContainerFixture.cs` (container wiring) | fixture | n/a | The *collection* shape has an analog (`RateLimiterTestCollection`); the Testcontainers/Respawn lifecycle does not — use RESEARCH Pattern 1 literally. |

---

## Metadata

**Analog search scope:** `Backend/tests/TaxReader.UnitTests/**`, `Backend/src/TaxReader.{Api,Infrastructure}/**`, `Frontend/src/{lib,hooks,providers,components,app}/**`, `.github/workflows/`, `Backend/Directory.Packages.props`.
**Files scanned:** ~25 read in full or targeted; 60+ test files enumerated.
**Pattern extraction date:** 2026-06-05
