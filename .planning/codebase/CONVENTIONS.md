# Code Conventions

**Analysis Date:** 2026-04-29

## Backend (C# / .NET 10)

### Project-wide Compiler Settings
Set globally in `Backend/Directory.Build.props`:
- `<Nullable>enable</Nullable>` — nullable reference types enforced everywhere
- `<ImplicitUsings>enable</ImplicitUsings>` — common usings auto-imported
- `<LangVersion>latest</LangVersion>` — newest C# features available
- `<AnalysisLevel>latest</AnalysisLevel>` — latest analyzer ruleset

### File-scoped namespaces
Always. Example from `Backend/src/TaxReader.Domain/Entities/Receipt.cs:1`:
```csharp
namespace TaxReader.Domain.Entities;

public class Receipt
{
    ...
}
```

### Primary constructors for DI
Used pervasively for Application handlers and Infrastructure services. Example `UploadReceiptFilesHandler.cs:12-19`:
```csharp
public class UploadReceiptFilesHandler(
    IAppDbContext dbContext,
    IPdfTextExtractor pdfExtractor,
    IImageTextExtractor imageExtractor,
    IEnumerable<IReceiptParser> parsers,
    IClassificationService classificationService,
    ICurrentUser currentUser)
{
    ...
}
```
The DbContext uses the same pattern: `public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options), IAppDbContext` (`AppDbContext.cs:7-8`).

### Records for DTOs and commands
Immutable, value-equal records — never classes — for any data carrier. Example `UploadReceiptFilesCommand.cs`:
```csharp
public record FileUploadItem(string FileName, long Length, Stream Stream);

public record UploadReceiptFilesCommand(
    IReadOnlyList<FileUploadItem> Files,
    string? SourceHint,
    int? YearHint,
    string? UploadedBy);
```

### Result<T> for error handling — no exceptions for control flow
Defined in `Backend/src/TaxReader.Domain/Common/Result.cs`. Every handler returns `Task<Result<T>>`; endpoints inspect `IsSuccess`. Example handler return shape:
```csharp
return Result<UploadReceiptFilesResponse>.Success(new UploadReceiptFilesResponse(successful, failed));
// or
return Result<AuthResponse>.Failure("Ungültige E-Mail oder Passwort.");
```
Endpoints translate the Result:
```csharp
return result.IsSuccess
    ? Results.Ok(result.Value)
    : Results.BadRequest(new { error = result.Error });
```

### Always pass CancellationToken
Per `CLAUDE.md` and consistently followed. Every handler signature ends with `CancellationToken cancellationToken = default`, threaded into every EF and HTTP call. Exception: `MarkFailedAsync` deliberately uses `CancellationToken.None` to persist failure state even if the request was cancelled (`UploadReceiptFilesHandler.cs:204-206`).

### Collection expressions
Used throughout for empty/single-item collections: `[]`, `[new(...)]`, `[".jpg", ".jpeg", ".png", ".webp"]` (`UploadReceiptFilesHandler.cs:21`).

### `var` usage
Used consistently for local variables when the type is obvious from the right-hand side.

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
Every resource follows the same shape (`Backend/src/TaxReader.Api/Endpoints/ReceiptFileEndpoints.cs`):
```csharp
public static class ReceiptFileEndpoints
{
    public static RouteGroupBuilder MapReceiptFileEndpoints(this RouteGroupBuilder group)
    {
        var receiptFiles = group.MapGroup("/receipt-files").WithTags("Receipt Files");

        receiptFiles.MapPost("/", async (...) => { ... })
            .WithName("UploadReceiptFiles")
            .WithSummary("Upload and process one or more receipt files (PDF, JPG, PNG, WEBP)");

        return group;
    }
}
```
Endpoints take handlers as parameters (resolved by ASP.NET parameter binding from DI), keep work to a few lines, and translate `Result<T>` to `IResult`.

### Validation
FluentValidation, registered via `AddValidatorsFromAssemblyContaining<UploadReceiptFilesCommand>` (`Program.cs:66`). One validator per Command/Query, colocated under `Application/Validators/`. Validators verify shape; handlers verify business invariants.

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
User-facing strings in `Result.Failure(...)` are always German since the product is teacher-focused (DE market):
- `"Ein Konto mit dieser E-Mail existiert bereits."` (`AuthService.cs:34`)
- `"Ungültige E-Mail oder Passwort."` (`AuthService.cs:95`)
- `"Keine Tokens verfügbar – bitte Credits aufladen."` (`AiOnlyClassificationService.cs:60`)

---

## Frontend (TypeScript / React 19 / Next.js 16)

### TypeScript settings (`Frontend/tsconfig.json`)
- `"strict": true` — full strictness
- `"target": "ES2017"`, `"moduleResolution": "bundler"`
- `"paths": { "@/*": ["./src/*"] }` — import via `@/components/...`, `@/lib/...`
- `"jsx": "preserve"` (Next.js handles JSX transform)

### Next.js conventions
**Read `node_modules/next/dist/docs/` before writing code** — `Frontend/AGENTS.md` warns "This is NOT the Next.js you know. This version has breaking changes — APIs, conventions, and file structure may all differ from your training data."
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
All user-visible strings German:
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
`sonner` via `toast.success(...)` / `toast.error(...)`. Loading toasts via `toast.promise(...)`.

### Icons
`lucide-react` exclusively.

### Linting
ESLint 9 + `eslint-config-next` 16.2.2 (TypeScript + core-web-vitals rulesets).

---

## Cross-stack Conventions

### Comments
- **Backend** comments are sparing — only when *why* is non-obvious. Strong examples:
  - `UploadReceiptFilesHandler.cs:46-50` — explains why batching all items into one Anthropic call (latency dominance)
  - `DependencyInjection.cs:40-43` — explains why `TesseractEngine` is Singleton (init cost + thread-safety)
  - `UploadReceiptFilesHandler.cs:204-206` — explains why `MarkFailedAsync` uses `CancellationToken.None`
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

---

*Conventions analysis: 2026-04-29*
