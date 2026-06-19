# Technology Stack

**Analysis Date:** 2026-06-19

## Languages

**Primary:**
- **C#** latest (LangVersion: latest) - Backend (.NET 10) across `Backend/src/TaxReader.Api`, `Backend/src/TaxReader.Application`, `Backend/src/TaxReader.Domain`, `Backend/src/TaxReader.Infrastructure`
- **TypeScript** ^5 - Frontend (Next.js 16 / React 19) across `Frontend/src/`

**Secondary:**
- **PowerShell** - Local orchestration scripts (`start.ps1`, `stop.ps1`)
- **Caddyfile DSL** - Reverse proxy configuration (`Caddyfile`)
- **Dockerfile** - Container definitions (`Backend/Dockerfile`, `Frontend/Dockerfile`)

## Runtime

**Environment:**
- **.NET 10** (`<TargetFramework>net10.0</TargetFramework>` in `Backend/Directory.Build.props`) — Backend runtime image `mcr.microsoft.com/dotnet/aspnet:10.0`
- **Node.js 22 Alpine** - Frontend runtime (`Frontend/Dockerfile` line 14: `FROM node:22-alpine AS runtime`)

**Package Manager:**
- **NuGet** with Central Package Management (`<ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>` in `Backend/Directory.Packages.props`); per-project `.csproj` files reference packages without versions. Master versions in `Backend/Directory.Packages.props`
- **npm** with `package-lock.json` committed (`Frontend/package-lock.json` present, ~360KB)

## Frameworks

**Core:**
- **ASP.NET Core 10 Minimal APIs** - HTTP server entry point `Backend/src/TaxReader.Api/Program.cs` (line 163: `app.RunAsync()`)
- **Next.js 16.2.2** - Frontend framework with App Router (`output: "standalone"` in `Frontend/next.config.ts`); all routes under `Frontend/src/app/`
- **React 19.2.4** - Component library for UI

**Data & ORM:**
- **Entity Framework Core 10.0.4** - ORM for PostgreSQL via `Npgsql.EntityFrameworkCore.PostgreSQL` 10.0.1
- **EFCore.NamingConventions 10.0.1** - Enforces snake_case DB column naming (configured in `Backend/src/TaxReader.Infrastructure/DependencyInjection.cs` line 27)

**API & Validation:**
- **Asp.Versioning.Http 8.1.0** - API versioning (route group `/api/v1` in `Backend/src/TaxReader.Api/Program.cs` line 153)
- **FluentValidation 12.0.0** with DependencyInjectionExtensions 12.0.0 - Request validation (registered at `Backend/src/TaxReader.Api/Program.cs` line 90)
- **Microsoft.AspNetCore.OpenApi 10.0.4** + **Scalar.AspNetCore 2.6.0** - Interactive API docs at `/scalar/v1`

**Authentication & Security:**
- **Microsoft.AspNetCore.Authentication.JwtBearer 10.0.4** + **System.IdentityModel.Tokens.Jwt 8.12.1** - JWT bearer token auth
- **BCrypt.Net-Next 4.0.3** - Password hashing (used in `Backend/src/TaxReader.Infrastructure/Services/AuthService.cs`)

**Data Export & Reports:**
- **QuestPDF 2026.2.4** (Community license) - Generates German-localized PDF tax export (`Backend/src/TaxReader.Infrastructure/Services/PdfExportService.cs`)

**Text Extraction & OCR:**
- **PdfPig 0.1.12** - Primary PDF text extraction via bounding-box reconstruction (`Backend/src/TaxReader.Infrastructure/Services/PdfPigTextExtractor.cs`)
- **Tesseract 5.2.0** - Local OCR fallback for image receipts (`Backend/src/TaxReader.Infrastructure/Services/TesseractImageTextExtractor.cs`); Singleton-scoped, LSTM-only mode, German + English packs

**Background Jobs:**
- **Hangfire 1.8.23** (Core, AspNetCore) - Job scheduling & queue (e.g., async receipt processing, token grant/revoke). Stores jobs in PostgreSQL via `Hangfire.PostgreSql 1.21.1`; also includes `Hangfire.MemoryStorage 1.8.1.2` for testing
- **Hangfire.PostgreSql 1.21.1** - PostgreSQL job storage backend

**Frontend UI & Styling:**
- **shadcn/ui ^4.1.2** - Component library (style: `base-nova` per `Frontend/components.json`)
- **@base-ui/react ^1.3.0** - Unstyled headless component primitives
- **Tailwind CSS ^4** - Utility-first CSS framework (PostCSS plugin `@tailwindcss/postcss` in `Frontend/postcss.config.mjs`)
- **next-themes ^0.4.6** - Dark mode theme switching

**Frontend State & Forms:**
- **TanStack React Query ^5.96.2** - Server state management (configured in `Frontend/src/providers/query-provider.tsx`)
- **TanStack React Table ^8.21.3** - Data table components
- **React Hook Form ^7.72.1** with `@hookform/resolvers ^5.2.2` - Form state management and validation
- **Zod ^4.3.6** - Schema validation for forms

**Frontend Utilities:**
- **axios ^1.14.0** - HTTP client (`Frontend/src/lib/api-client.ts`); configured with `baseURL: "/api/v1"` and JWT interceptors
- **sonner ^2.0.7** - Toast notifications
- **lucide-react ^1.7.0** - Icon library
- **class-variance-authority ^0.7.1**, **tailwind-merge ^3.5.0**, **clsx ^2.1.1** - Conditional CSS class helpers
- **tw-animate-css ^1.4.0** - Tailwind animation utilities

**Testing:**
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

**Observability & Error Tracking:**
- **Sentry ^6.4.1** (Core + AspNetCore) - Error tracking and performance monitoring (DSN bound from `Sentry__Dsn` env var; registered first in `Backend/src/TaxReader.Api/Program.cs` line 45 to catch DI-time exceptions)
- **@sentry/nextjs ^10.52.0** - Sentry SDK for Next.js (frontend error tracking; wrapped conditionally only when `NEXT_PUBLIC_SENTRY_ENABLED === "true"` in `Frontend/next.config.ts` line 49)

**Logging:**
- **Serilog 4.2.0** - Structured logging framework
- **Serilog.AspNetCore 9.0.0** - ASP.NET Core integration (`UseSerilogRequestLogging()`)
- **Serilog.Sinks.Console 6.0.0** - Console output
- **Serilog.Enrichers.Environment 3.0.1** - Environment enrichment (machine name, environment name)

**Dependency Injection & Assembly Scanning:**
- **Scrutor 6.1.0** - DI assembly scanning support

**Build & Dev Tools:**
- **Microsoft.EntityFrameworkCore.Design 10.0.4** - EF Core tooling (migrations)
- **Microsoft.EntityFrameworkCore.Tools 10.0.4** - dotnet ef CLI
- **TypeScript ^5** - Type checker
- **ESLint ^9** with `eslint-config-next 16.2.2` - Linting (TS + core-web-vitals; config: `Frontend/eslint.config.mjs`)
- **@vitejs/plugin-react ^6.0.2** - Vite React plugin (for Vitest)
- **vite-tsconfig-paths ^6.1.1** - Vite path alias plugin

**Payment Processing:**
- **Stripe.net 51.2.0** - Stripe API client (integrated in `Backend/src/TaxReader.Infrastructure/Services/StripePaymentProvider.cs`)

**Security Pinning & Transitive Dependencies:**
- **Newtonsoft.Json 13.0.3** - Pinned to override Hangfire's vulnerable 11.0.1 (mitigates GHSA-5crp-9r3c-p9vr; see `Backend/Directory.Packages.props` line 46)
- **Azure.Identity 1.13.2** - Pinned to override Respawn's vulnerable 1.3.0 (mitigates GHSA-5mfx-4wcx-rv27, GHSA-m5vv-6r4h-3vj9, GHSA-wvxc-855f-jvrv; consumed only by IntegrationTests; see `Backend/Directory.Packages.props` line 60)

## Configuration

**Environment:**
- `.env` at repo root (gitignored); template in `.env.example` (note: cannot be read per forbidden-files policy)
- **Backend ASP.NET Core**: Configuration sources in priority order:
  - `appsettings.json` - Default settings
  - `appsettings.{ASPNETCORE_ENVIRONMENT}.json` - Environment overrides (Development/Production)
  - Environment variables - Override with `__` for section nesting (e.g. `Jwt__Secret`, `Anthropic__ApiKey`, `Stripe__SecretKey`)
- **Frontend**: Environment variables read at **build time** (baked into Next.js standalone):
  - `BACKEND_API_URL` (default `http://localhost:5190` in dev; overridden to `http://api:8080` in Docker build)
  - `NEXT_PUBLIC_SENTRY_ENABLED` (default `false`; set to `true` to enable Sentry)
  - `NEXT_PUBLIC_SENTRY_DSN` (Sentry client-side DSN when enabled)
  - `SENTRY_ORG`, `SENTRY_PROJECT` (Sentry org/project IDs when enabled)
  - `NODE_ENV` (set to `production` in Docker)

**Strongly-Typed Options:**
- `Backend/src/TaxReader.Infrastructure/Configuration/JwtOptions.cs` - `Jwt__*` section
- `Backend/src/TaxReader.Infrastructure/Configuration/AnthropicOptions.cs` - `Anthropic__*` section (model default: `claude-haiku-4-5`)
- `Backend/src/TaxReader.Infrastructure/Configuration/TesseractOptions.cs` - `Tesseract__*` section (pool size default: 3)
- `Backend/src/TaxReader.Infrastructure/Configuration/StripeOptions.cs` - `Stripe__*` section
- `Backend/src/TaxReader.Infrastructure/Configuration/RefreshTokenOptions.cs` - `RefreshToken__*` section
- All bound via `services.Configure<TOptions>(configuration.GetSection(...))` in `Backend/src/TaxReader.Infrastructure/DependencyInjection.cs`

**Build & Runtime Configuration:**
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

**Development:**
- .NET 10 SDK (`dotnet` CLI)
- Node.js 22+ (with npm)
- Docker Desktop with Compose v2
- Tesseract OCR with `deu+eng` language packs (Windows: `C:/Program Files/Tesseract-OCR/tessdata`; Linux: `/usr/share/tesseract-ocr/5/tessdata`)
- PowerShell (for `start.ps1` / `stop.ps1` orchestration)

**Production:**
- Docker with Compose v2 (self-hosted stack via `docker-compose.yml`)
- Services: `db` (PostgreSQL 17 Alpine), `api` (.NET 10), `web` (Next.js 16 standalone), `caddy` (Caddy 2 Alpine)
- Only Caddy exposes ports (`80`, `443`, `443/udp` for HTTP/3); DB and API are internal Docker network only
- Tesseract installed in API container via apt-get (`Backend/Dockerfile` lines 12–18)
- PostgreSQL 17 Alpine running as `db` service (connection string via `Docker` network: `Host=db;Port=5432`)

---

*Stack analysis: 2026-06-19*
