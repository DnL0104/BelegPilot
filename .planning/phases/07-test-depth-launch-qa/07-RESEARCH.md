# Phase 7: Test Depth + Launch QA - Research

**Researched:** 2026-06-05
**Domain:** Integration testing (Testcontainers + Respawn / EF Core 10 / Postgres 17), frontend testing (Vitest 3 + RTL + Playwright 1.50 on Next.js 16), uptime/alert ops (BetterStack + Sentry), DE-localization CI guards
**Confidence:** HIGH (most claims VERIFIED against live codebase + bundled Next.js 16 docs + NuGet/npm registries)

## Summary

Phase 7 adds the test depth and ops readiness that gate commercial launch. The backend test project is **far more mature than the stale `codebase/TESTING.md` (2026-04-29) claims** — there are now 60+ test files including `WebApplicationFactory<Program>` integration tests (CORS, rate-limiting, Hangfire dashboard auth), Stripe webhook idempotency tests, refresh-token rotation/replay tests, and a deliberately-`[Skip]`ed migration smoke placeholder explicitly deferred to "Phase 7 QA-01 (Testcontainers)". The single missing capability the in-memory provider cannot deliver is **real PostgreSQL constraint + DDL + concurrency enforcement** — which is exactly what QA-01 (Testcontainers + Respawn) closes, and it is pre-targeted by `MigrationTests.cs`.

The frontend has **zero tests today** (confirmed: no Vitest/Jest/Playwright in `package.json`). Next.js 16 ships authoritative bundled testing guides (`node_modules/next/dist/docs/01-app/02-guides/testing/{vitest,playwright}.md`) that pin the exact dependency set and config — these supersede training data and the project's `Frontend/AGENTS.md` explicitly warns Next.js 16 has breaking changes, so the bundled docs are the source of truth. No health endpoints (`/health`, `/api/v1/health`) exist yet — they must be built fresh as part of OBS-03 before BetterStack can probe them. The D-01 backfill targets (`AuthService`, `AiOnlyClassificationService`, `TokenService`) have **no dedicated test files** (only indirect `AuthService.LoginAsync` coverage via `IsAdminClaimTests`).

**Primary recommendation:** Build a new `TaxReader.IntegrationTests` xUnit project (separate from `TaxReader.UnitTests`) using a single shared `PostgreSqlContainer` collection fixture + `WebApplicationFactory<Program>` with `ConnectionStrings:DefaultConnection` overridden to the container, Respawn 6.x reset between tests, run in the gated "heavy" CI job (D-03). Add Vitest 3 + RTL + Playwright 1.50 to the frontend per the bundled Next.js 16 guides — Vitest on every PR (D-04), Playwright in the heavy job (D-03). Build the two health endpoints, wire BetterStack as keyword monitors, extend the `hygiene-check` bash guard for DE localization (D-07).

## User Constraints (from CONTEXT.md)

### Locked Decisions
- **D-01:** Backend test scope = the QA-01/02/03 named critical paths **plus** a high-risk backfill of currently-untested money/security services with zero tests today: `AuthService` (register/login/BCrypt verify/refresh-token rotation+replay), `AiOnlyClassificationService` (token pre-charge, refund-on-Unknown, refund-on-failure, auto-confirm threshold), and `TokenService` (atomic ledger operations).
- **D-02:** Explicitly OUT of scope this phase: `PdfPigTextExtractor` bounding-box algorithm, `TesseractImageTextExtractor` pool/locking, `PdfExportService`/`CsvExportService` formatting, `ClaudeAiClassifier` HTTP/JSON-parsing. Deferred.
- **D-03:** Keep the three existing lightweight jobs (`hygiene-check`, `backend-build-test`, `frontend-lint-build`) on every PR. Add slow suites — Postgres Testcontainers integration (QA-01) + Playwright E2E (QA-03) — as a **separate "heavy" CI job/workflow** running on push-to-`main` + optional PR label `run-heavy`.
- **D-04:** Vitest unit/component tests (QA-02) are fast → run on **every PR** (fold into frontend job or a sibling fast job), NOT the heavy job.
- **D-05:** HARD launch blockers (must be green before "go"): (1) all automated suites green in CI (QA-01 + QA-02 + QA-03); (2) final lawyer sign-off on AGB + Datenschutzerklärung (QA-07), draft markers removed; (3) Phase 6 operator items closed — real Impressum/legal contact data filled (06-07 CI placeholder guard green) AND all four AVVs/DPAs signed (Anthropic, Stripe, Sentry, BetterStack).
- **D-06:** Tracked but NON-blocking (surface in go/no-go report; do NOT gate): native-speaker DE polish review beyond the automated guard; prior-phase manual UAT debt (Phases 2/3/4 HUMAN-UAT items).
- **D-07:** Enforce DE localization (QA-04) with BOTH layers: (a) an automated CI guard extending the 06-07 `hygiene-check` bash pattern — flag likely-English user-facing strings + assert money rendered via `Intl.NumberFormat('de-DE', { style: 'currency', currency: 'EUR' })`; (b) a one-time native-speaker review pass at launch (non-blocking per D-06).
- **D-08:** BetterStack monitors per OBS-03: `/health` (DB ping) + `/api/v1/health` (DB + Anthropic config); status page linked from footer; deploy-maintenance windows configurable. Sentry alert "quiet hours" 23:00–07:00 = HIGH-severity pages only. Exact alert-delivery channel for solo-dev paging (email + push default) left to research/planning.

### Claude's Discretion
- Exact Testcontainers/Respawn wiring, Playwright project config, Vitest setup, and test-file organization.
- **PITFALLS.md authoring:** QA-07 references a "Looks done but isn't" checklist at `PITFALLS.md`, but no such file exists yet — create it during this phase (likely in 07-05) as the canonical pre-launch verification checklist.

### Deferred Ideas (OUT OF SCOPE)
- Comprehensive backend test backfill (PdfPigTextExtractor, TesseractImageTextExtractor, PdfExport/CsvExport, ClaudeAiClassifier) — future hardening phase / backlog (per D-02).
- Treating native-speaker DE review and prior-phase (P2/3/4) manual UAT as hard launch gates — kept non-blocking this milestone (per D-06).

## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| QA-01 | PostgreSQL integration test project (`Testcontainers.PostgreSql` 4.x + `Respawn` 6.x): duplicate detection, cascade deletes, refresh-token rotation+replay, payment idempotency unique constraint, migration smoke against populated DB | New `TaxReader.IntegrationTests` project; verified all target UNIQUE constraints exist (`payments.stripe_event_id`, `receipt_files(user_id,content_hash)`, `refresh_tokens.token_hash`, `users.email`); WAF connection-string override pattern documented; `MigrationTests.cs` is the pre-existing `[Skip]`ed placeholder to fulfill |
| QA-02 | Vitest 3 unit + component tests: auth hooks (incl. JWT refresh shared-promise), upload state machine, RHF+Zod validation, classification-confirm/override | Bundled Next.js 16 Vitest guide pins exact deps + config; `api-client.ts` shared `refreshPromise` pattern documented; `format.ts` helpers are pure-fn test targets |
| QA-03 | Playwright 1.50 E2E happy path (register→login→upload→classify→confirm→report→export) in DE locale against standalone Next.js server | Bundled Next.js 16 Playwright guide; `webServer` + `locale`/`timezoneId` config documented; happy path matches existing routes |
| QA-04 | German localization audit: Sie-form, EUR via `Intl.NumberFormat('de-DE')`, native-speaker review | D-07 bash guard extending 06-07 pattern; `formatCurrency` already canonical in `format.ts` |
| QA-05 | Mobile-responsive QA at `sm` (640px) + `md` (768px): receipts list, upload, classification-confirm, dashboard, reports; phone photo-receipt upload | Playwright device/viewport projects can automate the responsive smoke; `use-mobile.ts` hook exists; manual phone-camera test stays HUMAN-UAT |
| QA-06 | Sentry alert rules tuned vs real baseline; status-page maintenance windows; quiet hours 23:00–07:00 HIGH-only | Sentry already wired (Phase 1); this is config-only tuning + BetterStack maintenance windows |
| QA-07 | Final lawyer review of AGB + Datenschutzerklärung; PITFALLS.md "Looks done but isn't" checklist verified end-to-end | PITFALLS.md to be authored this phase; lawyer review is a D-05 hard blocker, operator/human gated |
| OBS-03 | BetterStack monitors on `/health` (DB ping) + `/api/v1/health` (DB + Anthropic config); status page from footer; deploy maintenance windows | **No health endpoints exist yet** — must be built; ASP.NET Core HealthChecks recommended; BetterStack keyword monitor reads response body |

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Postgres constraint/DDL/cascade/concurrency tests (QA-01) | API + Database | Infrastructure | In-memory provider hides FK/UNIQUE enforcement; only a real Postgres container exercises migrations + constraints + concurrent inserts |
| Service-level backfill tests (D-01) | Application/Infrastructure | — | `AuthService`/`TokenService`/`AiOnlyClassificationService` are Infrastructure services; testable with in-memory DB + mocks (no container needed) |
| Vitest hook/component/form tests (QA-02) | Browser/Client | — | Hooks, forms, api-client interceptors all run client-side; jsdom suffices |
| Playwright E2E (QA-03) | Full stack (Browser → Frontend Server → API → DB) | — | Exercises the real standalone Next.js server + backend; needs the whole stack up |
| Health endpoints (OBS-03) | API/Backend | Database | `/health` and `/api/v1/health` are API endpoints probing DB + Anthropic config |
| DE-localization guard (QA-04/D-07) | CI (build tier) | Browser/Client | Static grep over `Frontend/src` source strings; no runtime |
| Uptime/alerting (OBS-03/QA-06) | External (BetterStack/Sentry) | API/Backend | Probes hit API endpoints; config lives in external SaaS dashboards |

## Standard Stack

### Backend Integration Tests (new `TaxReader.IntegrationTests` project)

| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| `Testcontainers.PostgreSql` | 4.x (latest 4.12.0) `[VERIFIED: nuget.org]` | Spin up disposable Postgres 17 in Docker for tests | The de-facto .NET container-for-tests library; pinned 4.x per REQUIREMENTS.md |
| `Respawn` | 6.x (latest 6.x = 6.2.1; 7.0.0 exists) `[VERIFIED: nuget.org]` | Reset DB to clean state between tests via deterministic DELETE | jbogard's standard test-DB cleaner; faster than drop/recreate; pinned 6.x per REQUIREMENTS.md |
| `Npgsql` | 10.0.x (project already uses 10.0.1) `[VERIFIED: codebase]` | Postgres ADO.NET driver — Respawn opens a raw `NpgsqlConnection` to reset | Already in the stack |
| `Microsoft.AspNetCore.Mvc.Testing` | (already referenced) `[VERIFIED: csproj]` | `WebApplicationFactory<Program>` host | Already used by CORS/rate-limit/Hangfire tests |
| xUnit + FluentAssertions 7 + Moq | (already in stack) | Test runner/assertions/mocks | Project standard; keep `Method_Scenario_Result` naming per CLAUDE.md |

**Decision point for the planner:** whether QA-01 lives in a NEW `TaxReader.IntegrationTests` project or a new folder inside `TaxReader.UnitTests`. Recommendation: **new project**, because (a) it lets the heavy CI job run `dotnet test TaxReader.IntegrationTests` independently from the fast `dotnet test` in `backend-build-test`, and (b) it isolates Docker/Testcontainers package references from the fast unit suite. `[ASSUMED]` — confirm with planner; both work.

### Frontend Unit/Component Tests (Vitest)

Exact dependency set from the **bundled Next.js 16 Vitest guide** (`Frontend/node_modules/next/dist/docs/01-app/02-guides/testing/vitest.md`) `[CITED: bundled Next.js 16 docs]`:

| Library | Version | Purpose |
|---------|---------|---------|
| `vitest` | 3.x (REQUIREMENTS pin; latest 3.x = 3.2.4) `[VERIFIED: npm]` | Test runner |
| `@vitejs/plugin-react` | latest 4.x `[VERIFIED: npm]` | React transform for Vitest |
| `jsdom` | latest `[VERIFIED: npm]` | DOM environment |
| `@testing-library/react` | 16.x (requires React 19 — project OK) `[VERIFIED: npm]` | Component render/query |
| `@testing-library/dom` | latest | Peer of RTL 16 |
| `@testing-library/jest-dom` | 6.x | `toBeInTheDocument` etc. matchers (add to setup file) |
| `@testing-library/user-event` | 14.x | Realistic user interactions for form tests |
| `vite-tsconfig-paths` | latest | Resolves `@/*` path alias (project uses `paths: { "@/*": ["./src/*"] }`) |

### Frontend E2E (Playwright)

| Library | Version | Purpose |
|---------|---------|---------|
| `@playwright/test` | 1.50.x (REQUIREMENTS pin; 1.50.0/1.50.1 available) `[VERIFIED: npm]` | E2E runner + browser automation |

### Backend Health Checks (OBS-03)

| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| `Microsoft.Extensions.Diagnostics.HealthChecks` (built into ASP.NET Core 10) | — | `AddHealthChecks()` + `MapHealthChecks("/health")` | First-party; no new package for a basic liveness probe |
| `AspNetCore.HealthChecks.NpgSql` (Xabaril/AspNetCore.Diagnostics.HealthChecks) | latest 9.x `[ASSUMED — verify version at plan time]` | DB-ping health check | Standard community DB health check; OR hand-roll a tiny `IHealthCheck` doing `dbContext.Database.CanConnectAsync()` to avoid a new dep |

**Recommendation:** `/health` = liveness + DB ping (`CanConnectAsync`); `/api/v1/health` = DB ping + Anthropic-configured check (assert `AnthropicOptions.ApiKey` present / `IAiClassifier.IsConfigured`). A custom `IHealthCheck` keeps it dependency-light and matches the "API is thin; Infrastructure implements external concerns" architecture. Return a JSON body (e.g. `{"status":"healthy","db":"up","anthropic":"configured"}`) so BetterStack can run a **keyword monitor** asserting on `"healthy"`, not just 200.

**Installation:**
```bash
# Backend (Central Package Management — add to Backend/Directory.Packages.props, version-less <PackageReference> in csproj)
#   Testcontainers.PostgreSql 4.x, Respawn 6.x  (+ Npgsql already present)

# Frontend (run in Frontend/)
npm install -D vitest@3 @vitejs/plugin-react jsdom @testing-library/react @testing-library/dom @testing-library/jest-dom @testing-library/user-event vite-tsconfig-paths
npm install -D @playwright/test@1.50
npx playwright install --with-deps   # downloads browsers; required in CI heavy job
```

### Version verification (performed this session)
- `Testcontainers.PostgreSql`: 4.x available, latest 4.12.0 `[VERIFIED: nuget.org flatcontainer index]`
- `Respawn`: 6.x available (6.0.0/6.1.0/6.2.0/6.2.1), 7.0.0 also published — pin 6.x per REQUIREMENTS `[VERIFIED: nuget.org]`
- `vitest@3`: latest 3.2.x `[VERIFIED: npm view]`
- `@playwright/test@1.50`: 1.50.0 + 1.50.1 published `[VERIFIED: npm view]`
- `@testing-library/react`: 16.3.2 (React 19-compatible) `[VERIFIED: npm view]`

## Architecture Patterns

### System Architecture Diagram

```
                          ┌──────────────────── CI (.github/workflows) ────────────────────┐
                          │                                                                  │
  PR to main ───────────► │  FAST jobs (D-03/D-04, every PR):                                 │
                          │    hygiene-check ──[extend]──► DE-localization guard (D-07/QA-04)│
                          │    backend-build-test  (dotnet test TaxReader.UnitTests)         │
                          │    frontend-lint-build + Vitest (QA-02, D-04)                    │
                          │                                                                  │
  push to main ─────────► │  HEAVY job (D-03, push-to-main + label `run-heavy`):              │
  (or `run-heavy` label)  │    ┌─ services: docker (Testcontainers needs Docker-in-CI) ─┐    │
                          │    │  dotnet test TaxReader.IntegrationTests (QA-01)         │    │
                          │    │     PostgreSqlContainer(17) ← Respawn reset per test    │    │
                          │    │     WebApplicationFactory<Program> → real Postgres      │    │
                          │    └─────────────────────────────────────────────────────────┘  │
                          │    ┌─ npx playwright install --with-deps ─┐                       │
                          │    │  next build && next start (standalone, DE locale)       │    │
                          │    │  Playwright E2E happy path (QA-03)  ← webServer config   │    │
                          │    └─────────────────────────────────────────────────────────┘  │
                          └──────────────────────────────────────────────────────────────────┘

  Production runtime (OBS-03):
   BetterStack ──HTTP probe──► Caddy :443 ──► /health (DB ping)
               ──HTTP probe──►              ──► /api/v1/health (DB + Anthropic config)
               └─ keyword monitor asserts JSON body "healthy"; maintenance windows; status page → footer link
   Sentry ──alert rules (QA-06)──► quiet hours 23:00-07:00 HIGH-only ──► email + push (solo-dev paging)
```

### Recommended Project Structure
```
Backend/tests/
├── TaxReader.UnitTests/            # existing — fast, in-memory, every PR
└── TaxReader.IntegrationTests/     # NEW (QA-01) — Postgres container, heavy job
    ├── Fixtures/
    │   ├── PostgresContainerFixture.cs     # ICollectionFixture: one container, Respawn checkpoint
    │   └── IntegrationTestCollection.cs     # [CollectionDefinition] shared across classes
    ├── IntegrationTestWebAppFactory.cs      # WAF<Program> + ConnectionStrings override
    ├── DuplicateDetectionTests.cs           # receipt_files (user_id, content_hash) UNIQUE
    ├── CascadeDeleteTests.cs                # ReceiptFile delete cascades
    ├── RefreshTokenRotationReplayTests.cs   # real token_hash UNIQUE + replay revoke
    ├── PaymentIdempotencyTests.cs           # payments.stripe_event_id UNIQUE under duplicate insert
    └── MigrationSmokeTests.cs               # migrate fresh container, seed, assert schema

Frontend/
├── vitest.config.mts
├── vitest.setup.ts                  # import '@testing-library/jest-dom'
├── playwright.config.ts             # webServer (next build/start), DE locale, projects
├── src/**/__tests__/*.test.tsx      # OR colocated *.test.tsx (Vitest)
└── e2e/                             # Playwright specs (separate from Vitest globs!)
```

### Pattern 1: Testcontainers + WebApplicationFactory + Respawn (QA-01 harness)
**What:** One shared Postgres container per test collection; the WAF overrides the app's connection string to point at the container; Respawn resets data between tests.
**When to use:** Every QA-01 test class.
```csharp
// Source: Milan Jovanović "Testcontainers Best Practices" + Respawn README [CITED]
// Fixtures/PostgresContainerFixture.cs
public sealed class PostgresContainerFixture : IAsyncLifetime
{
    public PostgreSqlContainer Container { get; } = new PostgreSqlBuilder()
        .WithImage("postgres:17-alpine")   // pin to match docker-compose.yml db image
        .Build();

    public Respawner Respawner { get; private set; } = default!;

    public async Task InitializeAsync()
    {
        await Container.StartAsync();
        // Apply EF migrations once so the schema (and constraints) exist before Respawn snapshots.
        // (Do this via the WAF's migrator or dbContext.Database.MigrateAsync().)
        await using var conn = new NpgsqlConnection(Container.GetConnectionString());
        await conn.OpenAsync();
        Respawner = await Respawner.CreateAsync(conn, new RespawnerOptions
        {
            DbAdapter = DbAdapter.Postgres,
            SchemasToInclude = ["public"],
            // exclude EF + Hangfire bookkeeping tables so migrations/jobs survive resets:
            TablesToIgnore = ["__EFMigrationsHistory"],
        });
    }

    public async Task ResetAsync()
    {
        await using var conn = new NpgsqlConnection(Container.GetConnectionString());
        await conn.OpenAsync();
        await Respawner.ResetAsync(conn);
    }

    public Task DisposeAsync() => Container.DisposeAsync().AsTask();
}

[CollectionDefinition(Name)]
public sealed class IntegrationTestCollection : ICollectionFixture<PostgresContainerFixture>
{
    public const string Name = "Postgres integration (shared container)";
}

// IntegrationTestWebAppFactory.cs — override the connection string the API reads.
// AppDbContext + Hangfire both read ConnectionStrings:DefaultConnection (verified in
// DependencyInjection.cs), so overriding this ONE setting redirects everything.
public sealed class IntegrationTestWebAppFactory(string connectionString)
    : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Production");          // exercise prod paths
        builder.UseSetting("ConnectionStrings:DefaultConnection", connectionString);
        builder.UseSetting("Jwt:Secret", "test-secret-test-secret-test-secret-1234");
        // RefreshToken HMAC pepper must be valid Base64 32-byte or the API refuses to boot
        // (RefreshTokenOptionsValidator + ValidateOnStart — see STATE.md 02-CR-01).
    }
}
```

### Pattern 2: Per-test Respawn reset
```csharp
[Collection(IntegrationTestCollection.Name)]
public sealed class PaymentIdempotencyTests(PostgresContainerFixture fixture) : IAsyncLifetime
{
    public Task InitializeAsync() => fixture.ResetAsync();   // clean slate before each test
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task SecondInsert_SameStripeEventId_ViolatesUniqueConstraint() { /* ... */ }
}
```

### Pattern 3: Vitest config for Next.js 16 + `@/*` alias
```ts
// Source: bundled Next.js 16 docs vitest.md [CITED]
// vitest.config.mts
import { defineConfig } from 'vitest/config'
import react from '@vitejs/plugin-react'
import tsconfigPaths from 'vite-tsconfig-paths'

export default defineConfig({
  plugins: [tsconfigPaths(), react()],   // tsconfigPaths resolves @/* → ./src/*
  test: {
    environment: 'jsdom',
    setupFiles: ['./vitest.setup.ts'],   // import '@testing-library/jest-dom'
    globals: true,
    exclude: ['**/node_modules/**', '**/e2e/**'],  // keep Playwright specs out of Vitest
  },
})
```

### Pattern 4: Testing the JWT shared-refresh-promise (QA-02 named target)
**What:** `api-client.ts` dedupes concurrent 401s through a single in-flight `refreshPromise`. The test fires N concurrent requests that 401, and asserts `/auth/refresh` is called exactly once.
```ts
// mock axios; make first call 401, refresh resolve once, assert single refresh call
// Key: the shared module-level `refreshPromise` must be reset between tests (vi.resetModules()).
```

### Pattern 5: Playwright config — standalone server + DE locale (QA-03)
```ts
// Source: bundled Next.js 16 docs playwright.md + Playwright TestOptions [CITED]
// playwright.config.ts
import { defineConfig, devices } from '@playwright/test'
export default defineConfig({
  testDir: './e2e',
  use: {
    baseURL: 'http://localhost:3000',
    locale: 'de-DE',
    timezoneId: 'Europe/Berlin',
  },
  projects: [
    { name: 'desktop', use: { ...devices['Desktop Chrome'] } },
    // QA-05 responsive smoke can ride here as md/sm viewport projects:
    { name: 'md',  use: { viewport: { width: 768, height: 1024 } } },
    { name: 'sm',  use: { viewport: { width: 640, height: 900 } } },
  ],
  webServer: {
    command: 'npm run build && npm run start',   // standalone production server, NOT dev
    url: 'http://localhost:3000',
    reuseExistingServer: !process.env.CI,
    timeout: 120_000,
  },
})
```

### Anti-Patterns to Avoid
- **Running QA-01 against in-memory provider:** defeats the entire purpose — in-memory does NOT enforce FK/UNIQUE/cascade or run real DDL (this is the documented gap).
- **One Postgres container per test:** slow. Share one container per collection (`ICollectionFixture`) + Respawn reset.
- **Parallel `WebApplicationFactory<Program>` runs:** Program.cs top-level statements break under parallel WAF — must serialize (`DisableParallelization = true`), proven by `RateLimiterTestCollection` (STATE.md 02-03). Apply the same to the integration collection.
- **Letting Vitest pick up Playwright specs:** keep `e2e/` out of Vitest globs and Playwright `testDir` separate, or both runners fight over the same files.
- **Playwright against `next dev`:** test the standalone production build (`next start`) — matches the Docker `output: "standalone"` runtime and catches build-only issues.
- **DE guard false-positives on code identifiers:** the bash guard must scope to user-facing string literals/JSX text, not variable names, imports, or English code symbols — restrict paths and patterns tightly (see Pitfall 4).

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Disposable Postgres for tests | Custom Docker scripts / shared dev DB | `Testcontainers.PostgreSql` | Dynamic ports, lifecycle, cleanup handled; pinned 4.x |
| Reset DB between tests | Manual `TRUNCATE`/`DELETE` ordering | `Respawn` 6.x | Computes FK-aware deletion order from live metadata; ignore-lists for EF/Hangfire tables |
| Connection-string injection to WAF | New env files / config hacks | `builder.UseSetting("ConnectionStrings:DefaultConnection", ...)` | API + Hangfire both read this one key (verified); one override redirects all |
| DOM matchers in Vitest | Custom assertions | `@testing-library/jest-dom` | Standard `toBeInTheDocument`/`toHaveValue` matchers |
| Path-alias resolution in Vitest | Manual `resolve.alias` | `vite-tsconfig-paths` | Reads the project's tsconfig `@/*` paths automatically |
| E2E browser automation | Selenium/custom | `@playwright/test` 1.50 | Bundled Next.js 16 guide; `webServer`, locale, CI install built-in |
| Uptime probing/paging | Custom cron + email | BetterStack monitors | Keyword monitors, maintenance windows, status page, paging channels |
| Liveness/DB health endpoint | Ad-hoc controller | ASP.NET Core `AddHealthChecks` / tiny `IHealthCheck` | First-party; structured + status-code semantics |

**Key insight:** Every "hard" part of this phase already has a blessed library. The real work is *wiring lifecycle correctly* (container sharing, Respawn ignore-lists, WAF serialization) and *deciding what to assert* (which constraints/paths), not building infrastructure.

## Runtime State Inventory

> This is a test-depth/ops phase, not a rename/refactor. Inventory included for the launch-readiness items that touch live external state.

| Category | Items Found | Action Required |
|----------|-------------|------------------|
| Stored data | None being renamed. QA-01 tests CREATE disposable container data only; production Postgres untouched. | None |
| Live service config | **BetterStack** monitors + status page + maintenance windows live in the BetterStack dashboard (not git). **Sentry** alert rules + quiet hours live in Sentry dashboard (not git). **AVVs/DPAs** (Anthropic/Stripe/Sentry/BetterStack) signed out-of-band (D-05 blocker). | Operator/manual setup in external dashboards; capture as HUMAN-UAT items |
| OS-registered state | None. CI runs in GitHub Actions ephemeral runners; no host registrations. | None |
| Secrets/env vars | Heavy CI job needs: `BETTERSTACK_*` (if API-provisioned), Docker available on runner (Testcontainers), Playwright browser cache. Integration tests need a valid Base64 32-byte refresh-token HMAC pepper or the API refuses to boot (STATE.md 02-CR-01). | Add CI secrets/settings; supply test pepper in WAF settings |
| Build artifacts | New `TaxReader.IntegrationTests` project + new frontend devDeps (`package-lock.json` regenerates). Playwright downloads browser binaries (`~/.cache/ms-playwright`) — cache in CI. | Reinstall/`npm ci`; cache Playwright browsers |

**Nothing found requiring data migration.** Verified: no production data renames in this phase.

## Common Pitfalls

### Pitfall 1: Hangfire's Postgres storage init fails inside the integration test host
**What goes wrong:** `DependencyInjection.cs` registers Hangfire with `UsePostgreSqlStorage(... DefaultConnection ...)`. When the WAF boots against the test container, Hangfire tries to create its schema in the container. If the connection string override is missing or Hangfire init throws, the whole WAF host fails to start.
**Why it happens:** Hangfire shares `DefaultConnection` with `AppDbContext` (verified at `DependencyInjection.cs:97-98`).
**How to avoid:** Either (a) let Hangfire create its schema in the container (it will, given a valid connection) and add Hangfire tables to Respawn's `TablesToIgnore`/separate schema, OR (b) strip Hangfire registration in the test host's `ConfigureServices`. Prefer (a) for fidelity unless boot time hurts.
**Warning signs:** "The entry point exited without ever building an IHost" or Npgsql schema errors at factory startup.

### Pitfall 2: Parallel WAF runs crash on top-level Program.cs
**What goes wrong:** Multiple `WebApplicationFactory<Program>` instances starting in parallel throw "entry point exited without ever building an IHost".
**Why it happens:** Program.cs uses top-level statements with one `await app.RunAsync()` (documented in `RateLimiterTestCollection.cs`).
**How to avoid:** Put all QA-01 integration classes in one `[CollectionDefinition(DisableParallelization = true)]`. This also pairs naturally with sharing one container.
**Warning signs:** Flaky failures only when the full suite runs, passing in isolation.

### Pitfall 3: Respawn deletes EF migration history / Hangfire tables
**What goes wrong:** `ResetAsync` wipes `__EFMigrationsHistory` or Hangfire job tables, so the next test sees an "unmigrated" or broken DB.
**Why it happens:** Default Respawn clears all tables in included schemas.
**How to avoid:** `TablesToIgnore = ["__EFMigrationsHistory", ...hangfire tables]` (or put Hangfire in its own schema and exclude that schema). Migrate ONCE at container init, before the Respawn checkpoint.
**Warning signs:** "relation does not exist" / "no migrations applied" on the second test in a class.

### Pitfall 4: DE-localization guard false positives
**What goes wrong:** A naive `grep -E '[A-Za-z]'` flags code identifiers, imports, `className`, ARIA roles, and legitimately-English technical tokens as "English user-facing strings", making the guard noisy and untrustworthy.
**Why it happens:** Distinguishing user-facing copy from code is genuinely hard with grep.
**How to avoid:** Scope tightly — target JSX text nodes and known user-string locations; maintain an allow-list of acceptable English tokens (brand names, code terms); for the EUR assertion, grep for raw `€`/`EUR` string concatenation or `toLocaleString` without `de-DE` and flag those, while asserting `formatCurrency`/`Intl.NumberFormat('de-DE'...)` is the only money path. Follow the 06-07 template exactly: `shell: bash`, `set -e`, **grep-inside-`if`** (so a no-match exit-1 doesn't abort under `set -e`), `exit 1` on violation.
**Warning signs:** Guard fails on PRs that are actually fully German; developers start ignoring it.

### Pitfall 5: Playwright/Testcontainers need infra the runner doesn't have by default
**What goes wrong:** Heavy CI job fails because Docker isn't available (Testcontainers) or browsers aren't installed (Playwright).
**Why it happens:** GitHub `ubuntu-latest` has Docker, but Playwright browsers must be installed explicitly; some self-hosted runners lack Docker.
**How to avoid:** `ubuntu-latest` provides Docker for Testcontainers. Add `npx playwright install --with-deps` step + cache `~/.cache/ms-playwright`. Pin `postgres:17-alpine` to match `docker-compose.yml`.
**Warning signs:** "Cannot connect to the Docker daemon" or "Executable doesn't exist at .../chromium".

### Pitfall 6: BetterStack only checks the status code, missing real degradation
**What goes wrong:** A monitor that only checks 200 passes even if the DB is down but the endpoint still returns 200.
**Why it happens:** Plain HTTP monitors check status only.
**How to avoid:** Make `/api/v1/health` return a JSON body whose status reflects DB + Anthropic config, and configure a BetterStack **keyword monitor** asserting `"healthy"` (or have the endpoint return 503 when unhealthy so a status-code monitor catches it). Set quiet-hours/maintenance windows so deploys don't page (D-08).
**Warning signs:** Green monitor during a known outage.

### Pitfall 7: In-memory idempotency test gives false confidence (QA-01 motivation)
**What goes wrong:** `StripeWebhookHandlerTests` already "tests" duplicate-event idempotency — but on the in-memory provider, which does NOT enforce the `stripe_event_id` UNIQUE index. It only exercises the handler's `AnyAsync` guard, not the DB constraint that protects against a race.
**Why it happens:** In-memory provider ignores relational constraints (documented in TESTING.md cons).
**How to avoid:** QA-01 must assert the **real** UNIQUE constraint rejects a concurrent/duplicate insert against Postgres. This is the canonical example of why QA-01 exists.
**Warning signs:** None at unit-test time — that's the danger; only a real Postgres test surfaces it.

## Code Examples

### Verify a real UNIQUE constraint fires (QA-01 — payment idempotency)
```csharp
// payments.stripe_event_id is UNIQUE (verified: PaymentConfiguration.cs:15).
// Insert the same event id twice directly; the SECOND SaveChanges must throw a
// DbUpdateException wrapping a Postgres 23505 unique_violation.
var act = async () => { /* add+save duplicate Payment */ };
await act.Should().ThrowAsync<DbUpdateException>();
```

### Cascade-delete fidelity (QA-01)
```csharp
// ReceiptFile delete cascades to ProcessingRun/Receipt/items (CLAUDE.md: cascade relied upon).
// In-memory approximates cascade; only real Postgres proves the FK ON DELETE CASCADE.
```

### DE-localization guard skeleton (D-07 — extends 06-07 pattern)
```bash
# Source: existing .github/workflows/ci.yml hygiene-check step (06-07) [VERIFIED: codebase]
set -e
src="Frontend/src"
# Assert money is formatted via the de-DE helper, never raw toLocaleString without locale.
if grep -rnE "toLocaleString\(\s*\)" "$src" --include="*.tsx" --include="*.ts"; then
  printf 'Localization guard FAILED: bare toLocaleString() — use formatCurrency / Intl.NumberFormat("de-DE").\n'
  exit 1
fi
printf 'Localization guard passed.\n'
```

### Health endpoint (OBS-03)
```csharp
// /health  → liveness + DB ping; /api/v1/health → DB + Anthropic config.
// Return JSON body so BetterStack keyword-monitors on "healthy"; 503 when unhealthy.
app.MapHealthChecks("/health");                 // DB ping
app.MapHealthChecks("/api/v1/health");          // DB + IAiClassifier.IsConfigured
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| In-memory EF provider for ALL backend tests | In-memory for unit speed + Testcontainers Postgres for constraint/DDL fidelity | This phase (QA-01) | Catches FK/UNIQUE/cascade/concurrency bugs the in-memory provider hides |
| Frontend untested | Vitest 3 + RTL 16 (React 19) unit/component + Playwright 1.50 E2E | This phase | First frontend coverage; DE-locale E2E happy path |
| `BeforeSend` Sentry hook | `SetBeforeSend` (non-deprecated) | Phase 1 (already done) | QA-06 only tunes existing rules |
| `sentry.client.config.ts` | `instrumentation-client.ts` | Phase 1 (already done) | Frontend Sentry consent-gated since Phase 6 |

**Deprecated/outdated:**
- `codebase/TESTING.md` (2026-04-29) — STALE. Claims "no integration tests", "no CI", and lists `AuthService`/`TokenService`/`AiOnlyClassificationService` as untested. **Verified false in part:** 60+ test files now exist incl. WAF integration tests, CI is live (`.github/workflows/ci.yml`), Stripe/refresh-token/Hangfire tests exist. **Still true:** no Postgres-backed integration tests; no frontend tests; the three named services have no *dedicated* test files (only indirect `AuthService.LoginAsync` coverage via `IsAdminClaimTests`).

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Backend unit framework | xUnit 2.9.2 + FluentAssertions 7 + Moq + EFCore.InMemory `[VERIFIED: csproj]` |
| Backend integration framework (NEW) | xUnit + Testcontainers.PostgreSql 4.x + Respawn 6.x + WebApplicationFactory<Program> |
| Frontend unit framework (NEW) | Vitest 3 + @testing-library/react 16 + jsdom |
| Frontend E2E framework (NEW) | Playwright 1.50 |
| Backend config file | none (xUnit convention) / new `TaxReader.IntegrationTests.csproj` (Wave 0) |
| Frontend config (Wave 0) | `vitest.config.mts`, `vitest.setup.ts`, `playwright.config.ts` |
| Quick run command (backend unit) | `dotnet test Backend/tests/TaxReader.UnitTests` |
| Quick run command (frontend unit) | `cd Frontend && npm run test` (Vitest) |
| Full suite (heavy) | `dotnet test Backend/tests/TaxReader.IntegrationTests` + `npx playwright test` |

### Phase Requirements → Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| QA-01 | Duplicate detection (`receipt_files` UNIQUE) | integration | `dotnet test ...IntegrationTests --filter DuplicateDetection` | ❌ Wave 0 |
| QA-01 | Cascade deletes | integration | `dotnet test ...IntegrationTests --filter Cascade` | ❌ Wave 0 |
| QA-01 | Refresh-token rotation + replay (real `token_hash` UNIQUE) | integration | `dotnet test ...IntegrationTests --filter RefreshToken` | ❌ Wave 0 (in-memory variant exists in UnitTests) |
| QA-01 | Payment idempotency (`stripe_event_id` UNIQUE) | integration | `dotnet test ...IntegrationTests --filter PaymentIdempotency` | ❌ Wave 0 (in-memory variant exists — insufficient, Pitfall 7) |
| QA-01 | Migration smoke vs populated DB | integration | `dotnet test ...IntegrationTests --filter Migration` | ❌ Wave 0 (`MigrationTests.cs` is a `[Skip]`ed placeholder) |
| D-01 | `AuthService` register/login/BCrypt/refresh | unit | `dotnet test ...UnitTests --filter AuthService` | ❌ Wave 0 (only `IsAdminClaimTests` indirect) |
| D-01 | `TokenService` ledger ops (consume/refund/add) | unit | `dotnet test ...UnitTests --filter TokenService` | ❌ Wave 0 |
| D-01 | `AiOnlyClassificationService` pre-charge/refund/threshold | unit | `dotnet test ...UnitTests --filter AiOnlyClassification` | ❌ Wave 0 |
| QA-02 | JWT refresh shared-promise dedupe | unit (Vitest) | `npm run test -- api-client` | ❌ Wave 0 |
| QA-02 | Upload state machine, RHF+Zod, classify-confirm | unit (Vitest) | `npm run test` | ❌ Wave 0 |
| QA-03 | E2E happy path DE locale | e2e (Playwright) | `npx playwright test` | ❌ Wave 0 |
| QA-04 | DE Sie-form + EUR formatting | CI guard | bash step in `hygiene-check` | ❌ Wave 0 |
| QA-05 | Responsive sm/md smoke | e2e (Playwright projects) | `npx playwright test --project=sm --project=md` | ❌ Wave 0 (phone-camera = HUMAN-UAT) |
| OBS-03 | Health endpoints DB + Anthropic | integration/unit | WAF test hitting `/health`, `/api/v1/health` | ❌ Wave 0 (endpoints don't exist) |

### Sampling Rate
- **Per task commit:** `dotnet test Backend/tests/TaxReader.UnitTests` (fast) and/or `cd Frontend && npm run test` for the slice touched.
- **Per wave merge:** full fast suite (both unit projects + Vitest) — these run on every PR (D-04).
- **Phase gate:** heavy job green (Testcontainers integration + Playwright E2E) on push-to-main before `/gsd-verify-work`; all of QA-01/02/03 green is a D-05 hard launch blocker.

### Wave 0 Gaps
- [ ] `Backend/tests/TaxReader.IntegrationTests/TaxReader.IntegrationTests.csproj` — new project (Testcontainers 4.x + Respawn 6.x via CPM)
- [ ] `Fixtures/PostgresContainerFixture.cs` + `IntegrationTestCollection.cs` — shared container + Respawn checkpoint
- [ ] `IntegrationTestWebAppFactory.cs` — connection-string override + valid test JWT secret + refresh-token pepper
- [ ] `Frontend/vitest.config.mts` + `vitest.setup.ts` + `package.json` `"test"` script + devDeps
- [ ] `Frontend/playwright.config.ts` + `e2e/` dir + `npx playwright install`
- [ ] Backend `/health` + `/api/v1/health` endpoints (OBS-03) — do not exist yet
- [ ] `PITFALLS.md` "Looks done but isn't" checklist (QA-07 / 07-05)
- [ ] CI heavy job/workflow (D-03) + DE-localization guard step in `hygiene-check` (D-07)

## Security Domain

`security_enforcement: true`, `security_asvs_level: 1`, `security_block_on: high` (config.json) — section required.

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V2 Authentication | yes | QA-01 must test refresh-token rotation **and replay-revokes-all** against real Postgres `token_hash` UNIQUE; D-01 backfills `AuthService` BCrypt verify + login failure path (German "Ungültige E-Mail oder Passwort.") |
| V3 Session Management | yes | Refresh-token rotation invalidates prior token; replay detection revokes all (real DB enforcement, not in-memory approximation) |
| V4 Access Control | yes | E2E + integration should confirm per-user data scoping (`ICurrentUser.UserId` filtering); export download one-time token IDOR (LEG-07) is a known sensitive surface to keep covered |
| V5 Input Validation | yes | RHF+Zod form-validation tests (QA-02); FluentValidation already unit-covered |
| V6 Cryptography | yes (verify, don't build) | BCrypt password hashing + HMAC refresh-token pepper — TEST behavior, never reimplement; pepper-validation fail-fast already exists (02-CR-01) |
| V7 Error Handling/Logging | yes | `UploadErrorCatalog` German strings; raw exceptions never in HTTP body (structural-grep tests exist) |

### Known Threat Patterns for this stack

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| Refresh-token replay after rotation | Spoofing/Elevation | Replay revokes all user tokens — **QA-01 must verify against real Postgres** (in-memory hides the `token_hash` UNIQUE that anchors detection) |
| Duplicate Stripe webhook → double token grant | Tampering | `payments.stripe_event_id` UNIQUE — **QA-01 must verify the real constraint** (in-memory test gives false confidence, Pitfall 7) |
| Health endpoint information disclosure | Information Disclosure | `/health` + `/api/v1/health` must NOT leak connection strings, secrets, stack traces, or Anthropic key — return minimal status only |
| Cross-user data access | Elevation | Per-user query scoping; E2E + integration assert a user cannot read another's receipts/exports |
| SQL injection | Tampering | EF Core parameterized LINQ everywhere (no raw SQL in handlers) — already the project standard |

## Project Constraints (from CLAUDE.md)

- **Test naming:** `Method_Scenario_Result` (e.g. `RegisterAsync_DuplicateEmail_ReturnsFailure`). Apply to new backend tests.
- **Result<T>** for error handling; **no exceptions for control flow** — assertions should check `Result.IsSuccess`/`.Error`, not catch exceptions (except where DB constraints legitimately throw `DbUpdateException`).
- **German user-facing strings** — tests asserting copy must expect German (e.g. "Ein Konto mit dieser E-Mail existiert bereits.", "Ungültige E-Mail oder Passwort.", "Keine Tokens verfügbar – bitte Credits aufladen.").
- **File-scoped namespaces, primary constructors for DI, records for DTOs, always pass CancellationToken** — new fixtures/factories follow these.
- **No repository pattern, no AutoMapper, no MediatR** — integration tests exercise `IAppDbContext`/`DbSet<T>` directly.
- **Central Package Management** — new NuGet versions go in `Backend/Directory.Packages.props`; csproj `<PackageReference>` are version-less.
- **EFCore snake_case naming** — Respawn `TablesToIgnore`/keyword monitors must use snake_case table names (e.g. `__EFMigrationsHistory` is EF's own name; app tables are `payments`, `receipt_files`, `refresh_tokens`).
- **GSD workflow enforcement** — all edits flow through a GSD command (this is planned phase work).
- **Frontend/AGENTS.md:** "This is NOT the Next.js you know" — Next.js 16 has breaking changes; READ `node_modules/next/dist/docs/` before writing frontend test code. The bundled vitest.md/playwright.md guides are authoritative over training data.

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | QA-01 should be a NEW `TaxReader.IntegrationTests` project (vs a folder in UnitTests) | Standard Stack | Low — both work; affects CI command granularity only. Planner confirms. |
| A2 | `AspNetCore.HealthChecks.NpgSql` version 9.x (or hand-rolled `IHealthCheck`) | Standard Stack | Low — verify exact version at plan time, or avoid the dep entirely with a 10-line custom check |
| A3 | Hangfire schema can be created in the test container and excluded from Respawn (vs stripped from test host) | Pitfall 1 | Medium — if Hangfire init is slow/fragile in CI, may need to strip it; affects fixture design |
| A4 | Solo-dev paging channel = BetterStack/Sentry email + push (no PagerDuty) | D-08/QA-06 | Low — D-08 leaves channel to planning; email+push is the documented default |
| A5 | Phone-camera photo-receipt upload (QA-05) stays a HUMAN-UAT item, not automated | Validation Architecture | Low — real-device camera cannot be automated in CI; viewport smoke is the automated part |

## Open Questions

1. **Does the heavy CI job run integration tests against a Testcontainers-managed Postgres, or against a GitHub Actions `services:` Postgres container?**
   - What we know: Testcontainers needs Docker, which `ubuntu-latest` provides; Testcontainers manages its own container lifecycle.
   - What's unclear: Whether to use Testcontainers' own container (recommended, matches local dev) or a CI `services:` block.
   - Recommendation: Use Testcontainers' container (one code path local + CI); do NOT add a `services: postgres` block — Testcontainers starts its own.

2. **Should `/health` and `/api/v1/health` be anonymous?**
   - What we know: All `/api/v1/*` routes are `RequireAuthorization()` by default; anonymous endpoints opt out.
   - What's unclear: BetterStack probes are unauthenticated, so both health endpoints must `.AllowAnonymous()`.
   - Recommendation: Both health endpoints anonymous; ensure they leak no secrets (Security Domain).

3. **Does `AiOnlyClassificationService` still exist as the registered `IClassificationService`, or was it replaced by `HybridClassificationService` (CLASS-02)?**
   - What we know: D-01 explicitly names `AiOnlyClassificationService` as a backfill target; the file still exists and is exercised via the hybrid composition.
   - What's unclear: Whether Phase 4's `HybridClassificationService` wraps it or replaced its registration.
   - Recommendation: Test `AiOnlyClassificationService` directly as D-01 dictates (file present, verified); the planner confirms the DI registration when wiring.

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| Docker (local + CI) | Testcontainers (QA-01) | ✓ (compose stack in use; `ubuntu-latest` has Docker) | Compose v2 | None — QA-01 requires it (heavy job only) |
| Postgres 17 image | QA-01 container | ✓ (pull at runtime; matches `postgres:17-alpine` in compose) | 17-alpine | None |
| Node 22 | Vitest/Playwright | ✓ (frontend uses node 22) | 22 | None |
| Playwright browsers | QA-03/QA-05 | ✗ until `npx playwright install` | — | None — install step required in heavy job |
| .NET 10 SDK | All backend tests | ✓ | 10.0.x | None |
| BetterStack account | OBS-03 | ✗ (operator must provision) | — | None — D-05 requires AVV signed too |

**Missing dependencies with no fallback:** Playwright browser binaries (install step), BetterStack account/provisioning (operator/HUMAN-UAT).
**Missing dependencies with fallback:** none.

## Sources

### Primary (HIGH confidence)
- Live codebase — `Backend/tests/TaxReader.UnitTests/*` (60+ files), `Backend/src/.../DependencyInjection.cs`, `AuthService.cs`, `TokenService.cs`, `AiOnlyClassificationService.cs`, `PaymentConfiguration.cs`, `RefreshTokenConfiguration.cs`, `ReceiptFileConfiguration.cs`, `Frontend/src/lib/api-client.ts`, `Frontend/src/lib/format.ts`, `.github/workflows/ci.yml`, `Frontend/package.json` `[VERIFIED]`
- Bundled Next.js 16 docs — `Frontend/node_modules/next/dist/docs/01-app/02-guides/testing/vitest.md` + `playwright.md` `[CITED: authoritative for installed Next.js 16]`
- nuget.org flatcontainer indexes — Testcontainers.PostgreSql, Respawn, Npgsql versions `[VERIFIED]`
- npm registry — vitest, @playwright/test, @testing-library/react versions `[VERIFIED]`

### Secondary (MEDIUM confidence)
- [Milan Jovanović — Testcontainers Best Practices for .NET](https://www.milanjovanovic.tech/blog/testcontainers-best-practices-dotnet-integration-testing) — WAF + IAsyncLifetime + connection-string override + collection-fixture patterns
- [Respawn README (jbogard)](https://github.com/jbogard/Respawn) — `Respawner.CreateAsync`, `DbAdapter.Postgres`, `SchemasToInclude`, `TablesToIgnore`, FK-aware deletion
- [Better Stack Uptime docs](https://betterstack.com/docs/uptime/uptime-monitor/) + [API monitor](https://betterstack.com/docs/uptime/api-monitor/) — keyword vs status-code monitors, maintenance windows, status page

### Tertiary (LOW confidence — flagged for validation)
- `AspNetCore.HealthChecks.NpgSql` exact version (A2) — verify at plan time or hand-roll
- BetterStack maintenance-window API field names — confirm in dashboard at wiring time

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — all versions verified against npm/nuget; deps from bundled Next.js 16 docs
- Architecture (WAF + Testcontainers + Respawn): HIGH — connection-string override verified against actual `DependencyInjection.cs`; WAF serialization proven by existing `RateLimiterTestCollection`
- Current test state: HIGH — enumerated live test files; confirmed TESTING.md staleness; confirmed no health endpoints; confirmed D-01 targets untested
- Pitfalls: HIGH for backend (codebase-grounded), MEDIUM for BetterStack (docs-only)
- Security domain: HIGH — constraints + threat patterns map to verified DB indexes and existing auth tests

**Research date:** 2026-06-05
**Valid until:** 2026-07-05 (stable stack; pinned versions; re-verify Playwright/Vitest minor only if upgrading off the pins)
