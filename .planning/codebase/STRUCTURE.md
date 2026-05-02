# Project Structure

**Analysis Date:** 2026-04-29

## Repository Layout

```
TaxReader/
├── .claude/                    # GSD planning + Claude config
├── .planning/                  # GSD planning artifacts (this map lives here)
├── Backend/                    # .NET 10 solution
│   ├── src/
│   │   ├── TaxReader.Api/             # ASP.NET Core entry point
│   │   ├── TaxReader.Application/     # CQRS handlers, DTOs, interfaces
│   │   ├── TaxReader.Domain/          # Entities, enums, Result<T>
│   │   └── TaxReader.Infrastructure/  # EF, services, parsers
│   ├── tests/
│   │   └── TaxReader.UnitTests/       # xUnit tests
│   ├── Directory.Build.props          # Global C# settings
│   ├── Directory.Packages.props       # Central NuGet versions
│   ├── Dockerfile
│   └── TaxReader.sln
├── Frontend/                   # Next.js 16 app
│   ├── src/
│   │   ├── app/                       # App-router pages
│   │   ├── components/                # React components
│   │   ├── hooks/                     # TanStack Query hooks
│   │   ├── lib/                       # api-client, format helpers
│   │   ├── providers/                 # Auth + QueryClient providers
│   │   └── types/                     # Shared TS types
│   ├── components.json                # shadcn/ui config
│   ├── next.config.ts
│   ├── package.json
│   ├── tsconfig.json
│   ├── eslint.config.mjs
│   ├── postcss.config.mjs
│   ├── Dockerfile
│   ├── AGENTS.md                      # AI agent instructions
│   └── CLAUDE.md                      # → @AGENTS.md
├── storage/                    # Local PDF/image storage (gitignored, dev-only)
├── .env / .env.example         # Backend env vars
├── .gitignore
├── CLAUDE.md                   # Top-level project instructions
├── Caddyfile                   # Reverse-proxy config
├── docker-compose.yml          # 4-service stack: db, api, web, caddy
├── start.ps1 / stop.ps1        # PowerShell orchestration
└── build-diag.txt              # ⚠ 1.8MB build diagnostic dump (see CONCERNS.md)
```

## Backend Structure

### `Backend/src/TaxReader.Api/`
```
Endpoints/
├── AuthEndpoints.cs            # /auth/register, /login, /refresh, DELETE /account
├── ClassificationEndpoints.cs  # /receipt-items/{id}/confirm, /batch-confirm, /pending-suggestions
├── ReceiptEndpoints.cs         # /receipts, /receipts/{id}, /receipts/{id}/items, /reclassify
├── ReceiptFileEndpoints.cs     # /receipt-files (POST upload, GET, DELETE, /bulk-delete)
├── ReportEndpoints.cs          # /reports/category-totals, /annual-summary, /export
├── SettingsEndpoints.cs        # /settings (GET, PUT)
└── TokenEndpoints.cs           # /tokens/balance, /transactions, /purchase
Middleware/
└── ExceptionHandlingMiddleware.cs
Services/
└── CurrentUser.cs              # HttpContext-backed ICurrentUser
Program.cs                      # Full bootstrap (173 lines)
appsettings.json                # Non-secret config
appsettings.Development.json
```

### `Backend/src/TaxReader.Application/`
```
Commands/
├── BatchConfirmCommand.cs / BatchConfirmHandler.cs
├── BulkDeleteReceiptFilesCommand.cs / BulkDeleteReceiptFilesHandler.cs
├── ConfirmClassificationCommand.cs / ConfirmClassificationHandler.cs
├── DeleteAccountHandler.cs
├── DeleteReceiptFileCommand.cs / DeleteReceiptFileHandler.cs
├── ReclassifyReceiptCommand.cs / ReclassifyReceiptHandler.cs
├── UpdateUserSettingsCommand.cs / UpdateUserSettingsHandler.cs
└── UploadReceiptFilesCommand.cs / UploadReceiptFilesHandler.cs
Queries/
├── GetAnnualSummaryQuery.cs / Handler.cs
├── GetCategoryTotalsQuery.cs / Handler.cs
├── GetExportDataHandler.cs
├── GetPendingSuggestionsQuery.cs / Handler.cs
├── GetReceiptByIdQuery.cs / Handler.cs
├── GetReceiptFilesQuery.cs / Handler.cs
├── GetReceiptItemsQuery.cs / Handler.cs
├── GetReceiptsQuery.cs / Handler.cs
└── GetUserSettingsHandler.cs
DTOs/
├── AuthDtos.cs                 # RegisterRequest, LoginRequest, AuthResponse, UserDto
├── ReceiptDto.cs / ReceiptFileDto.cs / ReceiptItemDto.cs
├── ItemClassificationDto.cs / PendingSuggestionDto.cs
├── CategoryTotalDto.cs / AnnualSummaryDto.cs / ExportItemDto.cs
├── TokenBalanceDto.cs
├── UploadReceiptFilesResponse.cs
└── UserSettingsDto.cs
Interfaces/
├── IAiClassifier.cs / IClassificationService.cs
├── IAppDbContext.cs / IAuthService.cs / ITokenService.cs
├── ICurrentUser.cs
├── IPdfTextExtractor.cs / IImageTextExtractor.cs
└── IReceiptParser.cs
Mapping/
└── DtoMappingExtensions.cs     # ToDto() extension methods
Validators/
├── ConfirmClassificationValidator.cs
├── GetAnnualSummaryValidator.cs
├── GetCategoryTotalsValidator.cs
└── UploadReceiptFilesValidator.cs
```

### `Backend/src/TaxReader.Domain/`
```
Common/
└── Result.cs                   # Result<T> success/failure container
Entities/
├── User.cs                     # User account + refresh-token state
├── ReceiptFile.cs              # Raw uploaded document
├── Receipt.cs                  # Parsed business document
├── ReceiptItem.cs              # Line item
├── ItemClassification.cs       # Classification decision (historical)
├── ClassificationRule.cs       # (Defined; not actively used — AI-only flow)
├── ProcessingRun.cs            # Pipeline execution record
├── UserTokenBalance.cs         # Token economy: current balance
└── TokenTransaction.cs         # Token economy: ledger entry
Enums/
├── Category.cs                 # ConsumablesAndOfficeSupplies, SpecialistLiterature, TeachingMaterials, DigitalToolsAndSoftware, OfficeEquipment, TravelAndCommuting, ProfessionalDevelopment, Unknown
├── ClassificationMethod.cs     # Rule, Manual, AI
├── ClassificationStatus.cs     # Suggested, Confirmed
├── FileStatus.cs               # Uploaded, Processing, Processed, Failed
├── ProcessingStatus.cs         # Pending, Extracting, Parsing, Classifying, Completed, Failed
└── TokenTransactionType.cs     # Adjustment, Consumption, Refund, Purchase
```

### `Backend/src/TaxReader.Infrastructure/`
```
Configuration/
├── AnthropicOptions.cs         # ApiKey, Model, MaxTokens, CostPerClassification
├── JwtOptions.cs               # Secret, Issuer, Audience, expirations
└── TesseractOptions.cs         # DataPath
Data/
├── AppDbContext.cs             # DbContext + IAppDbContext implementation
└── Configurations/             # IEntityTypeConfiguration<T> per entity
    ├── ClassificationRuleConfiguration.cs
    ├── ItemClassificationConfiguration.cs
    ├── ProcessingRunConfiguration.cs
    ├── ReceiptConfiguration.cs
    ├── ReceiptFileConfiguration.cs
    ├── ReceiptItemConfiguration.cs
    ├── TokenTransactionConfiguration.cs
    ├── UserConfiguration.cs
    └── UserTokenBalanceConfiguration.cs
Migrations/                     # 7 EF migrations (see INTEGRATIONS.md)
Parsers/
├── AmazonParser.cs             # Amazon.de invoices
├── EdukiParser.cs              # Eduki teaching-material invoices
└── GenericParser.cs            # Fallback (registered LAST)
Services/
├── AiOnlyClassificationService.cs   # Token-aware AI orchestration
├── AuthService.cs                   # BCrypt + JWT issuance
├── ClaudeAiClassifier.cs            # Anthropic API client
├── CsvExportService.cs              # CSV export
├── OcrTextNormalizer.cs             # Cleanup OCR artifacts
├── PdfExportService.cs              # QuestPDF tax-summary export
├── PdfPigTextExtractor.cs           # PDF → text via PdfPig
├── TesseractImageTextExtractor.cs   # Image → text via Tesseract (Singleton)
└── TokenService.cs                  # Atomic ledger operations
DependencyInjection.cs          # AddInfrastructure(IServiceCollection, IConfiguration)
```

### `Backend/tests/TaxReader.UnitTests/`
```
Application/
├── Commands/
│   ├── ConfirmClassificationHandlerTests.cs
│   └── UploadReceiptFilesHandlerTests.cs
├── Mapping/
│   └── DtoMappingExtensionsTests.cs
├── Queries/
│   ├── GetAnnualSummaryHandlerTests.cs
│   └── GetCategoryTotalsHandlerTests.cs
└── Validators/
    ├── ConfirmClassificationValidatorTests.cs
    ├── GetCategoryTotalsValidatorTests.cs
    └── UploadReceiptFilesValidatorTests.cs
Domain/
├── ReceiptFileTests.cs
├── ReceiptItemTests.cs
├── ReceiptTests.cs
└── ResultTests.cs
Helpers/
└── TestDataFactory.cs          # Static factories for entities
Infrastructure/
├── Parsers/
│   ├── AmazonParserTests.cs
│   ├── EdukiParserTests.cs
│   └── GenericParserTests.cs
└── Services/
    └── OcrTextNormalizerTests.cs
```

## Frontend Structure

### `Frontend/src/app/` (Next.js App Router)
```
layout.tsx                      # Root: Theme, AuthProvider, QueryProvider, Toaster
login/page.tsx
register/page.tsx
(authenticated)/                # Route group requiring auth
├── layout.tsx                  # Sidebar shell + auth gate
├── page.tsx                    # / dashboard
├── upload/page.tsx             # /upload
├── receipts/
│   ├── page.tsx                # /receipts (list)
│   └── [id]/page.tsx           # /receipts/{id} (detail)
├── reports/page.tsx            # /reports
└── settings/page.tsx           # /settings
(legal)/                        # Public legal pages
├── layout.tsx
├── datenschutz/page.tsx        # GDPR privacy page (German)
└── impressum/page.tsx          # Required German publisher info
```

### `Frontend/src/components/`
```
dashboard/
├── category-overview.tsx
├── dashboard-stats.tsx
├── pending-suggestions.tsx
├── quick-actions.tsx
├── recent-receipts.tsx
└── welcome-banner.tsx
layout/
├── app-sidebar.tsx             # shadcn Sidebar
├── header.tsx
└── theme-toggle.tsx
receipts/
├── classification-badge.tsx
├── classify-dialog.tsx
├── receipt-items-table.tsx
├── receipts-table.tsx
└── year-filter.tsx
reports/
├── annual-summary-card.tsx
├── category-breakdown.tsx
├── export-buttons.tsx
└── year-selector.tsx
tokens/
└── token-balance-badge.tsx
ui/                             # shadcn/ui generated primitives
upload/                         # File-drop + progress UI
```

### `Frontend/src/lib/`
```
api-client.ts                   # Axios instance + all backend calls + JWT refresh logic
format.ts                       # Currency/date helpers (German locale)
utils.ts                        # cn() Tailwind merge helper
```

### `Frontend/src/providers/` and `Frontend/src/hooks/`
```
providers/
├── auth-provider.tsx           # useAuth() context
└── query-provider.tsx          # TanStack QueryClient provider
hooks/
└── use-receipts.ts, use-receipt-files.ts, ...   # Wrapper hooks around api-client functions
```

## Naming Conventions

### File / Directory
- **C# files:** PascalCase, one public type per file matching the file name
- **C# folders:** PascalCase, plural for collections (`Endpoints/`, `Commands/`, `DTOs/`)
- **TypeScript files:** kebab-case (`api-client.ts`, `receipts-table.tsx`)
- **TypeScript folders:** lowercase, sometimes hyphenated when descriptive
- **Test files:** `<TypeUnderTest>Tests.cs` mirroring the production folder structure under `Application/`, `Domain/`, `Infrastructure/`

### Code symbols (C#)
- **Commands:** `<Verb><Noun>Command` (e.g. `UploadReceiptFilesCommand`)
- **Handlers:** matching `<Command>Handler` / `<Query>Handler`
- **Queries:** `Get<Noun>Query`
- **DTOs:** `<Noun>Dto` (e.g. `ReceiptDto`)
- **Interfaces:** `I<Noun>` (e.g. `IReceiptParser`)
- **Validators:** `<Command/Query>Validator`
- **Endpoints classes:** `<Resource>Endpoints` static class with `Map<Resource>Endpoints` extension method

### Database (snake_case via EFCore.NamingConventions)
- Entities `ReceiptFile`, `ItemClassification` → tables `receipt_files`, `item_classifications`
- Properties `ContentHash`, `UploadedAt` → columns `content_hash`, `uploaded_at`

## Key Locations Cheat Sheet

| Need | Path |
|---|---|
| Add a new endpoint | `Backend/src/TaxReader.Api/Endpoints/` + register in `Program.cs:155-161` |
| Add a new handler | `Backend/src/TaxReader.Application/Commands/` or `/Queries/` + register in `Program.cs:69-85` |
| Add a new DTO | `Backend/src/TaxReader.Application/DTOs/` + extension in `Mapping/DtoMappingExtensions.cs` |
| Add a domain entity | `Backend/src/TaxReader.Domain/Entities/` + `DbSet<>` in `AppDbContext.cs` + `Configurations/` + migration |
| Add a new infrastructure service | `Backend/src/TaxReader.Infrastructure/Services/` + binding in `DependencyInjection.cs` + `Application/Interfaces/` |
| Add a new receipt parser | `Backend/src/TaxReader.Infrastructure/Parsers/` + register in `DependencyInjection.cs:55-57` (priority order) |
| Add a frontend page | `Frontend/src/app/(authenticated)/<segment>/page.tsx` (auth) or `Frontend/src/app/<segment>/page.tsx` (public) |
| Add a frontend API call | `Frontend/src/lib/api-client.ts` (function) + `Frontend/src/hooks/use-*.ts` (TanStack hook) |
| Add backend env config | `Backend/src/TaxReader.Infrastructure/Configuration/<Name>Options.cs` + `services.Configure<>` in `DependencyInjection.cs` |
| Add a new migration | `dotnet ef migrations add <Name> -p Backend/src/TaxReader.Infrastructure -s Backend/src/TaxReader.Api` |
| Edit security headers | `Caddyfile` |
| Edit container topology | `docker-compose.yml` |

## Project References (Backend)

```
TaxReader.Api          → TaxReader.Application + TaxReader.Infrastructure
TaxReader.Application  → TaxReader.Domain
TaxReader.Infrastructure → TaxReader.Application + TaxReader.Domain
TaxReader.Domain       → (no project refs, no external NuGet refs in production code)
TaxReader.UnitTests    → all four production projects
```

---

*Structure analysis: 2026-04-29*
