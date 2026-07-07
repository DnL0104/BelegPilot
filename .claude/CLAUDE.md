<!-- GSD:project-start source:PROJECT.md -->

## Project

**TaxReader**

TaxReader is a web application that helps German private taxpayers turn a pile of receipt PDFs and images into a clean per-category-per-year expense summary they can transcribe into ELSTER or hand to their Steuerberater. The core pipeline already works (text extraction → format-specific parsing → AI classification → German-localized PDF/CSV report). This milestone hardens that existing build to a commercial DE launch standard — payments, operational visibility, data durability, legal compliance, a full UI redesign, and proven classification trust.

**Core Value:** Trustworthy classification — every line item correctly categorized into the right tax category, with reasoning the user can audit and override. If accuracy fails, the report is worthless.

### Constraints

- **Timeline**: Target the tax-season peak (~July 2026; commercial launch within ~3 months of 2026-05-02). Decided this milestone: the six gates take precedence over the date — launch may slip past the July peak rather than ship incomplete.
- **Tech stack**: Locked — .NET 10 / EF Core / PostgreSQL 17 / Next.js 16 / shadcn/ui / Anthropic / Tesseract. No rewrites.
- **Operations**: Solo developer, paging-style alerting expectation, no support team — automation must compensate.
- **Scale**: 100–500 paying users in the first 6 months. Design for that, not thousands.
- **Compliance**: GDPR mandatory; StBerG "Helfer, not Berater" positioning mandatory; GoBD where applicable; Anthropic AVV required for processing personal data.
- **Localization**: All end-user UI and copy in German.
- **Hosting**: Self-hosted Docker Compose with Caddy edge. No managed-cloud migration this milestone.
- **Budget**: Pre-revenue solo product. AI inference cost is pass-through via the token economy; other tooling (Sentry, payment fees, monitoring) bounded by what the product can absorb.

<!-- GSD:project-end -->

<!-- GSD:stack-start source:codebase/STACK.md -->

## Technology Stack

## Languages

- **C#** latest (LangVersion: latest) - Backend (.NET 10) across `Backend/src/TaxReader.Api`, `Backend/src/TaxReader.Application`, `Backend/src/TaxReader.Domain`, `Backend/src/TaxReader.Infrastructure`
- **TypeScript** ^5 - Frontend (Next.js 16 / React 19) across `Frontend/src/`
- **PowerShell** - Local orchestration scripts (`start.ps1`, `stop.ps1`)
- **Caddyfile DSL** - Reverse proxy configuration (`Caddyfile`)
- **Dockerfile** - Container definitions (`Backend/Dockerfile`, `Frontend/Dockerfile`)

## Runtime

- **.NET 10** (`<TargetFramework>net10.0</TargetFramework>` in `Backend/Directory.Build.props`) — Backend runtime image `mcr.microsoft.com/dotnet/aspnet:10.0`
- **Node.js 22 Alpine** - Frontend runtime (`Frontend/Dockerfile` line 14: `FROM node:22-alpine AS runtime`)
- **NuGet** with Central Package Management (`<ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>` in `Backend/Directory.Packages.props`); per-project `.csproj` files reference packages without versions. Master versions in `Backend/Directory.Packages.props`
- **npm** with `package-lock.json` committed (`Frontend/package-lock.json` present, ~360KB)

## Frameworks

- **ASP.NET Core 10 Minimal APIs** - HTTP server entry point `Backend/src/TaxReader.Api/Program.cs` (line 163: `app.RunAsync()`)
- **Next.js 16.2.2** - Frontend framework with App Router (`output: "standalone"` in `Frontend/next.config.ts`); all routes under `Frontend/src/app/`
- **React 19.2.4** - Component library for UI
- **Entity Framework Core 10.0.4** - ORM for PostgreSQL via `Npgsql.EntityFrameworkCore.PostgreSQL` 10.0.1
- **EFCore.NamingConventions 10.0.1** - Enforces snake_case DB column naming (configured in `Backend/src/TaxReader.Infrastructure/DependencyInjection.cs` line 27)
- **Asp.Versioning.Http 8.1.0** - API versioning (route group `/api/v1` in `Backend/src/TaxReader.Api/Program.cs` line 153)
- **FluentValidation 12.0.0** with DependencyInjectionExtensions 12.0.0 - Request validation (registered at `Backend/src/TaxReader.Api/Program.cs` line 90)
- **Microsoft.AspNetCore.OpenApi 10.0.4** + **Scalar.AspNetCore 2.6.0** - Interactive API docs at `/scalar/v1`
- **Microsoft.AspNetCore.Authentication.JwtBearer 10.0.4** + **System.IdentityModel.Tokens.Jwt 8.12.1** - JWT bearer token auth
- **BCrypt.Net-Next 4.0.3** - Password hashing (used in `Backend/src/TaxReader.Infrastructure/Services/AuthService.cs`)
- **QuestPDF 2026.2.4** (Community license) - Generates German-localized PDF tax export (`Backend/src/TaxReader.Infrastructure/Services/PdfExportService.cs`)
- **PdfPig 0.1.12** - Primary PDF text extraction via bounding-box reconstruction (`Backend/src/TaxReader.Infrastructure/Services/PdfPigTextExtractor.cs`)
- **Tesseract 5.2.0** - Local OCR fallback for image receipts (`Backend/src/TaxReader.Infrastructure/Services/TesseractImageTextExtractor.cs`); Singleton-scoped, LSTM-only mode, German + English packs
- **Hangfire 1.8.23** (Core, AspNetCore) - Job scheduling & queue (e.g., async receipt processing, token grant/revoke). Stores jobs in PostgreSQL via `Hangfire.PostgreSql 1.21.1`; also includes `Hangfire.MemoryStorage 1.8.1.2` for testing
- **Hangfire.PostgreSql 1.21.1** - PostgreSQL job storage backend
- **shadcn/ui ^4.1.2** - Component library (style: `base-nova` per `Frontend/components.json`)
- **@base-ui/react ^1.3.0** - Unstyled headless component primitives
- **Tailwind CSS ^4** - Utility-first CSS framework (PostCSS plugin `@tailwindcss/postcss` in `Frontend/postcss.config.mjs`)
- **next-themes ^0.4.6** - Dark mode theme switching
- **TanStack React Query ^5.96.2** - Server state management (configured in `Frontend/src/providers/query-provider.tsx`)
- **TanStack React Table ^8.21.3** - Data table components
- **React Hook Form ^7.72.1** with `@hookform/resolvers ^5.2.2` - Form state management and validation
- **Zod ^4.3.6** - Schema validation for forms
- **axios ^1.14.0** - HTTP client (`Frontend/src/lib/api-client.ts`); configured with `baseURL: "/api/v1"` and JWT interceptors
- **sonner ^2.0.7** - Toast notifications
- **lucide-react ^1.7.0** - Icon library
- **class-variance-authority ^0.7.1**, **tailwind-merge ^3.5.0**, **clsx ^2.1.1** - Conditional CSS class helpers
- **tw-animate-css ^1.4.0** - Tailwind animation utilities
- **xUnit 2.9.2** with `xunit.runner.visualstudio 2.8.2` - .NET test runner (`Backend/tests/TaxReader.UnitTests/`, `Backend/tests/TaxReader.IntegrationTests/`)
- **FluentAssertions 7.0.0** - Fluent assertion library
- **Moq 4.20.72** - Mocking framework
- **Microsoft.EntityFrameworkCore.InMemory 10.0.4** - In-memory test database (unit tests)
- **Testcontainers.PostgreSql 4.12.0** - Real PostgreSQL Docker container for integration tests
- **Respawn 6.2.1** - Database state reset between integration test runs
- **Microsoft.AspNetCore.Mvc.Testing 10.0.4** - WebApplicationFactory for API integration tests
- **Microsoft.NET.Test.Sdk 17.12.0** - Test discovery and execution
- **coverlet.collector 6.0.4** - Code coverage measurement
- **Vitest ^3.2.6** - Frontend test runner (`Frontend/vitest.config.mts`), environment: jsdom
- **@testing-library/react ^16.3.2**, **@testing-library/dom ^10.4.1**, **@testing-library/jest-dom ^6.9.1**, **@testing-library/user-event ^14.6.1** - React component testing utilities
- **Playwright ^1.60.0** - E2E test framework (`Frontend/playwright.config.ts`); per-test timeout: 180s; runs in Chrome Desktop on both CI and local
- **jsdom ^29.1.1** - DOM implementation for Vitest
- **Sentry ^6.4.1** (Core + AspNetCore) - Error tracking and performance monitoring (DSN bound from `Sentry__Dsn` env var; registered first in `Backend/src/TaxReader.Api/Program.cs` line 45 to catch DI-time exceptions)
- **@sentry/nextjs ^10.52.0** - Sentry SDK for Next.js (frontend error tracking; wrapped conditionally only when `NEXT_PUBLIC_SENTRY_ENABLED === "true"` in `Frontend/next.config.ts` line 49)
- **Serilog 4.2.0** - Structured logging framework
- **Serilog.AspNetCore 9.0.0** - ASP.NET Core integration (`UseSerilogRequestLogging()`)
- **Serilog.Sinks.Console 6.0.0** - Console output
- **Serilog.Enrichers.Environment 3.0.1** - Environment enrichment (machine name, environment name)
- **Scrutor 6.1.0** - DI assembly scanning support
- **Microsoft.EntityFrameworkCore.Design 10.0.4** - EF Core tooling (migrations)
- **Microsoft.EntityFrameworkCore.Tools 10.0.4** - dotnet ef CLI
- **TypeScript ^5** - Type checker
- **ESLint ^9** with `eslint-config-next 16.2.2` - Linting (TS + core-web-vitals; config: `Frontend/eslint.config.mjs`)
- **@vitejs/plugin-react ^6.0.2** - Vite React plugin (for Vitest)
- **vite-tsconfig-paths ^6.1.1** - Vite path alias plugin
- **Stripe.net 51.2.0** - Stripe API client (integrated in `Backend/src/TaxReader.Infrastructure/Services/StripePaymentProvider.cs`)
- **Newtonsoft.Json 13.0.3** - Pinned to override Hangfire's vulnerable 11.0.1 (mitigates GHSA-5crp-9r3c-p9vr; see `Backend/Directory.Packages.props` line 46)
- **Azure.Identity 1.13.2** - Pinned to override Respawn's vulnerable 1.3.0 (mitigates GHSA-5mfx-4wcx-rv27, GHSA-m5vv-6r4h-3vj9, GHSA-wvxc-855f-jvrv; consumed only by IntegrationTests; see `Backend/Directory.Packages.props` line 60)

## Configuration

- `.env` at repo root (gitignored); template in `.env.example` (note: cannot be read per forbidden-files policy)
- **Backend ASP.NET Core**: Configuration sources in priority order:
- **Frontend**: Environment variables read at **build time** (baked into Next.js standalone):
- `Backend/src/TaxReader.Infrastructure/Configuration/JwtOptions.cs` - `Jwt__*` section
- `Backend/src/TaxReader.Infrastructure/Configuration/AnthropicOptions.cs` - `Anthropic__*` section (model default: `claude-haiku-4-5`)
- `Backend/src/TaxReader.Infrastructure/Configuration/TesseractOptions.cs` - `Tesseract__*` section (pool size default: 3)
- `Backend/src/TaxReader.Infrastructure/Configuration/StripeOptions.cs` - `Stripe__*` section
- `Backend/src/TaxReader.Infrastructure/Configuration/RefreshTokenOptions.cs` - `RefreshToken__*` section
- All bound via `services.Configure<TOptions>(configuration.GetSection(...))` in `Backend/src/TaxReader.Infrastructure/DependencyInjection.cs`
- `Backend/Directory.Build.props` - Global C# compiler settings (`<Nullable>enable</Nullable>`, `<ImplicitUsings>enable</ImplicitUsings>`, `<LangVersion>latest</LangVersion>`, `<AnalysisLevel>latest</AnalysisLevel>`)
- `Backend/Directory.Packages.props` - Central NuGet version management
- `Backend/TaxReader.sln` - Solution file
- `Frontend/next.config.ts` - Next.js config (rewrites, Sentry wrapping, allowed dev origins)
- `Frontend/tsconfig.json` - TypeScript compiler settings (`strict: true`, `moduleResolution: "bundler"`, target `ES2017`, path alias `@/*`)
- `Frontend/components.json` - shadcn/ui configuration (style: `base-nova`)
- `Frontend/vitest.config.mts` - Vitest config (environment: jsdom, globals: true)
- `Frontend/playwright.config.ts` - Playwright E2E config (timeout: 180s, locale: de-DE, timezone: Europe/Berlin)
- `Frontend/eslint.config.mjs` - ESLint config (next core-web-vitals + typescript rules)
- `Frontend/postcss.config.mjs` - PostCSS config (Tailwind CSS 4 plugin)

## Platform Requirements

- .NET 10 SDK (`dotnet` CLI)
- Node.js 22+ (with npm)
- Docker Desktop with Compose v2
- Tesseract OCR with `deu+eng` language packs (Windows: `C:/Program Files/Tesseract-OCR/tessdata`; Linux: `/usr/share/tesseract-ocr/5/tessdata`)
- PowerShell (for `start.ps1` / `stop.ps1` orchestration)
- Docker with Compose v2 (self-hosted stack via `docker-compose.yml`)
- Services: `db` (PostgreSQL 17 Alpine), `api` (.NET 10), `web` (Next.js 16 standalone), `caddy` (Caddy 2 Alpine)
- Only Caddy exposes ports (`80`, `443`, `443/udp` for HTTP/3); DB and API are internal Docker network only
- Tesseract installed in API container via apt-get (`Backend/Dockerfile` lines 12–18)
- PostgreSQL 17 Alpine running as `db` service (connection string via `Docker` network: `Host=db;Port=5432`)

<!-- GSD:stack-end -->

<!-- GSD:conventions-start source:CONVENTIONS.md -->

## Conventions

## Naming Patterns

- Backend C# files: PascalCase (`UploadReceiptFilesCommand.cs`, `AuthService.cs`)
- Frontend TypeScript/React files: kebab-case (`receipts-table.tsx`, `use-receipts.ts`, `api-client.ts`)
- Test files: PascalCase with `Tests` suffix (`ConfirmClassificationHandlerTests.cs`)
- Frontend E2E test files: kebab-case with `.spec.ts` suffix (`happy-path.spec.ts`)
- Backend: PascalCase, `Async` suffix for async methods (`HandleAsync`, `RegisterAsync`, `SaveChangesAsync`)
- Frontend: camelCase for hooks (`useReceipts`, `useBulkDeleteFiles`), PascalCase for components (`ReceiptsTable`, `AuthenticatedLayout`)
- No naming indication for sync vs. async on frontend (native async/await idiom)
- Backend: camelCase for local variables; `CONSTANT_CASE` for static readonly constants
- Frontend: camelCase everywhere
- Never expose implementation details in variable names
- Backend interfaces: `I<Noun>` prefix (`IAppDbContext`, `IReceiptParser`, `ICurrentUser`, `IBackgroundJobClient`)
- Backend records: `<Noun>` (DTOs: `ReceiptDto`, commands: `UploadReceiptFilesCommand`, queries: `GetReceiptsQuery`)
- Backend entity classes: `<Noun>` (plain POCOs: `User`, `Receipt`, `ReceiptFile`)
- Backend handlers: `<Command|Query>Handler` (e.g., `UploadReceiptFilesHandler`, `GetReceiptFilesHandler`)
- Frontend TypeScript types: PascalCase (imported from `@/types/api`)
- Frontend zod schemas: lowercase with `Schema` suffix or inline in `.ts` files

## Code Style

- Backend: configured via Roslyn analyzers (`.editorconfig`-style rules defined in `Backend/Directory.Build.props`)
- Frontend: no explicit `.prettierrc` in source (relies on ESLint core-web-vitals)
- Line length preference: follow natural wrapping; no strict column limit
- Backend: Roslyn with `<AnalysisLevel>latest</AnalysisLevel>` and nullable reference type enforcement
- Frontend: ESLint 9 via `Frontend/eslint.config.mjs` with `eslint-config-next/core-web-vitals` and `eslint-config-next/typescript`
- Run backend linting: `dotnet build Backend`
- Run frontend linting: `cd Frontend && npm run lint`

## Import Organization

- Backend: standard namespace structure (`TaxReader.Api.*`, `TaxReader.Application.*`, `TaxReader.Infrastructure.*`)
- Frontend: `@/*` resolves to `Frontend/src/*` (`@/components`, `@/hooks`, `@/lib`, `@/types`, `@/providers`)

## Error Handling

- **Backend:** `Result<T>` wrapper pattern (no exceptions for control flow)
- **Frontend:** axios error handling with shared refresh token retry logic
- **Global exception handling (Backend):** `ExceptionHandlingMiddleware` (`Backend/src/TaxReader.Api/Middleware/ExceptionHandlingMiddleware.cs`) catches unhandled exceptions and returns ProblemDetails

## Logging

- Backend: Serilog 9.0.0 with `Serilog.AspNetCore` + `Serilog.Sinks.Console`
- Frontend: browser console only (no structured logging framework)
- Inject `ILogger<T>` via primary constructor: `public class Handler(ILogger<Handler> logger) { }`
- Always use structured logging with named placeholders, never string interpolation:
- Bootstrap logger before host build (`Backend/src/TaxReader.Api/Program.cs:29-31`)
- Final flush in `finally` block (`Backend/src/TaxReader.Api/Program.cs:171`)
- Use `UseSerilogRequestLogging()` middleware for automatic request/response logging
- `LogInformation` for startup events, major operations
- `LogWarning` for API client issues, recoverable errors
- `LogError` for unhandled exceptions (caught by middleware)

## Comments

- Why a non-obvious decision was made (e.g., "Sentry must be FIRST registration so it sees DI-time exceptions" at `Program.cs:39-40`)
- Explain constraints or gotchas tied to infrastructure (e.g., "Cascade delete relied on for cleanup" in `ARCHITECTURE.md`)
- Document pitfalls discovered during development (marked with "Pitfall N:" prefix)
- Explain complex business logic (e.g., token balance checks before processing)
- Don't restate what the code obviously does (`var x = 5; // set x to 5`)
- Don't comment out dead code — delete it instead
- Use triple-slash XML doc comments on public APIs that need clarification
- Example: `ClaudeAiClassifier.ParseBatchResult` summary explains the always-`expectedCount` invariant
- Not required for every public method; use when the contract is non-obvious
- Sparing; same principle as backend
- Comment setup/configuration (`vitest.config.mts` explains `@/` alias resolution and test environment)
- Inline comments for layout/styling tricks when CSS semantics aren't obvious

## Function Design

- Backend: handlers typically 30–80 lines (e.g., `ConfirmClassificationHandler.HandleAsync`)
- Frontend: components 50–150 lines before considering extraction
- If a function exceeds 200 lines, consider breaking it into smaller pieces
- Backend: primary constructor with dependency injection (e.g., `Handler(IAppDbContext db, ILogger<Handler> logger)`)
- Backend: explicit `CancellationToken cancellationToken = default` parameter on every async method
- Frontend: props as a single destructured object or TypeScript interface (prefer destructuring for clarity)
- Avoid optional positional parameters; use records or explicit defaults
- Backend handlers: always return `Result<TResponse>` or `Task<Result<TResponse>>`
- Frontend query hooks: return `UseQueryResult<T>` from TanStack Query (with `isLoading`, `data`, `error` properties)
- Frontend mutation hooks: return `UseMutationResult<TData, TError, TVariables>`

## Module Design

- Backend: one public type per file (command, query, handler, validator, DTO)
- Frontend: one default-exported component per file; helper components colocated as private functions
- File-scoped namespaces in backend (single namespace wrapping the whole file)
- Frontend uses `index.ts` for hook re-exports (e.g., `Frontend/src/hooks/index.ts`)
- Not used in backend; prefer explicit imports to clarify dependencies

## Records vs. Classes

- **Records:** DTOs, commands, queries, responses
- **Classes:** entities (with mutable properties for EF Core), services, handlers

## Language Features (Backend)

- **File-scoped namespaces:** `namespace TaxReader.Application.Commands;` (no braces)
- **Primary constructors:** `public class Handler(IAppDbContext db, ILogger<Handler> logger)`
- **Collection expressions:** `new List<T> { item1, item2 }` → `[item1, item2]`
- **`var` usage:** Prefer `var` for obviously-typed expressions; use explicit types for public API return values
- **Record patterns:** Use records for all DTOs/commands/queries
- **`with` expressions:** Immutable updates on records (e.g., `classification with { Status = Confirmed }`)

## Anti-Patterns to Avoid

### No Repository Pattern

### No AutoMapper

### No MediatR

### No Exceptions for Control Flow

<!-- GSD:conventions-end -->

<!-- GSD:architecture-start source:ARCHITECTURE.md -->

## Architecture

## High-Level Pattern

```

```

- `Domain` → nothing
- `Application` → `Domain` only (defines interfaces, never references Infrastructure)
- `Infrastructure` → `Application` + `Domain` (implements the interfaces)
- `Api` → all three; thin composition root (endpoint mapping + DI wiring only)

## Backend Layers

### Domain (`Backend/src/TaxReader.Domain/`)

- **Entities** (`Entities/`): `User`, `ReceiptFile`, `Receipt`, `ReceiptItem`,
- **Enums** (`Enums/`): `Category`, `ClassificationMethod`, `ClassificationStatus`,
- **Common** (`Common/`): `Result<T>` — sealed, `IsSuccess`/`Value`/`Error` +

### Application (`Backend/src/TaxReader.Application/`)

- **Commands** (`Commands/`): `<Verb><Noun>Command` + matching `Handler`
- **Queries** (`Queries/`): `Get<Noun>Query` + matching `Handler`
- **Jobs** (`Jobs/`): Hangfire background jobs — `ProcessReceiptFileJob`,
- **DTOs** (`DTOs/`): immutable records at the API boundary
- **Interfaces** (`Interfaces/`): ports implemented by Infrastructure
- **Validators** (`Validators/`): FluentValidation classes
- **Mapping** (`Mapping/DtoMappingExtensions.cs`): hand-written, no AutoMapper

### Infrastructure (`Backend/src/TaxReader.Infrastructure/`)

- **Data** (`Data/`): `AppDbContext` + one `IEntityTypeConfiguration<T>` per entity
- **Migrations** (`Migrations/`): 14 EF Core migrations (InitialCreate → AddAuditLog)
- **Services** (`Services/`): `AuthService`, `RefreshTokenService`,
- **Parsers** (`Parsers/`): `AmazonParser` → `EdukiParser` → `GenericParser`
- **Configuration** (`Configuration/`): typed `IOptions<T>` POCOs — `JwtOptions`,
- **Observability** (`Observability/SentryScrubbing.cs`): PII scrubbing for Sentry
- **Storage** (`Storage/FileSystemUploadBlobStore.cs`): upload blob persistence
- **DependencyInjection.cs**: single `AddInfrastructure` wiring extension

### API (`Backend/src/TaxReader.Api/`)

- **Program.cs**: full bootstrap — Sentry (first), Serilog, Infrastructure DI,
- **Endpoints** (`Endpoints/`): one static `Map<Resource>Endpoints` per resource —
- **Hangfire** (`Hangfire/`): `HangfireAdminAuthFilter`, `RecurringJobsBootstrap`
- **Middleware** (`Middleware/ExceptionHandlingMiddleware.cs`): last-resort
- **Services** (`Services/CurrentUser.cs`): HttpContext-backed `ICurrentUser`

## Frontend Layers (`Frontend/src/`)

- **app/**: Next.js App Router. Route groups `(authenticated)/` (billing, receipts,
- **components/**: feature-grouped (`receipts`, `reports`, `upload`, `dashboard`,
- **hooks/**: TanStack Query wrappers (`useReceipts`, `useBulkDeleteFiles`, …)
- **lib/**: `api-client.ts` (axios + JWT interceptors), `format.ts` (German locale),
- **providers/**: `query-provider.tsx`, `auth-provider.tsx`
- **types/**: shared API types (`@/types/api`)

## Data Flow: Upload → Report

## Other Major Flows

- **Auth**: `POST /auth/register` (BCrypt + welcome tokens + JWT pair),
- **Token economy**: `GET /tokens/balance`, `/tokens/transactions`; grants/revocations
- **Payments**: Stripe checkout via `StripePaymentProvider`; `POST` webhook
- **Audit**: `AuditLogger` writes append-only `AuditLogEntry` rows

## Key Abstractions

- **`Result<T>`** — every Application handler returns `Result<T>`; endpoints map
- **`IAppDbContext`** — lets Application use `DbSet<T>`/`SaveChangesAsync` without a
- **`ICurrentUser`** — reads the `sub` claim once per request; removes HttpContext
- **`IReceiptParser` strategy** — registered as multiple instances; handler iterates
- **`IClassificationService`** — isolates token-charging + AI orchestration from the
- **`IBackgroundJobClient`** — wraps Hangfire so Application enqueues jobs without a

## Entry Points

- **Backend HTTP**: `Backend/src/TaxReader.Api/Program.cs` (top-level statements;
- **Background worker**: Hangfire server (same process); recurring jobs registered by
- **Frontend HTTP**: Next.js App Router; root layout `Frontend/src/app/layout.tsx`.
- **Edge (prod)**: Caddy `:443` → `web:3000` (Next.js standalone) and `api:8080`.
- **Tests**: `dotnet test Backend`; `npm run test` / `test:e2e` in `Frontend`.

## Cross-Cutting Concerns

- **AuthN**: JWT bearer enforced globally on `/api/v1/*`; endpoints opt out with
- **AuthZ**: single authenticated role + per-user data scoping in handlers; admin
- **Rate limiting**: global 60/min per IP + named policies `auth-strict` (5/min),
- **Forwarded headers**: `UseForwardedHeaders` first (real client IP behind Caddy).
- **Validation**: FluentValidation from `Application/Validators/`.
- **Logging/observability**: Serilog + `UseSerilogRequestLogging`; Sentry (PII-scrubbed).
- **Exceptions**: `ExceptionHandlingMiddleware` is last-resort; primary path is `Result<T>`.
- **Config**: `IOptions<T>` POCOs; env vars nested via `__` (`Jwt__Secret`).
- **Cancellation**: `CancellationToken` threaded through every handler and EF call.

<!-- GSD:architecture-end -->

<!-- GSD:skills-start source:skills/ -->

## Project Skills

No project skills found. Add skills to any of: `.claude/skills/`, `.agents/skills/`, `.cursor/skills/`, `.github/skills/`, or `.codex/skills/` with a `SKILL.md` index file.
<!-- GSD:skills-end -->

<!-- GSD:workflow-start source:GSD defaults -->

## GSD Workflow Enforcement

Before using Edit, Write, or other file-changing tools, start work through a GSD command so planning artifacts and execution context stay in sync.

Use these entry points:

- `/gsd-quick` for small fixes, doc updates, and ad-hoc tasks
- `/gsd-debug` for investigation and bug fixing
- `/gsd-execute-phase` for planned phase work

Do not make direct repo edits outside a GSD workflow unless the user explicitly asks to bypass it.
<!-- GSD:workflow-end -->

<!-- GSD:profile-start -->

## Developer Profile

> Profile not yet configured. Run `/gsd-profile-user` to generate your developer profile.
> This section is managed by `generate-claude-profile` -- do not edit manually.
<!-- GSD:profile-end -->
