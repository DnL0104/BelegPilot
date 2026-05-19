# Phase 3: Background Pipeline + Tesseract Pool - Pattern Map

**Mapped:** 2026-05-19
**Files analyzed:** 25 (15 backend new/modified, 4 EF / config, 6 frontend new/modified)
**Analogs found:** 24 / 25 (one truly greenfield: `IDashboardAuthorizationFilter`)

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|-------------------|------|-----------|----------------|---------------|
| `Backend/src/TaxReader.Application/Commands/UploadReceiptFilesHandler.cs` (REWRITE) | command handler | request-in / job-out | self (lines 82-105 dedup + insert), gut classify loop | exact role, new data-flow |
| `Backend/src/TaxReader.Application/Jobs/ProcessReceiptFileJob.cs` (NEW) | Hangfire job class | job-internal | `UploadReceiptFilesHandler.cs:52-167` per-file loop body | role-match (extract+parse logic verbatim, new container) |
| `Backend/src/TaxReader.Application/Jobs/ClassifyBatchJob.cs` (NEW) | Hangfire job class | job-internal | `UploadReceiptFilesHandler.cs:169-202` cross-receipt batching + `AiOnlyClassificationService.cs:28-122` | role-match |
| `Backend/src/TaxReader.Application/Interfaces/IBackgroundJobClient.cs` (NEW) | Application port | abstraction | `IClassificationService.cs` (Application-only interface that Infra implements) | exact (interface shape) |
| `Backend/src/TaxReader.Application/Common/UploadErrorCatalog.cs` (NEW) | pure static mapper | transform | `Result<T>.Failure("German string")` convention (`AuthService.cs:34, 95`) | role-match (no direct analog; pattern from Result strings) |
| `Backend/src/TaxReader.Application/Queries/GetReceiptFileStatusHandler.cs` (NEW) | query handler | request-response | `GetReceiptByIdHandler.cs` (per-user scoping via `r.ReceiptFile.UserId == currentUser.UserId`) | exact |
| `Backend/src/TaxReader.Application/Commands/CancelReceiptFileHandler.cs` (NEW) | command handler | request-response | `DeleteReceiptFileHandler.cs` (per-user lookup + state change + save) | exact |
| `Backend/src/TaxReader.Application/DTOs/ReceiptFileStatusDto.cs` (NEW) | DTO record | response-out | `ReceiptFileDto.cs` (positional record, primitive props) | exact |
| `Backend/src/TaxReader.Application/DTOs/UploadAcceptedResponse.cs` (NEW) | DTO record | response-out | `UploadReceiptFilesCommand.cs:3` (`FileUploadItem` positional record) | exact |
| `Backend/src/TaxReader.Infrastructure/Services/TesseractEnginePool.cs` (NEW, replaces existing) | OCR service / pool | job-internal | `TesseractImageTextExtractor.cs` (Singleton+lock pattern → Channel pool) | exact role, new lifecycle |
| `Backend/src/TaxReader.Infrastructure/Services/TesseractEnginePoolWarmupService.cs` (NEW) | `IHostedService` | startup-only | no existing `IHostedService`; pattern from `IClassificationService` Singleton+lifecycle in `DependencyInjection.cs:57` | role-only (greenfield IHostedService shape) |
| `Backend/src/TaxReader.Infrastructure/Services/HangfireBackgroundJobClient.cs` (NEW) | Infra adapter | job-out | `AuthService.cs` (adapter implementing an Application interface) | exact role |
| `Backend/src/TaxReader.Infrastructure/Services/AuthService.cs` (MODIFY) | self | request-in | self lines 89-160 (add `IsAdmin` → `role` claim, line 144-150) | exact (in-place edit) |
| `Backend/src/TaxReader.Infrastructure/Services/AdminBootstrap/SeedAdminUsersHostedService.cs` (NEW) | `IHostedService` | startup-only | `TesseractEnginePoolWarmupService` (this phase, peer) + DI registration shape from `DependencyInjection.cs:57` | role-only (greenfield) |
| `Backend/src/TaxReader.Infrastructure/Configuration/TesseractOptions.cs` (MODIFY) | options POCO | startup-only | self (add `PoolSize` property; same `SectionName` const + simple settable property pattern) | exact (in-place edit) |
| `Backend/src/TaxReader.Domain/Entities/User.cs` (MODIFY) | entity | model | self (add `bool IsAdmin { get; set; } = false;` POCO property — same shape as `AutoConfirmThreshold`) | exact (in-place edit) |
| `Backend/src/TaxReader.Domain/Entities/ProcessingRun.cs` (MODIFY) | entity | model | self (add `string? ErrorCode { get; set; }` — same shape as `ErrorMessage`) | exact (in-place edit) |
| `Backend/src/TaxReader.Domain/Enums/ProcessingStatus.cs` (MODIFY) | enum | model | self (add `Queued`, `Cancelled` — see RESEARCH Pitfall 8 for ordering) | exact (in-place edit) |
| `Backend/src/TaxReader.Infrastructure/Data/Configurations/UserConfiguration.cs` (MODIFY) | EF config | startup-only | self (add `IsAdmin` mapping — pattern from existing `Property(e => e.Email)` calls at lines 16-18) | exact (in-place edit) |
| `Backend/src/TaxReader.Infrastructure/Migrations/{ts}_AddIsAdminToUsers.cs` (NEW) | EF migration | data migration | `20260514204609_AddRefreshTokensTable_DropLegacyRefreshTokenColumns.cs` (column add + ordering comment) | exact |
| `Backend/src/TaxReader.Infrastructure/Migrations/{ts}_AddQueuedAndCancelledProcessingStatuses.cs` (NEW) | EF migration | data migration | (no analog — enum-value reorder is unique; see RESEARCH Pitfall 8) | role-only |
| `Backend/src/TaxReader.Infrastructure/DependencyInjection.cs` (MODIFY) | DI bootstrap | startup-only | self lines 56-57 (Tesseract Singleton → swap to `TesseractEnginePool`; add Hangfire registration) | exact (in-place edit) |
| `Backend/src/TaxReader.Api/Program.cs` (MODIFY) | API bootstrap | startup-only | self lines 80-106 (handler `AddScoped` block; add new handlers + Hangfire dashboard pipeline step) | exact (in-place edit) |
| `Backend/src/TaxReader.Api/Hangfire/HangfireAdminAuthFilter.cs` (NEW) | dashboard auth filter | request-in | `Program.cs:67-79` (JWT `TokenValidationParameters` construction is the analog) | partial (interface shape is Hangfire-specific; JWT-validation logic mirrors existing) |
| `Backend/src/TaxReader.Api/Endpoints/ReceiptFileEndpoints.cs` (MODIFY) | endpoint group | request-in | self lines 14-50 (upload POST → 202; new status GET / cancel POST follow lines 65-78 delete pattern) | exact (in-place edit) |
| `Backend/src/TaxReader.Api/Endpoints/AuthEndpoints.cs` (MODIFY) | endpoint group | request-in | self lines 37-55 (login) — add `Response.Cookies.Append` after success | exact (in-place edit) |
| `Frontend/src/lib/api-client.ts` (MODIFY) | API client | request/response | self lines 138-165 (`uploadReceiptFiles` shape) + lines 167-184 (other typed endpoints) | exact (in-place edit) |
| `Frontend/src/hooks/use-receipt-files.ts` (MODIFY) | TanStack hook | server state | self lines 12-30 + `use-receipts.ts:14-20` (useQuery with `enabled: !!id`) | exact (in-place edit) |
| `Frontend/src/components/upload/upload-form.tsx` (MODIFY) | React component | UI | self lines 51-58 (placeholder cards) + lines 251-261 (per-state rendering) | exact (in-place edit) |

## Pattern Assignments

### `Backend/src/TaxReader.Application/Jobs/ProcessReceiptFileJob.cs` (Hangfire job, job-internal)

**Analog:** `Backend/src/TaxReader.Application/Commands/UploadReceiptFilesHandler.cs` lines 52-167 (per-file loop body)

**Imports pattern** — copy verbatim from `UploadReceiptFilesHandler.cs:1-12`:
```csharp
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Serilog.Context;
using TaxReader.Application.DTOs;
using TaxReader.Application.Interfaces;
using TaxReader.Application.Mapping;
using TaxReader.Domain.Common;
using TaxReader.Domain.Entities;
using TaxReader.Domain.Enums;

namespace TaxReader.Application.Jobs;
```

**Primary-constructor DI** — copy shape from `UploadReceiptFilesHandler.cs:14-22`:
```csharp
public class ProcessReceiptFileJob(
    IAppDbContext dbContext,
    IPdfTextExtractor pdfExtractor,
    IImageTextExtractor imageExtractor,   // bound to TesseractEnginePool in DI
    IEnumerable<IReceiptParser> parsers,
    IBackgroundJobClient jobClient,        // NEW abstraction; for the barrier (RESEARCH Pattern 2)
    ILogger<ProcessReceiptFileJob> logger)
{
    // [AutomaticRetry(Attempts = 3, DelaysInSeconds = new[] { 30, 120, 300 })]  ← D-04
    // method body below
}
```

**Core extract+parse pattern** — copy verbatim the body from `UploadReceiptFilesHandler.cs:111-156`, removing the outer `LogContext.PushProperty("ReceiptFileId", ...)` and replacing with `LogContext.PushProperty("JobId", receiptFileId)` per D-05. Specifically:
- Lines 116-117: `run.Status = ProcessingStatus.Extracting; await SaveChangesAsync`
- Lines 119-122: read stream → call `imageExtractor` OR `pdfExtractor`
- Lines 124-128: empty-text → `MarkFailedAsync` + `continue` (now → `return` since one file per job)
- Lines 131-141: `Parsing` status + parser selection + `MarkFailedAsync` on missing
- Lines 143-156: receipt + items + `SaveChangesAsync`

**Image-vs-PDF dispatch** — copy `UploadReceiptFilesHandler.cs:22-36` helper methods verbatim into the job class (or extract to a shared static helper):
```csharp
private static readonly HashSet<string> ImageExtensions = [".jpg", ".jpeg", ".png", ".webp"];
private static bool IsImageFile(string fileName) =>
    ImageExtensions.Contains(Path.GetExtension(fileName).ToLowerInvariant());
private static string GetMediaType(string fileName) => ...;
```

**Failure path** — copy `MarkFailedAsync` from `UploadReceiptFilesHandler.cs:208-227` verbatim, including the `CancellationToken.None` invariant on `SaveChangesAsync` (lines 214-218). When PIPE-05's `UploadErrorCatalog` is added, the catch site at line 161-165 becomes:
```csharp
catch (Exception ex)
{
    var (errorCode, germanMessage) = UploadErrorCatalog.Classify(ex);
    logger.LogError(ex, "{ErrorCode} during ProcessReceiptFileJob for ReceiptFile {ReceiptFileId}", errorCode, receiptFileId);
    run.ErrorCode = errorCode;       // new column from PIPE-05
    run.ErrorMessage = germanMessage; // safe German string; ex.Message NEVER persisted
    run.Status = ProcessingStatus.Failed;
    await dbContext.SaveChangesAsync(CancellationToken.None);
    throw; // Hangfire AutomaticRetry observes the throw; on final failure it moves to Hangfire's Failed state
}
```
(Per RESEARCH § Pattern 10 "Used at job-failure boundary" — lines 805-816 of 03-RESEARCH.md.)

**Barrier pattern (new responsibility)** — see RESEARCH Pattern 2 (lines 376-411 of 03-RESEARCH.md). No direct analog in the codebase. Idempotency-on-entry in `ClassifyBatchJob` mitigates the double-enqueue race per RESEARCH Pitfall 2.

---

### `Backend/src/TaxReader.Application/Jobs/ClassifyBatchJob.cs` (Hangfire job, job-internal)

**Analog A — cross-receipt batching:** `Backend/src/TaxReader.Application/Commands/UploadReceiptFilesHandler.cs` lines 169-202

**Cross-receipt-batching invariant** to preserve verbatim — `UploadReceiptFilesHandler.cs:46-50` comment + lines 175-201:
```csharp
// Cross-receipt batching: parse every file first, queue successes, then run
// a single AI classification call across all items. The Anthropic roundtrip
// (~1s with Haiku) dominates total wall time, so collapsing N sequential
// calls into 1 is the biggest win available without touching the model.

// ...
if (pending.Count > 0)
{
    foreach (var p in pending)
        p.Run.Status = ProcessingStatus.Classifying;
    await dbContext.SaveChangesAsync(cancellationToken);

    var allItems = pending.SelectMany(p => p.Receipt.Items).ToList();
    if (allItems.Count > 0)
    {
        var classifications = await classificationService.ClassifyItemsAsync(allItems, cancellationToken);
        foreach (var classification in classifications)
        {
            classification.Id = Guid.NewGuid();
            dbContext.ItemClassifications.Add(classification);
        }
    }
    // Finalize: mark every successfully-parsed receipt as Processed in one save.
    var now = DateTime.UtcNow;
    foreach (var p in pending)
    {
        p.Run.Status = ProcessingStatus.Completed;
        p.Run.CompletedAt = now;
        p.File.Status = FileStatus.Processed;
        successful.Add(new SuccessfulUpload(p.FileName, p.Receipt.ToDto()));
    }
    await dbContext.SaveChangesAsync(cancellationToken);
}
```
In `ClassifyBatchJob`, the "`pending`" list is reconstructed from DB by querying every `Receipt` whose `ReceiptFile.UploadBatchId == uploadBatchId` (or whose `ProcessingRun.Status == Parsing`); the rest of the block applies unchanged.

**Analog B — token pre-charge / per-item refund / AI failure refund:** `Backend/src/TaxReader.Infrastructure/Services/AiOnlyClassificationService.cs` lines 28-122

Per D-02, **do not move** the token-charging logic into the job; keep it inside `AiOnlyClassificationService.ClassifyItemsAsync` so the job calls `IClassificationService.ClassifyItemsAsync` exactly as `UploadReceiptFilesHandler` does today. Lines to leave unchanged:
- Lines 46-62 (pre-charge): `tokenService.TryConsumeManyAsync` + "Keine Tokens verfügbar – bitte Credits aufladen." German return
- Lines 64-75 (AI failure refund branch): `catch { tokenService.RefundManyAsync; return Unknown[] }` — **also the cancellation path** per D-12
- Lines 79-119 (per-item refund for Unknowns)

**Caveat (Integration Point from CONTEXT lines 200-202):** `AiOnlyClassificationService` currently depends on `ICurrentUser` (line 21 of that file). Hangfire jobs do NOT run inside an HTTP request — `ICurrentUser` (which reads `HttpContext.User.FindFirst("sub")`) returns `Guid.Empty` or throws. The job must pass `Guid userId` as a method parameter (Hangfire serializes job arguments) and the classification service signature must accept a `userId` parameter (or a different `IClassificationService.ClassifyItemsAsync` overload). This is the load-bearing refactor.

**[AutomaticRetry(Attempts = 0)]** per D-04 + RESEARCH § Pattern 3.

---

### `Backend/src/TaxReader.Application/Interfaces/IBackgroundJobClient.cs` (NEW interface)

**Analog:** `Backend/src/TaxReader.Application/Interfaces/IClassificationService.cs` — interface in Application namespace, no Infrastructure dependencies.

**Imports + shape** — copy from `IClassificationService.cs:1-10`:
```csharp
namespace TaxReader.Application.Interfaces;

public interface IBackgroundJobClient
{
    Task<string> EnqueueAsync<TJob>(System.Linq.Expressions.Expression<Func<TJob, Task>> methodCall, CancellationToken cancellationToken = default);
    Task DeleteAsync(string jobId, CancellationToken cancellationToken = default);
}
```
Mirrors the shape of `Hangfire.IBackgroundJobClient` (the LINQ-expression-of-method-call signature is Hangfire's native idiom and is the only Application-friendly way to encode "enqueue this typed call" without referencing Hangfire).

---

### `Backend/src/TaxReader.Application/Commands/CancelReceiptFileHandler.cs` (NEW)

**Analog:** `Backend/src/TaxReader.Application/Commands/DeleteReceiptFileHandler.cs` (whole file, 26 lines)

**Imports + primary-constructor + per-user scoping** — copy `DeleteReceiptFileHandler.cs:1-9` verbatim and extend with `IBackgroundJobClient`:
```csharp
using Microsoft.EntityFrameworkCore;
using TaxReader.Application.Interfaces;
using TaxReader.Domain.Common;
using TaxReader.Domain.Enums;

namespace TaxReader.Application.Commands;

public class CancelReceiptFileHandler(
    IAppDbContext dbContext,
    IBackgroundJobClient jobClient,
    ICurrentUser currentUser)
{
    public async Task<Result<bool>> HandleAsync(Guid receiptFileId, CancellationToken cancellationToken = default)
    {
        // ... (per-user filter same as DeleteReceiptFileHandler.cs:15-16)
    }
}
```

**Per-user scoping** — copy `DeleteReceiptFileHandler.cs:15-16` exactly:
```csharp
var receiptFile = await dbContext.ReceiptFiles
    .FirstOrDefaultAsync(f => f.Id == receiptFileId && f.UserId == currentUser.UserId, cancellationToken);
```

**Result-shape pattern (NotFound vs Conflict vs Success)** — RESEARCH § Code Examples "The cancel endpoint" (lines 1011-1045) provides the exact body. The string error markers (`"NotFound"`, `"TerminalState"`) are matched in the endpoint to map to 404 / 409 status codes — keep these as **sentinel English strings** (NOT user-facing German), since the endpoint translates them. The German strings go on the HTTP response body via the endpoint, matching the existing `DeleteReceiptFileHandler.cs:19` German pattern.

---

### `Backend/src/TaxReader.Application/Queries/GetReceiptFileStatusHandler.cs` (NEW)

**Analog:** `Backend/src/TaxReader.Application/Queries/GetReceiptByIdHandler.cs` (whole file, 26 lines)

**Imports + primary-constructor + per-user scoping** — copy from `GetReceiptByIdHandler.cs:1-9` and adapt the query target. Crucial scoping idiom from `GetReceiptByIdHandler.cs:18`:
```csharp
.Where(r => r.ReceiptFile.UserId == currentUser.UserId)  // join-then-scope
.FirstOrDefaultAsync(r => r.Id == query.Id, cancellationToken);
```

**Implementation** per RESEARCH Pattern 5 lines 472-491:
```csharp
public class GetReceiptFileStatusHandler(IAppDbContext db, ICurrentUser currentUser)
{
    public async Task<Result<ReceiptFileStatusDto>> HandleAsync(Guid receiptFileId, CancellationToken ct)
    {
        var run = await db.ProcessingRuns
            .Where(r => r.ReceiptFileId == receiptFileId && r.ReceiptFile.UserId == currentUser.UserId)
            .OrderByDescending(r => r.StartedAt)
            .FirstOrDefaultAsync(ct);

        if (run is null)
            return Result<ReceiptFileStatusDto>.Failure("Datei nicht gefunden.");

        return Result<ReceiptFileStatusDto>.Success(new ReceiptFileStatusDto(
            run.Status, run.CompletedAt ?? run.StartedAt, run.ErrorCode, run.ErrorMessage));
    }
}
```

---

### `Backend/src/TaxReader.Application/DTOs/ReceiptFileStatusDto.cs` (NEW)

**Analog:** `Backend/src/TaxReader.Application/DTOs/ReceiptFileDto.cs` (positional record)

**Imports + shape:**
```csharp
using TaxReader.Domain.Enums;

namespace TaxReader.Application.DTOs;

public record ReceiptFileStatusDto(
    ProcessingStatus Status,
    DateTime UpdatedAt,
    string? ErrorCode,
    string? ErrorMessage);
```
Per RESEARCH § Claude's Discretion line 81: PascalCase enum serialization (no `JsonStringEnumConverter` retrofit) — drop a per-property `[JsonConverter(typeof(JsonStringEnumConverter))]` on `Status` if string output is desired, per RESEARCH § Anti-Patterns line 843.

---

### `Backend/src/TaxReader.Application/Common/UploadErrorCatalog.cs` (NEW)

**Analog (style):** German `Result<T>.Failure(...)` strings — `AuthService.cs:34, 95, 101` and `AiOnlyClassificationService.cs:60`. No structural analog in the codebase.

**Implementation:** Copy verbatim from RESEARCH § Pattern 10 lines 778-801. Key shape — pure static class, no DI, no exceptions thrown:
```csharp
namespace TaxReader.Application.Common;

public static class UploadErrorCatalog
{
    public static (string ErrorCode, string GermanMessage) Classify(Exception ex) => ex switch
    {
        // Use existing Result.Failure German strings where they already exist
        // (e.g. AiOnlyClassificationService.cs:60 "Keine Tokens verfügbar – bitte Credits aufladen.")
        // ...full body in RESEARCH lines 780-801...
    };
}
```
**Custom exception types referenced** in RESEARCH (`NoTextExtractedException`, `ParserNotFoundException`, `InsufficientTokensException`) do not exist in the codebase yet. The planner decides whether to introduce them (`Backend/src/TaxReader.Application/Common/`) or use sentinel `Exception` subtypes detected via message-string sniffing. Given the convention "no exceptions for control flow" — the catalog should accept already-thrown exceptions from Tesseract/PdfPig/HttpClient, not introduce new throwables for normal failure paths.

---

### `Backend/src/TaxReader.Infrastructure/Services/TesseractEnginePool.cs` (NEW, replaces existing `TesseractImageTextExtractor.cs`)

**Analog:** `Backend/src/TaxReader.Infrastructure/Services/TesseractImageTextExtractor.cs` (whole file, 137 lines)

**Imports** — copy `TesseractImageTextExtractor.cs:1-7` verbatim, add `using System.Threading.Channels;`:
```csharp
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TaxReader.Application.Interfaces;
using TaxReader.Infrastructure.Configuration;
using Tesseract;

namespace TaxReader.Infrastructure.Services;
```

**OCR pipeline body — copy verbatim** from `TesseractImageTextExtractor.cs:45-125`. Specifically, do not touch:
- Lines 55-72: `Pix.LoadFromMemory`, downsample to 2400px max edge, `scaled = loaded.Scale(...)` (D-20)
- Lines 74-87: `engine.Process(working)` + `OcrTextNormalizer.Normalize(raw)` + `LogInformation("OCR done: {Chars} chars in {Ms} ms ...")`
- Lines 94-101: the `TesseractException` "Failed to initialise" → German fallback error message
- Lines 111-125: `CreateEngine()` — `Path.IsPathRooted` resolve + `EngineMode.LstmOnly` + `PageSegMode.SingleBlock`

**Engine lifecycle — REPLACE** the Singleton-with-lock pattern (lines 19-22 `Lock _gate` + `TesseractEngine? _engine`) with the `Channel<TesseractEngine>` design from RESEARCH § Pattern 7 (lines 590-705 of 03-RESEARCH.md). The `RunOcr` method body keeps everything except the `lock (_gate)` block — engine ownership is now per-call (acquired via `_channel.Reader.ReadAsync(ct)`, released via `_channel.Writer.TryWrite(engine)`).

**Tessdata path resolution** — copy `TesseractImageTextExtractor.cs:36-43` (the `ResolveTessDataPath` static method comment block + implementation) verbatim.

**Quarantine-and-replace (D-19)** — RESEARCH Pattern 7 lines 638-680. Always check `TryWrite` return value per RESEARCH Pitfall 6.

---

### `Backend/src/TaxReader.Infrastructure/Services/TesseractEnginePoolWarmupService.cs` (NEW `IHostedService`)

**Analog (lifecycle):** None in repo. Pattern from RESEARCH Pattern 8 (lines 711-736).

**Imports + shape:**
```csharp
using System.Diagnostics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TaxReader.Application.Interfaces;

namespace TaxReader.Infrastructure.Services;

public class TesseractEnginePoolWarmupService(
    IImageTextExtractor pool,
    ILogger<TesseractEnginePoolWarmupService> logger) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (pool is TesseractEnginePool concretePool)
        {
            var sw = Stopwatch.StartNew();
            concretePool.Initialize();
            logger.LogInformation("Tesseract pool warmup complete in {Ms}ms", sw.ElapsedMilliseconds);
        }
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
```
DI registration in `DependencyInjection.cs` per RESEARCH lines 732-735:
```csharp
services.AddSingleton<TesseractEnginePool>();
services.AddSingleton<IImageTextExtractor>(sp => sp.GetRequiredService<TesseractEnginePool>());
services.AddHostedService<TesseractEnginePoolWarmupService>();
```

---

### `Backend/src/TaxReader.Infrastructure/Services/HangfireBackgroundJobClient.cs` (NEW)

**Analog (style):** `Backend/src/TaxReader.Infrastructure/Services/AuthService.cs` — Infrastructure adapter implementing an Application interface (`IAuthService`), thin and DI-injected.

**Imports + primary-constructor:**
```csharp
using Hangfire;
using TaxReader.Application.Interfaces;

namespace TaxReader.Infrastructure.Services;

public class HangfireBackgroundJobClient(Hangfire.IBackgroundJobClient hangfireClient) : Application.Interfaces.IBackgroundJobClient
{
    public Task<string> EnqueueAsync<TJob>(System.Linq.Expressions.Expression<Func<TJob, Task>> methodCall, CancellationToken cancellationToken = default)
    {
        var jobId = hangfireClient.Enqueue(methodCall);
        return Task.FromResult(jobId);
    }

    public Task DeleteAsync(string jobId, CancellationToken cancellationToken = default)
    {
        hangfireClient.Delete(jobId);
        return Task.CompletedTask;
    }
}
```
**Naming-clash caveat:** Application's interface and Hangfire's interface share the name `IBackgroundJobClient`. Disambiguate via `using Hangfire;` + fully-qualified `Application.Interfaces.IBackgroundJobClient` on the class declaration (or alias `using HfClient = Hangfire.IBackgroundJobClient;`).

DI: `services.AddScoped<TaxReader.Application.Interfaces.IBackgroundJobClient, HangfireBackgroundJobClient>();` in `DependencyInjection.cs`.

---

### `Backend/src/TaxReader.Infrastructure/Services/AuthService.cs` (MODIFY — claim injection only)

**In-place edit at lines 144-150** — add `role` claim when `User.IsAdmin == true`:
```csharp
private string GenerateAccessToken(User user)
{
    var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.Secret));
    var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

    var claims = new List<Claim>
    {
        new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
        new(JwtRegisteredClaimNames.Email, user.Email),
        new("name", user.DisplayName),
        new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
    };
    if (user.IsAdmin) claims.Add(new Claim("role", "admin")); // D-07 + D-09

    var token = new JwtSecurityToken(...); // unchanged from current lines 152-157
    return new JwtSecurityTokenHandler().WriteToken(token);
}
```

**HTTP-context-free invariant** (D-10 + Phase 2 02-01) — do NOT set the `tr_access` cookie here. Cookie setting belongs in `AuthEndpoints.cs` (next section).

---

### `Backend/src/TaxReader.Infrastructure/Services/AdminBootstrap/SeedAdminUsersHostedService.cs` (NEW `IHostedService`)

**Analog (lifecycle):** `TesseractEnginePoolWarmupService` (this phase, peer). No code analog in repo.

**Implementation outline (D-08):**
```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TaxReader.Application.Interfaces;

namespace TaxReader.Infrastructure.Services.AdminBootstrap;

public class SeedAdminUsersHostedService(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<SeedAdminUsersHostedService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var csv = configuration["Hangfire:SeedAdminEmails"];
        if (string.IsNullOrWhiteSpace(csv)) return;

        var emails = csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(e => e.ToLowerInvariant())
            .ToArray();

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
        var matched = await db.Users.Where(u => emails.Contains(u.Email)).ToListAsync(cancellationToken);
        foreach (var u in matched) u.IsAdmin = true;
        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Seeded admin role for {Count} user(s)", matched.Count);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
```
**Idempotency** (D-08) — re-running sets `IsAdmin=true` on already-admin rows; EF marks unchanged. Safe.

**Scope creation pattern** (`IServiceScopeFactory`) — same shape used in `Program.cs:300-302` for auto-migration.

---

### `Backend/src/TaxReader.Infrastructure/Configuration/TesseractOptions.cs` (MODIFY)

**In-place edit** — add property below existing ones at line 18:
```csharp
namespace TaxReader.Infrastructure.Configuration;

public class TesseractOptions
{
    public const string SectionName = "Tesseract";
    public string TessDataPath { get; set; } = "/usr/share/tesseract-ocr/5/tessdata";
    public string Language { get; set; } = "deu+eng";

    /// <summary>D-16: bounded engine pool capacity. Tesseract__PoolSize env var.</summary>
    public int PoolSize { get; set; } = 3;
}
```
The `__`-nested env-var binding is automatic via `services.Configure<TesseractOptions>(...)` in `DependencyInjection.cs:56` — no change needed there for `PoolSize`.

---

### `Backend/src/TaxReader.Domain/Entities/User.cs` (MODIFY)

**In-place edit** — add `IsAdmin` POCO property. Pattern from existing `AutoConfirmThreshold` at lines 11-16:
```csharp
public class User
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>D-07: gates access to the Hangfire dashboard at /hangfire.</summary>
    public bool IsAdmin { get; set; } = false;

    public double? AutoConfirmThreshold { get; set; }  // unchanged
    public ICollection<ReceiptFile> ReceiptFiles { get; set; } = [];  // unchanged
    // ... rest unchanged
}
```

---

### `Backend/src/TaxReader.Domain/Entities/ProcessingRun.cs` (MODIFY)

**In-place edit** — add `ErrorCode`. Pattern from existing `ErrorMessage` at line 12:
```csharp
public class ProcessingRun
{
    public Guid Id { get; set; }
    public Guid ReceiptFileId { get; set; }
    public ProcessingStatus Status { get; set; } = ProcessingStatus.Pending;
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }

    /// <summary>D-21: stable enum for frontend switch. Pairs with German ErrorMessage.</summary>
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }

    public string StepDetails { get; set; } = "[]";  // unchanged
    public ReceiptFile ReceiptFile { get; set; } = null!;
}
```

---

### `Backend/src/TaxReader.Domain/Enums/ProcessingStatus.cs` (MODIFY)

**In-place edit** — D-06 numeric order vs. RESEARCH Pitfall 8 trade-off. **Recommend the appended-values variant** to avoid the data-migration enum-renumber risk:
```csharp
namespace TaxReader.Domain.Enums;

public enum ProcessingStatus
{
    Pending = 0,
    Extracting = 1,
    Parsing = 2,
    Classifying = 3,
    Completed = 4,
    Failed = 5,
    Queued = 6,      // NEW — D-06
    Cancelled = 7    // NEW — D-06
}
```
Per RESEARCH Pitfall 8 lines 932-936 and § Runtime State Inventory lines 873-878, this preserves existing-row integrity. Planner should re-confirm with user before adopting; D-06's stated order (`Queued=1, Extracting=2, ...`) is the alternative but requires an in-migration UPDATE that renumbers rows in descending order.

---

### `Backend/src/TaxReader.Infrastructure/Data/Configurations/UserConfiguration.cs` (MODIFY)

**In-place edit** — add `IsAdmin` column mapping. Pattern from `UserConfiguration.cs:16-18` (the `Property(e => e.Email).IsRequired().HasMaxLength(...)` style):
```csharp
public void Configure(EntityTypeBuilder<User> builder)
{
    builder.ToTable("users");
    builder.HasKey(e => e.Id);
    builder.Property(e => e.Id).HasDefaultValueSql("gen_random_uuid()");

    builder.Property(e => e.Email).IsRequired().HasMaxLength(320);
    builder.Property(e => e.DisplayName).IsRequired().HasMaxLength(200);
    builder.Property(e => e.PasswordHash).IsRequired().HasMaxLength(200);
    builder.Property(e => e.IsAdmin).IsRequired().HasDefaultValue(false);  // NEW

    // ... rest unchanged (HasIndex, HasMany, etc.)
}
```
The `snake_case` naming convention (`UseSnakeCaseNamingConvention` in `DependencyInjection.cs:23`) converts `IsAdmin` → `is_admin` automatically.

---

### `Backend/src/TaxReader.Infrastructure/Migrations/{ts}_AddIsAdminToUsers.cs` (NEW)

**Analog:** `Backend/src/TaxReader.Infrastructure/Migrations/20260514204609_AddRefreshTokensTable_DropLegacyRefreshTokenColumns.cs`

**Generate via** `dotnet ef migrations add AddIsAdminToUsers -p Backend/src/TaxReader.Infrastructure -s Backend/src/TaxReader.Api` (per CLAUDE.md command).

**Expected shape** (verifies after EF scaffolding):
```csharp
namespace TaxReader.Infrastructure.Migrations
{
    public partial class AddIsAdminToUsers : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_admin",
                table: "users",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "is_admin", table: "users");
        }
    }
}
```
Comment-block pattern from `20260514_AddRefreshTokensTable...cs:15-17` ("D-XX: ... runs FIRST so ...") is the convention for ordering hints.

---

### `Backend/src/TaxReader.Infrastructure/Migrations/{ts}_AddQueuedAndCancelledProcessingStatuses.cs` (NEW)

**Analog:** None direct. The migration is **pure code-level** (enum mapping is read-time in EF) — no DDL is strictly required IF the appended-values variant of `ProcessingStatus` is adopted (recommended above). The migration may then be a no-op except for adding `processing_runs.error_code` column.

**If adding the `error_code` column** (required by PIPE-05 / D-21):
```csharp
public partial class AddQueuedAndCancelledProcessingStatuses : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "error_code",
            table: "processing_runs",
            type: "character varying(50)",
            maxLength: 50,
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "error_code", table: "processing_runs");
    }
}
```

**If the planner accepts D-06's stated enum-renumber order** instead of the appended variant, this migration must additionally contain the descending-order UPDATE statements documented in RESEARCH Pitfall 8 (lines 931-935) — **before** the enum mapping change takes effect at the EF layer.

---

### `Backend/src/TaxReader.Infrastructure/DependencyInjection.cs` (MODIFY)

**In-place edits at lines 52-57 (Tesseract block):**
- Remove: `services.AddSingleton<IImageTextExtractor, TesseractImageTextExtractor>();` (line 57)
- Replace with: the three lines from RESEARCH Pattern 8 lines 732-735:
```csharp
services.Configure<TesseractOptions>(configuration.GetSection(TesseractOptions.SectionName));
services.AddSingleton<TesseractEnginePool>();
services.AddSingleton<IImageTextExtractor>(sp => sp.GetRequiredService<TesseractEnginePool>());
services.AddHostedService<TesseractEnginePoolWarmupService>();
```
- Drop `using TaxReader.Infrastructure.Services;` if `TesseractImageTextExtractor` was the only consumer (re-check after removal).

**New additions** (after Tesseract block, before parsers block at line 67):
```csharp
// Hangfire (RESEARCH Pattern 1, lines 339-359)
services.AddHangfire(config => config
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UseSerilogLogProvider()
    .UsePostgreSqlStorage(c => c.UseNpgsqlConnection(
        configuration.GetConnectionString("DefaultConnection")),
        new PostgreSqlStorageOptions
        {
            PrepareSchemaIfNecessary = true,
            QueuePollInterval = TimeSpan.FromSeconds(1),
            InvisibilityTimeout = TimeSpan.FromMinutes(30)
        }));

var poolSize = configuration.GetValue<int>("Tesseract:PoolSize", 3);
services.AddHangfireServer(options =>
{
    options.WorkerCount = poolSize;  // D-16 — never more workers than engines (RESEARCH Pitfall 7)
    options.Queues = new[] { "default" };
    options.CancellationCheckInterval = TimeSpan.FromSeconds(2);
});

// Application-layer abstraction → Hangfire adapter
services.AddScoped<TaxReader.Application.Interfaces.IBackgroundJobClient, HangfireBackgroundJobClient>();

// Admin seeding (D-08)
services.AddHostedService<AdminBootstrap.SeedAdminUsersHostedService>();
```

**Pattern for `IOptions<T>` config registration** to follow when adding any new options class (e.g. a future `HangfireOptions`) — `DependencyInjection.cs:38-42`:
```csharp
services.AddSingleton<IValidateOptions<RefreshTokenOptions>, RefreshTokenOptionsValidator>();
services
    .AddOptions<RefreshTokenOptions>()
    .Bind(configuration.GetSection(RefreshTokenOptions.SectionName))
    .ValidateOnStart();
```

---

### `Backend/src/TaxReader.Api/Program.cs` (MODIFY)

**New `using` directives** to add at the top of the file:
```csharp
using Hangfire;
using TaxReader.Api.Hangfire;
using TaxReader.Application.Jobs;
```

**New handler registrations** to add inside the `AddScoped` block at lines 90-106 (mirrors the existing pattern):
```csharp
builder.Services.AddScoped<CancelReceiptFileHandler>();
builder.Services.AddScoped<GetReceiptFileStatusHandler>();
```

**Hangfire dashboard registration** — insert AFTER `app.UseAuthorization();` at current line 278, BEFORE `if (app.Environment.IsDevelopment())` at line 281. From RESEARCH Pattern 6 lines 573-579:
```csharp
app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = new[] { new HangfireAdminAuthFilter(
        app.Services.GetRequiredService<IOptions<JwtOptions>>()) },
    DisplayStorageConnectionString = false,
    DashboardTitle = "TaxReader Background Jobs"
});
```

**Recurring jobs registration (D-23)** — insert AFTER the `app.MapGroup("/api/v1")...` block at line 307, BEFORE `await app.RunAsync();` at line 317. From RESEARCH Pattern 9 lines 746-763 — exact cron schedules and idempotency guards per D-23 are planner-decided.

**Migration ordering invariant** (RESEARCH Pitfall 1 lines 882-887) — keep `MigrateAsync` at lines 298-303 BEFORE the Hangfire dashboard middleware is wired.

---

### `Backend/src/TaxReader.Api/Hangfire/HangfireAdminAuthFilter.cs` (NEW)

**Analog (JWT validation logic):** `Backend/src/TaxReader.Api/Program.cs` lines 67-79 (the existing `AddJwtBearer` `TokenValidationParameters` block). No direct analog for `IDashboardAuthorizationFilter` shape — it's a Hangfire-specific contract.

**Full body** — copy verbatim from RESEARCH Pattern 6 lines 528-567.

**Imports the planner must add:**
```csharp
using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Hangfire.Dashboard;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using TaxReader.Infrastructure.Configuration;

namespace TaxReader.Api.Hangfire;
```

**Validation-parameter shape** to mirror exactly from `Program.cs:67-79`:
```csharp
var validationParameters = new TokenValidationParameters
{
    ValidateIssuer = true,
    ValidateAudience = true,
    ValidateLifetime = true,
    ValidateIssuerSigningKey = true,
    ValidIssuer = _jwt.Issuer,
    ValidAudience = _jwt.Audience,
    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.Secret)),
    ClockSkew = TimeSpan.FromSeconds(30)
};
```
**Use the same `Jwt__Secret`** as the API itself — no separate secret.

---

### `Backend/src/TaxReader.Api/Endpoints/ReceiptFileEndpoints.cs` (MODIFY)

**Modification 1: Upload endpoint — change 201 to 202 + drop rate limiter**

Lines 14-50: gut the success-branch logic. The endpoint accepts the form upload, builds the command, calls the handler (which now only persists rows + enqueues jobs), and returns 202:
```csharp
receiptFiles.MapPost("/", async (
    IFormFileCollection files,
    string? sourceHint,
    int? yearHint,
    string? uploadedBy,
    UploadReceiptFilesHandler handler,
    CancellationToken cancellationToken) =>
{
    var fileItems = files.Select(f => new FileUploadItem(f.FileName, f.Length, f.OpenReadStream())).ToList();
    var command = new UploadReceiptFilesCommand(fileItems, sourceHint, yearHint, uploadedBy);
    var result = await handler.HandleAsync(command, cancellationToken);

    if (result.IsFailure)
        return Results.BadRequest(new { error = result.Error });

    // D-03: 202 Accepted with per-file { receiptFileId, jobId, fileName }
    return Results.Accepted(value: result.Value);
})
.DisableAntiforgery()
.WithName("UploadReceiptFiles")
.WithSummary("Accept one or more receipt files for background processing");
```
- **Remove `.RequireRateLimiting("upload-concurrency")`** per CONTEXT line 138 + Phase 2 D-07 sunset note.
- Keep `.DisableAntiforgery()` (multipart upload from SPA still requires this).

**Modification 2: Status endpoint (NEW)** — pattern from `ReceiptFileEndpoints.cs:65-77` (the delete endpoint):
```csharp
receiptFiles.MapGet("/{id:guid}/status", async (
    Guid id,
    GetReceiptFileStatusHandler handler,
    CancellationToken cancellationToken) =>
{
    var result = await handler.HandleAsync(id, cancellationToken);
    return result.IsSuccess
        ? Results.Ok(result.Value)
        : Results.NotFound(new { error = result.Error });
})
.WithName("GetReceiptFileStatus")
.WithSummary("Get current processing status of a receipt file (polled at 2s intervals)");
```

**Modification 3: Cancel endpoint (NEW)** — full body from RESEARCH § Code Examples lines 993-1008.

---

### `Backend/src/TaxReader.Api/Endpoints/AuthEndpoints.cs` (MODIFY)

**Modification — set `tr_access` cookie at login + refresh + logout.** Full pattern from RESEARCH § Code Examples lines 944-985 ("Setting the tr_access cookie in the endpoint layer").

Existing `auth.MapPost("/login", ...)` at lines 37-55 — modify to also accept `IOptions<JwtOptions>` and set the cookie:
```csharp
auth.MapPost("/login", async (
    LoginRequest request,
    IAuthService authService,
    IOptions<JwtOptions> jwtOptions,  // NEW
    HttpContext httpContext,
    CancellationToken cancellationToken) =>
{
    var userAgent = httpContext.Request.Headers.UserAgent.ToString();
    var ipAddress = httpContext.Connection.RemoteIpAddress?.ToString();

    var result = await authService.LoginAsync(request, userAgent, ipAddress, cancellationToken);
    if (result.IsFailure)
        return Results.Unauthorized();

    // D-10: HttpOnly cookie scoped to /hangfire for dashboard browser auth
    httpContext.Response.Cookies.Append("tr_access", result.Value!.AccessToken, new CookieOptions
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Strict,
        Path = "/hangfire",
        Expires = DateTimeOffset.UtcNow.AddMinutes(jwtOptions.Value.AccessTokenExpirationMinutes)
    });
    return Results.Ok(result.Value);
})
.AllowAnonymous()
.RequireRateLimiting("auth-strict")
.WithName("Login");
```
Apply the same cookie-set to `auth.MapPost("/refresh", ...)` at lines 57-75 (it rotates the access token; cookie must be re-set).

**New `/auth/logout` endpoint** — per RESEARCH lines 978-985 (the cookie-clear pattern). Per Phase 3 Deferred Idea #4 (CONTEXT line 227), this endpoint is **in scope for Phase 3**.

---

### `Frontend/src/lib/api-client.ts` (MODIFY)

**Analog (existing endpoint shape):** Lines 167-184 (`getReceiptFiles`, `deleteReceiptFile`, `bulkDeleteReceiptFiles`) — single axios call → typed return.

**New exports** to add (paste below `bulkDeleteReceiptFiles` around line 185):
```typescript
export interface ReceiptFileStatus {
  status: "Pending" | "Queued" | "Extracting" | "Parsing" | "Classifying" | "Completed" | "Failed" | "Cancelled";
  updatedAt: string;        // ISO-8601 UTC
  errorCode?: string;
  errorMessage?: string;
}

export async function getReceiptFileStatus(id: string): Promise<ReceiptFileStatus> {
  const { data } = await api.get<ReceiptFileStatus>(`/receipt-files/${id}/status`);
  return data;
}

export async function cancelReceiptFile(id: string): Promise<void> {
  await api.post(`/receipt-files/${id}/cancel`);
}
```

**Modify `uploadReceiptFiles`** at lines 138-165 — the response shape changes from `UploadReceiptFilesResponse` (per-file success/failure) to the 202 payload `{ files: [{ receiptFileId, jobId, fileName }] }` per D-03. The 400/409 unwrap logic at lines 150-163 is no longer needed — the new flow returns 202 always (per-file outcome comes from polling).

```typescript
export interface UploadAcceptedResponse {
  files: Array<{ receiptFileId: string; jobId: string; fileName: string }>;
}

export async function uploadReceiptFiles(files: File[]): Promise<UploadAcceptedResponse> {
  const formData = new FormData();
  files.forEach((file) => formData.append("files", file));
  const { data } = await api.post<UploadAcceptedResponse>("/receipt-files", formData);
  return data;
}
```

**Bearer/refresh interceptor** at lines 33-73 — **no changes**. The `tr_access` HttpOnly cookie is browser-managed (set by the backend, sent automatically on `/hangfire` requests); the SPA still uses `Authorization: Bearer ...` for `/api/v1` calls via `localStorage`. Two transports, one JWT (per D-10).

---

### `Frontend/src/hooks/use-receipt-files.ts` (MODIFY)

**Analog (polling pattern):** `Frontend/src/hooks/use-receipts.ts` lines 14-20 (`useReceiptById` with `enabled: !!id`).

**Modify `useUploadFiles`** at lines 19-30 — `mutationFn` return type changes from `UploadReceiptFilesResponse` to `UploadAcceptedResponse`; the `onSuccess` invalidation keys remain the same (per-file polling will refresh per-row state via the new `useReceiptFileStatus` hook).

**New `useReceiptFileStatus` hook** — copy the TanStack Query v5 idiom from RESEARCH Pattern 5 lines 500-514. Specifically:
```typescript
import { useQuery } from "@tanstack/react-query";
import { getReceiptFileStatus, type ReceiptFileStatus } from "@/lib/api-client";

const TERMINAL_STATUSES = new Set<ReceiptFileStatus["status"]>(["Completed", "Failed", "Cancelled"]);

export function useReceiptFileStatus(id: string | null) {
  return useQuery({
    queryKey: ["receipt-file-status", id],
    queryFn: () => getReceiptFileStatus(id!),
    enabled: !!id,
    refetchInterval: (query) => {
      const status = query.state.data?.status;
      if (!status) return 2000;
      return TERMINAL_STATUSES.has(status) ? false : 2000;
    },
  });
}
```

**New `useCancelReceiptFile` mutation** — mirror the existing `useDeleteFile` pattern at lines 32-42:
```typescript
export function useCancelReceiptFile() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => cancelReceiptFile(id),
    onSuccess: (_, id) => {
      queryClient.invalidateQueries({ queryKey: ["receipt-file-status", id] });
      queryClient.invalidateQueries({ queryKey: queryKeys.receiptFiles.all });
    },
  });
}
```

**Next.js 16 caveat:** Per `Frontend/AGENTS.md`, this is NOT the Next.js the model knows. Verify TanStack Query v5 (^5.96.2) idioms by reading `node_modules/@tanstack/react-query/build/modern/queryClient.d.ts` and `node_modules/next/dist/docs/` before writing code.

---

### `Frontend/src/components/upload/upload-form.tsx` (MODIFY)

**Analog (per-file card rendering):** Lines 202-294 (`ResultCard` component — `state: "processing" | "done" | "error"` switch).

**Critical change** — the placeholder cards at lines 51-58 currently hold position until `uploadMutation.mutateAsync` resolves with the synchronous-pipeline `UploadReceiptFilesResponse`. Post-Phase-3, `mutateAsync` resolves at 202 (within ~1s) with `{ files: [{ receiptFileId, jobId, fileName }] }`. The per-file cards must then poll `useReceiptFileStatus(receiptFileId)` per file until the status becomes terminal.

**State machine** — extend the existing `FileState` type at line 24:
```typescript
type FileState = "queued" | "extracting" | "parsing" | "classifying" | "done" | "error" | "cancelled";
```
(Mapping from `ReceiptFileStatus["status"]` PascalCase to lowercase `FileState`.)

**Per-card cancel button** — add `<Button variant="ghost" onClick={() => cancelMutation.mutateAsync(result.id)} disabled={isTerminal(state)} aria-label="Abbrechen">` to the `ResultCard` body at lines 239-248 (alongside the existing "Entfernen" trash button). Disabled when `state in TERMINAL_STATUSES`.

**Toast wording** — preserve the existing German patterns at lines 96-115 (`toast.success("X Beleg(e) erfolgreich verarbeitet")`, `toast.error("Verarbeitung fehlgeschlagen")`); add `toast.success("Vorgang abgebrochen")` and `toast.error("Abbruch fehlgeschlagen")` for the cancel flow (RESEARCH § Anti-Patterns — toast strings come from `UploadErrorCatalog` German messages where applicable).

**shadcn primitives** — no new components. Reuse existing `Skeleton`, `Alert` (RESEARCH Pattern 11 lines 821-826). `Frontend/src/components/ui/skeleton.tsx` exists; `Alert` does NOT (verify) — see Shared Patterns below.

---

### `Frontend/src/app/(authenticated)/receipts/page.tsx` (MODIFY), `receipts/[id]/page.tsx` (MODIFY), `dashboard/page.tsx` (MODIFY), `reports/page.tsx` (MODIFY)

**Analog:** `Frontend/src/components/upload/upload-form.tsx` `ResultCard` (per-state rendering with shadcn primitives).

**Pattern reuse** — per RESEARCH Pattern 11 lines 820-835, each page implements the empty / loading / error states using:
- `Skeleton` (exists at `Frontend/src/components/ui/skeleton.tsx`) for loading
- `Alert` + `AlertCircle` from `lucide-react` for terminal-error states — **`alert.tsx` shadcn primitive is NOT in `Frontend/src/components/ui/`** (verified via `ls` — no `alert.tsx`); planner must run `npx shadcn@latest add alert` or compose inline with existing primitives.
- `sonner` toast (`toast.error`, `toast.success`) — already wired
- German `Sie`-form copy per `CONVENTIONS.md`

Per-page state-machine table is in RESEARCH lines 830-835.

## Shared Patterns

### Authentication & per-user data scoping

**Source:** `Backend/src/TaxReader.Application/Commands/DeleteReceiptFileHandler.cs:15-16` and `Backend/src/TaxReader.Application/Queries/GetReceiptByIdHandler.cs:18-19`
**Apply to:** Every new handler (`GetReceiptFileStatusHandler`, `CancelReceiptFileHandler`, and the body of `ProcessReceiptFileJob` when it loads the ReceiptFile)

```csharp
var receiptFile = await dbContext.ReceiptFiles
    .FirstOrDefaultAsync(f => f.Id == command.ReceiptFileId && f.UserId == currentUser.UserId, cancellationToken);

if (receiptFile is null)
    return Result<bool>.Failure($"Receipt file with id '{command.ReceiptFileId}' not found.");
```

**Job-context caveat (CONTEXT lines 200-202):** `ICurrentUser` reads `HttpContext.User.FindFirst("sub")`. Hangfire jobs run OUTSIDE HTTP requests — `ICurrentUser` resolves to a default-`Guid.Empty` value. Jobs receive `userId` via Hangfire-serialized method arguments and filter queries against that parameter, not against `ICurrentUser.UserId`.

### German user-facing strings via `Result<T>.Failure`

**Source:** `Backend/src/TaxReader.Infrastructure/Services/AuthService.cs:34, 95, 101` and `Backend/src/TaxReader.Infrastructure/Services/AiOnlyClassificationService.cs:60`
**Apply to:** All new endpoint responses, `UploadErrorCatalog` mappings, `ReceiptFileStatusDto.ErrorMessage`, frontend toast strings

Examples already in codebase (reuse where they fit):
- `"Ein Konto mit dieser E-Mail existiert bereits."`
- `"Ungültige E-Mail oder Passwort."`
- `"Keine Tokens verfügbar – bitte Credits aufladen."`
- `"Ungültiges Passwort."`

Apply the Sie-form convention (CLAUDE.md / CONVENTIONS.md): formal/polite (`bitte`, `Sie`). RESEARCH Pattern 10 includes the full German catalog for upload errors.

### Structured logging with named placeholders

**Source:** `Backend/src/TaxReader.Infrastructure/Services/TesseractImageTextExtractor.cs:82-86` and `Backend/src/TaxReader.Infrastructure/Services/AiOnlyClassificationService.cs:37, 57, 72`
**Apply to:** Every log call in new code (jobs, pool, hosted services, dashboard filter)

```csharp
logger.LogInformation(
    "OCR done: {Chars} chars in {Ms} ms (input {InW}×{InH}px, processed {OutW}×{OutH}px{Note})",
    normalized.Length, sw.ElapsedMilliseconds, ...);
```
Never use string interpolation in log templates. Always named placeholders.

**LogContext (D-05)** — wrap both job entry points:
```csharp
using var _ = Serilog.Context.LogContext.PushProperty("JobId", receiptFileId);
```
Pattern already in use at `UploadReceiptFilesHandler.cs:111`.

### Primary-constructor DI for handlers / services

**Source:** Every existing handler (e.g. `UploadReceiptFilesHandler.cs:14-22`, `AuthService.cs:16-19`)
**Apply to:** `ProcessReceiptFileJob`, `ClassifyBatchJob`, `CancelReceiptFileHandler`, `GetReceiptFileStatusHandler`, `TesseractEnginePool`, `TesseractEnginePoolWarmupService`, `SeedAdminUsersHostedService`, `HangfireBackgroundJobClient`, `HangfireAdminAuthFilter`

```csharp
public class XxxHandler(
    IAppDbContext dbContext,
    ICurrentUser currentUser,
    ILogger<XxxHandler> logger)
{
    public async Task<Result<T>> HandleAsync(...) { ... }
}
```

### `Result<T>` for handler returns

**Source:** `Backend/src/TaxReader.Domain/Common/Result.cs` (whole file, 26 lines)
**Apply to:** Every new Application command/query handler. Sentinel-string `Result.Failure("NotFound")` / `Result.Failure("TerminalState")` is the established pattern (RESEARCH § Code Examples lines 1023-1028), and the endpoint maps to HTTP status codes via `switch`.

### Endpoint `Result<T>` translation

**Source:** `Backend/src/TaxReader.Api/Endpoints/AuthEndpoints.cs:28-30` and `ReceiptFileEndpoints.cs:30-31, 72-74`
**Apply to:** All new endpoints (status, cancel)

```csharp
return result.IsSuccess
    ? Results.Ok(result.Value)              // or Results.NoContent(), Results.Accepted(...)
    : Results.NotFound(new { error = result.Error });   // or BadRequest / Conflict / Unauthorized
```

### Cancellation: always pass `CancellationToken`

**Source:** Every existing handler signature (`UploadReceiptFilesHandler.cs:38-40`, `AuthService.cs:24-28`)
**Apply to:** Every method in this phase, EXCEPT `MarkFailedAsync`-style "persist-failure-regardless" sites that must use `CancellationToken.None` per the convention at `UploadReceiptFilesHandler.cs:214-218`.

```csharp
public async Task<Result<T>> HandleAsync(..., CancellationToken cancellationToken = default)
```

Hangfire 1.7.0+ supports plain `CancellationToken` (no `IJobCancellationToken` needed) per RESEARCH § State of the Art line 1052.

### IOptions config

**Source:** `Backend/src/TaxReader.Infrastructure/Configuration/TesseractOptions.cs` (whole file)
**Apply to:** Any new options class (e.g. for Hangfire seed emails, if extracted to a strongly-typed POCO). Pattern: `public const string SectionName = "X";` + POCO properties with defaults + binding via `services.Configure<X>(configuration.GetSection(X.SectionName))`.

### EF entity configuration

**Source:** `Backend/src/TaxReader.Infrastructure/Data/Configurations/UserConfiguration.cs` (whole file)
**Apply to:** `User` (add `IsAdmin` mapping), `ProcessingRun` (add `ErrorCode` mapping — see `ProcessingRunConfiguration.cs` for the pattern). `UseSnakeCaseNamingConvention` (DI line 23) handles column-naming automatically.

### Hangfire-free Application layer

**Source:** `Backend/src/TaxReader.Application/Interfaces/IClassificationService.cs` — Application defines the contract; Infrastructure (`AiOnlyClassificationService.cs`) implements.
**Apply to:** `IBackgroundJobClient` (Application) ← `HangfireBackgroundJobClient` (Infrastructure). The Application project must NOT have a `<PackageReference Include="Hangfire..." />` — verify after edits.

### `tr_access` cookie in endpoint layer (not `AuthService`)

**Source:** RESEARCH lines 944-985 + Phase 2 02-01 invariant "AuthService stays HTTP-context-free"
**Apply to:** `AuthEndpoints.cs` login + refresh + logout. NEVER touch `AuthService.cs` for cookie I/O.

## No Analog Found

Files with no close match in the codebase (planner should use RESEARCH.md patterns + external docs):

| File | Role | Data Flow | Reason |
|------|------|-----------|--------|
| `Backend/src/TaxReader.Api/Hangfire/HangfireAdminAuthFilter.cs` | dashboard auth filter | request-in | First `IDashboardAuthorizationFilter` in repo; pattern from RESEARCH Pattern 6 (lines 524-567) + Hangfire docs. JWT-validation logic mirrors `Program.cs:67-79` though. |
| `Backend/src/TaxReader.Application/Jobs/ProcessReceiptFileJob.cs` `[AutomaticRetry]` attribute usage | Hangfire annotation | retry config | Hangfire-specific; pattern from RESEARCH Pattern 3 (lines 426-431). |
| `Backend/src/TaxReader.Infrastructure/Migrations/{ts}_AddQueuedAndCancelledProcessingStatuses.cs` enum-renumber UPDATE (if D-06 strict order is adopted) | data migration | DDL+DML | No existing migration mutates row values; RESEARCH Pitfall 8 lines 873-878 is the only guide. Planner SHOULD adopt the appended-values variant instead to avoid this. |
| `Frontend/src/components/ui/alert.tsx` shadcn primitive | UI primitive | UI | Not present in `Frontend/src/components/ui/` (verified). Planner must `npx shadcn@latest add alert` or compose inline. |

## Metadata

**Analog search scope:**
- Backend: `Backend/src/TaxReader.{Domain,Application,Infrastructure,Api}/` (recursive)
- Frontend: `Frontend/src/{hooks,lib,components,app}/` (recursive)
- Skipped: `Backend/tests/`, `Backend/src/**/obj/`, `Backend/src/**/bin/`, `Frontend/.next/`, `Frontend/node_modules/`

**Files scanned (key analogs read in full):**
- `Backend/src/TaxReader.Application/Commands/UploadReceiptFilesHandler.cs` (234 lines)
- `Backend/src/TaxReader.Application/Commands/DeleteReceiptFileHandler.cs` (26 lines)
- `Backend/src/TaxReader.Application/Queries/GetReceiptByIdHandler.cs` (26 lines)
- `Backend/src/TaxReader.Application/Queries/GetReceiptFilesHandler.cs` (23 lines)
- `Backend/src/TaxReader.Infrastructure/Services/TesseractImageTextExtractor.cs` (137 lines)
- `Backend/src/TaxReader.Infrastructure/Services/AiOnlyClassificationService.cs` (137 lines)
- `Backend/src/TaxReader.Infrastructure/Services/AuthService.cs` (161 lines)
- `Backend/src/TaxReader.Infrastructure/DependencyInjection.cs` (73 lines)
- `Backend/src/TaxReader.Api/Program.cs` (327 lines)
- `Backend/src/TaxReader.Api/Endpoints/ReceiptFileEndpoints.cs` (99 lines)
- `Backend/src/TaxReader.Api/Endpoints/AuthEndpoints.cs` (132 lines)
- `Backend/src/TaxReader.Domain/Common/Result.cs` (26 lines)
- `Backend/src/TaxReader.Domain/Entities/User.cs`, `ProcessingRun.cs` (~17 lines each)
- `Backend/src/TaxReader.Domain/Enums/ProcessingStatus.cs` (11 lines)
- `Backend/src/TaxReader.Infrastructure/Configuration/TesseractOptions.cs` (19 lines)
- `Backend/src/TaxReader.Infrastructure/Data/Configurations/UserConfiguration.cs` (37 lines)
- `Backend/src/TaxReader.Infrastructure/Migrations/20260514204609_AddRefreshTokensTable_DropLegacyRefreshTokenColumns.cs` (102 lines)
- `Frontend/src/hooks/use-receipts.ts` (21 lines)
- `Frontend/src/hooks/use-receipt-files.ts` (54 lines)
- `Frontend/src/lib/api-client.ts` (299 lines)
- `Frontend/src/components/upload/upload-form.tsx` (294 lines)

**Pattern extraction date:** 2026-05-19
