---
title: Testing
focus: quality
last_mapped: 2026-06-19
---

# Testing

The backend is heavily tested (two test projects, ~70 test files). The frontend
now has a test stack configured (Vitest + Testing Library + Playwright) —
**note this contradicts the older CLAUDE.md claim that "Frontend has no test
framework configured"; the framework is present in `Frontend/package.json` as of
this mapping.**

## Backend

### Frameworks & Tools
| Tool | Version | Role |
|------|---------|------|
| xUnit | 2.9.2 | test runner (`<Using Include="Xunit" />` global) |
| `xunit.runner.visualstudio` | 2.8.2 | VS / `dotnet test` adapter |
| FluentAssertions | 7.0.0 | assertions (`.Should()...`) |
| Moq | 4.20.72 | mocking interfaces |
| `Microsoft.EntityFrameworkCore.InMemory` | 10.0.4 | in-memory DB for unit tests |
| `Microsoft.AspNetCore.Mvc.Testing` | — | `WebApplicationFactory` for endpoint/integration tests |
| `coverlet.collector` | 6.0.4 | code coverage |
| `Microsoft.NET.Test.Sdk` | 17.12.0 | test host |

Versions are centralized in `Backend/Directory.Packages.props` (Central Package
Management); the test `.csproj` files reference packages without versions.

### Projects
- **`Backend/tests/TaxReader.UnitTests/`** — fast, isolated. Uses EF Core InMemory
  and Moq. References Domain, Application, Infrastructure, **and** Api (so it can
  test endpoint wiring, rate-limit policies, Hangfire bootstrap, CORS, health).
- **`Backend/tests/TaxReader.IntegrationTests/`** — real PostgreSQL via
  Testcontainers (`Fixtures/PostgresContainerFixture.cs`) behind a shared
  `IntegrationTestCollection`, driven through `IntegrationTestWebAppFactory.cs`.
  Covers cascade deletes, duplicate detection, payment idempotency, refresh-token
  rotation/replay, and migration smoke.

### Organization
Unit tests mirror the source tree, grouped by concern:
- `Application/` — commands, queries, validators, mapping (e.g.
  `Commands/UploadReceiptFilesHandlerTests.cs`, `Queries/GetAnnualSummaryHandlerTests.cs`)
- `Auth/` — hashing, refresh rotation, replay detection, multi-device, admin claim
- `Domain/` — entity behavior + `ResultTests.cs`
- `Infrastructure/Parsers/` — `AmazonParserTests`, `EdukiParserTests`, `GenericParserTests`
- `Infrastructure/Tesseract/` — engine pool sizing/warmup
- `Services/` — `AuthServiceTests`, `TokenServiceTests`, `AiOnlyClassificationServiceTests`,
  `RuleBasedClassifierTests`, `StripePaymentProviderTests`
- `Pipeline/` — `ProcessReceiptFileJobTests`, `ClassifyBatchJobTests`,
  `SumValidationTests`, `JobErrorLeakageTests`, retry wiring
- `Jobs/` — token grant/revoke, export cleanup, export user-data
- `Hangfire/` — dashboard auth, wiring, recurring jobs, admin seeding
- `RateLimiting/` — per-policy tests + forwarded-headers behavior
- `Webhooks/` — `StripeWebhookHandlerTests`
- `Observability/` — Sentry scrubbing, Serilog enrichment
- `Configuration/`, `Cors/`, `Health/`

### Fixtures & Helpers (`TaxReader.UnitTests/Helpers/`)
- `TestDataFactory.cs` — builds domain entities for tests
- `HangfireTestFactory.cs`, `RateLimitTestFactory.cs` — configured
  `WebApplicationFactory` variants
- xUnit `[Collection]` markers serialize tests that share global state
  (`PipelineTestCollection`, `RateLimiterTestCollection`, `IntegrationTestCollection`)

### Mocking Strategy
- Interfaces (`IAiClassifier`, `ITokenService`, `IBackgroundJobClient`,
  `IUploadBlobStore`, …) mocked with Moq so handlers/jobs are tested in isolation.
- EF Core InMemory stands in for the real DB in unit tests; integration tests use a
  real Postgres container instead.

### Running
```bash
dotnet test Backend                       # all backend tests
dotnet test Backend/tests/TaxReader.UnitTests
dotnet test Backend/tests/TaxReader.IntegrationTests   # requires Docker (Testcontainers)
```

## Frontend

### Frameworks & Tools (`Frontend/package.json`)
| Tool | Version | Role |
|------|---------|------|
| Vitest | ^3.2.6 | unit/component test runner |
| `@vitejs/plugin-react` | ^6.0.2 | React transform for Vitest |
| `vite-tsconfig-paths` | ^6.1.1 | resolves `@/*` path alias in tests |
| jsdom | ^29.1.1 | DOM environment |
| `@testing-library/react` | ^16.3.2 | component rendering |
| `@testing-library/dom` | ^10.4.1 | DOM queries |
| `@testing-library/user-event` | ^14.6.1 | interaction simulation |
| `@testing-library/jest-dom` | ^6.9.1 | DOM matchers |
| `@playwright/test` | ^1.60.0 | end-to-end browser tests |

### Organization
- **E2E**: `Frontend/e2e/happy-path.spec.ts`, configured by
  `Frontend/playwright.config.ts`. CI raises the per-test timeout to 180s so the
  full upload→classify→report pipeline can finish (see recent commits); the spec
  dismisses the cookie banner and uses a real Anthropic key for the classify step.
- **Unit/component**: Vitest is wired (`@vitejs/plugin-react`, `jsdom`,
  Testing Library) but no `*.test.ts`/`*.spec.ts` unit files were found under
  `Frontend/src/` at mapping time — the harness exists ahead of broad coverage.

### Running
```bash
cd Frontend
npm run test        # vitest (watch)
npm run test:run    # vitest run (CI)
npm run test:e2e    # playwright
```

## CI
- `.github/workflows/ci.yml` orchestrates the build/test pipeline (backend tests +
  frontend E2E). E2E artifacts are captured on failure; a local keyfile is
  gitignored.

## Coverage Posture
- **Backend**: broad and deep — domain, application handlers, jobs, parsers, auth,
  rate limiting, webhooks, observability, plus a dedicated integration suite.
  Coverage collected via `coverlet.collector`.
- **Frontend**: E2E happy-path covered; component/unit coverage is nascent (tooling
  present, tests not yet written).

## Conventions
- Backend: one test class per unit under test; `[Fact]`/`[Theory]`; FluentAssertions
  for readable assertions; `CancellationToken` passed where the API requires it.
- Async tests use `async Task` + `await` (no `.Result`/`.Wait()`).
- Shared mutable-state suites are serialized via xUnit collections to avoid flakiness.
