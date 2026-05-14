# Phase 2: Auth + Rate-Limit Hardening - Pattern Map

**Mapped:** 2026-05-12
**Files analyzed:** 26 (10 NEW, 16 MODIFIED — backend + frontend + infra)
**Analogs found:** 24 / 26 (2 NEW files have no in-repo analog — see "No Analog Found")

## File Classification

### Backend NEW files

| New File | Role | Data Flow | Closest Analog | Match Quality |
|----------|------|-----------|----------------|---------------|
| `Backend/src/TaxReader.Domain/Entities/RefreshToken.cs` | entity (POCO) | persisted state | `Backend/src/TaxReader.Domain/Entities/ReceiptFile.cs` | exact (FK + nav + UTC timestamps) |
| `Backend/src/TaxReader.Infrastructure/Data/Configurations/RefreshTokenConfiguration.cs` | EF config | schema mapping | `Backend/src/TaxReader.Infrastructure/Data/Configurations/UserConfiguration.cs` | exact (HasKey + HasMaxLength + HasIndex + HasMany/HasOne) |
| `Backend/src/TaxReader.Application/Interfaces/IRefreshTokenService.cs` | Application port (interface) | request-response | `Backend/src/TaxReader.Application/Interfaces/IAuthService.cs` | exact (Result-returning auth port) |
| `Backend/src/TaxReader.Infrastructure/Services/RefreshTokenService.cs` | Infrastructure service impl | CRUD + crypto | `Backend/src/TaxReader.Infrastructure/Services/AuthService.cs` | exact (BCrypt/HMAC + EF Core writes + Result) |
| `Backend/src/TaxReader.Infrastructure/Configuration/RefreshTokenOptions.cs` | config POCO (IOptions) | settings | `Backend/src/TaxReader.Infrastructure/Configuration/JwtOptions.cs` | exact (one secret + SectionName) |
| `Backend/src/TaxReader.Infrastructure/Configuration/RateLimitOptions.cs` (optional) | config POCO (IOptions) | settings | `Backend/src/TaxReader.Infrastructure/Configuration/JwtOptions.cs` | exact (numbers + SectionName) |
| `Backend/src/TaxReader.Infrastructure/Migrations/<timestamp>_AddRefreshTokensTable_DropLegacyRefreshTokenColumns.cs` | EF migration | DDL | `Backend/src/TaxReader.Infrastructure/Migrations/20260412095923_AddAuthAndUserScoping.cs` | exact (CreateTable + DropColumn + cascade FK) |
| `Backend/src/TaxReader.Application/Validators/DeleteAccountValidator.cs` | FluentValidation | request validation | `Backend/src/TaxReader.Application/Validators/ConfirmClassificationValidator.cs` | exact (single-rule NotEmpty validator) |

### Backend NEW tests

| New Test File | Role | Data Flow | Closest Analog | Match Quality |
|---------------|------|-----------|----------------|---------------|
| `Backend/tests/TaxReader.UnitTests/Helpers/RateLimitTestFactory.cs` | test helper | WAF host build | `Backend/tests/TaxReader.UnitTests/Cors/CorsConfigurationTests.cs` (lines 42–57: `BuildFactory`) | role-match (factory helper) |
| `Backend/tests/TaxReader.UnitTests/RateLimiting/AuthStrictPolicyTests.cs` | integration test | HTTP client | `Backend/tests/TaxReader.UnitTests/Cors/CorsConfigurationTests.cs` | exact (WAF + UseSetting) |
| `Backend/tests/TaxReader.UnitTests/RateLimiting/AuthRefreshPolicyTests.cs` | integration test | HTTP client | `CorsConfigurationTests.cs` | exact |
| `Backend/tests/TaxReader.UnitTests/RateLimiting/UploadConcurrencyPolicyTests.cs` | integration test | HTTP client | `CorsConfigurationTests.cs` | exact |
| `Backend/tests/TaxReader.UnitTests/RateLimiting/GlobalPolicyTests.cs` | integration test | HTTP client | `CorsConfigurationTests.cs` | exact |
| `Backend/tests/TaxReader.UnitTests/RateLimiting/RejectedResponseShapeTests.cs` | integration test | HTTP client | `CorsConfigurationTests.cs` + RESEARCH Example 4 | exact |
| `Backend/tests/TaxReader.UnitTests/RateLimiting/ForwardedHeadersTests.cs` | integration test | HTTP client + IOptions | `CorsConfigurationTests.cs` (lines 59–66 IOptions resolve) | exact (option-resolution shape) |
| `Backend/tests/TaxReader.UnitTests/Auth/RefreshTokenServiceTests.cs` | unit test | in-memory EF | `Backend/tests/TaxReader.UnitTests/Application/Commands/ConfirmClassificationHandlerTests.cs` | exact (UseInMemoryDatabase + Mock<ICurrentUser>) |
| `Backend/tests/TaxReader.UnitTests/Auth/ReplayDetectionTests.cs` | unit test | in-memory EF | `ConfirmClassificationHandlerTests.cs` | exact |
| `Backend/tests/TaxReader.UnitTests/Auth/DeleteAccountHandlerTests.cs` | unit test | in-memory EF + Moq | `ConfirmClassificationHandlerTests.cs` | exact |
| `Backend/tests/TaxReader.UnitTests/Auth/HmacPepperHashingTests.cs` | unit test | pure | `Backend/tests/TaxReader.UnitTests/Configuration/AnthropicOptionsTests.cs` | role-match (pure POCO/algorithm test) |
| `Backend/tests/TaxReader.UnitTests/Application/Validators/DeleteAccountValidatorTests.cs` | unit test | pure | `Backend/tests/TaxReader.UnitTests/Application/Validators/ConfirmClassificationValidatorTests.cs` | exact |

### MODIFIED files (analog = current shape)

| Modified File | Role | Type of Change |
|---------------|------|----------------|
| `Backend/src/TaxReader.Infrastructure/Services/AuthService.cs` | service | Replace direct `user.RefreshToken =` writes (lines 74, 98, 120) with `IRefreshTokenService.IssueAsync(...)` calls; accept `userAgent`/`ipAddress` params on `RegisterAsync`/`LoginAsync`/`RefreshAsync` |
| `Backend/src/TaxReader.Domain/Entities/User.cs` | entity | Drop `RefreshToken` (line 9) and `RefreshTokenExpiresAt` (line 10); add `ICollection<RefreshToken> RefreshTokens` nav |
| `Backend/src/TaxReader.Infrastructure/Data/Configurations/UserConfiguration.cs` | EF config | Drop `RefreshToken` HasMaxLength mapping (line 19); add `HasMany(e => e.RefreshTokens)` nav config |
| `Backend/src/TaxReader.Infrastructure/Data/AppDbContext.cs` | DbContext | Add `DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();` between lines 17 and 18 |
| `Backend/src/TaxReader.Application/Interfaces/IAppDbContext.cs` | interface | Add `DbSet<RefreshToken> RefreshTokens { get; }` to interface (line 17) |
| `Backend/src/TaxReader.Api/Program.cs` | bootstrap | `UseForwardedHeaders` FIRST; `AddRateLimiter` + `OnRejected`; `app.UseRateLimiter()` after `UseSerilogRequestLogging` and before `UseAuthentication`; register `IRefreshTokenService` (Scoped); bind `RefreshTokenOptions` from `RefreshToken` section |
| `Backend/src/TaxReader.Api/Endpoints/AuthEndpoints.cs` | endpoint group | Chain `.RequireRateLimiting("auth-strict")` on /login, /register, /account; `.RequireRateLimiting("auth-refresh")` on /refresh; bind `DeleteAccountRequest` JSON body on /account; extract UA/IP from `HttpContext` on /login + /register + /refresh |
| `Backend/src/TaxReader.Api/Endpoints/ReceiptFileEndpoints.cs` | endpoint group | Chain `.RequireRateLimiting("upload-concurrency")` on `POST /receipt-files` |
| `Backend/src/TaxReader.Application/Commands/DeleteAccountHandler.cs` | handler | Accept `DeleteAccountRequest`; BCrypt.Verify password; call `IRefreshTokenService.RevokeAllForUserAsync` before cascade-delete |
| `Backend/src/TaxReader.Application/DTOs/AuthDtos.cs` | DTO | Add `public record DeleteAccountRequest(string Password);` line |
| `Backend/src/TaxReader.Infrastructure/DependencyInjection.cs` | DI registration | `services.Configure<RefreshTokenOptions>(configuration.GetSection(RefreshTokenOptions.SectionName));` + `services.AddScoped<IRefreshTokenService, RefreshTokenService>();` |
| `Frontend/src/app/(authenticated)/settings/page.tsx` | client component | Swap `confirmInput` Input (line 227–237) for password Input bound to `password` state; remove `CONFIRM_PHRASE` constant; on 401 surface inline error |
| `Frontend/src/lib/api-client.ts` | API client | `deleteAccount(password: string)` signature with `axios.delete("/api/v1/auth/account", { data: { password }, headers })` — bypass shared interceptor like `register`/`login` do (lines 105–119) |
| `docker-compose.yml` | infra | Add `RefreshToken__HashKey: ${REFRESHTOKEN_HASHKEY}` to `api` service env block (next to `Jwt__Secret` at line 32) |
| `.env.example` | infra | Add `REFRESHTOKEN_HASHKEY=` placeholder with `openssl rand -base64 32` hint after JWT block |
| `CLAUDE.md` | docs | Mention `refresh_tokens` table under Domain Terms and rate-limit policies under API Design |

## Pattern Assignments

### `Backend/src/TaxReader.Domain/Entities/RefreshToken.cs` (entity)

**Analog:** `Backend/src/TaxReader.Domain/Entities/ReceiptFile.cs`

**File-scoped namespace + public mutable POCO** (lines 1–21):
```csharp
using TaxReader.Domain.Enums;

namespace TaxReader.Domain.Entities;

public class ReceiptFile
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string OriginalFileName { get; set; } = string.Empty;
    public string ContentHash { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string? SourceHint { get; set; }
    public int? YearHint { get; set; }
    public string? UploadedBy { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    public FileStatus Status { get; set; } = FileStatus.Uploaded;

    public User User { get; set; } = null!;
    public Receipt? Receipt { get; set; }
    public ICollection<ProcessingRun> ProcessingRuns { get; set; } = [];
}
```

**Apply to RefreshToken:** Same shape — `Guid Id`, `Guid UserId`, nullable strings for `UserAgent`, `DateTime` UTC defaults via `= DateTime.UtcNow`, `string TokenHash = string.Empty;`, nullable `Guid? ReplacedByTokenId`, `User User { get; set; } = null!;` nav. For the nullable inet column use `System.Net.IPAddress? IpAddress` (Npgsql maps to `inet`). Add the matching `ICollection<RefreshToken> RefreshTokens { get; set; } = [];` to `User.cs`.

**Domain has ZERO dependencies (architecture rule):** No `using Microsoft.*`, no `using System.Security.Cryptography`. Only `System.Net.IPAddress` (which is in `System.Net.Primitives`, part of BCL).

---

### `Backend/src/TaxReader.Infrastructure/Data/Configurations/RefreshTokenConfiguration.cs` (EF config)

**Analog:** `Backend/src/TaxReader.Infrastructure/Data/Configurations/UserConfiguration.cs`

**Full config skeleton** (lines 1–33):
```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaxReader.Domain.Entities;

namespace TaxReader.Infrastructure.Data.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasDefaultValueSql("gen_random_uuid()");

        builder.Property(e => e.Email).IsRequired().HasMaxLength(320);
        builder.Property(e => e.DisplayName).IsRequired().HasMaxLength(200);
        builder.Property(e => e.PasswordHash).IsRequired().HasMaxLength(200);
        builder.Property(e => e.RefreshToken).HasMaxLength(500);

        builder.HasIndex(e => e.Email).IsUnique();

        builder.HasMany(e => e.ReceiptFiles)
            .WithOne(e => e.User)
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.TokenBalance)
            .WithOne(e => e.User)
            .HasForeignKey<UserTokenBalance>(e => e.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
```

**Apply to RefreshTokenConfiguration:**
- `builder.ToTable("refresh_tokens");` — snake_case auto via `UseSnakeCaseNamingConvention` so don't manually set column names.
- `builder.HasKey(e => e.Id);` + `builder.Property(e => e.Id).HasDefaultValueSql("gen_random_uuid()");`
- `builder.Property(e => e.TokenHash).IsRequired().HasMaxLength(44);` (Base64 of 32-byte HMAC-SHA256 = 44 chars per RESEARCH Pattern 2)
- `builder.Property(e => e.UserAgent).HasMaxLength(500);` (nullable per D-02)
- `builder.HasIndex(e => e.TokenHash).IsUnique();` (O(1) lookup)
- `builder.HasIndex(e => new { e.UserId, e.RevokedAt });` (composite — supports `WHERE user_id = $1 AND revoked_at IS NULL`)
- `builder.HasOne<User>().WithMany(u => u.RefreshTokens).HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.Cascade);`
- Self-FK for `ReplacedByTokenId`: `builder.HasOne<RefreshToken>().WithMany().HasForeignKey(e => e.ReplacedByTokenId).OnDelete(DeleteBehavior.NoAction);`
- Auto-discovered by `AppDbContext.OnModelCreating` line 23: `modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);` — no manual registration required.

---

### `Backend/src/TaxReader.Application/Interfaces/IRefreshTokenService.cs` (Application port)

**Analog:** `Backend/src/TaxReader.Application/Interfaces/IAuthService.cs`

**Full file** (lines 1–11):
```csharp
using TaxReader.Application.DTOs;
using TaxReader.Domain.Common;

namespace TaxReader.Application.Interfaces;

public interface IAuthService
{
    Task<Result<AuthResponse>> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default);
    Task<Result<AuthResponse>> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);
    Task<Result<AuthResponse>> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default);
}
```

**Apply to IRefreshTokenService (per RESEARCH Pattern 5 + Claude's Discretion notes):**
```csharp
using TaxReader.Domain.Common;

namespace TaxReader.Application.Interfaces;

public interface IRefreshTokenService
{
    Task<string> IssueAsync(
        Guid userId,
        string? userAgent,
        string? ipAddress,
        CancellationToken cancellationToken = default);

    Task<Result<(Guid UserId, string PlaintextToken)>> ValidateAndRotateAsync(
        string plaintextToken,
        string? userAgent,
        string? ipAddress,
        CancellationToken cancellationToken = default);

    Task<int> RevokeAllForUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}
```

**Convention echoes:** file-scoped namespace; `Async` suffix; always default `CancellationToken`; `Result<T>` for error paths; no `IsSuccess`/`Failure` thrown.

---

### `Backend/src/TaxReader.Infrastructure/Services/RefreshTokenService.cs` (Infrastructure service)

**Analog:** `Backend/src/TaxReader.Infrastructure/Services/AuthService.cs` (primary), `TokenService.cs` (secondary — for `ICurrentUser`-less variant)

**Primary-constructor DI + `IOptions<T>.Value` cached + `Result<T>` failure pattern** (`AuthService.cs` lines 1–34):
```csharp
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using TaxReader.Application.DTOs;
using TaxReader.Application.Interfaces;
using TaxReader.Domain.Common;
using TaxReader.Domain.Entities;
using TaxReader.Domain.Enums;
using TaxReader.Infrastructure.Configuration;

namespace TaxReader.Infrastructure.Services;

public class AuthService(
    IAppDbContext dbContext,
    IOptions<JwtOptions> jwtOptions) : IAuthService
{
    private readonly JwtOptions _jwt = jwtOptions.Value;
    private const int InitialFreeTokens = 10;

    public async Task<Result<AuthResponse>> RegisterAsync(
        RegisterRequest request,
        CancellationToken cancellationToken = default)
    {
        var emailNormalized = request.Email.Trim().ToLowerInvariant();

        var exists = await dbContext.Users
            .AnyAsync(u => u.Email == emailNormalized, cancellationToken);

        if (exists)
            return Result<AuthResponse>.Failure("Ein Konto mit dieser E-Mail existiert bereits.");
```

**Random base64 token generation** (`AuthService.cs:152`):
```csharp
var refreshToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
```

**Apply to RefreshTokenService:**
- Primary constructor: `public class RefreshTokenService(IAppDbContext dbContext, IOptions<RefreshTokenOptions> refreshTokenOptions, IOptions<JwtOptions> jwtOptions, ILogger<RefreshTokenService> logger) : IRefreshTokenService`
- Cache options once: `private readonly byte[] _pepper = Convert.FromBase64String(refreshTokenOptions.Value.HashKey);`
- HMAC pattern (RESEARCH Pattern 2): `HMACSHA256.HashData(_pepper, plaintextBytes)` — static API, zero alloc, CA1850-compliant. Reuse `Convert.ToBase64String(RandomNumberGenerator.GetBytes(64))` for plaintext generation.
- Bulk revoke (RESEARCH Pattern 4): `ExecuteUpdateAsync` not entity-tracking. Filter `Where(t => t.UserId == userId && t.RevokedAt == null)`.
- German error strings: `Result<...>.Failure("Ungültiges oder abgelaufenes Refresh-Token.")` (matches `AuthService.cs:117`).
- Replay-detection log: wrap entire method body in `using (LogContext.PushProperty("UserId", existing.UserId)) { ... }` (Phase 1 OBS-02 pattern, see `SerilogEnrichmentTests.cs:21-29`); then `logger.LogWarning("Refresh token replay detected");` + `SentrySdk.CaptureMessage("Refresh token replay detected", scope => scope.SetExtra("user.id_hash", HashUserId(existing.UserId)), SentryLevel.Warning);`
- Structured logging convention: named placeholders (`logger.LogWarning("Anthropic API returned {Status}: {Body}", ...)`), never string interpolation.

**Composition for ValidateAndRotateAsync** comes directly from RESEARCH Example 1 (lines 836–954 of RESEARCH.md) — that excerpt is canonical and the executor should treat it as the body to write.

---

### `Backend/src/TaxReader.Infrastructure/Configuration/RefreshTokenOptions.cs` (config POCO)

**Analog:** `Backend/src/TaxReader.Infrastructure/Configuration/JwtOptions.cs`

**Full file** (lines 1–12):
```csharp
namespace TaxReader.Infrastructure.Configuration;

public class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Secret { get; set; } = string.Empty;
    public string Issuer { get; set; } = "TaxReader";
    public string Audience { get; set; } = "TaxReader";
    public int AccessTokenExpirationMinutes { get; set; } = 60;
    public int RefreshTokenExpirationDays { get; set; } = 30;
}
```

**Apply to RefreshTokenOptions:**
```csharp
namespace TaxReader.Infrastructure.Configuration;

public class RefreshTokenOptions
{
    public const string SectionName = "RefreshToken";

    /// <summary>Base64-encoded 32-byte HMAC-SHA256 pepper. Generate with: openssl rand -base64 32.</summary>
    public string HashKey { get; set; } = string.Empty;
}
```

**Binding in Program.cs** (mirrors `JwtOptions` line 55): `builder.Services.Configure<RefreshTokenOptions>(builder.Configuration.GetSection(RefreshTokenOptions.SectionName));` — or wire inside `AddInfrastructure` next to `AnthropicOptions` registration (`DependencyInjection.cs:33`).

**Env var convention:** `RefreshToken__HashKey` (double-underscore section-nesting per Anthropic / Jwt pattern).

---

### `Backend/src/TaxReader.Infrastructure/Configuration/RateLimitOptions.cs` (optional, only if windows configurable)

**Analog:** `Backend/src/TaxReader.Infrastructure/Configuration/AnthropicOptions.cs` (multi-property POCO)

**Full file** (lines 1–13):
```csharp
namespace TaxReader.Infrastructure.Configuration;

public class AnthropicOptions
{
    public const string SectionName = "Anthropic";

    public string? ApiKey { get; set; }
    public string Model { get; set; } = "claude-haiku-4-5";
    /// <summary>Tokens (credits) consumed per AI classification call.</summary>
    public int CostPerClassification { get; set; } = 1;
}
```

**Recommendation:** Per RESEARCH "Claude's Discretion" the windows/limits are spec-locked (5/min, 30/min, 60/min, concurrency=2). Hardcode them inline in `Program.cs` `AddRateLimiter` (RESEARCH Pattern 1) rather than introducing a config POCO. Skip this file unless an environment variable is genuinely needed.

---

### `Backend/src/TaxReader.Infrastructure/Migrations/<timestamp>_AddRefreshTokensTable_DropLegacyRefreshTokenColumns.cs` (EF migration)

**Analog:** `Backend/src/TaxReader.Infrastructure/Migrations/20260412095923_AddAuthAndUserScoping.cs`

**CreateTable shape** (lines 47–62):
```csharp
migrationBuilder.CreateTable(
    name: "users",
    columns: table => new
    {
        id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
        email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
        display_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
        password_hash = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
        refresh_token = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
        refresh_token_expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
        created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
    },
    constraints: table =>
    {
        table.PrimaryKey("pk_users", x => x.id);
    });
```

**Cascade FK + composite index** (lines 76–94):
```csharp
migrationBuilder.CreateIndex(
    name: "ix_receipt_files_user_id_content_hash",
    table: "receipt_files",
    columns: new[] { "user_id", "content_hash" },
    unique: true);

migrationBuilder.CreateIndex(
    name: "ix_users_email",
    table: "users",
    column: "email",
    unique: true);

migrationBuilder.AddForeignKey(
    name: "fk_receipt_files_users_user_id",
    table: "receipt_files",
    column: "user_id",
    principalTable: "users",
    principalColumn: "id",
    onDelete: ReferentialAction.Cascade);
```

**Symmetric Down** (lines 106–130):
```csharp
protected override void Down(MigrationBuilder migrationBuilder)
{
    migrationBuilder.DropForeignKey(
        name: "fk_receipt_files_users_user_id",
        table: "receipt_files");

    migrationBuilder.DropTable(name: "users");

    migrationBuilder.DropIndex(
        name: "ix_user_token_balances_user_id",
        table: "user_token_balances");

    migrationBuilder.DropColumn(
        name: "user_id",
        table: "user_token_balances");
}
```

**Apply to new migration (per D-15 + RESEARCH Pattern 3):**
1. `migrationBuilder.CreateTable("refresh_tokens", ...)` with columns matching D-02 schema (`token_hash character varying(44)`, `user_agent character varying(500) nullable`, `ip_address inet nullable` typed as `IPAddress`).
2. Add `pk_refresh_tokens` primary key + `fk_refresh_tokens_users_user_id` cascade FK to `users`.
3. Add `fk_refresh_tokens_refresh_tokens_replaced_by_token_id` self-FK (no cascade).
4. `migrationBuilder.CreateIndex("ix_refresh_tokens_token_hash", unique: true)` and `migrationBuilder.CreateIndex("ix_refresh_tokens_user_id_revoked_at", columns: new[] { "user_id", "revoked_at" })`.
5. **AFTER** CreateTable: `migrationBuilder.DropColumn(name: "refresh_token", table: "users");` and `migrationBuilder.DropColumn(name: "refresh_token_expires_at", table: "users");`.
6. `Down()`: re-add the two `users` columns (nullable, no data restore — accepted per D-15) and `DropTable("refresh_tokens")`.

**Generation command:** `dotnet ef migrations add AddRefreshTokensTable_DropLegacyRefreshTokenColumns -p Backend/src/TaxReader.Infrastructure -s Backend/src/TaxReader.Api`

---

### `Backend/src/TaxReader.Application/Validators/DeleteAccountValidator.cs` (FluentValidation)

**Analog:** `Backend/src/TaxReader.Application/Validators/ConfirmClassificationValidator.cs`

**Full file** (lines 1–23):
```csharp
using FluentValidation;
using TaxReader.Application.Commands;
using TaxReader.Domain.Enums;

namespace TaxReader.Application.Validators;

public class ConfirmClassificationValidator : AbstractValidator<ConfirmClassificationCommand>
{
    public ConfirmClassificationValidator()
    {
        RuleFor(x => x.ReceiptItemId)
            .NotEqual(Guid.Empty)
            .WithMessage("ReceiptItemId must not be empty.");

        RuleFor(x => x.Category)
            .IsInEnum()
            .WithMessage("Category must be a valid value.");

        RuleFor(x => x.Category)
            .NotEqual(Category.Unknown)
            .WithMessage("Category must not be Unknown when confirming.");
    }
}
```

**Apply to DeleteAccountValidator:**
```csharp
using FluentValidation;
using TaxReader.Application.DTOs;

namespace TaxReader.Application.Validators;

public class DeleteAccountValidator : AbstractValidator<DeleteAccountRequest>
{
    public DeleteAccountValidator()
    {
        RuleFor(x => x.Password)
            .NotEmpty()
            .WithMessage("Passwort ist erforderlich.");
    }
}
```

**Auto-discovered** by `Program.cs:81` line: `builder.Services.AddValidatorsFromAssemblyContaining<UploadReceiptFilesCommand>();` (same Application assembly).

---

### `Backend/src/TaxReader.Application/Commands/DeleteAccountHandler.cs` (MODIFIED)

**Current state** (`DeleteAccountHandler.cs:1-32`):
```csharp
using Microsoft.EntityFrameworkCore;
using TaxReader.Application.Interfaces;
using TaxReader.Domain.Common;

namespace TaxReader.Application.Commands;

public class DeleteAccountHandler(
    IAppDbContext dbContext,
    ICurrentUser currentUser)
{
    public async Task<Result<bool>> HandleAsync(CancellationToken cancellationToken = default)
    {
        var userId = currentUser.UserId;

        var user = await dbContext.Users
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user is null)
            return Result<bool>.Failure("User not found.");

        dbContext.Users.Remove(user);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result<bool>.Success(true);
    }
}
```

**Apply changes per D-13 (RESEARCH Example 3):**
- Add `IRefreshTokenService refreshTokenService` to primary constructor.
- Change signature: `public async Task<Result<bool>> HandleAsync(DeleteAccountRequest request, CancellationToken cancellationToken = default)`.
- After fetching `user`: `if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash)) return Result<bool>.Failure("Ungültiges Passwort.");` (reuses `AuthService.cs:94` exact pattern).
- Before `dbContext.Users.Remove(user)`: `await refreshTokenService.RevokeAllForUserAsync(userId, cancellationToken);` (defense-in-depth).
- Keep cascade-delete comment block (lines 21–25). Add `using TaxReader.Application.DTOs;` import.

---

### `Backend/src/TaxReader.Infrastructure/Services/AuthService.cs` (MODIFIED)

**Three change sites** (lines 73–75, 97–99, 119–121):

Each currently looks like:
```csharp
var (accessToken, refreshToken) = GenerateTokens(user);
user.RefreshToken = refreshToken;
user.RefreshTokenExpiresAt = DateTime.UtcNow.AddDays(_jwt.RefreshTokenExpirationDays);
```

**Replace each with:**
```csharp
var accessToken = GenerateAccessToken(user);
var refreshToken = await _refreshTokenService.IssueAsync(user.Id, userAgent, ipAddress, cancellationToken);
```

**Method signature changes:**
- `RegisterAsync(RegisterRequest request, string? userAgent, string? ipAddress, CancellationToken)`
- `LoginAsync(LoginRequest request, string? userAgent, string? ipAddress, CancellationToken)`
- `RefreshAsync(string refreshToken, string? userAgent, string? ipAddress, CancellationToken)` — body becomes `var result = await _refreshTokenService.ValidateAndRotateAsync(refreshToken, userAgent, ipAddress, ct);` then issue a new JWT for `result.Value.UserId`.

**Constructor adds:** `IRefreshTokenService refreshTokenService` (primary-constructor positional).

**Don't inject `IHttpContextAccessor`** (Pitfall 8 in RESEARCH): endpoint extracts UA/IP and passes them in.

---

### `Backend/src/TaxReader.Api/Endpoints/AuthEndpoints.cs` (MODIFIED)

**Current `/login` shape** (lines 29–42):
```csharp
auth.MapPost("/login", async (
    LoginRequest request,
    IAuthService authService,
    CancellationToken cancellationToken) =>
{
    var result = await authService.LoginAsync(request, cancellationToken);

    return result.IsSuccess
        ? Results.Ok(result.Value)
        : Results.Unauthorized();
})
.AllowAnonymous()
.WithName("Login")
.WithSummary("Authenticate and receive JWT tokens");
```

**Modified version (per RESEARCH Example 2 + D-09/D-12):**
```csharp
auth.MapPost("/login", async (
    LoginRequest request,
    IAuthService authService,
    HttpContext httpContext,
    CancellationToken cancellationToken) =>
{
    var userAgent = httpContext.Request.Headers.UserAgent.ToString();
    var ipAddress = httpContext.Connection.RemoteIpAddress?.ToString();

    var result = await authService.LoginAsync(request, userAgent, ipAddress, cancellationToken);

    return result.IsSuccess
        ? Results.Ok(result.Value)
        : Results.Unauthorized();
})
.AllowAnonymous()
.RequireRateLimiting("auth-strict")
.WithName("Login")
.WithSummary("Authenticate and receive JWT tokens");
```

**Apply same shape to:**
- `/register` → `.AllowAnonymous().RequireRateLimiting("auth-strict")`
- `/refresh` → `.AllowAnonymous().RequireRateLimiting("auth-refresh")`
- `/account` (DELETE) → `.RequireRateLimiting("auth-strict")` (authenticated → partition by `sub`); add `DeleteAccountRequest request` parameter to bind JSON body; map BCrypt-failure `Result.Failure` to 401:
```csharp
auth.MapDelete("/account", async (
    DeleteAccountRequest request,
    DeleteAccountHandler handler,
    CancellationToken cancellationToken) =>
{
    var result = await handler.HandleAsync(request, cancellationToken);

    if (result.IsSuccess)
        return Results.NoContent();

    if (result.Error == "Ungültiges Passwort.")
        return Results.Json(new { error = result.Error }, statusCode: 401);

    return Results.NotFound(new { error = result.Error });
})
.RequireRateLimiting("auth-strict")
.WithName("DeleteAccount");
```

---

### `Backend/src/TaxReader.Api/Endpoints/ReceiptFileEndpoints.cs` (MODIFIED)

**Current shape** (lines 14–49 — `MapPost("/")`): see `ReceiptFileEndpoints.cs:14-49`.

**Single-line change:** Add `.RequireRateLimiting("upload-concurrency")` to the existing chain. Position after `.DisableAntiforgery()` (line 47):
```csharp
})
.DisableAntiforgery()
.RequireRateLimiting("upload-concurrency")
.WithName("UploadReceiptFiles")
.WithSummary("Upload and process one or more receipt files (PDF, JPG, PNG, WEBP)");
```

---

### `Backend/src/TaxReader.Api/Program.cs` (MODIFIED — major)

**Current pipeline order** (lines 137–153):
```csharp
var app = builder.Build();

// D-02: log resolved Anthropic configuration so any drift between code,
// compose, and env is visible without throwing. Logs once at startup.
var resolvedAnthropicOptions = app.Services
    .GetRequiredService<Microsoft.Extensions.Options.IOptions<AnthropicOptions>>().Value;
app.Logger.LogInformation(
    "Anthropic configuration resolved: Model={Model}, CostPerClassification={CostPerClassification}",
    resolvedAnthropicOptions.Model,
    resolvedAnthropicOptions.CostPerClassification);

// Middleware
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseCors();
app.UseSerilogRequestLogging();
app.UseAuthentication();
app.UseAuthorization();
```

**Required pipeline order (RESEARCH Pattern 1 + Pitfall 2):**
1. `app.UseForwardedHeaders();` ← **FIRST** (before anything that reads `RemoteIpAddress`)
2. `app.UseMiddleware<ExceptionHandlingMiddleware>();`
3. `app.UseCors();`
4. `app.UseSerilogRequestLogging();`
5. `app.UseRateLimiter();` ← **NEW** (after request logging so 429s log, before auth so anon rate-limits fire first)
6. `app.UseAuthentication();`
7. `app.UseAuthorization();`

**Service registration block** — add immediately before `builder.Services.AddCors(...)` (line 103):

```csharp
// .NET 10: KnownNetworks is OBSOLETE — use KnownIPNetworks with System.Net.IPNetwork
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor
                             | ForwardedHeaders.XForwardedProto
                             | ForwardedHeaders.XForwardedHost;
    options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse("172.16.0.0/12"));
    options.ForwardLimit = 1;
});

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    // ... policies (auth-strict, auth-refresh, upload-concurrency) + OnRejected
    // Full body comes from RESEARCH.md Pattern 1 (lines 357–446).
});
```

**CRITICAL .NET 10 breaking change (Pitfall 1):** Do NOT write `options.KnownNetworks.Add(new IPNetwork(...))` — that property is OBSOLETE (`ASPDEPR005`). Use `options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse("172.16.0.0/12"))`. The type is `System.Net.IPNetwork` (in `System.Net.Primitives`), NOT `Microsoft.AspNetCore.HttpOverrides.IPNetwork`.

**Required usings to add to Program.cs:**
```csharp
using System.Net;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Mvc;       // for ProblemDetails
```

**Register `IRefreshTokenService`** in `DependencyInjection.cs` next to `AddScoped<IAuthService, AuthService>()` (line 30):
```csharp
services.Configure<RefreshTokenOptions>(configuration.GetSection(RefreshTokenOptions.SectionName));
services.AddScoped<IRefreshTokenService, RefreshTokenService>();
```

---

### `Backend/src/TaxReader.Infrastructure/Data/AppDbContext.cs` (MODIFIED)

**Current** (lines 10–18):
```csharp
public DbSet<User> Users => Set<User>();
public DbSet<ReceiptFile> ReceiptFiles => Set<ReceiptFile>();
public DbSet<Receipt> Receipts => Set<Receipt>();
public DbSet<ReceiptItem> ReceiptItems => Set<ReceiptItem>();
public DbSet<ItemClassification> ItemClassifications => Set<ItemClassification>();
public DbSet<ClassificationRule> ClassificationRules => Set<ClassificationRule>();
public DbSet<ProcessingRun> ProcessingRuns => Set<ProcessingRun>();
public DbSet<UserTokenBalance> UserTokenBalances => Set<UserTokenBalance>();
public DbSet<TokenTransaction> TokenTransactions => Set<TokenTransaction>();
```

**Add one line** (preserve alphabetic-ish order; group near `Users`):
```csharp
public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
```

**No `OnModelCreating` change needed** — `ApplyConfigurationsFromAssembly` (line 23) auto-discovers `RefreshTokenConfiguration`.

---

### `Backend/src/TaxReader.Application/Interfaces/IAppDbContext.cs` (MODIFIED)

**Current** (lines 1–20):
```csharp
using Microsoft.EntityFrameworkCore;
using TaxReader.Domain.Entities;

namespace TaxReader.Application.Interfaces;

public interface IAppDbContext
{
    DbSet<User> Users { get; }
    DbSet<ReceiptFile> ReceiptFiles { get; }
    DbSet<Receipt> Receipts { get; }
    DbSet<ReceiptItem> ReceiptItems { get; }
    DbSet<ItemClassification> ItemClassifications { get; }
    DbSet<ClassificationRule> ClassificationRules { get; }
    DbSet<ProcessingRun> ProcessingRuns { get; }
    DbSet<UserTokenBalance> UserTokenBalances { get; }
    DbSet<TokenTransaction> TokenTransactions { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
```

**Add one line:** `DbSet<RefreshToken> RefreshTokens { get; }`

---

### `Backend/src/TaxReader.Application/DTOs/AuthDtos.cs` (MODIFIED)

**Current** (lines 1–9):
```csharp
namespace TaxReader.Application.DTOs;

public record RegisterRequest(string Email, string DisplayName, string Password);
public record LoginRequest(string Email, string Password);
public record RefreshRequest(string RefreshToken);

public record AuthResponse(string AccessToken, string RefreshToken, UserDto User);
public record UserDto(Guid Id, string Email, string DisplayName);
```

**Add one record:** `public record DeleteAccountRequest(string Password);`

---

### `Backend/src/TaxReader.Domain/Entities/User.cs` (MODIFIED)

**Current** (lines 1–23):
```csharp
namespace TaxReader.Domain.Entities;

public class User
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string? RefreshToken { get; set; }
    public DateTime? RefreshTokenExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    // ...
    public ICollection<ReceiptFile> ReceiptFiles { get; set; } = [];
    public UserTokenBalance? TokenBalance { get; set; }
    public ICollection<TokenTransaction> TokenTransactions { get; set; } = [];
}
```

**Changes:**
- DELETE line 9: `public string? RefreshToken { get; set; }`
- DELETE line 10: `public DateTime? RefreshTokenExpiresAt { get; set; }`
- ADD nav near line 20: `public ICollection<RefreshToken> RefreshTokens { get; set; } = [];`

---

### `Backend/src/TaxReader.Infrastructure/Data/Configurations/UserConfiguration.cs` (MODIFIED)

**Current** (lines 16–32):
```csharp
builder.Property(e => e.Email).IsRequired().HasMaxLength(320);
builder.Property(e => e.DisplayName).IsRequired().HasMaxLength(200);
builder.Property(e => e.PasswordHash).IsRequired().HasMaxLength(200);
builder.Property(e => e.RefreshToken).HasMaxLength(500);     // <-- DELETE

builder.HasIndex(e => e.Email).IsUnique();

builder.HasMany(e => e.ReceiptFiles)
    .WithOne(e => e.User)
    .HasForeignKey(e => e.UserId)
    .OnDelete(DeleteBehavior.Cascade);

builder.HasOne(e => e.TokenBalance)
    .WithOne(e => e.User)
    .HasForeignKey<UserTokenBalance>(e => e.UserId)
    .OnDelete(DeleteBehavior.Cascade);
```

**Changes:**
- DELETE line 19: `builder.Property(e => e.RefreshToken).HasMaxLength(500);`
- The `HasMany(e => e.RefreshTokens)` nav can be configured here OR in `RefreshTokenConfiguration` — match the existing project preference (ReceiptFiles nav is in `UserConfiguration`). Add:
```csharp
builder.HasMany(e => e.RefreshTokens)
    .WithOne()
    .HasForeignKey(e => e.UserId)
    .OnDelete(DeleteBehavior.Cascade);
```

---

### `Frontend/src/lib/api-client.ts` (MODIFIED)

**Current `deleteAccount`** (lines 126–129):
```typescript
export async function deleteAccount(): Promise<void> {
  await api.delete("/auth/account");
  clearAuthStorage();
}
```

**Bypass-interceptor pattern from existing `register` (line 105–112):**
```typescript
export async function register(email: string, displayName: string, password: string): Promise<AuthResponse> {
  const { data } = await axios.post<AuthResponse>("/api/v1/auth/register", {
    email,
    displayName,
    password,
  });
  return data;
}
```

**Apply to deleteAccount (per RESEARCH Pattern 6 + Pitfall 6 — DELETE body via `data` config, raw axios to bypass refresh-interceptor so wrong-password 401 surfaces inline):**
```typescript
export async function deleteAccount(password: string): Promise<void> {
  await axios.delete("/api/v1/auth/account", {
    headers: { Authorization: `Bearer ${getAccessToken()}` },
    data: { password },
  });
  clearAuthStorage();
}
```

**Reason for raw `axios` not `api`:** The shared `api` instance has the refresh-on-401 interceptor (lines 43–73). On wrong password, that interceptor would refresh the access token (which succeeds because the user's session is still valid), retry the DELETE, hit the same 401, then `clearAuthStorage()` + redirect to /login — that's wrong UX. We want the inline error. Raw axios skips the interceptor.

---

### `Frontend/src/app/(authenticated)/settings/page.tsx` (MODIFIED)

**Current dialog body** (lines 222–238):
```tsx
<div className="space-y-3 py-2">
  <p className="text-sm text-muted-foreground">
    Gib <span className="font-mono font-semibold text-foreground">{CONFIRM_PHRASE}</span> ein,
    um die Löschung zu bestätigen:
  </p>
  <Input
    value={confirmInput}
    onChange={(e) => setConfirmInput(e.target.value)}
    placeholder={CONFIRM_PHRASE}
    disabled={isDeleting}
    onKeyDown={(e) => {
      if (e.key === "Enter" && confirmInput === CONFIRM_PHRASE) {
        handleDeleteAccount();
      }
    }}
  />
</div>
```

**Apply changes per D-11:**
- Replace `CONFIRM_PHRASE` paragraph with German copy: `"Geben Sie zur Bestätigung Ihr Passwort ein."`
- Replace `Input` with password input bound to new `password` state:
```tsx
<Input
  type="password"
  value={password}
  onChange={(e) => setPassword(e.target.value)}
  disabled={isDeleting}
  onKeyDown={(e) => {
    if (e.key === "Enter" && password.length >= 1) {
      handleDeleteAccount();
    }
  }}
/>
{deleteError && (
  <p className="text-sm text-rose-600 dark:text-rose-400">{deleteError}</p>
)}
```
- Disable button until `password.length >= 1` (line 251 currently `confirmInput !== CONFIRM_PHRASE`):
```tsx
disabled={password.length === 0 || isDeleting}
```
- On 401, set `deleteError` to `"Ungültiges Passwort."` instead of closing dialog. Keep `CONFIRM_PHRASE` constant removable; keep the irreversibility-warning paragraph (lines 215–220) and destructive button styling.

**Frontend NOTE (per `Frontend/AGENTS.md`):** Next.js APIs may differ from training. Before adjusting routing/server-component plumbing here, read `node_modules/next/dist/docs/` for the installed version. The dialog itself is a `"use client"` component using shadcn `Dialog`/`Input` so the change is React-side, not Next.js-side.

---

### `docker-compose.yml` (MODIFIED)

**Current `api` env block** (lines 28–43):
```yaml
environment:
  ASPNETCORE_ENVIRONMENT: Production
  ASPNETCORE_URLS: http://+:8080
  ConnectionStrings__DefaultConnection: Host=db;Port=5432;Database=belegpilot;Username=${POSTGRES_USER:-postgres};Password=${POSTGRES_PASSWORD:-postgres}
  Jwt__Secret: ${JWT_SECRET}
  Jwt__Issuer: BelegPilot
  Jwt__Audience: BelegPilot
  Jwt__AccessTokenExpirationMinutes: ${JWT_ACCESS_EXPIRY_MINUTES:-60}
  Jwt__RefreshTokenExpirationDays: ${JWT_REFRESH_EXPIRY_DAYS:-30}
  Anthropic__ApiKey: ${ANTHROPIC_API_KEY}
  # ...
```

**Add one line** after `Jwt__RefreshTokenExpirationDays`:
```yaml
RefreshToken__HashKey: ${REFRESHTOKEN_HASHKEY}
```

---

### `.env.example` (MODIFIED)

**Current JWT block** (lines 13–15):
```dotenv
# ── JWT ───────────────────────────────────────────────────────────────────────
# Generate with: openssl rand -base64 48
JWT_SECRET=change-this-to-a-strong-random-secret-minimum-32-characters
```

**Add block after JWT (mirrors style — comment + generation hint + placeholder):**
```dotenv
# ── Refresh Token ─────────────────────────────────────────────────────────────
# HMAC-SHA256 pepper for refresh-token hashing (256-bit, server-side secret).
# Rotating this value invalidates ALL existing refresh tokens (forces re-login).
# Generate with: openssl rand -base64 32
REFRESHTOKEN_HASHKEY=
```

---

## Test Pattern Assignments

### `Backend/tests/TaxReader.UnitTests/RateLimiting/*Tests.cs` (integration tests via WAF)

**Analog:** `Backend/tests/TaxReader.UnitTests/Cors/CorsConfigurationTests.cs`

**Test class skeleton + WebApplicationFactory builder** (lines 1–57):
```csharp
using FluentAssertions;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace TaxReader.UnitTests.Cors;

public class CorsConfigurationTests
{
    [Fact]
    public void Production_NoOrigins_DeniesAll()
    {
        using var factory = BuildFactory(environment: "Production", origins: null);
        var policy = ResolveDefaultPolicy(factory);

        policy.Origins.Should().BeEmpty(
            "D-07: non-Development without CORS_ALLOWED_ORIGINS must deny all cross-origin requests");
    }

    private static WebApplicationFactory<Program> BuildFactory(string environment, string? origins)
    {
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment(environment);
            builder.UseSetting("CORS_ALLOWED_ORIGINS", origins ?? string.Empty);
            builder.UseSetting("Jwt:Secret", "test-secret-test-secret-test-secret-1234");
            builder.UseSetting("Jwt:Issuer", "test");
            builder.UseSetting("Jwt:Audience", "test");
            builder.UseSetting(
                "ConnectionStrings:DefaultConnection",
                "Host=localhost;Port=5432;Database=test;Username=test;Password=test");
        });
    }

    private static CorsPolicy ResolveDefaultPolicy(WebApplicationFactory<Program> factory)
    {
        using var scope = factory.Services.CreateScope();
        var corsOptions = scope.ServiceProvider
            .GetRequiredService<IOptions<CorsOptions>>().Value;
        var policy = corsOptions.GetPolicy(corsOptions.DefaultPolicyName!);
        policy.Should().NotBeNull("the default CORS policy must be registered");
        return policy!;
    }
}
```

**Apply to rate-limit tests (per RESEARCH Example 4):**

Add `RefreshToken:HashKey` to test settings:
```csharp
builder.UseSetting("RefreshToken:HashKey", Convert.ToBase64String(new byte[32]));
```

Use the factory's `CreateClient()` to make HTTP calls; assert `response.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);` after burning the policy budget. Verify German body shape:
```csharp
response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
response.Headers.RetryAfter.Should().NotBeNull();
var body = await response.Content.ReadFromJsonAsync<ProblemResponse>();
body!.Title.Should().Be("Zu viele Anfragen.");
body.Detail.Should().Contain("Sekunden erneut");
body.Status.Should().Be(429);
```

For option-resolution tests of `ForwardedHeadersOptions` use the `CorsConfigurationTests.cs:59-66` pattern with `IOptions<ForwardedHeadersOptions>`:
```csharp
using var scope = factory.Services.CreateScope();
var fwd = scope.ServiceProvider
    .GetRequiredService<IOptions<ForwardedHeadersOptions>>().Value;
fwd.KnownIPNetworks.Should().Contain(n => n.ToString() == "172.16.0.0/12");
fwd.ForwardLimit.Should().Be(1);
```

---

### `Backend/tests/TaxReader.UnitTests/Auth/RefreshTokenServiceTests.cs` (unit test)

**Analog:** `Backend/tests/TaxReader.UnitTests/Application/Commands/ConfirmClassificationHandlerTests.cs`

**In-memory EF + Mock<ICurrentUser> + IDisposable** (lines 1–67):
```csharp
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using TaxReader.Application.Commands;
using TaxReader.Application.Interfaces;
using TaxReader.Domain.Entities;
using TaxReader.Domain.Enums;
using TaxReader.Infrastructure.Data;
using TaxReader.UnitTests.Helpers;

namespace TaxReader.UnitTests.Application.Commands;

public class ConfirmClassificationHandlerTests : IDisposable
{
    private static readonly Guid TestUserId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");

    private readonly AppDbContext _dbContext;
    private readonly Mock<ICurrentUser> _currentUserMock;
    private readonly ConfirmClassificationHandler _handler;

    public ConfirmClassificationHandlerTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContext = new AppDbContext(options);
        _currentUserMock = new Mock<ICurrentUser>();
        _currentUserMock.Setup(u => u.UserId).Returns(TestUserId);
        _handler = new ConfirmClassificationHandler(_dbContext, _currentUserMock.Object);
    }

    [Fact]
    public async Task HandleAsync_ValidItem_CreatesConfirmedClassification()
    {
        // arrange-act-assert with FluentAssertions
        result.IsSuccess.Should().BeTrue();
    }

    public void Dispose() => _dbContext.Dispose();
}
```

**Apply to RefreshTokenServiceTests:** Same pattern. Build a `RefreshTokenOptions` directly (`new RefreshTokenOptions { HashKey = Convert.ToBase64String(new byte[32]) }`) wrapped in `Options.Create(...)`; build a real `RefreshTokenService` over the in-memory `AppDbContext`. Don't mock the service-under-test. **Test naming convention:** `Method_Scenario_Result` (e.g. `IssueAsync_ValidUser_StoresHashedRow`, `ValidateAndRotateAsync_RevokedToken_TriggersReplayRevoke`).

**For ReplayDetectionTests** specifically: seed two rows for the same user, set `RevokedAt` on one, present its plaintext, assert `RevokeAllForUserAsync` zeroes out all `revoked_at IS NULL` rows by counting affected entities.

---

### `Backend/tests/TaxReader.UnitTests/Application/Validators/DeleteAccountValidatorTests.cs`

**Analog:** `Backend/tests/TaxReader.UnitTests/Application/Validators/ConfirmClassificationValidatorTests.cs`

**Full file** (lines 1–48):
```csharp
using FluentAssertions;
using FluentValidation.TestHelper;
using TaxReader.Application.Commands;
using TaxReader.Application.Validators;
using TaxReader.Domain.Enums;

namespace TaxReader.UnitTests.Application.Validators;

public class ConfirmClassificationValidatorTests
{
    private readonly ConfirmClassificationValidator _validator = new();

    [Fact]
    public void Validate_ValidCommand_NoErrors()
    {
        var command = new ConfirmClassificationCommand(
            Guid.NewGuid(),
            Category.ConsumablesAndOfficeSupplies);

        var result = _validator.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_EmptyReceiptItemId_HasError()
    {
        var command = new ConfirmClassificationCommand(
            Guid.Empty,
            Category.ConsumablesAndOfficeSupplies);

        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.ReceiptItemId);
    }
}
```

**Apply to DeleteAccountValidatorTests:** Two facts — `Validate_NonEmptyPassword_NoErrors` and `Validate_EmptyPassword_HasError`. Reuse `FluentValidation.TestHelper.TestValidate` + `ShouldHaveValidationErrorFor(x => x.Password)` / `ShouldNotHaveAnyValidationErrors()`.

---

### `Backend/tests/TaxReader.UnitTests/Auth/HmacPepperHashingTests.cs`

**Analog:** `Backend/tests/TaxReader.UnitTests/Configuration/AnthropicOptionsTests.cs`

**Full file** (lines 1–35):
```csharp
using FluentAssertions;
using TaxReader.Infrastructure.Configuration;

namespace TaxReader.UnitTests.Configuration;

public class AnthropicOptionsTests
{
    [Fact]
    public void Default_Model_IsHaiku4_5()
    {
        var options = new AnthropicOptions();
        options.Model.Should().Be("claude-haiku-4-5");
    }

    [Fact]
    public void SectionName_IsAnthropic()
    {
        AnthropicOptions.SectionName.Should().Be("Anthropic");
    }
}
```

**Apply to HmacPepperHashingTests:** Pure-algorithm tests for the `ComputeHash` helper inside `RefreshTokenService` — assert deterministic output for the same pepper+plaintext, different output for different peppers, fixed-length 44-character Base64 output. Naming: `ComputeHash_SameInputs_ProducesSameOutput`, `ComputeHash_DifferentPeppers_ProducesDifferentOutputs`, `ComputeHash_AnyInput_Returns44CharBase64`.

---

### `Backend/tests/TaxReader.UnitTests/Helpers/RateLimitTestFactory.cs`

**Analog:** The static `BuildFactory` helper inside `CorsConfigurationTests.cs:42-57` plus `TestDataFactory.cs:1-111` for shape.

**`TestDataFactory.cs` shape** (lines 1–30):
```csharp
using TaxReader.Domain.Entities;
using TaxReader.Domain.Enums;

namespace TaxReader.UnitTests.Helpers;

public static class TestDataFactory
{
    public static ReceiptFile CreateReceiptFile(
        Guid? id = null,
        string fileName = "test.pdf",
        string contentHash = "ABC123",
        // ...
        FileStatus status = FileStatus.Uploaded)
    {
        return new ReceiptFile
        {
            Id = id ?? Guid.NewGuid(),
            // ...
        };
    }
}
```

**Apply to RateLimitTestFactory:** Static class that returns a configured `WebApplicationFactory<Program>` with all required `UseSetting` calls (`Jwt:*`, `RefreshToken:HashKey`, `ConnectionStrings:DefaultConnection`). Single method `CreateFactory(string environment = "Production")` so the 6 rate-limit test files can share one builder.

---

## Shared Patterns (apply to all relevant new files)

### Primary-constructor DI

**Source:** `Backend/src/TaxReader.Infrastructure/Services/AuthService.cs:17-21`, `TokenService.cs:8`, `DeleteAccountHandler.cs:7-10`
**Apply to:** All new Application handlers and Infrastructure services
```csharp
public class RefreshTokenService(
    IAppDbContext dbContext,
    IOptions<RefreshTokenOptions> refreshTokenOptions,
    IOptions<JwtOptions> jwtOptions,
    ILogger<RefreshTokenService> logger) : IRefreshTokenService
{
    private readonly byte[] _pepper = Convert.FromBase64String(refreshTokenOptions.Value.HashKey);
    private readonly JwtOptions _jwt = jwtOptions.Value;
}
```

### `Result<T>` error handling

**Source:** `Backend/src/TaxReader.Domain/Common/Result.cs:1-26`
**Apply to:** Every `RefreshTokenService` method that can fail; every modified handler
```csharp
return Result<bool>.Failure("Ungültiges Passwort.");
return Result<AuthResponse>.Success(new AuthResponse(...));
```
Never throw for control flow (per `CLAUDE.md` "Patterns We DON'T Use: Exceptions for control flow").

### Async + CancellationToken

**Source:** Every `*Async` method across `AuthService.cs`, `TokenService.cs`, handlers
**Apply to:** Every new method
- `Async` suffix mandatory
- `CancellationToken cancellationToken = default` last parameter
- Thread the token through every `await dbContext.X(..., cancellationToken)`

### File-scoped namespaces

**Source:** Every backend `.cs` file (e.g. `User.cs:1`, `AuthService.cs:15`)
**Apply to:** Every new C# file — never use block-style namespaces.

### Structured logging with named placeholders

**Source:** `Program.cs:143-146`, `Program.cs:128-130`
**Apply to:** All `ILogger<T>` calls in new services
```csharp
logger.LogInformation(
    "Refresh token rotated successfully for {UserId}",
    existing.UserId);

logger.LogWarning("Refresh token replay detected");  // no placeholders, but {UserId} is on LogContext scope
```
Never use string interpolation (`$"..."`) inside log message templates.

### Serilog LogContext scope for correlation

**Source:** `Backend/tests/TaxReader.UnitTests/Observability/SerilogEnrichmentTests.cs:21-29` (test that locks the pattern in), `UploadReceiptFilesHandler` per the structural assertion at `SerilogEnrichmentTests.cs:53-69`
**Apply to:** `RefreshTokenService.ValidateAndRotateAsync` body
```csharp
using (LogContext.PushProperty("UserId", existing.UserId))
{
    // expiry check, replay detection, rotation, save, return
}
```
Required import: `using Serilog.Context;`

### German user-facing strings (Sie-form)

**Source:** `AuthService.cs:34, :95, :117`, `Program.cs:128`
**Apply to:** Every `Result<T>.Failure("...")` returned from new code and 429 ProblemDetails body
- `"Ein Konto mit dieser E-Mail existiert bereits."`
- `"Ungültige E-Mail oder Passwort."`
- `"Ungültiges Passwort."` (new — D-10)
- `"Ungültiges oder abgelaufenes Refresh-Token."` (matches D-04 silent posture)
- `"Zu viele Anfragen."` (429 title)
- `"Bitte versuchen Sie es in {N} Sekunden erneut."` (429 detail)

### Per-user data scoping

**Source:** `TokenService.cs:20-23` (`Where(b => b.UserId == userId)`), `GetCurrentUser` endpoint `AuthEndpoints.cs:77-78`
**Apply to:** All new `RefreshTokenService` queries — always filter by `userId` from the caller; never trust client-supplied user identifiers.

### Sentry PII allow-list (replay-detection extras)

**Source:** Phase 1 D-14 PII allow-list (`user.id_hash` permitted)
**Apply to:** Replay-detection event in `ValidateAndRotateAsync`
```csharp
SentrySdk.CaptureMessage(
    "Refresh token replay detected",
    scope => scope.SetExtra("user.id_hash", HashUserId(existing.UserId)),
    SentryLevel.Warning);
```
`HashUserId` = first 16 hex chars of `SHA256.HashData(userId.ToByteArray())` per RESEARCH Example 1.

### `IOptions<T>` + `SectionName` constant + `__` env nesting

**Source:** `JwtOptions.cs:5`, `AnthropicOptions.cs:5`, `TesseractOptions.cs:5`
**Apply to:** Every new options POCO
- `public const string SectionName = "RefreshToken";`
- Bind via `services.Configure<RefreshTokenOptions>(configuration.GetSection(RefreshTokenOptions.SectionName));`
- Env var: `RefreshToken__HashKey` (double-underscore section separator)

### .NET 10 `KnownIPNetworks` (NOT `KnownNetworks`)

**Source:** RESEARCH Pitfall 1 (lines 776–780)
**Apply to:** `Program.cs` `ForwardedHeadersOptions` configuration ONLY
```csharp
// CORRECT (.NET 10)
options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse("172.16.0.0/12"));

// WRONG — generates ASPDEPR005
options.KnownNetworks.Add(new IPNetwork(IPAddress.Parse("172.16.0.0"), 12));
```
Use `System.Net.IPNetwork` (not `Microsoft.AspNetCore.HttpOverrides.IPNetwork`). Set `ForwardLimit = 1` explicitly (Pitfall 9 — Caddy is the only hop).

### EF Core via `IAppDbContext.DbSet<T>` directly (no repository)

**Source:** `TokenService.cs:22`, `DeleteAccountHandler.cs:15-16`
**Apply to:** `RefreshTokenService` — no repository class, hit `dbContext.RefreshTokens` directly. Use `FirstOrDefaultAsync` for single-row lookup; `ExecuteUpdateAsync` for bulk revoke (RESEARCH Pattern 4).

### `dotnet ef migrations add` command

**Source:** `CLAUDE.md` (lines 76–78 of original instructions)
```bash
dotnet ef migrations add AddRefreshTokensTable_DropLegacyRefreshTokenColumns \
  -p Backend/src/TaxReader.Infrastructure \
  -s Backend/src/TaxReader.Api
```

---

## No Analog Found

Files with no close existing match in the codebase — planner should reference RESEARCH.md patterns directly:

| File | Role | Data Flow | Reason | Use Instead |
|------|------|-----------|--------|-------------|
| `Backend/src/TaxReader.Api/Program.cs` rate-limiter block | bootstrap | rate-limit policies + ForwardedHeaders | No prior in-repo rate-limit registration exists; this is the first | RESEARCH Pattern 1 (lines 326–477) is canonical |
| `Backend/tests/TaxReader.UnitTests/RateLimiting/RejectedResponseShapeTests.cs` 429 body assertion | integration test | HTTP client | No prior in-repo test asserts ProblemDetails German content | RESEARCH Example 4 (lines 1043–1095) — extends `CorsConfigurationTests` shape |

For these, the executor should treat the RESEARCH.md code excerpts as the "write code that looks like this" reference. Both excerpts are fully-formed and tested against the .NET 10 API surface.

## Metadata

**Analog search scope:**
- `Backend/src/TaxReader.Domain/Entities/`
- `Backend/src/TaxReader.Domain/Common/`
- `Backend/src/TaxReader.Application/Interfaces/`
- `Backend/src/TaxReader.Application/Commands/`
- `Backend/src/TaxReader.Application/Validators/`
- `Backend/src/TaxReader.Application/DTOs/`
- `Backend/src/TaxReader.Infrastructure/Services/`
- `Backend/src/TaxReader.Infrastructure/Configuration/`
- `Backend/src/TaxReader.Infrastructure/Data/`
- `Backend/src/TaxReader.Infrastructure/Data/Configurations/`
- `Backend/src/TaxReader.Infrastructure/Migrations/`
- `Backend/src/TaxReader.Api/Endpoints/`
- `Backend/src/TaxReader.Api/Program.cs`
- `Backend/tests/TaxReader.UnitTests/` (all subdirectories)
- `Frontend/src/lib/api-client.ts`
- `Frontend/src/app/(authenticated)/settings/page.tsx`
- `docker-compose.yml`, `.env.example`

**Files scanned:** ~50 (Backend C# under analysis; Frontend TypeScript scanned for two specific files only — full Frontend pattern survey not warranted at this phase since UI changes are minimal swap-existing-Input changes)

**Pattern extraction date:** 2026-05-12

**Key .NET 10 breaking change called out:** `ForwardedHeadersOptions.KnownNetworks` → `KnownIPNetworks` + `System.Net.IPNetwork.Parse(...)` (ASPDEPR005). Embedded in every Program.cs / ForwardedHeaders pattern excerpt above and again in "Shared Patterns".
