# Coding Conventions

**Analysis Date:** 2026-06-19

## Naming Patterns

**Files:**
- Backend C# files: PascalCase (`UploadReceiptFilesCommand.cs`, `AuthService.cs`)
- Frontend TypeScript/React files: kebab-case (`receipts-table.tsx`, `use-receipts.ts`, `api-client.ts`)
- Test files: PascalCase with `Tests` suffix (`ConfirmClassificationHandlerTests.cs`)
- Frontend E2E test files: kebab-case with `.spec.ts` suffix (`happy-path.spec.ts`)

**Functions & Methods:**
- Backend: PascalCase, `Async` suffix for async methods (`HandleAsync`, `RegisterAsync`, `SaveChangesAsync`)
- Frontend: camelCase for hooks (`useReceipts`, `useBulkDeleteFiles`), PascalCase for components (`ReceiptsTable`, `AuthenticatedLayout`)
- No naming indication for sync vs. async on frontend (native async/await idiom)

**Variables:**
- Backend: camelCase for local variables; `CONSTANT_CASE` for static readonly constants
- Frontend: camelCase everywhere
- Never expose implementation details in variable names

**Types:**
- Backend interfaces: `I<Noun>` prefix (`IAppDbContext`, `IReceiptParser`, `ICurrentUser`, `IBackgroundJobClient`)
- Backend records: `<Noun>` (DTOs: `ReceiptDto`, commands: `UploadReceiptFilesCommand`, queries: `GetReceiptsQuery`)
- Backend entity classes: `<Noun>` (plain POCOs: `User`, `Receipt`, `ReceiptFile`)
- Backend handlers: `<Command|Query>Handler` (e.g., `UploadReceiptFilesHandler`, `GetReceiptFilesHandler`)
- Frontend TypeScript types: PascalCase (imported from `@/types/api`)
- Frontend zod schemas: lowercase with `Schema` suffix or inline in `.ts` files

## Code Style

**Formatting:**
- Backend: configured via Roslyn analyzers (`.editorconfig`-style rules defined in `Backend/Directory.Build.props`)
- Frontend: no explicit `.prettierrc` in source (relies on ESLint core-web-vitals)
- Line length preference: follow natural wrapping; no strict column limit

**Linting:**
- Backend: Roslyn with `<AnalysisLevel>latest</AnalysisLevel>` and nullable reference type enforcement
- Frontend: ESLint 9 via `Frontend/eslint.config.mjs` with `eslint-config-next/core-web-vitals` and `eslint-config-next/typescript`
  - Rules override: `react-hooks/set-state-in-effect: "warn"` (intentional SSR-safe patterns are allowed as warnings)
- Run backend linting: `dotnet build Backend`
- Run frontend linting: `cd Frontend && npm run lint`

## Import Organization

**Order (Backend):**
1. System namespaces (`using System;`, `using System.Collections.Generic;`)
2. Third-party namespaces (`using FluentValidation;`, `using Microsoft.EntityFrameworkCore;`)
3. Project namespaces (`using TaxReader.Application.Interfaces;`, `using TaxReader.Domain.Entities;`)

**Order (Frontend):**
1. External libraries (`import { useState } from "react";`, `import { useQuery } from "@tanstack/react-query";`)
2. Relative imports from `@/` alias (`import { Button } from "@/components/ui/button";`, `import { useReceipts } from "@/hooks/use-receipts";`)
3. Type imports (often grouped with relative imports)

**Path Aliases:**
- Backend: standard namespace structure (`TaxReader.Api.*`, `TaxReader.Application.*`, `TaxReader.Infrastructure.*`)
- Frontend: `@/*` resolves to `Frontend/src/*` (`@/components`, `@/hooks`, `@/lib`, `@/types`, `@/providers`)

## Error Handling

**Patterns:**
- **Backend:** `Result<T>` wrapper pattern (no exceptions for control flow)
  - Return `Result<T>.Success(value)` for success paths
  - Return `Result<T>.Failure(error)` for validation/not-found errors
  - Endpoints translate `result.IsSuccess` → HTTP status (200/201) or `result.IsFailure` → error status (400/404/409)
  - Example: `Backend/src/TaxReader.Domain/Common/Result.cs` defines the pattern
  - Handler example: `Backend/src/TaxReader.Application/Commands/ConfirmClassificationHandler.cs`

- **Frontend:** axios error handling with shared refresh token retry logic
  - Query/mutation functions return typed payloads; errors are Promises that reject
  - API client (`@/lib/api-client.ts`) handles 401 with automatic token refresh + single shared `refreshPromise` to dedupe concurrent retries
  - Upload endpoint (`@/lib/api-client.ts:143-156`) unwraps 400/409 structured errors as success-shaped objects so the UI renders per-file outcomes
  - Toast notifications via `sonner` library show user-facing error messages (German localized)

- **Global exception handling (Backend):** `ExceptionHandlingMiddleware` (`Backend/src/TaxReader.Api/Middleware/ExceptionHandlingMiddleware.cs`) catches unhandled exceptions and returns ProblemDetails

## Logging

**Framework:** 
- Backend: Serilog 9.0.0 with `Serilog.AspNetCore` + `Serilog.Sinks.Console`
- Frontend: browser console only (no structured logging framework)

**Patterns (Backend):**
- Inject `ILogger<T>` via primary constructor: `public class Handler(ILogger<Handler> logger) { }`
- Always use structured logging with named placeholders, never string interpolation:
  ```csharp
  logger.LogWarning("Anthropic API returned {Status}: {Body}", response.StatusCode, body);
  ```
- Bootstrap logger before host build (`Backend/src/TaxReader.Api/Program.cs:29-31`)
- Final flush in `finally` block (`Backend/src/TaxReader.Api/Program.cs:171`)
- Use `UseSerilogRequestLogging()` middleware for automatic request/response logging

**Severity levels:**
- `LogInformation` for startup events, major operations
- `LogWarning` for API client issues, recoverable errors
- `LogError` for unhandled exceptions (caught by middleware)

## Comments

**When to Comment (Backend):**
- Why a non-obvious decision was made (e.g., "Sentry must be FIRST registration so it sees DI-time exceptions" at `Program.cs:39-40`)
- Explain constraints or gotchas tied to infrastructure (e.g., "Cascade delete relied on for cleanup" in `ARCHITECTURE.md`)
- Document pitfalls discovered during development (marked with "Pitfall N:" prefix)
- Explain complex business logic (e.g., token balance checks before processing)

**When NOT to comment:**
- Don't restate what the code obviously does (`var x = 5; // set x to 5`)
- Don't comment out dead code — delete it instead

**JSDoc/TSDoc (Backend):**
- Use triple-slash XML doc comments on public APIs that need clarification
- Example: `ClaudeAiClassifier.ParseBatchResult` summary explains the always-`expectedCount` invariant
- Not required for every public method; use when the contract is non-obvious

**Comments (Frontend):**
- Sparing; same principle as backend
- Comment setup/configuration (`vitest.config.mts` explains `@/` alias resolution and test environment)
- Inline comments for layout/styling tricks when CSS semantics aren't obvious

## Function Design

**Size:**
- Backend: handlers typically 30–80 lines (e.g., `ConfirmClassificationHandler.HandleAsync`)
- Frontend: components 50–150 lines before considering extraction
- If a function exceeds 200 lines, consider breaking it into smaller pieces

**Parameters:**
- Backend: primary constructor with dependency injection (e.g., `Handler(IAppDbContext db, ILogger<Handler> logger)`)
- Backend: explicit `CancellationToken cancellationToken = default` parameter on every async method
- Frontend: props as a single destructured object or TypeScript interface (prefer destructuring for clarity)
- Avoid optional positional parameters; use records or explicit defaults

**Return Values:**
- Backend handlers: always return `Result<TResponse>` or `Task<Result<TResponse>>`
- Frontend query hooks: return `UseQueryResult<T>` from TanStack Query (with `isLoading`, `data`, `error` properties)
- Frontend mutation hooks: return `UseMutationResult<TData, TError, TVariables>`

## Module Design

**Exports:**
- Backend: one public type per file (command, query, handler, validator, DTO)
- Frontend: one default-exported component per file; helper components colocated as private functions
- File-scoped namespaces in backend (single namespace wrapping the whole file)

**Barrel Files:**
- Frontend uses `index.ts` for hook re-exports (e.g., `Frontend/src/hooks/index.ts`)
- Not used in backend; prefer explicit imports to clarify dependencies

## Records vs. Classes

**Backend:**
- **Records:** DTOs, commands, queries, responses
  - Example: `record UploadReceiptFilesCommand(IReadOnlyList<FileUploadItem> Files, ...)`
  - Immutable by default; `init` accessors for optional properties
- **Classes:** entities (with mutable properties for EF Core), services, handlers
  - Example: `public class User { public string Email { get; set; } }`
  - Handlers are concrete classes (no interfaces, injected directly)

## Language Features (Backend)

- **File-scoped namespaces:** `namespace TaxReader.Application.Commands;` (no braces)
- **Primary constructors:** `public class Handler(IAppDbContext db, ILogger<Handler> logger)`
- **Collection expressions:** `new List<T> { item1, item2 }` → `[item1, item2]`
- **`var` usage:** Prefer `var` for obviously-typed expressions; use explicit types for public API return values
- **Record patterns:** Use records for all DTOs/commands/queries
- **`with` expressions:** Immutable updates on records (e.g., `classification with { Status = Confirmed }`)

## Anti-Patterns to Avoid

### No Repository Pattern
**What happens:** Handlers use `IAppDbContext.DbSet<T>` directly with LINQ-to-EF
**Why this is correct:** Reduces abstraction; queries are explicit and testable
**Example:** `Backend/src/TaxReader.Application/Commands/ConfirmClassificationHandler.cs` queries `dbContext.ReceiptItems` directly

### No AutoMapper
**What happens:** Hand-written extension methods in `DtoMappingExtensions.cs`
**Why this is correct:** Mappings are visible and debuggable; no magic reflection at runtime
**Example:** `Backend/src/TaxReader.Application/Mapping/DtoMappingExtensions.cs` has explicit `ToDto()` extension methods

### No MediatR
**What happens:** Handlers are concrete classes registered as Scoped in `Program.cs` and injected directly into endpoints
**Why this is correct:** Type-safe, explicit DI; no service locator anti-pattern
**Example:** `UploadReceiptFilesHandler` is injected as `handler` parameter in the endpoint

### No Exceptions for Control Flow
**What happens:** Use `Result<T>` for validation errors, not-found errors, and business logic failures
**Why this is correct:** Performance; clarity about expected vs. exceptional paths
**Endpoints translate:** `result.IsSuccess` → 200/201; `result.IsFailure` → 400/404/409

---

*Conventions analysis: 2026-06-19*
