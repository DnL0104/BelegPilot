# Phase 06: Legal + Consent + Data Export + AVVs — Pattern Map

**Mapped:** 2026-06-02
**Files analyzed:** 27 (new + modified)
**Analogs found:** 25 / 27

---

## File Classification

| New / Modified File | Role | Data Flow | Closest Analog | Match Quality |
|---------------------|------|-----------|----------------|---------------|
| `Backend/src/TaxReader.Domain/Entities/AuditLogEntry.cs` | model | CRUD | `Backend/src/TaxReader.Domain/Entities/TokenTransaction.cs` | exact |
| `Backend/src/TaxReader.Domain/Enums/AuditAction.cs` | model | — | `Backend/src/TaxReader.Domain/Enums/TokenTransactionType.cs` | exact |
| `Backend/src/TaxReader.Application/Interfaces/IAuditLogger.cs` | service interface | request-response | `Backend/src/TaxReader.Application/Interfaces/IClassificationService.cs` | exact |
| `Backend/src/TaxReader.Infrastructure/Services/AuditLogger.cs` | service | CRUD | `Backend/src/TaxReader.Application/Jobs/GrantTokensJob.cs` (DbSet.Add + SaveChangesAsync) | role-match |
| `Backend/src/TaxReader.Infrastructure/Data/Configurations/AuditLogEntryConfiguration.cs` | config | — | `Backend/src/TaxReader.Infrastructure/Data/Configurations/TokenTransactionConfiguration.cs` | exact |
| `Backend/src/TaxReader.Infrastructure/Migrations/YYYYMMDD_AddAuditLog.cs` | migration | — | `Backend/src/TaxReader.Infrastructure/Migrations/20260528160059_AddPaymentsTableAndStripeCustomerId.cs` | exact |
| `Backend/src/TaxReader.Application/Interfaces/IAppDbContext.cs` (MODIFIED) | service interface | — | self (add one line) | — |
| `Backend/src/TaxReader.Infrastructure/Data/AppDbContext.cs` (MODIFIED) | config | — | self (add one line) | — |
| `Backend/src/TaxReader.Application/Jobs/ExportUserDataJob.cs` | job | batch | `Backend/src/TaxReader.Application/Jobs/GrantTokensJob.cs` | exact |
| `Backend/src/TaxReader.Application/Jobs/ExportCleanupJob.cs` | job | batch | `Backend/src/TaxReader.Application/Jobs/RefreshTokenCleanupJob.cs` | exact |
| `Backend/src/TaxReader.Api/Endpoints/ExportEndpoints.cs` | controller | request-response | `Backend/src/TaxReader.Api/Endpoints/SettingsEndpoints.cs` | role-match |
| `Backend/src/TaxReader.Api/Hangfire/RecurringJobsBootstrap.cs` (MODIFIED) | config | — | self (add one entry) | — |
| `Backend/src/TaxReader.Infrastructure/DependencyInjection.cs` (MODIFIED) | config | — | self (add two registrations) | — |
| `Backend/src/TaxReader.Application/Commands/DeleteAccountHandler.cs` (MODIFIED) | handler | request-response | self | — |
| `Backend/src/TaxReader.Application/Jobs/GrantTokensJob.cs` (MODIFIED) | job | batch | self | — |
| `Backend/src/TaxReader.Application/Jobs/RevokeTokensJob.cs` (MODIFIED) | job | batch | self | — |
| `Backend/src/TaxReader.Infrastructure/Services/RefreshTokenService.cs` (MODIFIED) | service | request-response | self | — |
| `Backend/src/TaxReader.Application/Commands/SaveClassificationRuleHandler.cs` (MODIFIED) | handler | request-response | self | — |
| `Frontend/src/app/(legal)/agb/page.tsx` | component | — | `Frontend/src/app/(legal)/impressum/page.tsx` | exact |
| `Frontend/src/app/(legal)/widerruf/page.tsx` | component | — | `Frontend/src/app/(legal)/impressum/page.tsx` | exact |
| `Frontend/src/app/(legal)/impressum/page.tsx` (MODIFIED) | component | — | self | — |
| `Frontend/src/app/(legal)/datenschutz/page.tsx` (MODIFIED) | component | — | self | — |
| `Frontend/src/app/(legal)/layout.tsx` (MODIFIED) | component | — | self | — |
| `Frontend/src/components/layout/footer.tsx` | component | — | `Frontend/src/components/layout/header.tsx` | partial-match |
| `Frontend/src/providers/consent-provider.tsx` | provider | event-driven | `Frontend/src/providers/auth-provider.tsx` | exact |
| `Frontend/src/components/consent/cookie-banner.tsx` | component | event-driven | `Frontend/src/app/(authenticated)/settings/page.tsx` (Dialog pattern) | role-match |
| `Frontend/src/components/consent/consent-settings-dialog.tsx` | component | event-driven | `Frontend/src/app/(authenticated)/settings/page.tsx` (Dialog + shadcn) | role-match |
| `Frontend/instrumentation-client.ts` (MODIFIED) | utility | event-driven | self | — |
| `Frontend/src/app/(authenticated)/settings/page.tsx` (MODIFIED) | component | request-response | self | — |
| `Frontend/src/app/(authenticated)/layout.tsx` (MODIFIED) | component | — | self | — |
| `Frontend/src/app/layout.tsx` (MODIFIED) | component | — | self | — |
| `Frontend/src/providers/auth-provider.tsx` (MODIFIED — PUBLIC_PATHS) | provider | — | self | — |
| `Frontend/src/lib/api-client.ts` (MODIFIED) | utility | request-response | self (export trigger + status + download functions) | — |

---

## Pattern Assignments

### `Backend/src/TaxReader.Domain/Entities/AuditLogEntry.cs` (model)

**Analog:** `Backend/src/TaxReader.Domain/Entities/TokenTransaction.cs`

**Imports + namespace pattern** (lines 1–3):
```csharp
using TaxReader.Domain.Enums;

namespace TaxReader.Domain.Entities;
```

**Core entity shape** (full file — 19 lines):
```csharp
public class TokenTransaction
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    // ...typed properties...
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public User User { get; set; } = null!;  // <-- navigation property
}
```

**Deviation for AuditLogEntry:** Do NOT add a navigation property to `User` — audit rows must survive user deletion, so there is intentionally no nav prop. Both `ActorUserId` and `SubjectUserId` are nullable Guids with no EF-tracked relationship. `Metadata` is `Dictionary<string, object?> = []` (collection expression, .NET 10 style).

---

### `Backend/src/TaxReader.Domain/Enums/AuditAction.cs` (enum)

**Analog:** `Backend/src/TaxReader.Domain/Enums/TokenTransactionType.cs`

**Full file pattern** (lines 1–9):
```csharp
namespace TaxReader.Domain.Enums;

public enum TokenTransactionType
{
    Purchase = 0,
    Consumption = 1,
    Refund = 2,
    Adjustment = 3
}
```

Copy verbatim structure. New enum values (from RESEARCH.md):
`AccountDeleted`, `TokensGranted`, `TokensRevoked`, `RefreshTokenReplayDetected`, `ClassificationRuleCreated`, `DataExportRequested`, `DataExportDownloaded`.

---

### `Backend/src/TaxReader.Application/Interfaces/IAuditLogger.cs` (service interface)

**Analog:** `Backend/src/TaxReader.Application/Interfaces/IClassificationService.cs`

**Full file pattern** (lines 1–16):
```csharp
using TaxReader.Domain.Entities;

namespace TaxReader.Application.Interfaces;

public interface IClassificationService
{
    /// <summary>
    /// Classifies a batch ... caller MUST pass the owning userId explicitly
    /// (ICurrentUser returns Guid.Empty inside a job).
    /// </summary>
    Task<IReadOnlyList<ItemClassification>> ClassifyItemsAsync(
        IEnumerable<ReceiptItem> items,
        Guid userId,
        CancellationToken cancellationToken = default);
}
```

New interface: single method `RecordAsync(AuditAction action, Guid? actorUserId, Guid? subjectUserId, Dictionary<string, object?> metadata, CancellationToken cancellationToken = default)` returning `Task`. Import `TaxReader.Domain.Enums` instead of `TaxReader.Domain.Entities`.

---

### `Backend/src/TaxReader.Infrastructure/Data/Configurations/AuditLogEntryConfiguration.cs` (EF config)

**Analog:** `Backend/src/TaxReader.Infrastructure/Data/Configurations/TokenTransactionConfiguration.cs`

**Full file** (lines 1–31):
```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaxReader.Domain.Entities;

namespace TaxReader.Infrastructure.Data.Configurations;

public class TokenTransactionConfiguration : IEntityTypeConfiguration<TokenTransaction>
{
    public void Configure(EntityTypeBuilder<TokenTransaction> builder)
    {
        builder.ToTable("token_transactions");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasDefaultValueSql("gen_random_uuid()");

        builder.Property(e => e.UserKey).IsRequired().HasMaxLength(100);
        builder.Property(e => e.Type).HasConversion<string>().IsRequired().HasMaxLength(50);
        builder.Property(e => e.Amount).IsRequired();
        builder.Property(e => e.BalanceAfter).IsRequired();
        builder.Property(e => e.Description).IsRequired().HasMaxLength(500);
        builder.Property(e => e.CreatedAt).IsRequired();

        builder.HasIndex(e => new { e.UserId, e.CreatedAt })
            .IsDescending(false, true);

        builder.HasOne(e => e.User)
            .WithMany(u => u.TokenTransactions)
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
```

**Deviation for AuditLogEntry:**
- `builder.ToTable("audit_log")`
- `builder.Property(e => e.Metadata).HasColumnType("jsonb").IsRequired()` — Npgsql maps `Dictionary<string, object?>` to PostgreSQL jsonb when `HasColumnType("jsonb")` is set
- `builder.HasIndex(e => e.SubjectUserId)` and `builder.HasIndex(e => e.CreatedAt)` for query performance
- Do NOT configure `HasOne`/`WithMany` for either `ActorUserId` or `SubjectUserId` — no FK constraint, no cascade, no nav prop
- Also see PaymentConfiguration (lines 1–22) for the simpler no-nav-prop pattern: `builder.HasOne(e => e.User).WithMany().HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.Cascade)` — audit log does NOT use this, but the file shows how PaymentConfiguration omits `WithMany(u => u.Payments)` when no reverse nav is needed

---

### `Backend/src/TaxReader.Infrastructure/Migrations/YYYYMMDD_AddAuditLog.cs` (EF migration)

**Analog:** `Backend/src/TaxReader.Infrastructure/Migrations/20260528160059_AddPaymentsTableAndStripeCustomerId.cs`

**Structure pattern** (lines 1–70):
```csharp
using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaxReader.Infrastructure.Migrations
{
    public partial class AddPaymentsTableAndStripeCustomerId : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "payments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    // ...
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_payments", x => x.id);
                    table.ForeignKey(...);
                });

            migrationBuilder.CreateIndex(
                name: "ix_payments_stripe_event_id",
                table: "payments",
                column: "stripe_event_id",
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "payments");
        }
    }
}
```

**Key column types for audit_log:**
- `id`: `type: "uuid"`, `defaultValueSql: "gen_random_uuid()"`
- `action`: `type: "character varying(100)"` (stored as string)
- `actor_user_id`: `type: "uuid"`, `nullable: true`
- `subject_user_id`: `type: "uuid"`, `nullable: true`
- `metadata`: `type: "jsonb"`, `nullable: false`
- `created_at`: `type: "timestamp with time zone"`, `nullable: false`

**No FK constraint** on `actor_user_id` or `subject_user_id` in the migration Up — no `table.ForeignKey(...)` for these columns. This is the append-only enforcement mechanism.

**Do not hand-write this migration** — generate it with:
```
dotnet ef migrations add AddAuditLog -p Backend/src/TaxReader.Infrastructure -s Backend/src/TaxReader.Api
```

---

### `Backend/src/TaxReader.Application/Interfaces/IAppDbContext.cs` (MODIFIED — add one line)

**Existing shape** (lines 1–21):
```csharp
using Microsoft.EntityFrameworkCore;
using TaxReader.Domain.Entities;

namespace TaxReader.Application.Interfaces;

public interface IAppDbContext
{
    DbSet<User> Users { get; }
    // ...
    DbSet<Payment> Payments { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
```

**Insert after line 17** (before `Payment`):
```csharp
    DbSet<AuditLogEntry> AuditLogEntries { get; }
```

---

### `Backend/src/TaxReader.Infrastructure/Data/AppDbContext.cs` (MODIFIED — add one line)

**Existing shape** (lines 1–27):
```csharp
public class AppDbContext(DbContextOptions<AppDbContext> options)
    : DbContext(options), IAppDbContext
{
    public DbSet<User> Users => Set<User>();
    // ...
    public DbSet<Payment> Payments => Set<Payment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
```

**Insert after `Payments` line:**
```csharp
    public DbSet<AuditLogEntry> AuditLogEntries => Set<AuditLogEntry>();
```

`ApplyConfigurationsFromAssembly` (line 25) auto-discovers `AuditLogEntryConfiguration` — no further change needed in `OnModelCreating`.

---

### `Backend/src/TaxReader.Infrastructure/Services/AuditLogger.cs` (service, CRUD)

**Analog:** `Backend/src/TaxReader.Application/Jobs/GrantTokensJob.cs` — specifically the `dbContext.TokenTransactions.Add(new TokenTransaction {...})` + `SaveChangesAsync` idiom (lines 39–62).

**Primary constructor + DbSet.Add pattern** (lines 11–65):
```csharp
public class GrantTokensJob(IAppDbContext dbContext, ILogger<GrantTokensJob> logger)
{
    [AutomaticRetry(Attempts = 3, DelaysInSeconds = new[] { 30, 120, 300 })]
    public async Task HandleAsync(Guid userId, int credits, CancellationToken cancellationToken)
    {
        using var _scope = LogContext.PushProperty("JobId", $"Grant_{userId}_{credits}");

        // ... query ...

        dbContext.TokenTransactions.Add(new TokenTransaction
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            // ...
            CreatedAt = DateTime.UtcNow
        });

        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Granted {Credits} tokens to User {UserId} — new balance: {Balance}",
            credits, userId, balance.Balance);
    }
}
```

**AuditLogger deviations:** No `[AutomaticRetry]` (not a Hangfire job), no `LogContext.PushProperty`. Namespace is `TaxReader.Infrastructure.Services`. Primary constructor: `(IAppDbContext dbContext)` only — no logger needed (the five call-site callers own their own loggers). Single `RecordAsync` method adds to `dbContext.AuditLogEntries` then calls `SaveChangesAsync`.

---

### `Backend/src/TaxReader.Application/Jobs/ExportUserDataJob.cs` (job, batch)

**Analog:** `Backend/src/TaxReader.Application/Jobs/GrantTokensJob.cs` (lines 1–65) — exact match for fire-and-forget Hangfire pattern with `[AutomaticRetry]`, `LogContext.PushProperty`, primary constructor DI, direct `IAppDbContext` access (no `ICurrentUser`).

**Imports pattern** (lines 1–8):
```csharp
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Serilog.Context;
using TaxReader.Application.Interfaces;
using TaxReader.Domain.Entities;
using TaxReader.Domain.Enums;
```

Add `using System.IO.Compression;` and `using System.Text.Json;`.

**Class skeleton to copy:**
```csharp
public class GrantTokensJob(IAppDbContext dbContext, ILogger<GrantTokensJob> logger)
{
    [AutomaticRetry(Attempts = 3, DelaysInSeconds = new[] { 30, 120, 300 })]
    public async Task HandleAsync(Guid userId, int credits, CancellationToken cancellationToken)
    {
        using var _scope = LogContext.PushProperty("JobId", $"Grant_{userId}_{credits}");
        // ... query dbContext, mutate, SaveChangesAsync ...
    }
}
```

**ExportUserDataJob signature:** `HandleAsync(Guid userId, string exportToken, CancellationToken cancellationToken)`. Inject `IAppDbContext`, `ILogger<ExportUserDataJob>`, and `ExportTokenStore` (Singleton). Do NOT inject `ICurrentUser` — same constraint as `GrantTokensJob` (documented in lines 18–19 of that file: "ITokenService depends on ICurrentUser (HTTP context) — not injectable in Hangfire jobs").

**ZipArchive pattern** (BCL, no package):
```csharp
var exportsDir = Path.Combine(Path.GetTempPath(), "taxreader-exports");
Directory.CreateDirectory(exportsDir);
var zipPath = Path.Combine(exportsDir, exportToken + ".zip");

using var zipStream = new FileStream(zipPath, FileMode.Create);
using var archive = new ZipArchive(zipStream, ZipArchiveMode.Create, leaveOpen: false);

var entry = archive.CreateEntry("receipts.json", CompressionLevel.Optimal);
using var writer = new StreamWriter(entry.Open());
await JsonSerializer.SerializeAsync(writer.BaseStream, data, cancellationToken: cancellationToken);
```

---

### `Backend/src/TaxReader.Application/Jobs/ExportCleanupJob.cs` (job, batch)

**Analog:** `Backend/src/TaxReader.Application/Jobs/RefreshTokenCleanupJob.cs` (lines 1–42) — recurring daily cleanup, `[DisableConcurrentExecution]`, `[AutomaticRetry(Attempts = 0)]`, `HandleAsync(CancellationToken)` signature, logger only constructor.

**Full analog** (lines 1–42):
```csharp
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TaxReader.Application.Interfaces;

namespace TaxReader.Application.Jobs;

/// <summary>
/// D-23 #1: removes refresh_tokens rows whose ExpiresAt is older than now - 7 days.
/// ...
/// </summary>
[DisableConcurrentExecution(timeoutInSeconds: 600)]
[AutomaticRetry(Attempts = 0)]
public class RefreshTokenCleanupJob(IAppDbContext dbContext, ILogger<RefreshTokenCleanupJob> logger)
{
    public async Task HandleAsync(CancellationToken cancellationToken)
    {
        var cutoff = DateTime.UtcNow.AddDays(-7);
        // ... query, RemoveRange, SaveChangesAsync ...
        logger.LogInformation(
            "Refresh-token cleanup deleted {Count} expired rows (cutoff {Cutoff:o})",
            expired.Count, cutoff);
    }
}
```

**ExportCleanupJob deviations:** No `IAppDbContext` needed (no DB rows to delete — files only). Constructor: `(ILogger<ExportCleanupJob> logger)` only. Body: `Directory.GetFiles(exportsDir, "*.zip")` → `File.GetCreationTimeUtc(file) < cutoff` → `File.Delete(file)`. Cutoff: `DateTime.UtcNow.AddHours(-24)`.

---

### `Backend/src/TaxReader.Api/Endpoints/ExportEndpoints.cs` (controller, request-response)

**Analog:** `Backend/src/TaxReader.Api/Endpoints/SettingsEndpoints.cs` (lines 1–45) — `MapGroup` sub-group, `Result<T>` translation, authorized by default, scoped handler injection.

**MapGroup + handler pattern** (lines 7–43):
```csharp
public static class SettingsEndpoints
{
    public static RouteGroupBuilder MapSettingsEndpoints(this RouteGroupBuilder group)
    {
        var settings = group.MapGroup("/settings").WithTags("Settings");

        settings.MapGet("/", async (
            GetUserSettingsHandler handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(cancellationToken);
            return result.IsSuccess
                ? Results.Ok(result.Value)
                : Results.NotFound(new { error = result.Error });
        })
        .WithName("GetUserSettings")
        .WithSummary("Get the current user's settings");

        return group;
    }
}
```

**ExportEndpoints structure:**
- `group.MapGroup("/export").WithTags("Export")`
- `POST /export/request` — injects `ICurrentUser`, `IBackgroundJobClient`, `ExportTokenStore`; enqueues `ExportUserDataJob`; returns `{ exportId: token }`
- `GET /export/status` — injects `ICurrentUser`, `ExportTokenStore`; returns `{ status: "Generating" | "Ready" | "Expired" }`
- `GET /export/download` — injects `ICurrentUser`, `ExportTokenStore`; validates token ownership (403 on mismatch); streams zip; invalidates token

**Ownership validation pattern** (from PaymentEndpoints.cs lines 51–54):
```csharp
var user = await dbContext.Users
    .FirstOrDefaultAsync(u => u.Id == currentUser.UserId, cancellationToken);
```
For export: `ExportTokenStore.TryGet(token, out var record)` where `record.UserId` is compared to `currentUser.UserId`. 403 on mismatch.

---

### `Backend/src/TaxReader.Api/Hangfire/RecurringJobsBootstrap.cs` (MODIFIED — add one entry)

**Existing shape** (lines 1–34 — full file):
```csharp
public static class RecurringJobsBootstrap
{
    public static void Register(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<IRecurringJobManager>();

        manager.AddOrUpdate<RefreshTokenCleanupJob>(
            recurringJobId: "refresh-tokens-cleanup",
            methodCall: job => job.HandleAsync(CancellationToken.None),
            cronExpression: "0 3 * * *",
            options: new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });

        manager.AddOrUpdate<HangfireFailedJobCleanupJob>(
            recurringJobId: "hangfire-failed-cleanup",
            methodCall: job => job.HandleAsync(CancellationToken.None),
            cronExpression: "0 4 * * 0",
            options: new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });
    }
}
```

**Add after the two existing entries:**
```csharp
        manager.AddOrUpdate<ExportCleanupJob>(
            recurringJobId: "export-cleanup",
            methodCall: job => job.HandleAsync(CancellationToken.None),
            cronExpression: "0 2 * * *",
            options: new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });
```

---

### Audit Call-Site Modifications (D-13 — five files)

All five follow the same primary-constructor injection pattern. Add `IAuditLogger auditLogger` as the last constructor parameter, then insert `await auditLogger.RecordAsync(...)` at the described location.

#### `Backend/src/TaxReader.Application/Commands/DeleteAccountHandler.cs` (MODIFIED)

**Current constructor** (lines 8–11):
```csharp
public class DeleteAccountHandler(
    IAppDbContext dbContext,
    ICurrentUser currentUser,
    IRefreshTokenService refreshTokenService)
```

**Add parameter:**
```csharp
public class DeleteAccountHandler(
    IAppDbContext dbContext,
    ICurrentUser currentUser,
    IRefreshTokenService refreshTokenService,
    IAuditLogger auditLogger)
```

**Insert location:** After `RevokeAllForUserAsync` call (line 35), before `dbContext.Users.Remove(user)` (line 42). The audit entry is recorded while the user row still exists:
```csharp
await refreshTokenService.RevokeAllForUserAsync(userId, cancellationToken);

// Phase 6 LEG-08: record before cascade delete fires
await auditLogger.RecordAsync(
    AuditAction.AccountDeleted,
    actorUserId: userId,
    subjectUserId: userId,
    metadata: new Dictionary<string, object?> { ["email_hash"] = HashEmail(user.Email) },
    cancellationToken);

dbContext.Users.Remove(user);
```

#### `Backend/src/TaxReader.Application/Jobs/GrantTokensJob.cs` (MODIFIED)

**Current constructor** (line 11):
```csharp
public class GrantTokensJob(IAppDbContext dbContext, ILogger<GrantTokensJob> logger)
```

**Add parameter:**
```csharp
public class GrantTokensJob(IAppDbContext dbContext, ILogger<GrantTokensJob> logger, IAuditLogger auditLogger)
```

**Insert location:** After `SaveChangesAsync` call (line 61), before `logger.LogInformation` (line 62):
```csharp
await dbContext.SaveChangesAsync(cancellationToken);
await auditLogger.RecordAsync(
    AuditAction.TokensGranted,
    actorUserId: null,
    subjectUserId: userId,
    metadata: new Dictionary<string, object?> { ["credits"] = credits },
    cancellationToken);
logger.LogInformation("Granted {Credits} tokens to User {UserId} ...", ...);
```

#### `Backend/src/TaxReader.Application/Jobs/RevokeTokensJob.cs` (MODIFIED)

**Same pattern as GrantTokensJob.** Insert after `SaveChangesAsync` (line 64):
```csharp
await auditLogger.RecordAsync(
    AuditAction.TokensRevoked,
    actorUserId: null,
    subjectUserId: userId,
    metadata: new Dictionary<string, object?> { ["credits"] = credits },
    cancellationToken);
```

#### `Backend/src/TaxReader.Infrastructure/Services/RefreshTokenService.cs` (MODIFIED)

**Current constructor** (lines 23–27):
```csharp
public class RefreshTokenService(
    IAppDbContext dbContext,
    IOptions<RefreshTokenOptions> refreshTokenOptions,
    IOptions<JwtOptions> jwtOptions,
    ILogger<RefreshTokenService> logger) : IRefreshTokenService
```

**Add parameter:**
```csharp
public class RefreshTokenService(
    IAppDbContext dbContext,
    IOptions<RefreshTokenOptions> refreshTokenOptions,
    IOptions<JwtOptions> jwtOptions,
    ILogger<RefreshTokenService> logger,
    IAuditLogger auditLogger) : IRefreshTokenService
```

**Insert location:** In the replay-detection block (lines 88–98), after `SentrySdk.CaptureMessage` (line 94), before `RevokeAllForUserAsync`:
```csharp
SentrySdk.CaptureMessage("Refresh token replay detected", ..., SentryLevel.Warning);

await auditLogger.RecordAsync(
    AuditAction.RefreshTokenReplayDetected,
    actorUserId: existing.UserId,
    subjectUserId: existing.UserId,
    metadata: new Dictionary<string, object?> { ["token_id_hash"] = HashUserId(existing.Id) },
    cancellationToken);

await RevokeAllForUserAsync(existing.UserId, cancellationToken);
```

Note: `IAuditLogger` is in the Application layer; `RefreshTokenService` is in Infrastructure. Infrastructure referencing Application is layering-compliant (Infrastructure implements Application interfaces).

#### `Backend/src/TaxReader.Application/Commands/SaveClassificationRuleHandler.cs` (MODIFIED)

**Current constructor** (line 18):
```csharp
public class SaveClassificationRuleHandler(IAppDbContext dbContext, ICurrentUser currentUser)
```

**Add parameter:**
```csharp
public class SaveClassificationRuleHandler(IAppDbContext dbContext, ICurrentUser currentUser, IAuditLogger auditLogger)
```

**Insert location:** After `SaveChangesAsync` (line 61), before the return statement (line 63):
```csharp
await dbContext.SaveChangesAsync(cancellationToken);
await auditLogger.RecordAsync(
    AuditAction.ClassificationRuleCreated,
    actorUserId: currentUser.UserId,
    subjectUserId: currentUser.UserId,
    metadata: new Dictionary<string, object?>
    {
        ["rule_id"] = rule.Id,
        ["category"] = command.Category.ToString()
    },
    cancellationToken);

return Result<ClassificationRuleDto>.Success(rule.ToDto());
```

---

### `Frontend/src/app/(legal)/agb/page.tsx` (component, new legal page)

**Analog:** `Frontend/src/app/(legal)/impressum/page.tsx` (lines 1–97) — exact match for Server Component legal page structure.

**Page structure to copy:**
```tsx
import Link from "next/link";

export const metadata = {
  title: "Impressum – BelegPilot",
};

export default function ImpressumPage() {
  return (
    <div className="mx-auto max-w-2xl space-y-8 px-6 py-10 text-sm leading-relaxed">
      <h1 className="text-2xl font-bold tracking-tight">Impressum</h1>

      <section>
        <h2 className="mb-2 text-base font-semibold">...</h2>
        <p className="text-muted-foreground">...</p>
      </section>

      <div className="border-t pt-6 text-xs text-muted-foreground">
        <Link href="/datenschutz" className="hover:underline">Datenschutzerklärung</Link>
      </div>
    </div>
  );
}
```

**Draft marker component** (apply to all four legal pages, consistent placement after `<h1>`):
```tsx
function DraftWarning() {
  return (
    <div className="rounded border border-yellow-400 bg-yellow-50 dark:bg-yellow-500/10 px-4 py-2 text-sm text-yellow-800 dark:text-yellow-200">
      ⚠ Entwurf – anwaltliche Prüfung ausstehend
    </div>
  );
}
```

Color pattern follows existing Tailwind dark-mode pattern from settings page (lines 97–98): `bg-emerald-100 ... dark:bg-emerald-950/50`.

---

### `Frontend/src/app/(legal)/widerruf/page.tsx` (component, new legal page)

Same analog and structure as `agb/page.tsx` above. See impressum analog. Metadata title: `"Widerrufsbelehrung – TaxReader"`.

---

### `Frontend/src/app/(legal)/layout.tsx` (MODIFIED)

**Current shape** (lines 1–24 — full file):
```tsx
import Link from "next/link";
import { ThemeToggle } from "@/components/layout/theme-toggle";

export default function LegalLayout({ children }: { children: React.ReactNode }) {
  return (
    <div className="min-h-screen bg-background">
      <header className="flex h-14 shrink-0 items-center justify-between border-b bg-card px-6">
        <Link href="/" className="flex items-center gap-2.5">
          <div className="flex h-8 w-8 items-center justify-center rounded-lg bg-emerald-600 text-sm font-bold text-white">
            B
          </div>
          <span className="text-lg font-bold tracking-tight">BelegPilot</span>  {/* RENAME to TaxReader */}
        </Link>
        <ThemeToggle />
      </header>
      <main>{children}</main>  {/* ADD <Footer /> after this */}
    </div>
  );
}
```

**Two changes:**
1. `<span>BelegPilot</span>` → `<span>TaxReader</span>` (and `"B"` avatar → `"T"`)
2. Add `<Footer />` import from `@/components/layout/footer` and mount after `</main>`

---

### `Frontend/src/components/layout/footer.tsx` (component, new)

**Analog:** `Frontend/src/components/layout/header.tsx` (lines 1–26) — closest existing layout component, same Tailwind structural patterns, same import conventions.

**Header structural pattern** (lines 12–26):
```tsx
export function Header({ title }: HeaderProps) {
  return (
    <header className="flex h-14 shrink-0 items-center justify-between border-b bg-card px-6">
      <div className="flex items-center gap-2">
        ...
      </div>
      <div className="flex items-center gap-3">
        ...
      </div>
    </header>
  );
}
```

Footer is a Server Component (no `"use client"`). Uses `Link` from `next/link`. Nav items: `/impressum`, `/datenschutz`, `/agb`, `/widerruf`, plus a "Cookie-Einstellungen" button/link. The Cookie-Einstellungen link must trigger the consent settings panel — since Footer is a Server Component and the panel state lives in `ConsentProvider` (client), the simplest approach is a `<FooterCookieLink />` child client component that calls `useConsent().openSettings()`.

---

### `Frontend/src/providers/consent-provider.tsx` (provider, event-driven)

**Analog:** `Frontend/src/providers/auth-provider.tsx` (lines 1–108) — exact match for `"use client"` context + localStorage + `createContext` + typed `ContextValue` interface + exported hook.

**Full provider pattern** (lines 1–108):
```tsx
"use client";

import { createContext, useCallback, useContext, useEffect, useState } from "react";
// ...

interface AuthContextValue { ... }

const AuthContext = createContext<AuthContextValue | null>(null);

export function AuthProvider({ children }: { children: React.ReactNode }) {
  const [user, setUser] = useState<AuthUser | null>(null);
  const [isLoading, setIsLoading] = useState(true);

  // On mount, restore session from localStorage
  useEffect(() => {
    const stored = localStorage.getItem("user");
    // ...
  }, []);

  return (
    <AuthContext value={{ user, isLoading, ... }}>
      {children}
    </AuthContext>
  );
}

export function useAuth() {
  const context = useContext(AuthContext);
  if (!context) throw new Error("useAuth must be used within AuthProvider");
  return context;
}
```

**ConsentProvider deviations:**
- Key: `"taxreader-consent"` instead of `"user"`
- State shape: `{ notwendig: true, fehleranalyse: boolean, decided: boolean }` — no `isLoading` (localStorage read is synchronous on mount)
- Additional state: `settingsPanelOpen: boolean`
- Extra methods: `acceptAll()`, `acceptNecessary()`, `updateConsent(partial)`, `openSettings()`, `closeSettings()`
- Sentry init/close called inside `acceptAll()` / `acceptNecessary()` (see Pattern 5 in RESEARCH.md for exact `Sentry.isInitialized()` guard)

---

### `Frontend/src/components/consent/cookie-banner.tsx` (component, event-driven)

**Analog:** Dialog + Button pattern from `Frontend/src/app/(authenticated)/settings/page.tsx` (lines 212–277) — Dialog with equal-prominence action buttons.

**Dialog structure to copy** (lines 212–277):
```tsx
<Dialog open={deleteDialogOpen} onOpenChange={(open) => { ... }}>
  <DialogContent className="sm:max-w-md">
    <DialogHeader>
      <DialogTitle>...</DialogTitle>
      <DialogDescription>...</DialogDescription>
    </DialogHeader>
    <div className="space-y-3 py-2">
      ...
    </div>
    <DialogFooter>
      <Button variant="outline" onClick={...}>Abbrechen</Button>
      <Button variant="destructive" onClick={...}>Konto löschen</Button>
    </DialogFooter>
  </DialogContent>
</Dialog>
```

**Cookie banner deviations:**
- Not a Dialog — rendered as a fixed bottom bar or card (no `DialogContent`)
- Three buttons with **equal visual prominence** (TTDSG): "Alle akzeptieren", "Nur notwendige", "Einstellungen" — all `variant="outline"` or differentiated only by label, not size/color hierarchy
- Visibility driven by `useConsent().consent.decided === false`
- `"use client"` directive required

---

### `Frontend/src/components/consent/consent-settings-dialog.tsx` (component, event-driven)

**Analog:** `Frontend/src/app/(authenticated)/settings/page.tsx` lines 212–277 (Dialog) + lines 95–183 (card with toggle controls).

**Card section pattern** (lines 95–99):
```tsx
<div className="rounded-xl border border-border bg-card p-6 shadow-sm">
  <div className="flex items-start gap-3 mb-5">
    <div className="flex h-10 w-10 shrink-0 items-center justify-center rounded-lg bg-emerald-100 text-emerald-700 dark:bg-emerald-950/50 dark:text-emerald-400">
      <Sparkles className="h-5 w-5" />
    </div>
```

Dialog controlled by `ConsentProvider.settingsPanelOpen`. Contains two rows:
- "Notwendig" (always checked, disabled toggle)
- "Fehleranalyse – Sentry" (checkbox default unchecked — TTDSG)

---

### `Frontend/instrumentation-client.ts` (MODIFIED — consent gate)

**Current shape** (lines 1–23 — full file):
```typescript
import * as Sentry from "@sentry/nextjs";
import { scrubEvent } from "@/lib/sentry-scrubber";

// D-16: Frontend Sentry stays disabled in production until Phase 6 wires
// the TTDSG cookie banner.
if (process.env.NEXT_PUBLIC_SENTRY_ENABLED === "true") {
  Sentry.init({
    dsn: process.env.NEXT_PUBLIC_SENTRY_DSN,
    environment: process.env.NEXT_PUBLIC_SENTRY_ENV ?? "production",
    sendDefaultPii: false,
    tracesSampleRate: 0,
    replaysOnErrorSampleRate: 0,
    replaysSessionSampleRate: 0,
    beforeSend(event) {
      return scrubEvent(event);
    },
  });
}

export const onRouterTransitionStart = Sentry.captureRouterTransitionStart;
```

**Change:** Replace `if (process.env.NEXT_PUBLIC_SENTRY_ENABLED === "true")` with a compound condition that also checks `localStorage`. Add `hasSentryConsent()` helper above the `if` block:
```typescript
const SENTRY_CONSENT_KEY = "taxreader-consent";

function hasSentryConsent(): boolean {
  try {
    const raw = localStorage.getItem(SENTRY_CONSENT_KEY);
    if (!raw) return false;
    const parsed = JSON.parse(raw) as { fehleranalyse?: boolean };
    return parsed.fehleranalyse === true;
  } catch {
    return false;
  }
}

if (process.env.NEXT_PUBLIC_SENTRY_ENABLED === "true" && hasSentryConsent()) {
  Sentry.init({ /* same config as before */ });
}
```

`localStorage` is safe here: `instrumentation-client.ts` runs after HTML load, before React hydration — `window` and `localStorage` are available (verified in RESEARCH.md, Next.js 16 docs).

---

### `Frontend/src/app/(authenticated)/settings/page.tsx` (MODIFIED — export trigger)

**Analog:** Self — existing `deleteAccount` flow (lines 68–84) shows async trigger with inline status states (`isDeleting`, `deleteError`). The export trigger follows the same state-machine pattern.

**Async trigger + status pattern** (lines 38–43, 68–84):
```tsx
const [isDeleting, setIsDeleting] = useState(false);
const [deleteError, setDeleteError] = useState<string | null>(null);

const handleDeleteAccount = async () => {
  if (password.length === 0) return;
  setIsDeleting(true);
  setDeleteError(null);
  try {
    await deleteAccount(password);
    logout();
  } catch (err) {
    const status = (err as {...}).response?.status;
    // ... error state ...
    setIsDeleting(false);
  }
};
```

**Export trigger states:** `"idle" | "generating" | "ready" | "error"`. The trigger card section follows the card pattern (lines 95–99). On "ready", show download link. Status polled via TanStack Query or `setInterval`.

---

### `Frontend/src/providers/auth-provider.tsx` (MODIFIED — PUBLIC_PATHS)

**Current line 29:**
```tsx
const PUBLIC_PATHS = ["/login", "/register", "/impressum", "/datenschutz"];
```

**Change to:**
```tsx
const PUBLIC_PATHS = ["/login", "/register", "/impressum", "/datenschutz", "/agb", "/widerruf"];
```

---

### `Frontend/src/lib/api-client.ts` (MODIFIED — export endpoints)

**Pattern:** Copy the `getUserSettings` / `updateUserSettings` function pattern (lines 274–283):
```typescript
export async function getUserSettings(): Promise<UserSettings> {
  const { data } = await api.get<UserSettings>("/settings");
  return data;
}
```

**New functions to add:**
```typescript
export async function requestDataExport(): Promise<{ exportToken: string }> {
  const { data } = await api.post<{ exportToken: string }>("/export/request");
  return data;
}

export async function getExportStatus(exportToken: string): Promise<{ status: "Generating" | "Ready" | "Expired" }> {
  const { data } = await api.get<{ status: "Generating" | "Ready" | "Expired" }>("/export/status", {
    params: { token: exportToken },
  });
  return data;
}

// Download is a direct link — no api-client function needed; use <a href="/api/v1/export/download?token=...">
```

---

### `Frontend/src/app/(authenticated)/layout.tsx` (MODIFIED — mount ConsentBanner + Footer)

**Current shape** (lines 1–34 — full file):
```tsx
"use client";

import { Loader2 } from "lucide-react";
import { SidebarProvider, SidebarInset } from "@/components/ui/sidebar";
import { AppSidebar } from "@/components/layout/app-sidebar";
import { useAuth } from "@/providers/auth-provider";

export default function AuthenticatedLayout({ children }: ...) {
  const { user, isLoading } = useAuth();
  // ...
  return (
    <SidebarProvider>
      <AppSidebar />
      <SidebarInset className="overflow-hidden">{children}</SidebarInset>
    </SidebarProvider>
  );
}
```

**Changes:** Add `<Footer />` and `<CookieBanner />` mounts. Place Footer outside `SidebarInset` (sibling to it, inside `SidebarProvider`) to avoid the `overflow-hidden` clip (Open Question #3 in RESEARCH.md). Place `<CookieBanner />` as last child of `SidebarProvider` so it overlays the sidebar layout.

---

### `Frontend/src/app/layout.tsx` (MODIFIED — mount ConsentProvider)

**Current providers nesting** (lines 37–44):
```tsx
<ThemeProvider>
  <QueryProvider>
    <TooltipProvider>
      <AuthProvider>
        {children}
      </AuthProvider>
      <Toaster richColors />
    </TooltipProvider>
  </QueryProvider>
</ThemeProvider>
```

**Add `ConsentProvider`** wrapping `AuthProvider` (or as a sibling inside `TooltipProvider`):
```tsx
<ThemeProvider>
  <QueryProvider>
    <TooltipProvider>
      <ConsentProvider>
        <AuthProvider>
          {children}
        </AuthProvider>
      </ConsentProvider>
      <Toaster richColors />
    </TooltipProvider>
  </QueryProvider>
</ThemeProvider>
```

---

## Shared Patterns

### Primary Constructor DI
**Source:** `Backend/src/TaxReader.Application/Jobs/GrantTokensJob.cs` line 11, `Backend/src/TaxReader.Application/Commands/SaveClassificationRuleHandler.cs` line 18
**Apply to:** `AuditLogger`, `ExportUserDataJob`, `ExportCleanupJob`, all five call-site modifications
```csharp
public class GrantTokensJob(IAppDbContext dbContext, ILogger<GrantTokensJob> logger)
```

### Result<T> Translation in Endpoints
**Source:** `Backend/src/TaxReader.Api/Endpoints/SettingsEndpoints.cs` lines 18–22
**Apply to:** `ExportEndpoints`
```csharp
return result.IsSuccess
    ? Results.Ok(result.Value)
    : Results.NotFound(new { error = result.Error });
```

### EF IEntityTypeConfiguration<T> + snake_case
**Source:** `Backend/src/TaxReader.Infrastructure/Data/Configurations/TokenTransactionConfiguration.cs` lines 1–31
**Apply to:** `AuditLogEntryConfiguration`
```csharp
public class TokenTransactionConfiguration : IEntityTypeConfiguration<TokenTransaction>
{
    public void Configure(EntityTypeBuilder<TokenTransaction> builder)
    {
        builder.ToTable("token_transactions");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasDefaultValueSql("gen_random_uuid()");
        // ...
    }
}
```

### Structured Logging (named placeholders, never interpolation)
**Source:** `Backend/src/TaxReader.Application/Jobs/GrantTokensJob.cs` line 62
**Apply to:** All new Backend files
```csharp
logger.LogInformation("Granted {Credits} tokens to User {UserId} — new balance: {Balance}",
    credits, userId, balance.Balance);
```

### `"use client"` Provider Pattern
**Source:** `Frontend/src/providers/auth-provider.tsx` lines 1, 27–28
**Apply to:** `ConsentProvider`, `CookieBanner`, `ConsentSettingsDialog`
```tsx
"use client";
// ...
const AuthContext = createContext<AuthContextValue | null>(null);
```

### German Sie-form + Toast notifications
**Source:** `Frontend/src/app/(authenticated)/settings/page.tsx` lines 60–66
**Apply to:** All new Frontend components
```tsx
toast.success("Einstellungen gespeichert");
toast.error("Speichern fehlgeschlagen");
// Confirm dialog copy uses "Sie": "Geben Sie zur Bestätigung Ihr Passwort ein."
```

### shadcn Dialog pattern
**Source:** `Frontend/src/app/(authenticated)/settings/page.tsx` lines 212–277
**Apply to:** `ConsentSettingsDialog`
```tsx
<Dialog open={...} onOpenChange={(open) => { ... }}>
  <DialogContent className="sm:max-w-md">
    <DialogHeader>
      <DialogTitle>...</DialogTitle>
    </DialogHeader>
    <DialogFooter>
      <Button variant="outline">...</Button>
      <Button>...</Button>
    </DialogFooter>
  </DialogContent>
</Dialog>
```

### Tailwind dark-mode card section
**Source:** `Frontend/src/app/(authenticated)/settings/page.tsx` lines 95–99
**Apply to:** All new shadcn card sections in settings and consent dialogs
```tsx
<div className="rounded-xl border border-border bg-card p-6 shadow-sm">
  <div className="flex items-start gap-3 mb-5">
    <div className="flex h-10 w-10 shrink-0 items-center justify-center rounded-lg bg-emerald-100 text-emerald-700 dark:bg-emerald-950/50 dark:text-emerald-400">
```

---

## No Analog Found

| File | Role | Data Flow | Reason |
|------|------|-----------|--------|
| `Frontend/src/app/(legal)/agb/page.tsx` (content) | component | — | Legal content is authored from DE law templates; structure has analog (impressum), but the AGB section/clause structure (StBerG safe positioning, VSBG, refund policy) has no codebase precedent |
| `Frontend/src/app/(legal)/widerruf/page.tsx` (content) | component | — | Statutory text §356 BGB + Muster-Widerrufsformular has no codebase precedent for the content itself, only for the page wrapper |

Note: Both files have a structural analog (`impressum/page.tsx`) for the React/TSX wrapper — the "no analog" applies only to the substantive legal copy, which is authored per D-01.

---

## Metadata

**Analog search scope:** `Backend/src/TaxReader.Domain/`, `Backend/src/TaxReader.Application/`, `Backend/src/TaxReader.Infrastructure/`, `Backend/src/TaxReader.Api/`, `Frontend/src/`
**Files read:** 30
**Pattern extraction date:** 2026-06-02
