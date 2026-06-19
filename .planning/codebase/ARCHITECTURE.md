---
title: Architecture
focus: arch
last_mapped: 2026-06-19
---

# Architecture

> **Naming note:** The repo and solution are named **TaxReader**, but the running
> product is branded **BelegPilot** (see startup log `"Starting BelegPilot API"`
> in `Backend/src/TaxReader.Api/Program.cs:35` and the Scalar API title at line 348).
> Treat the two names as the same system.

## High-Level Pattern

Clean / onion architecture with strict inward-only dependencies, split across a
.NET backend and a Next.js frontend that talk over a versioned REST API
(`/api/v1`).

```
Frontend (Next.js 16, App Router)
        │  HTTPS  (axios → /api/v1 rewrite)
        ▼
Caddy edge (TLS, security headers)  →  API (ASP.NET Core 10 Minimal APIs)
        │
        ▼
Application (CQRS handlers, interfaces, DTOs)
        │
        ▼
Domain (entities, enums, Result<T>)  ◀── zero dependencies
        ▲
        │  implements interfaces
Infrastructure (EF Core, parsers, AI, OCR, Stripe, Hangfire)
        │
        ▼
PostgreSQL 17  +  Anthropic API  +  Stripe  +  Sentry
```

**Dependency rule (verify with `dotnet list <proj> reference`):**
- `Domain` → nothing
- `Application` → `Domain` only (defines interfaces, never references Infrastructure)
- `Infrastructure` → `Application` + `Domain` (implements the interfaces)
- `Api` → all three; thin composition root (endpoint mapping + DI wiring only)

## Backend Layers

### Domain (`Backend/src/TaxReader.Domain/`)
Pure shape, no logic.
- **Entities** (`Entities/`): `User`, `ReceiptFile`, `Receipt`, `ReceiptItem`,
  `ItemClassification`, `ClassificationRule`, `ProcessingRun`, `UserTokenBalance`,
  `TokenTransaction`, `RefreshToken`, `Payment`, `AuditLogEntry`
- **Enums** (`Enums/`): `Category`, `ClassificationMethod`, `ClassificationStatus`,
  `FileStatus`, `ProcessingStatus`, `TokenTransactionType`, `PaymentStatus`,
  `AuditAction`
- **Common** (`Common/`): `Result<T>` — sealed, `IsSuccess`/`Value`/`Error` +
  static `Success`/`Failure` factories (`Common/Result.cs`)

### Application (`Backend/src/TaxReader.Application/`)
CQRS without MediatR — handlers are concrete classes injected directly into endpoints.
- **Commands** (`Commands/`): `<Verb><Noun>Command` + matching `Handler`
  (e.g. `UploadReceiptFilesHandler`, `ConfirmClassificationHandler`,
  `BatchConfirmHandler`, `ReclassifyReceiptHandler`, `BulkDeleteReceiptFilesHandler`,
  `DeleteAccountHandler`, `AcknowledgeSumMismatchHandler`)
- **Queries** (`Queries/`): `Get<Noun>Query` + matching `Handler`
  (e.g. `GetReceiptsHandler`, `GetCategoryTotalsHandler`, `GetAnnualSummaryHandler`,
  `GetExportDataHandler`, `GetPendingSuggestionsHandler`)
- **Jobs** (`Jobs/`): Hangfire background jobs — `ProcessReceiptFileJob`,
  `ClassifyBatchJob`, `ExportUserDataJob`, `ExportCleanupJob`, `GrantTokensJob`,
  `RevokeTokensJob`, `RefreshTokenCleanupJob`, `HangfireFailedJobCleanupJob`
- **DTOs** (`DTOs/`): immutable records at the API boundary
- **Interfaces** (`Interfaces/`): ports implemented by Infrastructure
  (`IAppDbContext`, `IAiClassifier`, `IClassificationService`, `IReceiptParser`,
  `IPdfTextExtractor`, `IImageTextExtractor`, `ITokenService`, `IAuthService`,
  `IRefreshTokenService`, `IBackgroundJobClient`, `IStripePaymentProvider`,
  `IUploadBlobStore`, `IExportTokenStore`, `IAuditLogger`, `ICurrentUser`)
- **Validators** (`Validators/`): FluentValidation classes
- **Mapping** (`Mapping/DtoMappingExtensions.cs`): hand-written, no AutoMapper

### Infrastructure (`Backend/src/TaxReader.Infrastructure/`)
Concrete implementations.
- **Data** (`Data/`): `AppDbContext` + one `IEntityTypeConfiguration<T>` per entity
  under `Data/Configurations/`; snake_case via `UseSnakeCaseNamingConvention()`
- **Migrations** (`Migrations/`): 14 EF Core migrations (InitialCreate → AddAuditLog)
- **Services** (`Services/`): `AuthService`, `RefreshTokenService`,
  `ClaudeAiClassifier`, `AiOnlyClassificationService`, `HybridClassificationService`,
  `RuleBasedClassifier`, `PdfPigTextExtractor`, `OcrTextNormalizer`,
  `TesseractEnginePool` (+ warmup hosted service), `TokenService`,
  `PdfExportService`, `CsvExportService`, `ExportTokenStore`,
  `StripePaymentProvider`, `StripeWebhookHandler`, `AuditLogger`,
  `HangfireBackgroundJobClient`, `SeedAdminUsersHostedService`
- **Parsers** (`Parsers/`): `AmazonParser` → `EdukiParser` → `GenericParser`
  (priority order, registered in `DependencyInjection.cs`)
- **Configuration** (`Configuration/`): typed `IOptions<T>` POCOs — `JwtOptions`,
  `AnthropicOptions`, `TesseractOptions`, `RefreshTokenOptions`, `StripeOptions`,
  `UploadStorageOptions`
- **Observability** (`Observability/SentryScrubbing.cs`): PII scrubbing for Sentry
- **Storage** (`Storage/FileSystemUploadBlobStore.cs`): upload blob persistence
- **DependencyInjection.cs**: single `AddInfrastructure` wiring extension

### API (`Backend/src/TaxReader.Api/`)
Thin composition root.
- **Program.cs**: full bootstrap — Sentry (first), Serilog, Infrastructure DI,
  JWT auth, FluentValidation, handler registration, forwarded headers, rate
  limiting (4 named policies + global), CORS, OpenAPI/Scalar, auto-migration,
  Hangfire dashboard, endpoint mapping, recurring job bootstrap
- **Endpoints** (`Endpoints/`): one static `Map<Resource>Endpoints` per resource —
  Auth, ReceiptFile, Receipt, Classification, Report, Token, Payment, Settings,
  Export, Health
- **Hangfire** (`Hangfire/`): `HangfireAdminAuthFilter`, `RecurringJobsBootstrap`
- **Middleware** (`Middleware/ExceptionHandlingMiddleware.cs`): last-resort
  error → ProblemDetails translation
- **Services** (`Services/CurrentUser.cs`): HttpContext-backed `ICurrentUser`

## Frontend Layers (`Frontend/src/`)
- **app/**: Next.js App Router. Route groups `(authenticated)/` (billing, receipts,
  receipts/[id], reports, settings, upload) and `(legal)/` (agb, datenschutz,
  impressum, widerruf); top-level `login/`, `register/`
- **components/**: feature-grouped (`receipts`, `reports`, `upload`, `dashboard`,
  `tokens`, `consent`, `layout`) + shadcn primitives in `components/ui/`
- **hooks/**: TanStack Query wrappers (`useReceipts`, `useBulkDeleteFiles`, …)
- **lib/**: `api-client.ts` (axios + JWT interceptors), `format.ts` (German locale),
  `utils.ts` (`cn`)
- **providers/**: `query-provider.tsx`, `auth-provider.tsx`
- **types/**: shared API types (`@/types/api`)

## Data Flow: Upload → Report

1. **Upload** — `POST /api/v1/receipt-files` → `UploadReceiptFilesHandler`
   persists files via `IUploadBlobStore`, returns **202 Accepted** and enqueues a
   Hangfire `ProcessReceiptFileJob` per file.
2. **Process** (background) — `ProcessReceiptFileJob` extracts text
   (`PdfPigTextExtractor` for PDFs, `TesseractEnginePool` OCR for images),
   normalizes (`OcrTextNormalizer`), picks the first matching `IReceiptParser`
   (Amazon → Eduki → Generic), persists `Receipt` + `ReceiptItem`s and a
   `ProcessingRun`, then enqueues `ClassifyBatchJob`.
3. **Classify** (background) — `ClassifyBatchJob` → `IClassificationService`
   (`AiOnlyClassificationService`) charges tokens via `ITokenService` and calls
   `ClaudeAiClassifier` (Anthropic, model `claude-haiku-4-5`). Results stored as
   `ItemClassification` rows. **Classification is append-only** (never overwritten).
4. **Confirm/Override** — user confirms suggestions
   (`POST /receipt-items/{id}/confirm`, `/batch-confirm`) → marks `Confirmed` +
   `ClassificationMethod.Manual`. `POST /receipts/{id}/reclassify` re-runs AI.
5. **Report** — `GET /reports/category-totals`, `/annual-summary`,
   `/export?format=csv|pdf` aggregate **Confirmed** classifications by `Category`.
   Calculations are **item-level**, not receipt-level.

## Other Major Flows
- **Auth**: `POST /auth/register` (BCrypt + welcome tokens + JWT pair),
  `/auth/login`, `/auth/refresh` (rotating refresh tokens via `RefreshTokenService`,
  replay detection), `DELETE /auth/account` (cascade delete all user data)
- **Token economy**: `GET /tokens/balance`, `/tokens/transactions`; grants/revocations
  via `GrantTokensJob` / `RevokeTokensJob` driven by Stripe events
- **Payments**: Stripe checkout via `StripePaymentProvider`; `POST` webhook
  (anonymous, raw-body, outside `/api/v1` auth group) → `StripeWebhookHandler`
- **Audit**: `AuditLogger` writes append-only `AuditLogEntry` rows

## Key Abstractions
- **`Result<T>`** — every Application handler returns `Result<T>`; endpoints map
  `IsSuccess`→200/201/204 and `IsFailure`→400/404/409. No exceptions for control flow.
- **`IAppDbContext`** — lets Application use `DbSet<T>`/`SaveChangesAsync` without a
  reference to EF Core. Registered `AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>())`.
- **`ICurrentUser`** — reads the `sub` claim once per request; removes HttpContext
  from handlers; per-user data scoping enforced inside handler queries.
- **`IReceiptParser` strategy** — registered as multiple instances; handler iterates
  `IEnumerable<IReceiptParser>` and picks the first whose `CanParse` returns true.
- **`IClassificationService`** — isolates token-charging + AI orchestration from the
  pipeline job.
- **`IBackgroundJobClient`** — wraps Hangfire so Application enqueues jobs without a
  Hangfire reference.

## Entry Points
- **Backend HTTP**: `Backend/src/TaxReader.Api/Program.cs` (top-level statements;
  `app.RunAsync()` at line 377). Listens on `ASPNETCORE_URLS` (`http://+:8080` in
  container, `http://localhost:5190` dev).
- **Background worker**: Hangfire server (same process); recurring jobs registered by
  `RecurringJobsBootstrap.Register`; dashboard at `/hangfire` (admin-gated).
- **Frontend HTTP**: Next.js App Router; root layout `Frontend/src/app/layout.tsx`.
- **Edge (prod)**: Caddy `:443` → `web:3000` (Next.js standalone) and `api:8080`.
- **Tests**: `dotnet test Backend`; `npm run test` / `test:e2e` in `Frontend`.

## Cross-Cutting Concerns
- **AuthN**: JWT bearer enforced globally on `/api/v1/*`; endpoints opt out with
  `.AllowAnonymous()`.
- **AuthZ**: single authenticated role + per-user data scoping in handlers; admin
  role (`IsAdmin` claim) gates the Hangfire dashboard.
- **Rate limiting**: global 60/min per IP + named policies `auth-strict` (5/min),
  `auth-refresh` (30/min), `upload-concurrency` (2+4). German 429 ProblemDetails.
- **Forwarded headers**: `UseForwardedHeaders` first (real client IP behind Caddy).
- **Validation**: FluentValidation from `Application/Validators/`.
- **Logging/observability**: Serilog + `UseSerilogRequestLogging`; Sentry (PII-scrubbed).
- **Exceptions**: `ExceptionHandlingMiddleware` is last-resort; primary path is `Result<T>`.
- **Config**: `IOptions<T>` POCOs; env vars nested via `__` (`Jwt__Secret`).
- **Cancellation**: `CancellationToken` threaded through every handler and EF call.
