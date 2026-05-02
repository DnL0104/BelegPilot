# Architecture

**Analysis Date:** 2026-04-29

## High-Level Pattern

**Clean Architecture** (Onion / Hexagonal) split across four .NET assemblies plus a Next.js frontend.

```
┌──────────────────────────────────────────────────────┐
│  Frontend (Next.js)                                  │
│  Caddy → web:3000 → /api/v1/* rewrite → api:8080     │
└──────────────────────────────────────────────────────┘
                          │ HTTP + JWT
                          ▼
┌──────────────────────────────────────────────────────┐
│  TaxReader.Api  (Minimal API endpoints, DI wiring)   │
└──────────────────────────────────────────────────────┘
              │ depends on ↓
┌──────────────────────────────────────────────────────┐
│  TaxReader.Application  (CQRS handlers, DTOs,        │
│  validators, interfaces)                             │
└──────────────────────────────────────────────────────┘
              │ depends on ↓             ▲ implemented by
┌──────────────────────────────────────────────────────┐
│  TaxReader.Domain  (entities, enums, Result<T>)      │
│  ZERO DEPENDENCIES — no EF, no Microsoft.*           │
└──────────────────────────────────────────────────────┘
              ▲ implemented by
┌──────────────────────────────────────────────────────┐
│  TaxReader.Infrastructure  (EF DbContext, Anthropic  │
│  client, PdfPig, Tesseract, BCrypt, JWT, parsers)    │
└──────────────────────────────────────────────────────┘
```

**Strict rules** (per `CLAUDE.md`):
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
  - `Commands/` — write operations (record + handler pair, e.g. `UploadReceiptFilesCommand` + `UploadReceiptFilesHandler`)
  - `Queries/` — read operations (record + handler pair, e.g. `GetReceiptsQuery` + `GetReceiptsHandler`)
- **DTOs** (`DTOs/`): immutable records used at the API boundary (e.g. `ReceiptDto`, `AnnualSummaryDto`, `AuthDtos`)
- **Interfaces** (`Interfaces/`): ports the Infrastructure layer implements
  - `IAppDbContext`, `IAuthService`, `IClassificationService`, `IAiClassifier`, `IPdfTextExtractor`, `IImageTextExtractor`, `IReceiptParser`, `ITokenService`, `ICurrentUser`
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

The canonical end-to-end path through `UploadReceiptFilesHandler.HandleAsync` (`Backend/src/TaxReader.Application/Commands/UploadReceiptFilesHandler.cs`):

```
Frontend  POST /api/v1/receipt-files (multipart)
  │
  ▼
Endpoint  ReceiptFileEndpoints.MapPost (line 14)
  │  builds UploadReceiptFilesCommand from IFormFileCollection
  ▼
Handler   UploadReceiptFilesHandler.HandleAsync
  │  for each file:
  │    1. SHA-256 hash → check duplicates per UserId
  │    2. Insert ReceiptFile (Status=Processing) + ProcessingRun (Status=Pending)
  │    3. ProcessingStatus.Extracting →
  │         IPdfTextExtractor.ExtractTextAsync   (PDF)
  │         IImageTextExtractor.ExtractTextAsync (JPG/PNG/WEBP, via Tesseract)
  │    4. ProcessingStatus.Parsing →
  │         IReceiptParser.CanParse (Amazon → Eduki → Generic)
  │         parser.Parse(rawText, file)
  │         Add Receipt + ReceiptItems
  │    5. queue into pending list (defer classification)
  │
  │  After loop: cross-receipt batch
  │    6. ProcessingStatus.Classifying for all pending
  │    7. IClassificationService.ClassifyItemsAsync(allItems)
  │         → AiOnlyClassificationService:
  │             a. token pre-charge (TokenService.TryConsumeManyAsync)
  │             b. ClaudeAiClassifier.ClassifyBatchAsync (single API call)
  │             c. per-item refund for Unknowns
  │             d. auto-confirm if confidence ≥ user threshold
  │    8. ProcessingStatus.Completed, FileStatus.Processed
  │
  ▼
Response  201 Created { successful: [...], failed: [...] }
          409 Conflict if all failures are duplicates
          400 Bad Request if mixed/all errors
```

**Key design choice — cross-receipt batching** (`UploadReceiptFilesHandler.cs:46-50`): the Anthropic round-trip dominates wall-clock latency, so all parsed items across all receipts in the upload batch go in **one** AI call. Items already carry `ReceiptItemId`, so the response maps back without tracking which item belonged to which receipt.

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

---

*Architecture analysis: 2026-04-29*
