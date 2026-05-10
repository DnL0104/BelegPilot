
## Overview

BelegPilot is an API-first system that ingests PDF receipts (e.g., Amazon, EDUKI), extracts structured data, classifies expenses into tax-relevant categories, and provides aggregated reporting for tax preparation (focused on teachers).

The system is split into:

-   `backend/` → .NET API and processing pipeline
-   `frontend/` → Next.js UI

----------


## General Guidelines

### Think Before Coding

Don't assume. Don't hide confusion. Surface tradeoffs.

Before implementing:

    State your assumptions explicitly. If uncertain, ask.
    If multiple interpretations exist, present them - don't pick silently.
    If a simpler approach exists, say so. Push back when warranted.
    If something is unclear, stop. Name what's confusing. Ask.

### 2. Simplicity First

Minimum code that solves the problem. Nothing speculative.

    No features beyond what was asked.
    No abstractions for single-use code.
    No "flexibility" or "configurability" that wasn't requested.
    No error handling for impossible scenarios.
    If you write 200 lines and it could be 50, rewrite it.

Ask yourself: "Would a senior engineer say this is overcomplicated?" If yes, simplify.
3. Surgical Changes

Touch only what you must. Clean up only your own mess.

When editing existing code:

    Try to improve it
    comment only meaningful things.
    Don't refactor things that aren't broken.
    If you notice unrelated dead code, mention it - don't delete it.

When your changes create orphans:

    Remove imports/variables/functions that YOUR changes made unused.
    Don't remove pre-existing dead code unless asked.

The test: Every changed line should trace directly to the user's request.
4. Goal-Driven Execution

Define success criteria. Loop until verified.

Transform tasks into verifiable goals:

    "Add validation" → "Write tests for invalid inputs, then make them pass"
    "Fix the bug" → "Write a test that reproduces it, then make it pass"
    "Refactor X" → "Ensure tests pass before and after"

For multi-step tasks, state a brief plan:

1. [Step] → verify: [check]
2. [Step] → verify: [check]
3. [Step] → verify: [check]

Strong success criteria let you loop independently. Weak criteria ("make it work") require constant clarification.


## Tech Stack

### Backend

-   .NET 10, ASP.NET Core Minimal APIs
-   Entity Framework Core 10 with PostgreSQL
-   FluentValidation for request validation
-   Serilog for structured logging
-   UglyToad.PdfPig for PDF text extraction
-   xUnit + FluentAssertions + Moq for testing
-   Docker + Docker Compose

### Frontend

-   Next.js (App Router)
-   React 19
-   TypeScript
-   Tailwind CSS
-   shadcn/ui
-   TanStack Query
-   TanStack Table
-   React Hook Form + Zod
-   Axios

----------

## Project Structure

-   backend/
    -   src/
        -   BelegPilot.Api/
        -   BelegPilot.Application/
        -   BelegPilot.Domain/
        -   BelegPilot.Infrastructure/
    -   tests/
        -   BelegPilot.UnitTests/
-   frontend/
    -   app/
    -   components/
    -   lib/
    -   types/

----------

## Commands

### Backend

-   Build:
    -   `dotnet build backend`
-   Run API:
    -   `dotnet run --project backend/src/BelegPilot.Api`
-   Test:
    -   `dotnet test backend`
-   Add Migration:
    -   `dotnet ef migrations add <Name> -p backend/src/BelegPilot.Infrastructure -s backend/src/BelegPilot.Api`
-   Update Database:
    -   `dotnet ef database update -p backend/src/BelegPilot.Infrastructure -s backend/src/BelegPilot.Api`

----------

### Frontend

-   Install dependencies:
    -   `cd frontend && npm install`
-   Run dev server:
    -   `cd frontend && npm run dev`
-   Build:
    -   `cd frontend && npm run build`

----------

### Docker (Full Stack)

-   Run everything:
    -   `docker compose up --build`
-   Reset:
    -   `docker compose down -v`

----------

## Architecture Overview

Pipeline:

Upload → Storage → Extraction → Parsing → Classification → Reporting

----------

## Architecture Rules

-   Domain layer has ZERO dependencies
-   Application defines interfaces only
-   Infrastructure implements external concerns
-   API is thin (only endpoints + DI)
-   No Application → Infrastructure reference
-   EF Core is used directly (no repository pattern)

----------

## Domain Terms

### ReceiptFile

-   Represents a raw uploaded document (PDF)
-   Contains metadata, storage path, and hash
-   Represents the technical origin of data

----------

### Receipt

-   Represents a parsed business document
-   Contains:
    -   vendor
    -   purchase date
    -   totals
    -   raw text

----------

### ReceiptItem

-   Represents a single expense entry
-   Contains:
    -   description
    -   amount
    -   quantity
-   Smallest tax-relevant unit

----------

### Category

-   ConsumablesAndOfficeSupplies
-   SpecialistLiterature
-   Unknown

----------

### ItemClassification

-   Represents a classification decision
-   Contains:
    -   category
    -   method (Rule, Manual, AI)
    -   status (Suggested, Confirmed)
    -   reason

----------

### ClassificationRule

-   Represents a rule used for classification

----------

### ProcessingRun

-   Represents a single pipeline execution

----------

## Database Design

### Tables

-   receipt_files
-   receipts
-   receipt_items
-   item_classifications
-   categories
-   classification_rules
-   processing_runs

----------

### Principles

-   UUID primary keys
-   decimal(18,2) for money
-   UTC timestamps
-   no stored aggregates
-   classification is historical

----------

## API Design

### Receipt Files

-   POST /api/receipt-files
-   GET /api/receipt-files
-   POST /api/receipt-files/{id}/process

----------

### Receipts

-   GET /api/receipts
-   GET /api/receipts/{id}

----------

### Items

-   GET /api/receipts/{id}/items

----------

### Classification

-   POST /api/receipt-items/{id}/confirm

----------

### Reporting

-   GET /api/reports/category-totals?year=2025
-   GET /api/reports/annual-summary?year=2025

----------

## Code Conventions

### Naming

-   Commands: ProcessReceiptCommand
-   Queries: GetCategoryTotalsQuery
-   DTOs: ReceiptDto

----------

### Patterns We Use

-   Primary constructors for DI
-   Records for DTOs and commands
-   Result<T> pattern for error handling
-   File-scoped namespaces
-   Always pass CancellationToken

----------

### Patterns We DON'T Use

-   Repository pattern
-   AutoMapper
-   Stored procedures
-   Exceptions for control flow

----------

## Classification Strategy

### Phase 1

-   Rule-based
-   keyword matching
-   source-based defaults

### Phase 2

-   DB-driven rules
-   AI support

----------

## Parsing Strategy

-   AmazonParser
-   EdukiParser
-   GenericParser

----------

## Frontend Architecture

### Overview

The frontend is a Next.js application consuming the backend API.

Responsibilities:

-   Upload PDFs
-   Display receipts and items
-   Allow classification correction
-   Show aggregated tax data

----------

### Routing

-   `/` → Dashboard
-   `/upload` → Upload
-   `/receipts` → List
-   `/receipts/[id]` → Detail
-   `/reports` → Reports

----------

### State Management

-   TanStack Query for server state
-   Local state for UI

----------

### Components

#### Upload

-   FileDropzone
-   UploadProgress

#### Receipts

-   ReceiptTable
-   ReceiptItemList

#### Reports

-   CategorySummaryCard
-   YearSelector

----------

### UI Patterns

-   Cards for summaries
-   Tables for structured data
-   Dialogs for editing
-   Toast notifications

----------

### UX Principles

-   Minimal clicks
-   Fast feedback
-   Clear classification visibility
-   Editable suggestions

----------

### Styling

-   Tailwind CSS
-   Responsive design
-   Consistent spacing

----------

### Error Handling

-   Toast-based error feedback
-   Automatic retries via TanStack Query

----------

## Testing

-   Unit tests for domain and application
-   FluentAssertions
-   Moq

### Naming Convention

-   Method_Scenario_Result

----------

## Logging

-   Serilog
-   Structured logging
-   Pipeline tracking

----------

## Storage

-   Initial:
    -   local filesystem
-   Future:
    -   cloud storage

----------

## Scaling Strategy

-   Background jobs
-   OCR integration
-   AI classification
-   Multi-user support
-   Frontend expansion

----------

## Key Decisions

-   Classification is historical
-   Item-level calculation
-   API-first design
-   Modular parsing
-   Clear separation of backend and frontend

<!-- GSD:project-start source:PROJECT.md -->
## Project

**TaxReader**

TaxReader is a web application that helps German private taxpayers turn a pile of receipt PDFs and images into a clean per-category-per-year expense summary they can transcribe into ELSTER or hand to their Steuerberater. Receipts are text-extracted (PdfPig + Tesseract OCR), parsed by format-specific parsers (Amazon, Eduki, Generic), AI-classified into tax-relevant categories, and aggregated into a German-localized PDF/CSV report. **This milestone hardens the existing build for a commercial DE launch by tax season.**

**Core Value:** Trustworthy classification — every line item correctly categorized into the right tax category, with reasoning the user can audit and override. If accuracy fails, the report is worthless.

### Constraints

- **Tech stack**: .NET 10 / EF Core / PostgreSQL / Next.js 16 / shadcn/ui / Anthropic / Tesseract — locked. No rewrites in this milestone.
- **Timeline**: Commercial launch within ~3 months of 2026-05-02 — target window is by tax-season peak (~July 2026).
- **Operations**: Solo developer with paging-style alerting expectation. No on-call rotation, no support team. Automation must compensate.
- **Scale target**: 100–500 paying users in first 6 months. Design for that, not thousands.
- **Compliance**: GDPR mandatory. StBerG positioning ("Helfer, not Berater") mandatory. GoBD where applicable. Anthropic AVV (Auftragsverarbeitungsvertrag) required for processing personal data.
- **Localization**: All end-user UI and copy in German.
- **Hosting**: Self-hosted Docker Compose stack with Caddy edge. Not migrating to managed cloud in this milestone.
- **Budget**: AI inference cost flows through the token economy (pass-through). Other tooling (Sentry, payment provider fees, monitoring) bounded by what a pre-revenue solo product can absorb.

**Anthropic model:** `claude-haiku-4-5` is the production default — ~10× cheaper and ~3-5× faster than Sonnet, sufficient for the 13-DE-category classification choice. Single source of truth lives in `Backend/src/TaxReader.Infrastructure/Configuration/AnthropicOptions.cs`. Override per-environment via the `Anthropic__Model` env var; the API logs the resolved value at startup so any drift is visible immediately.
<!-- GSD:project-end -->

<!-- GSD:stack-start source:codebase/STACK.md -->
## Technology Stack

## Languages
- C# (latest, `<LangVersion>latest</LangVersion>`) — Backend (.NET 10) across `Backend/src/TaxReader.Api`, `Backend/src/TaxReader.Application`, `Backend/src/TaxReader.Domain`, `Backend/src/TaxReader.Infrastructure`
- TypeScript ^5 — Frontend (Next.js / React) across `Frontend/src/`
- PowerShell — Local orchestration scripts (`start.ps1`, `stop.ps1`)
- Caddyfile DSL — Reverse proxy config (`Caddyfile`)
- Dockerfile — Build definitions (`Backend/Dockerfile`, `Frontend/Dockerfile`)
## Runtime
- .NET 10 (`<TargetFramework>net10.0</TargetFramework>` in `Backend/Directory.Build.props`) — backend runtime image `mcr.microsoft.com/dotnet/aspnet:10.0`
- Node.js 22 Alpine — frontend runtime (`Frontend/Dockerfile`, line 14: `FROM node:22-alpine AS runtime`)
- NuGet with **Central Package Management** (`<ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>` in `Backend/Directory.Packages.props`); per-project `.csproj` files contain `<PackageReference>` without versions
- npm with `package-lock.json` committed (`Frontend/package-lock.json` present, ~360KB)
## Frameworks
- ASP.NET Core 10 Minimal APIs — entry point `Backend/src/TaxReader.Api/Program.cs`
- Entity Framework Core 10.0.4 with PostgreSQL via `Npgsql.EntityFrameworkCore.PostgreSQL` 10.0.1
- `EFCore.NamingConventions` 10.0.1 — snake_case naming (configured in `Backend/src/TaxReader.Infrastructure/DependencyInjection.cs`, line 22)
- FluentValidation 12.0.0 (+ DI extensions) — registered in `Program.cs` line 66
- Serilog 9.0.0 (`Serilog.AspNetCore`, `Serilog.Sinks.Console`)
- `Asp.Versioning.Http` 8.1.0 — API versioning (route group `/api/v1` in `Program.cs` line 153)
- `Microsoft.AspNetCore.OpenApi` 10.0.4 + `Scalar.AspNetCore` 2.6.0 — API docs at `/scalar/v1`
- `Microsoft.AspNetCore.Authentication.JwtBearer` 10.0.4 + `System.IdentityModel.Tokens.Jwt` 8.12.1
- `BCrypt.Net-Next` 4.0.3 — password hashing (used in `Backend/src/TaxReader.Infrastructure/Services/AuthService.cs`)
- `Scrutor` 6.1.0 — DI assembly scanning support
- Next.js 16.2.2 (App Router, `output: "standalone"`) — `Frontend/next.config.ts`
- React 19.2.4 + React DOM 19.2.4
- TanStack React Query ^5.96.2 — server state (`Frontend/src/providers/query-provider.tsx`)
- TanStack React Table ^8.21.3 — data tables
- React Hook Form ^7.72.1 + `@hookform/resolvers` ^5.2.2 + Zod ^4.3.6 — form handling and validation
- shadcn/ui ^4.1.2 (style: `base-nova`) — registered in `Frontend/components.json`
- `@base-ui/react` ^1.3.0 — primitive UI components
- Tailwind CSS ^4 (PostCSS plugin `@tailwindcss/postcss`) — config in `Frontend/postcss.config.mjs`; CSS variables enabled
- `tw-animate-css` ^1.4.0, `tailwind-merge` ^3.5.0, `class-variance-authority` ^0.7.1, `clsx` ^2.1.1
- `next-themes` ^0.4.6 — theme switching
- `lucide-react` ^1.7.0 — icon library (declared in `components.json`)
- `sonner` ^2.0.7 — toast notifications
- xUnit 2.9.2 + `xunit.runner.visualstudio` 2.8.2 — backend test runner
- FluentAssertions 7.0.0 — assertion library
- Moq 4.20.72 — mocking
- `Microsoft.EntityFrameworkCore.InMemory` 10.0.4 — in-memory test DB
- `Microsoft.NET.Test.Sdk` 17.12.0
- `coverlet.collector` 6.0.4 — code coverage
- Test project: `Backend/tests/TaxReader.UnitTests/TaxReader.UnitTests.csproj`
- Frontend has **no test framework configured** in `package.json`
- Docker Compose v2 — orchestrates `db`, `api`, `web`, `caddy` services (`docker-compose.yml`)
- ESLint ^9 with `eslint-config-next` 16.2.2 (TS + core-web-vitals) — `Frontend/eslint.config.mjs`
- TypeScript ^5 with `paths: { "@/*": ["./src/*"] }` — `Frontend/tsconfig.json`
- Caddy 2-alpine — reverse proxy with automatic HTTPS (`Caddyfile`)
## Key Dependencies
- `PdfPig` 0.1.12 — primary PDF text extractor (`Backend/src/TaxReader.Infrastructure/Services/PdfPigTextExtractor.cs`); uses bounding-box-based line reconstruction
- `Tesseract` 5.2.0 — local OCR fallback for image receipts (`Backend/src/TaxReader.Infrastructure/Services/TesseractImageTextExtractor.cs`); Singleton-scoped, LSTM-only mode, German + English language packs
- `QuestPDF` 2026.2.4 (Community license) — generates German-localized PDF tax export (`Backend/src/TaxReader.Infrastructure/Services/PdfExportService.cs`)
- `Microsoft.Extensions.Http` 10.0.0 — `HttpClient` factory for Anthropic API client
- `axios` ^1.14.0 — HTTP client (`Frontend/src/lib/api-client.ts`); configured with `baseURL: "/api/v1"` and JWT interceptors
- PostgreSQL 17 Alpine (`docker-compose.yml`, line 3: `image: postgres:17-alpine`)
- Caddy 2-alpine — TLS termination, security headers, zstd/gzip compression
## Configuration
- `.env` (gitignored) at repo root, template in `.env.example`
- Required variables (from `.env.example` and `docker-compose.yml`):
- ASP.NET Core configuration sources: `appsettings.json`, `appsettings.Development.json`, environment variables (with `__` for section nesting, e.g. `Jwt__Secret`)
- Frontend reads `BACKEND_API_URL` (default `http://localhost:5190`) in `Frontend/next.config.ts`
- Backend reads optional `CORS_ALLOWED_ORIGINS` (comma-separated) in `Program.cs` line 28
- Backend reads optional `RUN_MIGRATIONS=true` to auto-migrate on startup (`Program.cs` lines 137–149)
- `Backend/Directory.Build.props` — global `<Nullable>enable</Nullable>`, `<ImplicitUsings>enable</ImplicitUsings>`, `<AnalysisLevel>latest</AnalysisLevel>`
- `Backend/Directory.Packages.props` — central NuGet versions
- `Backend/TaxReader.sln` — solution file
- `Frontend/next.config.ts` — `output: "standalone"`, `/api/v1/*` rewrites to backend, dynamic `allowedDevOrigins` from local LAN
- `Frontend/tsconfig.json` — `strict: true`, `moduleResolution: "bundler"`, target `ES2017`
## Platform Requirements
- .NET 10 SDK
- Node.js 22+ (Frontend Dockerfile uses `node:22-alpine`)
- Docker Desktop with Compose v2
- Tesseract OCR with `deu+eng` language packs (Windows dev path hardcoded as `C:/Program Files/Tesseract-OCR/tessdata` in `TesseractOptions.cs` comment; `appsettings.Development.json` sets relative path `tessdata`)
- PowerShell (for `start.ps1` / `stop.ps1` orchestration)
- Self-hosted Docker stack via `docker-compose.yml` — services: `db` (Postgres 17), `api` (.NET 10), `web` (Next.js standalone), `caddy` (TLS edge)
- Only Caddy exposes ports (`80`, `443`, `443/udp` for HTTP/3); DB and API are internal-only on Docker network
- Tesseract installed in API runtime container via `apt-get` (`Backend/Dockerfile` lines 12–18: `tesseract-ocr`, `tesseract-ocr-deu`, `tesseract-ocr-eng`, `libgdiplus`)
- API container runs migrations on boot when `RUN_MIGRATIONS=true`
<!-- GSD:stack-end -->

<!-- GSD:conventions-start source:CONVENTIONS.md -->
## Conventions

## Backend (C# / .NET 10)
### Project-wide Compiler Settings
- `<Nullable>enable</Nullable>` — nullable reference types enforced everywhere
- `<ImplicitUsings>enable</ImplicitUsings>` — common usings auto-imported
- `<LangVersion>latest</LangVersion>` — newest C# features available
- `<AnalysisLevel>latest</AnalysisLevel>` — latest analyzer ruleset
### File-scoped namespaces
### Primary constructors for DI
### Records for DTOs and commands
### Result<T> for error handling — no exceptions for control flow
### Always pass CancellationToken
### Collection expressions
### `var` usage
### Patterns NOT used (per `CLAUDE.md`)
- ❌ **Repository pattern** — handlers use `IAppDbContext.DbSet<T>` directly with EF Core
- ❌ **AutoMapper** — hand-written extension methods in `Application/Mapping/DtoMappingExtensions.cs`
- ❌ **MediatR** — handlers are concrete classes injected directly into endpoints
- ❌ **Stored procedures** — all queries via LINQ-to-EF
- ❌ **Exceptions for control flow** — `Result<T>` everywhere
### Naming
- **Commands:** `<Verb><Noun>Command` + matching `<Command>Handler` (e.g. `UploadReceiptFilesCommand` / `Handler`)
- **Queries:** `Get<Noun>Query` + matching `<Query>Handler`
- **DTOs:** `<Noun>Dto` (e.g. `ReceiptDto`, `AuthResponse`)
- **Interfaces:** `I<Noun>` (e.g. `IReceiptParser`, `IAppDbContext`)
- **Validators:** `<Command>Validator`
- **Endpoint classes:** `<Resource>Endpoints` static + `Map<Resource>Endpoints` extension on `RouteGroupBuilder`
### Endpoint registration pattern
### Validation
### Logging
- `ILogger<T>` injected via primary constructor
- Structured logging always: `logger.LogWarning("Anthropic API returned {Status}: {Body}", response.StatusCode, body);` — message templates with named placeholders, never string interpolation
- Bootstrap logger before host build (`Program.cs:18-20`)
- Final flush in `finally` block (`Program.cs:171`)
### Configuration
- `IOptions<TOptions>` pattern with strongly typed POCOs in `Infrastructure/Configuration/`
- Bound via `services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName))`
- Each options class exposes `public const string SectionName = "Jwt";`
- Environment variables use `__` for section nesting (e.g. `Jwt__Secret`)
### EF Core idioms
- `Set<T>()` returning `DbSet<T>` properties on `AppDbContext`
- One `IEntityTypeConfiguration<T>` per entity in `Data/Configurations/`
- `ApplyConfigurationsFromAssembly` to wire them all up (`AppDbContext.cs:23`)
- Cascade delete relied on for cleanup (e.g. `ReceiptFile` removal cascades to `ProcessingRun`)
- snake_case via `UseSnakeCaseNamingConvention()`
### Error messages: localized German
- `"Ein Konto mit dieser E-Mail existiert bereits."` (`AuthService.cs:34`)
- `"Ungültige E-Mail oder Passwort."` (`AuthService.cs:95`)
- `"Keine Tokens verfügbar – bitte Credits aufladen."` (`AiOnlyClassificationService.cs:60`)
## Frontend (TypeScript / React 19 / Next.js 16)
### TypeScript settings (`Frontend/tsconfig.json`)
- `"strict": true` — full strictness
- `"target": "ES2017"`, `"moduleResolution": "bundler"`
- `"paths": { "@/*": ["./src/*"] }` — import via `@/components/...`, `@/lib/...`
- `"jsx": "preserve"` (Next.js handles JSX transform)
### Next.js conventions
- App Router only (no `pages/` directory)
- Route groups: `(authenticated)/`, `(legal)/` for shared layouts
- Server Components by default; `"use client"` directive for interactive components
- `output: "standalone"` (`next.config.ts`) for Docker
### Component file conventions
- `"use client";` as the first line for interactive components
- One default-exported component per file (PascalCase function name)
- Helper components colocated as private functions in the same file when small (e.g. `ReceiptStatusBadge` inside `receipts-table.tsx:21`)
- File names kebab-case (`receipts-table.tsx`); component names PascalCase (`ReceiptsTable`)
### Forms and validation
- React Hook Form + Zod via `@hookform/resolvers`
- `zodResolver(schema)` on `useForm`
### State
- **Server state:** TanStack Query — wrapped in dedicated hooks under `Frontend/src/hooks/` (e.g. `useReceipts`, `useBulkDeleteFiles`); never call `axios` directly from components
- **Local state:** `useState` for transient UI state (selection sets, dialog open, etc.)
- **Auth state:** `useAuth()` context from `Frontend/src/providers/auth-provider.tsx`
- No Redux, no Zustand, no Jotai
### Styling
- Tailwind CSS 4 utility-first
- shadcn/ui (style: `base-nova` per `components.json`) primitives in `Frontend/src/components/ui/`
- `cn(...)` helper from `@/lib/utils` for conditional classes (wraps `tailwind-merge` + `clsx`)
- Dark mode via `next-themes`; selectors written as `bg-red-50 dark:bg-red-500/10`
- CSS variables enabled (`components.json`)
### Localization (German UI)
- `"Vorschlag" / "Vorschläge"` (singular/plural — see `receipts-table.tsx:45`)
- `"Beleg(e) ausgewählt"`, `"Auswahl aufheben"`, `"Löschen fehlgeschlagen"`
- `"Laden..."` for loading states
- Dates and currencies formatted via `Frontend/src/lib/format.ts` helpers (German locale)
### API client (`Frontend/src/lib/api-client.ts`)
- Single axios instance with `baseURL: "/api/v1"` — relies on Next.js rewrite to backend
- Bearer token attached via request interceptor; refreshed once on 401 (with shared in-flight `refreshPromise` to dedupe concurrent retries)
- One exported async function per endpoint, returning typed payload from `@/types/api`
- Error semantics for upload: 400 / 409 with structured body are unwrapped and returned as success-shaped objects so the UI can render per-file outcomes (`api-client.ts:143-156`)
### Toast notifications
### Icons
### Linting
## Cross-stack Conventions
### Comments
- **Backend** comments are sparing — only when *why* is non-obvious. Strong examples:
- **Frontend** follows the same style; in-line explanations only when behavior would surprise.
- Triple-slash XML doc comments on public APIs that need clarification (e.g. `ClaudeAiClassifier.ParseBatchResult` summary explains the always-`expectedCount` invariant)
### Async
- Backend: `Async` suffix on every async method (e.g. `HandleAsync`, `ClassifyBatchAsync`)
- Backend: `await` on every awaitable; `ConfigureAwait` not used (modern ASP.NET Core idiom)
- Frontend: `async` / `await`, never raw `.then()` chains in app code
### Identifiers
- GUIDs for primary keys everywhere
- `decimal` for money (never `double`)
- `DateTime` UTC for timestamps; `DateOnly` for `PurchaseDate`
<!-- GSD:conventions-end -->

<!-- GSD:architecture-start source:ARCHITECTURE.md -->
## Architecture

## High-Level Pattern
```
```
- Domain: zero dependencies (verify with `dotnet list Backend/src/TaxReader.Domain reference`)
- Application defines interfaces only (`Backend/src/TaxReader.Application/Interfaces/`)
- Infrastructure implements those interfaces
- Application does **NOT** reference Infrastructure
- API is thin: only endpoint mapping + DI registration
## Layers in Detail
### Domain (`Backend/src/TaxReader.Domain/`)
- **Entities** (`Entities/`): plain POCOs with public mutable properties — `User`, `ReceiptFile`, `Receipt`, `ReceiptItem`, `ItemClassification`, `ClassificationRule`, `ProcessingRun`, `UserTokenBalance`, `TokenTransaction`
- **Enums** (`Enums/`): `Category`, `ClassificationMethod`, `ClassificationStatus`, `FileStatus`, `ProcessingStatus`, `TokenTransactionType`
- **Common** (`Common/`): `Result<T>` — sealed class with `IsSuccess`/`Value`/`Error` and static factories `Success`/`Failure` (`Backend/src/TaxReader.Domain/Common/Result.cs`)
- **No services, no logic, no validation.** Pure shape.
### Application (`Backend/src/TaxReader.Application/`)
- **CQRS folders**:
- **DTOs** (`DTOs/`): immutable records used at the API boundary (e.g. `ReceiptDto`, `AnnualSummaryDto`, `AuthDtos`)
- **Interfaces** (`Interfaces/`): ports the Infrastructure layer implements
- **Validators** (`Validators/`): FluentValidation classes (e.g. `UploadReceiptFilesValidator`, `ConfirmClassificationValidator`)
- **Mapping** (`Mapping/`): hand-written extension methods in `DtoMappingExtensions.cs` — **no AutoMapper**
- Handlers are concrete classes registered as `Scoped` in `Program.cs:69-85`; injected directly into endpoints (no MediatR)
### Infrastructure (`Backend/src/TaxReader.Infrastructure/`)
- **Data** (`Data/`): `AppDbContext.cs` + per-entity `Configurations/` (one `IEntityTypeConfiguration<T>` per entity)
- **Migrations** (`Migrations/`): EF Core migrations (7 to date — see `INTEGRATIONS.md`)
- **Services** (`Services/`): concrete implementations of Application interfaces — `AuthService`, `ClaudeAiClassifier`, `AiOnlyClassificationService`, `PdfPigTextExtractor`, `TesseractImageTextExtractor`, `TokenService`, `OcrTextNormalizer`, `PdfExportService`, `CsvExportService`
- **Parsers** (`Parsers/`): receipt-format-specific parsers — `AmazonParser`, `EdukiParser`, `GenericParser` (registered in priority order in `DependencyInjection.cs:55-57`)
- **Configuration** (`Configuration/`): typed `IOptions<T>` POCOs — `JwtOptions`, `AnthropicOptions`, `TesseractOptions`
- **DependencyInjection.cs**: single static `AddInfrastructure` extension method that wires every binding
### API (`Backend/src/TaxReader.Api/`)
- **Program.cs**: full bootstrap (Serilog, JWT, FluentValidation, CORS, OpenAPI, endpoint groups, auto-migration)
- **Endpoints** (`Endpoints/`): one static `MapXxxEndpoints` extension per resource — Auth, ReceiptFile, Receipt, Classification, Report, Token, Settings
- **Middleware** (`Middleware/`): `ExceptionHandlingMiddleware.cs` for top-level error → ProblemDetails translation
- **Services** (`Services/`): `CurrentUser.cs` (HttpContext-backed `ICurrentUser` impl)
- All routes mounted under `app.MapGroup("/api/v1").RequireAuthorization()` (`Program.cs:153`); anonymous endpoints opt-out
## Data Flow: Upload Pipeline
```
```
## Other Major Flows
### Authentication
- `POST /auth/register` → `AuthService.RegisterAsync` → BCrypt hash + 10 free welcome tokens + JWT pair
- `POST /auth/login` → `AuthService.LoginAsync`
- `POST /auth/refresh` → `AuthService.RefreshAsync` (rotates both tokens)
- `DELETE /auth/account` → `DeleteAccountHandler` (cascades all user data)
### Classification confirmation
- `POST /receipt-items/{id}/confirm` → `ConfirmClassificationCommand` → marks classification as `Confirmed` + records `ClassificationMethod.Manual`
- `POST /receipt-items/batch-confirm` → `BatchConfirmCommand` → bulk-confirm a list of item IDs
- `POST /receipts/{id}/reclassify` → re-runs AI classification for a single receipt
### Reporting
- `GET /reports/category-totals?year=` → aggregates `Confirmed` classifications by `Category`
- `GET /reports/annual-summary?year=` → totals + breakdown by month/category
- `GET /reports/export?year=&format=` → CSV (`CsvExportService`) or PDF (`PdfExportService`)
### Token economy
- `GET /tokens/balance` — current `UserTokenBalance.Balance`
- `GET /tokens/transactions` — recent ledger entries
- `POST /tokens/purchase` — placeholder for top-ups (no payment provider integrated; see `CONCERNS.md`)
## Key Abstractions
- **`Result<T>`**: every Application handler returns `Result<TResponse>` instead of throwing. Endpoints translate `IsSuccess` → 200/201/204; `IsFailure` → 400/404/409. No exceptions for control flow.
- **`ICurrentUser`**: removes `HttpContext` dependency from handlers; reads `sub` claim once per request (Scoped).
- **`IAppDbContext`**: lets Application use `DbSet<T>` and `SaveChangesAsync` without referencing `Microsoft.EntityFrameworkCore`. Implemented by `AppDbContext`, registered via `services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>())`.
- **`IReceiptParser` strategy**: registered in DI as multiple instances; handler iterates via `IEnumerable<IReceiptParser>` and picks the first whose `CanParse` returns true. Order = registration order in `DependencyInjection.cs:55-57` (Amazon → Eduki → Generic fallback).
- **`IClassificationService`** (currently single impl `AiOnlyClassificationService`): isolates the token-charging + AI-call orchestration from the upload handler, so the handler doesn't know about tokens or the AI.
## Entry Points
- **Backend HTTP entry:** `Backend/src/TaxReader.Api/Program.cs` — top-level statements; `app.RunAsync()` at line 163. Listens on `ASPNETCORE_URLS` (default `http://+:8080` in container, `http://localhost:5190` in dev launchSettings)
- **Backend test entry:** `dotnet test Backend` runs `Backend/tests/TaxReader.UnitTests`
- **Frontend HTTP entry:** Next.js `app/` router; root layout `Frontend/src/app/layout.tsx`; route groups `(authenticated)`, `(legal)`, plus top-level `login/`, `register/`
- **Edge entry (production):** Caddy on `:443` → `web:3000` (Next.js standalone server)
## Cross-Cutting Concerns
- **Authentication:** JWT bearer enforced globally on `/api/v1/*` (`Program.cs:153`); endpoints opt out with `.AllowAnonymous()`
- **Authorization:** Single role (authenticated user); per-user data scoping enforced inside handlers via `ICurrentUser.UserId` filtering on queries (e.g. `dbContext.ReceiptFiles.Where(f => f.UserId == userId)`)
- **Validation:** FluentValidation registered via `AddValidatorsFromAssemblyContaining<UploadReceiptFilesCommand>` (`Program.cs:66`); validators colocated in `Application/Validators/`
- **Logging:** Serilog with `UseSerilogRequestLogging` middleware; structured logging via `ILogger<T>` injection
- **Exception handling:** `ExceptionHandlingMiddleware` (`Backend/src/TaxReader.Api/Middleware/ExceptionHandlingMiddleware.cs`) — last line of defence; primary error path is `Result<T>.Failure`
- **Configuration:** `IOptions<T>` pattern with section-bound POCOs in `Infrastructure/Configuration/`; sections nested via `__` env var separator (`Jwt__Secret`, `Anthropic__ApiKey`)
- **Cancellation:** `CancellationToken` threaded through every handler and EF call (per `CLAUDE.md` convention "Always pass CancellationToken")
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
