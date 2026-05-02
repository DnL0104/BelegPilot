# Technology Stack

**Analysis Date:** 2026-04-29

## Languages

**Primary:**
- C# (latest, `<LangVersion>latest</LangVersion>`) — Backend (.NET 10) across `Backend/src/TaxReader.Api`, `Backend/src/TaxReader.Application`, `Backend/src/TaxReader.Domain`, `Backend/src/TaxReader.Infrastructure`
- TypeScript ^5 — Frontend (Next.js / React) across `Frontend/src/`

**Secondary:**
- PowerShell — Local orchestration scripts (`start.ps1`, `stop.ps1`)
- Caddyfile DSL — Reverse proxy config (`Caddyfile`)
- Dockerfile — Build definitions (`Backend/Dockerfile`, `Frontend/Dockerfile`)

## Runtime

**Environment:**
- .NET 10 (`<TargetFramework>net10.0</TargetFramework>` in `Backend/Directory.Build.props`) — backend runtime image `mcr.microsoft.com/dotnet/aspnet:10.0`
- Node.js 22 Alpine — frontend runtime (`Frontend/Dockerfile`, line 14: `FROM node:22-alpine AS runtime`)

**Package Manager:**
- NuGet with **Central Package Management** (`<ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>` in `Backend/Directory.Packages.props`); per-project `.csproj` files contain `<PackageReference>` without versions
- npm with `package-lock.json` committed (`Frontend/package-lock.json` present, ~360KB)

## Frameworks

**Core (Backend):**
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

**Core (Frontend):**
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

**Testing:**
- xUnit 2.9.2 + `xunit.runner.visualstudio` 2.8.2 — backend test runner
- FluentAssertions 7.0.0 — assertion library
- Moq 4.20.72 — mocking
- `Microsoft.EntityFrameworkCore.InMemory` 10.0.4 — in-memory test DB
- `Microsoft.NET.Test.Sdk` 17.12.0
- `coverlet.collector` 6.0.4 — code coverage
- Test project: `Backend/tests/TaxReader.UnitTests/TaxReader.UnitTests.csproj`
- Frontend has **no test framework configured** in `package.json`

**Build/Dev:**
- Docker Compose v2 — orchestrates `db`, `api`, `web`, `caddy` services (`docker-compose.yml`)
- ESLint ^9 with `eslint-config-next` 16.2.2 (TS + core-web-vitals) — `Frontend/eslint.config.mjs`
- TypeScript ^5 with `paths: { "@/*": ["./src/*"] }` — `Frontend/tsconfig.json`
- Caddy 2-alpine — reverse proxy with automatic HTTPS (`Caddyfile`)

## Key Dependencies

**Critical (Backend):**
- `PdfPig` 0.1.12 — primary PDF text extractor (`Backend/src/TaxReader.Infrastructure/Services/PdfPigTextExtractor.cs`); uses bounding-box-based line reconstruction
- `Tesseract` 5.2.0 — local OCR fallback for image receipts (`Backend/src/TaxReader.Infrastructure/Services/TesseractImageTextExtractor.cs`); Singleton-scoped, LSTM-only mode, German + English language packs
- `QuestPDF` 2026.2.4 (Community license) — generates German-localized PDF tax export (`Backend/src/TaxReader.Infrastructure/Services/PdfExportService.cs`)
- `Microsoft.Extensions.Http` 10.0.0 — `HttpClient` factory for Anthropic API client

**Critical (Frontend):**
- `axios` ^1.14.0 — HTTP client (`Frontend/src/lib/api-client.ts`); configured with `baseURL: "/api/v1"` and JWT interceptors

**Infrastructure:**
- PostgreSQL 17 Alpine (`docker-compose.yml`, line 3: `image: postgres:17-alpine`)
- Caddy 2-alpine — TLS termination, security headers, zstd/gzip compression

## Configuration

**Environment:**
- `.env` (gitignored) at repo root, template in `.env.example`
- Required variables (from `.env.example` and `docker-compose.yml`):
  - `POSTGRES_USER`, `POSTGRES_PASSWORD`
  - `DOMAIN` (default `localhost`, used by Caddy for TLS)
  - `JWT_SECRET` (min 32 chars, generated via `openssl rand -base64 48`)
  - `JWT_ACCESS_EXPIRY_MINUTES` (default 60), `JWT_REFRESH_EXPIRY_DAYS` (default 30)
  - `ANTHROPIC_API_KEY` (format `sk-ant-...`)
  - `ANTHROPIC_MODEL` (default `claude-sonnet-4-5`; backend code defaults to `claude-haiku-4-5` in `AnthropicOptions.cs`)
  - `ANTHROPIC_MAX_TOKENS` (default 1024), `ANTHROPIC_COST_PER_CLASSIFICATION` (default 1)
- ASP.NET Core configuration sources: `appsettings.json`, `appsettings.Development.json`, environment variables (with `__` for section nesting, e.g. `Jwt__Secret`)
- Frontend reads `BACKEND_API_URL` (default `http://localhost:5190`) in `Frontend/next.config.ts`
- Backend reads optional `CORS_ALLOWED_ORIGINS` (comma-separated) in `Program.cs` line 28
- Backend reads optional `RUN_MIGRATIONS=true` to auto-migrate on startup (`Program.cs` lines 137–149)

**Build:**
- `Backend/Directory.Build.props` — global `<Nullable>enable</Nullable>`, `<ImplicitUsings>enable</ImplicitUsings>`, `<AnalysisLevel>latest</AnalysisLevel>`
- `Backend/Directory.Packages.props` — central NuGet versions
- `Backend/TaxReader.sln` — solution file
- `Frontend/next.config.ts` — `output: "standalone"`, `/api/v1/*` rewrites to backend, dynamic `allowedDevOrigins` from local LAN
- `Frontend/tsconfig.json` — `strict: true`, `moduleResolution: "bundler"`, target `ES2017`

## Platform Requirements

**Development:**
- .NET 10 SDK
- Node.js 22+ (Frontend Dockerfile uses `node:22-alpine`)
- Docker Desktop with Compose v2
- Tesseract OCR with `deu+eng` language packs (Windows dev path hardcoded as `C:/Program Files/Tesseract-OCR/tessdata` in `TesseractOptions.cs` comment; `appsettings.Development.json` sets relative path `tessdata`)
- PowerShell (for `start.ps1` / `stop.ps1` orchestration)

**Production:**
- Self-hosted Docker stack via `docker-compose.yml` — services: `db` (Postgres 17), `api` (.NET 10), `web` (Next.js standalone), `caddy` (TLS edge)
- Only Caddy exposes ports (`80`, `443`, `443/udp` for HTTP/3); DB and API are internal-only on Docker network
- Tesseract installed in API runtime container via `apt-get` (`Backend/Dockerfile` lines 12–18: `tesseract-ocr`, `tesseract-ocr-deu`, `tesseract-ocr-eng`, `libgdiplus`)
- API container runs migrations on boot when `RUN_MIGRATIONS=true`

---

*Stack analysis: 2026-04-29*
