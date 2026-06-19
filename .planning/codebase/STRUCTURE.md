---
title: Structure
focus: arch
last_mapped: 2026-06-19
---

# Directory Structure

Monorepo with a .NET backend and a Next.js frontend, orchestrated by Docker
Compose behind a Caddy edge.

```
TaxReader/
├── Backend/                     # .NET 10 solution (TaxReader.sln)
│   ├── Directory.Build.props    # global: net10.0, Nullable, ImplicitUsings, AnalysisLevel
│   ├── Directory.Packages.props # Central Package Management (NuGet versions)
│   ├── Dockerfile               # aspnet:10.0 runtime + Tesseract apt packages
│   ├── src/
│   │   ├── TaxReader.Domain/         # entities, enums, Result<T> — zero deps
│   │   ├── TaxReader.Application/    # CQRS handlers, jobs, interfaces, DTOs, validators
│   │   ├── TaxReader.Infrastructure/ # EF Core, services, parsers, config, migrations
│   │   └── TaxReader.Api/            # Program.cs, endpoints, middleware, Hangfire wiring
│   └── tests/
│       ├── TaxReader.UnitTests/         # xUnit unit tests (in-memory EF, Moq)
│       └── TaxReader.IntegrationTests/  # Postgres Testcontainers + WebAppFactory
├── Frontend/                    # Next.js 16 (App Router, standalone output)
│   ├── src/                     # app/, components/, hooks/, lib/, providers/, types/
│   ├── e2e/                     # Playwright specs (happy-path.spec.ts)
│   ├── instrumentation*.ts      # Sentry instrumentation (client + server)
│   ├── next.config.ts           # /api/v1 rewrites, standalone, allowedDevOrigins
│   ├── playwright.config.ts
│   ├── components.json          # shadcn/ui (style: base-nova)
│   └── Dockerfile               # node:22-alpine runtime
├── Caddyfile                    # TLS edge, security headers, compression
├── docker-compose.yml           # db, api, web, caddy services
├── .github/workflows/ci.yml     # CI pipeline
├── CLAUDE.md                    # project instructions
└── .planning/                   # GSD planning artifacts (this map lives here)
```

## Backend Layout (`Backend/src/`)

### TaxReader.Domain
| Folder | Contents |
|--------|----------|
| `Entities/` | `User`, `ReceiptFile`, `Receipt`, `ReceiptItem`, `ItemClassification`, `ClassificationRule`, `ProcessingRun`, `UserTokenBalance`, `TokenTransaction`, `RefreshToken`, `Payment`, `AuditLogEntry` |
| `Enums/` | `Category`, `ClassificationMethod`, `ClassificationStatus`, `FileStatus`, `ProcessingStatus`, `TokenTransactionType`, `PaymentStatus`, `AuditAction` |
| `Common/` | `Result.cs` |

### TaxReader.Application
| Folder | Contents |
|--------|----------|
| `Commands/` | `<Verb><Noun>Command` + `Handler` pairs (upload, confirm, batch-confirm, reclassify, delete, bulk-delete, cancel, save-rule, acknowledge-mismatch, update-settings, delete-account) |
| `Queries/` | `Get<Noun>Query` + `Handler` pairs (receipts, receipt-by-id, items, file-status, category-totals, annual-summary, export-data, pending-suggestions, user-settings) |
| `Jobs/` | Hangfire jobs: `ProcessReceiptFileJob`, `ClassifyBatchJob`, `ExportUserDataJob`, `ExportCleanupJob`, `GrantTokensJob`, `RevokeTokensJob`, `RefreshTokenCleanupJob`, `HangfireFailedJobCleanupJob` |
| `DTOs/` | `<Noun>Dto` records + response shapes (`UploadReceiptFilesResponse`, `UploadAcceptedResponse`, `PaymentDtos`, `AuthDtos`, …) |
| `Interfaces/` | `I<Noun>` ports implemented by Infrastructure |
| `Validators/` | `<Command>Validator` FluentValidation classes |
| `Mapping/` | `DtoMappingExtensions.cs` |
| `Common/` | `UploadErrorCatalog.cs` |
| `Exceptions/` | `InsufficientTokensException`, `NoTextExtractedException`, `ParserNotFoundException` |

### TaxReader.Infrastructure
| Folder | Contents |
|--------|----------|
| `Data/` | `AppDbContext.cs` |
| `Data/Configurations/` | one `IEntityTypeConfiguration<T>` per entity |
| `Migrations/` | 14 EF Core migrations + `AppDbContextModelSnapshot.cs` |
| `Services/` | auth, AI classifiers, extractors, OCR, tokens, exports, Stripe, audit, Hangfire client, admin seeding |
| `Parsers/` | `AmazonParser`, `EdukiParser`, `GenericParser` |
| `Configuration/` | `JwtOptions`, `AnthropicOptions`, `TesseractOptions`, `RefreshTokenOptions`, `StripeOptions`, `UploadStorageOptions` |
| `Observability/` | `SentryScrubbing.cs` |
| `Storage/` | `FileSystemUploadBlobStore.cs` |
| (root) | `DependencyInjection.cs` |

### TaxReader.Api
| Folder | Contents |
|--------|----------|
| `Endpoints/` | `Auth`, `ReceiptFile`, `Receipt`, `Classification`, `Report`, `Token`, `Payment`, `Settings`, `Export`, `Health` |
| `Hangfire/` | `HangfireAdminAuthFilter`, `RecurringJobsBootstrap` |
| `Middleware/` | `ExceptionHandlingMiddleware` |
| `Services/` | `CurrentUser` |
| `Properties/` | `launchSettings.json` |
| (root) | `Program.cs`, `appsettings.json`, `appsettings.Development.json` |

## Frontend Layout (`Frontend/src/`)
| Folder | Contents |
|--------|----------|
| `app/(authenticated)/` | `billing/`, `receipts/`, `receipts/[id]/`, `reports/`, `settings/`, `upload/` |
| `app/(legal)/` | `agb/`, `datenschutz/`, `impressum/`, `widerruf/` (German legal pages) |
| `app/login/`, `app/register/` | auth pages |
| `components/` | feature groups: `receipts`, `reports`, `upload`, `dashboard`, `tokens`, `consent`, `layout` |
| `components/ui/` | shadcn/ui primitives |
| `hooks/` | TanStack Query hooks |
| `lib/` | `api-client.ts`, `format.ts`, `utils.ts` |
| `providers/` | `query-provider.tsx`, `auth-provider.tsx` |
| `types/` | shared API types |

## Test Layout (`Backend/tests/`)
- `TaxReader.UnitTests/` — mirrors source layout: `Application/`, `Auth/`,
  `Domain/`, `Infrastructure/`, `Services/`, `Pipeline/`, `Jobs/`, `Hangfire/`,
  `RateLimiting/`, `Configuration/`, `Cors/`, `Health/`, `Observability/`,
  `Webhooks/`, `Helpers/` (factories)
- `TaxReader.IntegrationTests/` — `Fixtures/` (Postgres container, test collection),
  `IntegrationTestWebAppFactory.cs`, plus cascade-delete, duplicate-detection,
  payment-idempotency, refresh-token-rotation, and migration-smoke suites

## Naming Conventions (file/dir)
- **Backend files**: PascalCase matching the primary type (`UploadReceiptFilesHandler.cs`).
  File-scoped namespaces; one type per file (configs, endpoints, DTOs).
- **Backend folders**: PascalCase by role (`Commands`, `Queries`, `Services`, `Parsers`).
- **Migrations**: `<timestamp>_<Name>.cs` (+ `.Designer.cs`).
- **Frontend files**: kebab-case (`receipts-table.tsx`); component names PascalCase.
- **Frontend route groups**: parenthesized `(authenticated)`, `(legal)`; dynamic
  segments bracketed (`[id]`).
- **Config files**: root-level (`docker-compose.yml`, `Caddyfile`, `.env`).

## Key Locations (quick reference)
| Need | Path |
|------|------|
| API bootstrap / DI | `Backend/src/TaxReader.Api/Program.cs` |
| Infrastructure wiring | `Backend/src/TaxReader.Infrastructure/DependencyInjection.cs` |
| DB context | `Backend/src/TaxReader.Infrastructure/Data/AppDbContext.cs` |
| Anthropic config (single source) | `Backend/src/TaxReader.Infrastructure/Configuration/AnthropicOptions.cs` |
| Parser priority order | `Backend/src/TaxReader.Infrastructure/DependencyInjection.cs` |
| Frontend API client | `Frontend/src/lib/api-client.ts` |
| Frontend rewrites/config | `Frontend/next.config.ts` |
| Compose stack | `docker-compose.yml` |
| Edge / TLS | `Caddyfile` |
