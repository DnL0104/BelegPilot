# Phase 1: Foundation Cleanup + CI - Pattern Map

**Mapped:** 2026-05-04
**Files analyzed:** 22 (10 new + 12 modified + 1 disk-only delete)
**Analogs found:** 14 strong / 8 establish-new-pattern

## File Classification

| New / Modified File | Role | Data Flow | Closest Analog | Match Quality |
|--------------------|------|-----------|----------------|---------------|
| `.github/workflows/ci.yml` | config (CI/CD) | event-driven (PR/push) | none | establishes new pattern (RESEARCH.md Pattern 9) |
| `README.md` (repo root) | docs | static | `Backend/README.md` | role-match |
| `Frontend/instrumentation-client.ts` | config (Next.js convention) | event-driven (browser init) | none | establishes new pattern (RESEARCH.md Pattern 3) |
| `Frontend/instrumentation.ts` | config (Next.js convention) | event-driven (server runtime hook) | none | establishes new pattern |
| `Frontend/sentry.server.config.ts` | config | event-driven (Node runtime init) | none | establishes new pattern |
| `Frontend/sentry.edge.config.ts` | config | event-driven (Edge runtime init) | none | establishes new pattern |
| `Backend/.dockerignore` | config (Docker) | static | `Frontend/.dockerignore` | exact (different layer) |
| `Backend/src/TaxReader.Infrastructure/Observability/SentryScrubbing.cs` | utility (static helper, observability) | transform (event in/out) | `Backend/src/TaxReader.Infrastructure/Services/OcrTextNormalizer.cs` | exact (file-scoped namespace + static partial helper + regex) |
| `Backend/tests/TaxReader.UnitTests/Configuration/AnthropicOptionsTests.cs` | test (POCO assertion) | request-response | `Backend/tests/TaxReader.UnitTests/Domain/ResultTests.cs` | exact (simplest pure unit test shape) |
| `Backend/tests/TaxReader.UnitTests/Cors/CorsConfigurationTests.cs` | test (integration via WebApplicationFactory) | request-response | `Backend/tests/TaxReader.UnitTests/Application/Commands/ConfirmClassificationHandlerTests.cs` | role-match (Arrange/Act/Assert + IDisposable rig) |
| `Backend/tests/TaxReader.UnitTests/Observability/SentryScrubbingTests.cs` | test (transform unit) | transform | `Backend/tests/TaxReader.UnitTests/Infrastructure/Services/OcrTextNormalizerTests.cs` | exact (Theory + InlineData + FluentAssertions on a static helper) |
| `Backend/tests/TaxReader.UnitTests/Observability/SerilogEnrichmentTests.cs` | test (config + handler scope assertion) | request-response | `Backend/tests/TaxReader.UnitTests/Application/Commands/ConfirmClassificationHandlerTests.cs` | role-match (handler scope + Mock + InMemory DB) |
| `Backend/src/TaxReader.Api/Program.cs` | config (host bootstrap) | event-driven | self (existing) | modify in place |
| `Backend/src/TaxReader.Api/appsettings.json` | config | static | self (existing) | modify in place |
| `Backend/src/TaxReader.Api/appsettings.Development.json` | config | static | self (existing) | modify in place |
| `Backend/src/TaxReader.Application/Commands/UploadReceiptFilesHandler.cs` | controller-equivalent (CQRS handler) | request-response | self (existing) | modify in place |
| `Backend/Directory.Packages.props` | config (NuGet CPM) | static | self (existing) | append entries |
| `Backend/src/TaxReader.Api/TaxReader.Api.csproj` | config (project) | static | self (existing) | append entries |
| `Backend/src/TaxReader.Application/TaxReader.Application.csproj` | config (project) | static | self (existing) | append entries |
| `Backend/tests/TaxReader.UnitTests/TaxReader.UnitTests.csproj` | config (test project) | static | self (existing) | append entries |
| `Frontend/next.config.ts` | config | static | self (existing) | wrap export |
| `Frontend/package.json` | config (npm) | static | self (existing) | append dependency |
| `docker-compose.yml` | config (orchestration) | static | self (existing) | modify env block |
| `.env.example` | config (env template) | static | self (existing) | modify entries |
| `.gitignore` | config (VCS) | static | self (existing) | append entries |
| `CLAUDE.md` | docs | static | self (existing) | append section |

> **Disk-only delete (no code analog):** `Backend/src/TaxReader.Api/storage/2026/04/` — untracked PII PDFs. Plain `rm -rf` on disk. CI hygiene-check (Pattern 8 in RESEARCH.md) prevents reintroduction.

---

## Pattern Assignments

### `Backend/src/TaxReader.Infrastructure/Observability/SentryScrubbing.cs` (utility, transform)

**Analog:** `Backend/src/TaxReader.Infrastructure/Services/OcrTextNormalizer.cs`

**Why this analog:** Both are **static partial classes** that perform pure transforms (one input → one output) with regex-based field scrubbing. Both live under `Backend/src/TaxReader.Infrastructure/<Folder>/`. Both have a single public static method that the rest of the codebase calls. No DI, no logger, no `Result<T>` (these are pure helpers).

**File-scoped namespace + static partial class shape** (`OcrTextNormalizer.cs:1-13`):
```csharp
using System.Text.RegularExpressions;

namespace TaxReader.Infrastructure.Services;

/// <summary>
/// Normalises common OCR artefacts so downstream parsers can use consistent
/// patterns regardless of how Tesseract rendered the source.
/// </summary>
public static partial class OcrTextNormalizer
{
    public static string Normalize(string text)
    {
        if (string.IsNullOrEmpty(text))
            return text;
        ...
    }
}
```

**Generated regex pattern** (`OcrTextNormalizer.cs:33-43`) — copy this style if planner wants `[GeneratedRegex]` for the UUID matcher:
```csharp
[GeneratedRegex(@"(\d)EUR\b")]
private static partial Regex DigitEurAttachedRegex();
```

> **Deviation from RESEARCH.md Pattern 1:** RESEARCH.md (line 262) puts the file at `Backend/src/TaxReader.Api/Observability/SentryScrubbing.cs`, but the canonical_refs section of CONTEXT.md (and the upstream `<files_to_read>`) place it under `Infrastructure/Observability/`. The Infrastructure location is consistent with the existing utility analog (`Infrastructure/Services/OcrTextNormalizer.cs`) and with the architectural rule "API is thin: only endpoints + DI" (CONVENTIONS.md). **Recommend Infrastructure location** — flag for planner to confirm with the user if needed.

**No `Result<T>` here:** `SentryScrubbing.Scrub` returns `SentryEvent?` (Sentry's built-in nullable contract for "drop this event"); it does NOT throw and does NOT return `Result<T>`. Result<T> is for application-layer failures, not pure transforms.

---

### `Backend/tests/TaxReader.UnitTests/Configuration/AnthropicOptionsTests.cs` (test, request-response)

**Analog:** `Backend/tests/TaxReader.UnitTests/Domain/ResultTests.cs`

**Why this analog:** Simplest possible unit test shape — instantiate a class, assert property values. No DI, no DbContext, no mocks. Fits FND-02's requirement: "default model in `AnthropicOptions.cs` is `claude-haiku-4-5`."

**Imports + xUnit + FluentAssertions pattern** (`ResultTests.cs:1-7`):
```csharp
using FluentAssertions;
using TaxReader.Domain.Common;

namespace TaxReader.UnitTests.Domain;

public class ResultTests
{
```

**Fact + Arrange/Act/Assert pattern** (`ResultTests.cs:8-17`):
```csharp
[Fact]
public void Success_CreatesSuccessResult_WithValue()
{
    var result = Result<string>.Success("hello");

    result.IsSuccess.Should().BeTrue();
    result.IsFailure.Should().BeFalse();
    result.Value.Should().Be("hello");
    result.Error.Should().BeNull();
}
```

**Naming convention** (CONVENTIONS.md & `Result<T>` tests): `Method_Scenario_Result`. For Phase 1, test method names should be e.g. `Default_Model_IsHaiku4_5` and `Default_CostPerClassification_IsOne` (per RESEARCH.md Validation Architecture table line 991).

---

### `Backend/tests/TaxReader.UnitTests/Cors/CorsConfigurationTests.cs` (test, request-response)

**Analog:** `Backend/tests/TaxReader.UnitTests/Application/Commands/ConfirmClassificationHandlerTests.cs`

**Why this analog:** This is the closest the codebase has to an integration-flavoured test — it constructs an `IDisposable` test rig in the constructor, holds state across tests, and asserts on a real component (DbContext + handler). The CORS test will follow the same pattern but use `WebApplicationFactory<Program>` instead of `AppDbContext`.

**Constructor-fixture + IDisposable shape** (`ConfirmClassificationHandlerTests.cs:13-31`):
```csharp
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
```

**Async test pattern** (`ConfirmClassificationHandlerTests.cs:33-54`):
```csharp
[Fact]
public async Task HandleAsync_ValidItem_CreatesConfirmedClassification()
{
    // Arrange
    var item = TestDataFactory.CreateReceiptItem();
    ...
    // Act
    var result = await _handler.HandleAsync(command);

    // Assert
    result.IsSuccess.Should().BeTrue();
    ...
}
```

**Dispose pattern** (`ConfirmClassificationHandlerTests.cs:67`):
```csharp
public void Dispose() => _dbContext.Dispose();
```

**For CORS specifically** the planner needs `Microsoft.AspNetCore.Mvc.Testing` (NOT currently in `Directory.Packages.props` — see RESEARCH.md line 1009). The factory should override `CORS_ALLOWED_ORIGINS` env var via `WebApplicationFactory<Program>.WithWebHostBuilder` to test both the Dev and Production branches of the policy.

---

### `Backend/tests/TaxReader.UnitTests/Observability/SentryScrubbingTests.cs` (test, transform)

**Analog:** `Backend/tests/TaxReader.UnitTests/Infrastructure/Services/OcrTextNormalizerTests.cs`

**Why this analog:** Both test a pure static helper. Heavy use of `[Theory] + [InlineData]` to walk through the rule matrix is exactly what the OBS-01 scrubber needs (request body, query allow-list, headers allow-list, UUID masking, user.id_hash, breadcrumb suppression).

**Theory + InlineData pattern** (`OcrTextNormalizerTests.cs:8-23`):
```csharp
public class OcrTextNormalizerTests
{
    [Theory]
    [InlineData("", "")]
    [InlineData("   ", "   ")]
    public void Normalize_EmptyOrWhitespace_ReturnsUnchanged(string input, string expected)
    {
        OcrTextNormalizer.Normalize(input).Should().Be(expected);
    }

    [Theory]
    [InlineData("9,99EUR",          "9,99 €")]
    [InlineData("Total: 12,50EUR",  "Total: 12,50 €")]
    public void Normalize_DigitAttachedToEur_InsertsEuroSign(string input, string expected)
    {
        OcrTextNormalizer.Normalize(input).Should().Be(expected);
    }
```

**Multi-rule "applies all fixes" combination test** (`OcrTextNormalizerTests.cs:69-90`) — equivalent for SentryScrubbing would be a single "kitchen-sink" SentryEvent with body + query + headers + URL + user that asserts every rule fires:
```csharp
[Fact]
public void Normalize_MixedArtefactsInSameText_AppliesAllFixes()
{
    var input = """
        Rechnung
        1 Tinte 9,99EUR
        ...
        """;
    var expected = """...""";
    OcrTextNormalizer.Normalize(input).Should().Be(expected);
}
```

**Test-method-naming for the six D-14 rules** (per Validation Architecture table line 995):
- `Scrub_RequestBody_StrippedToNull`
- `Scrub_QueryString_AllowsOnlyPagePageSizeYearFormat`
- `Scrub_Headers_AllowsOnlyUserAgent`
- `Scrub_UrlWithUuid_MaskedToColonId`
- `Scrub_User_EmailDroppedIdHashed`
- `Scrub_RawReceiptContentInExtras_NeverSet` (verify the handler never calls `Sentry.SetExtra` on receipt content)

---

### `Backend/tests/TaxReader.UnitTests/Observability/SerilogEnrichmentTests.cs` (test, request-response)

**Analog (a):** `Backend/tests/TaxReader.UnitTests/Application/Commands/ConfirmClassificationHandlerTests.cs` (for the handler-scope half)
**Analog (b):** `Backend/tests/TaxReader.UnitTests/Infrastructure/Services/OcrTextNormalizerTests.cs` (for the config-only assertions)

**Why two analogs:** OBS-02 has two halves:
1. **Config half** — assert `appsettings.json` loads `WithEnvironmentName` and `FromLogContext`. Pure config parse — Theory/Fact pattern.
2. **Handler half** — assert that `LogContext.PushProperty("ReceiptFileId", ...)` makes the property visible on log lines emitted from `UploadReceiptFilesHandler.HandleAsync`. Needs an `ILogger<T>` test sink (capture log events) and a way to invoke the handler scope. Use the same DbContext-fixture pattern as ConfirmClassificationHandlerTests, plus a Serilog `TestLogger` / `InMemorySink`.

**Wave 0 helper to add per RESEARCH.md line 1008:** `Backend/tests/TaxReader.UnitTests/Helpers/TestLoggerProvider.cs` (or similar) to capture log events. The simplest implementation is a `Serilog.Sinks.InMemory` sink or a custom `ILoggerProvider` that stores `LogRecord` instances. Planner should pick whichever is lightest.

> **Important coupling:** OBS-02's handler test depends on `using Serilog.Context;` resolving in `TaxReader.Application` — RESEARCH.md Open Question 2 + Pattern 7 flag this as a likely missing `<PackageReference Include="Serilog" />` in `TaxReader.Application.csproj`. Plan should add the reference up front to avoid a CS0246 mid-implementation.

---

### `Backend/src/TaxReader.Api/Program.cs` (config, event-driven) — MODIFY IN PLACE

**Existing block to modify** (`Program.cs:18-26`, bootstrap area):
```csharp
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting BelegPilot API");

    var builder = WebApplication.CreateBuilder(args);
```

**Insert site for `UseSentry`** — immediately after `var builder = WebApplication.CreateBuilder(args);` (line 26), before the existing `corsOrigins` block at line 28. Per RESEARCH.md Pitfall 1, Sentry must be **first** so it sees DI-time exceptions. Code shape comes verbatim from RESEARCH.md Pattern 1.

**Existing block to modify** (`Program.cs:87-112`, CORS):
```csharp
// CORS
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        if (corsOrigins is { Length: > 0 })
        {
            policy.WithOrigins(corsOrigins)
                  .AllowAnyHeader()
                  .AllowAnyMethod();
            return;
        }

        if (builder.Environment.IsDevelopment())
        {
            policy.WithOrigins("http://localhost:3000")
                  .AllowAnyHeader()
                  .AllowAnyMethod();
            return;
        }

        policy.WithOrigins("http://localhost:3000")  // ← BUG (D-07): non-Dev fallback to dev origin
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});
```

**Replacement shape:** RESEARCH.md Pattern 4 verbatim. The non-Dev branch becomes `Log.Warning(...)` with no `WithOrigins` call.

**Existing block to modify** (`Program.cs:117`, post-build):
```csharp
var app = builder.Build();
```

**Insert site for D-02 startup-log line** — immediately after `var app = builder.Build();` per RESEARCH.md Pattern 5 Action 4. Use `app.Logger.LogInformation(...)` with structured placeholders (CONVENTIONS.md "structured logging always: never string interpolation"). **Only log `Model` and `CostPerClassification`** — `MaxTokens` is not a property on `AnthropicOptions` (RESEARCH.md Pattern 5 planner note).

**One-liner cleanup** (`Program.cs:24`): `Log.Information("Starting BelegPilot API");` — strictly Phase 1 is not a rebrand pass, but this string is a known stale "BelegPilot" reference. CONTEXT.md `<deferred>` marks the rebrand for a follow-up. **Recommend touching this line in this phase only if planner is also touching `CLAUDE.md`'s `BelegPilot` → `TaxReader` line for D-02 documentation** — otherwise leave alone (surgical-changes rule).

---

### `Backend/src/TaxReader.Api/appsettings.json` + `appsettings.Development.json` (config) — MODIFY IN PLACE

**Existing `appsettings.json` Serilog block** (`appsettings.json:5-19`):
```json
"Serilog": {
  "MinimumLevel": {
    "Default": "Information",
    "Override": {
      "Microsoft": "Warning",
      "Microsoft.EntityFrameworkCore": "Warning",
      "System": "Warning"
    }
  },
  "WriteTo": [
    {
      "Name": "Console"
    }
  ]
}
```

**Replacement shape:** Add `"Using"` array (lists assemblies for reflection) and `"Enrich"` array (lists enricher names). Code shape: RESEARCH.md Pattern 6 verbatim.

**Existing `appsettings.Development.json` Serilog block** (`appsettings.Development.json:21-30`):
```json
"Serilog": {
  "MinimumLevel": {
    "Default": "Debug",
    "Override": {
      "Microsoft": "Information",
      "Microsoft.EntityFrameworkCore.Database.Command": "Information",
      "System": "Warning"
    }
  }
}
```

**Action:** Inherit the new `Using` + `Enrich` from `appsettings.json` (no override needed — `IConfiguration` merges arrays by index, but for arrays this is risky → safer to repeat the `Enrich` list in Development). Add a `WriteTo` with a more readable `outputTemplate` for dev, per RESEARCH.md Pattern 6 second-half ("readable plain-text dev template").

> **Critical pitfall** (RESEARCH.md Pitfall 2): Pairing `"WithEnvironmentName"` in `Enrich` requires `Serilog.Enrichers.Environment` to be in `Using`. Otherwise it's silently ignored.

---

### `Backend/src/TaxReader.Application/Commands/UploadReceiptFilesHandler.cs` (handler, request-response) — MODIFY IN PLACE

**Existing imports block** (`UploadReceiptFilesHandler.cs:1-8`):
```csharp
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using TaxReader.Application.DTOs;
using TaxReader.Application.Interfaces;
using TaxReader.Application.Mapping;
using TaxReader.Domain.Common;
using TaxReader.Domain.Entities;
using TaxReader.Domain.Enums;
```

**Add:** `using Serilog.Context;` to this block. Project-reference question — RESEARCH.md Open Q2 + Pattern 7 — the `Serilog` package is likely transitive but Application's csproj does NOT declare it directly. Plan should add `<PackageReference Include="Serilog" />` to `Backend/src/TaxReader.Application/TaxReader.Application.csproj` (and `<PackageVersion Include="Serilog" Version="..." />` to `Directory.Packages.props`).

**Existing per-file loop body** (`UploadReceiptFilesHandler.cs:49-157` — the entire `foreach (var file in command.Files)` block):
```csharp
foreach (var file in command.Files)
{
    using var ms = new MemoryStream();
    await file.Stream.CopyToAsync(ms, cancellationToken);
    ms.Position = 0;

    // Duplicate detection
    var hash = Convert.ToHexString(await SHA256.HashDataAsync(ms, cancellationToken));
    ...
    var receiptFile = new ReceiptFile { ... };
    dbContext.ReceiptFiles.Add(receiptFile);

    var run = new ProcessingRun { ... };
    dbContext.ProcessingRuns.Add(run);
    await dbContext.SaveChangesAsync(cancellationToken);

    try
    {
        // Step 1: Extract text from stream in memory
        run.Status = ProcessingStatus.Extracting;
        ...
    }
    catch (Exception ex)
    {
        await MarkFailedAsync(run, receiptFile, $"Processing failed: {ex.Message}");
        ...
    }
}
```

**Insertion site for `LogContext.PushProperty`** — wrap the `try { ... } catch { ... }` block (currently lines 104-156) in a `using (LogContext.PushProperty("ReceiptFileId", receiptFile.Id))`. Per CONTEXT.md D-18, the property must scope around the per-file processing block (extraction + parsing + classification). The `dbContext.ReceiptFiles.Add` and `ProcessingRun` insert can be inside or outside the scope; inside is preferred so log lines for the initial save also carry the ID. Code shape verbatim from RESEARCH.md Pattern 7.

**Important per Pitfall 4:** The `using` keyword is mandatory — `LogContext.PushProperty` returns `IDisposable` and missing the `using` leaks the property into unrelated requests via `AsyncLocal`.

---

### `Backend/src/TaxReader.Infrastructure/Configuration/AnthropicOptions.cs` (config POCO) — VERIFY

**Existing file** (already correct per CONTEXT.md D-02):
```csharp
namespace TaxReader.Infrastructure.Configuration;

public class AnthropicOptions
{
    public const string SectionName = "Anthropic";

    public string? ApiKey { get; set; }
    // Haiku is plenty for an 8-category classification choice — ~3-5× faster and ~10×
    // cheaper than Sonnet for this task. Override in appsettings for higher accuracy.
    public string Model { get; set; } = "claude-haiku-4-5";
    /// <summary>Tokens (credits) consumed per AI classification call.</summary>
    public int CostPerClassification { get; set; } = 1;
}
```

**Action:** No code change. The default value (`"claude-haiku-4-5"`) is the canonical source of truth (D-02). The downstream config files (`docker-compose.yml:38`, `.env.example:19`) need updates — see those entries below. Optional: update the comment `"8-category classification choice"` to reflect the actual category count if the planner is in this file anyway (low-risk one-word edit).

---

### `Backend/Directory.Packages.props` (config) — APPEND

**Existing relevant block** (lines 26-27):
```xml
<PackageVersion Include="Serilog.AspNetCore" Version="9.0.0" />
<PackageVersion Include="Serilog.Sinks.Console" Version="6.0.0" />
```

**Add new entries** (per RESEARCH.md Standard Stack):
```xml
<PackageVersion Include="Sentry.AspNetCore" Version="6.4.1" />
<PackageVersion Include="Serilog.Enrichers.Environment" Version="3.0.1" />
<PackageVersion Include="Microsoft.AspNetCore.Mvc.Testing" Version="10.0.4" />
<!-- Optional but recommended per RESEARCH.md Open Q2: -->
<PackageVersion Include="Serilog" Version="<match transitively-pulled version>" />
```

> **Version verification:** RESEARCH.md provides `curl` commands (lines 159-162) to confirm the latest stable versions before commit. Run on first task action.

> **`packages.lock.json`:** Per Pitfall 5, the cache strategy (Pattern 9) glob-includes `Backend/**/packages.lock.json` and `Backend/Directory.Packages.props`. With CPM, lock files don't auto-generate; planner should add `<RestorePackagesWithLockFile>true</RestorePackagesWithLockFile>` to `Backend/Directory.Build.props` and commit the resulting lock files (one-time, low-risk).

---

### `Backend/src/TaxReader.Api/TaxReader.Api.csproj` (config) — APPEND

**Existing block** (lines 10-23):
```xml
<ItemGroup>
  <PackageReference Include="Asp.Versioning.Http" />
  <PackageReference Include="FluentValidation.DependencyInjectionExtensions" />
  <PackageReference Include="Microsoft.AspNetCore.Authentication.JwtBearer" />
  <PackageReference Include="Microsoft.AspNetCore.OpenApi" />
  ...
  <PackageReference Include="Serilog.AspNetCore" />
  <PackageReference Include="Serilog.Sinks.Console" />
</ItemGroup>
```

**Add:**
```xml
<PackageReference Include="Sentry.AspNetCore" />
<PackageReference Include="Serilog.Enrichers.Environment" />
```

(No version — CPM resolves from `Directory.Packages.props`.)

---

### `Backend/src/TaxReader.Application/TaxReader.Application.csproj` (config) — APPEND

**Existing block** (lines 9-12):
```xml
<ItemGroup>
  <PackageReference Include="FluentValidation" />
  <PackageReference Include="Microsoft.EntityFrameworkCore" />
</ItemGroup>
```

**Add (per RESEARCH.md Open Q2):**
```xml
<PackageReference Include="Serilog" />
```

This may be redundant if `Serilog` flows transitively from `Serilog.AspNetCore` via `TaxReader.Api`'s reference chain — verify with `dotnet build` after adding `using Serilog.Context;` to `UploadReceiptFilesHandler.cs`. If build succeeds without it, remove and document in plan output.

---

### `Backend/tests/TaxReader.UnitTests/TaxReader.UnitTests.csproj` (config) — APPEND

**Existing block** (lines 10-18):
```xml
<ItemGroup>
  <PackageReference Include="coverlet.collector" />
  <PackageReference Include="Microsoft.NET.Test.Sdk" />
  <PackageReference Include="xunit" />
  <PackageReference Include="xunit.runner.visualstudio" />
  <PackageReference Include="FluentAssertions" />
  <PackageReference Include="Moq" />
  <PackageReference Include="Microsoft.EntityFrameworkCore.InMemory" />
</ItemGroup>
```

**Add:**
```xml
<PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" />
```

(Required by `CorsConfigurationTests` for `WebApplicationFactory<Program>`.)

---

### `Backend/.dockerignore` (config) — NEW

**Analog:** `Frontend/.dockerignore`

**Existing analog content** (`Frontend/.dockerignore`:1-7):
```
node_modules
.next
.git
.env*.local
npm-debug.log*
Dockerfile
.dockerignore
```

**Adapt for Backend:** Per RESEARCH.md Security Domain "Backend Dockerfile spot-check" (line 1042) — `Backend/Dockerfile` line 4 uses `COPY . .` which would copy any leaked `storage/` PDFs into the image. Add `.dockerignore` at `Backend/.dockerignore`:
```
bin
obj
src/TaxReader.Api/storage
**/build-diag*.txt
**/*.binlog
.git
.dockerignore
Dockerfile
```

The `bin`/`obj` exclusions are good hygiene; the `src/TaxReader.Api/storage` and `build-diag*.txt` / `*.binlog` are the load-bearing PII / artifact exclusions.

---

### `Frontend/instrumentation-client.ts` (config, event-driven) — NEW

**Analog:** none in repo (Phase 1 establishes the convention).

**Authoritative source:** RESEARCH.md Pattern 3 (lines 376-449). The shape is documented verbatim there with TypeScript-strict-friendly code. Two things planner must NOT skip:
1. The `if (process.env.NEXT_PUBLIC_SENTRY_ENABLED === "true")` gate around `Sentry.init` (D-16).
2. The `export const onRouterTransitionStart = Sentry.captureRouterTransitionStart;` line — Next.js 16 requires it for breadcrumb capture even when the feature flag is off.

**Frontend AGENTS.md warning** (`Frontend/AGENTS.md`):
> "This is NOT the Next.js you know. This version has breaking changes — APIs, conventions, and file structure may all differ from your training data. Read the relevant guide in `node_modules/next/dist/docs/` before writing any code."

This applies HARD here. The file MUST be `instrumentation-client.ts` (not `sentry.client.config.ts`) — verified in RESEARCH.md citation against Next.js 16 docs.

**TypeScript settings** (`Frontend/tsconfig.json`): `"strict": true`. The scrubber function in RESEARCH.md Pattern 3 satisfies strict null checks (uses `delete` + type-narrowing); copy verbatim.

---

### `Frontend/instrumentation.ts` + `sentry.server.config.ts` + `sentry.edge.config.ts` (config) — NEW

**Analog:** none (establishes new pattern).

**Authoritative source:** RESEARCH.md Pattern 3 (lines 451-502). All three files use the standard Sentry Next.js boilerplate. `instrumentation.ts` is the dispatch hook (`NEXT_RUNTIME` switch); `sentry.server.config.ts` and `sentry.edge.config.ts` are the runtime-specific init bodies.

**Note:** D-16 leaves frontend Sentry disabled in production until Phase 6, BUT the **server-side** init (`sentry.server.config.ts`) WILL fire if `SENTRY_DSN_FRONTEND_SERVER` is set, because server errors aren't browser PII. This matches CONTEXT.md D-16 ("Backend Sentry runs unconditionally"). For Phase 1, leave the server DSN env var unset in `docker-compose.yml`'s `web` service so even server-side capture is no-op.

**Optional (planner discretion):** Use the wizard `cd Frontend && npx @sentry/wizard@latest -i nextjs` to scaffold all four files automatically, then post-edit `instrumentation-client.ts` to add the `NEXT_PUBLIC_SENTRY_ENABLED` gate. RESEARCH.md line 522 endorses this.

---

### `Frontend/next.config.ts` (config) — MODIFY (wrap export)

**Existing file** (`next.config.ts`:1-46) — full file:
```typescript
import { networkInterfaces } from "node:os";
import type { NextConfig } from "next";

const apiUrl = process.env.BACKEND_API_URL ?? "http://localhost:5190";

function isPrivateIpv4(address: string) { ... }
function getAllowedDevOrigins() { ... }

const nextConfig: NextConfig = {
  output: "standalone",
  allowedDevOrigins: getAllowedDevOrigins(),
  async rewrites() {
    return [
      {
        source: "/api/v1/:path*",
        destination: `${apiUrl}/api/v1/:path*`,
      },
    ];
  },
};

export default nextConfig;
```

**Modify:** Replace the final `export default nextConfig;` with the conditional `withSentryConfig` wrap from RESEARCH.md Pitfall 6 (the safer of the two options):
```typescript
import { withSentryConfig } from "@sentry/nextjs";

// ... existing nextConfig ...

export default process.env.NEXT_PUBLIC_SENTRY_ENABLED === "true"
  ? withSentryConfig(nextConfig, {
      silent: true,
      org: process.env.SENTRY_ORG,
      project: process.env.SENTRY_PROJECT,
      // Source-map upload deferred per CONTEXT.md <deferred> — do NOT pass authToken.
    })
  : nextConfig;
```

**Critical:** The conditional form (vs always-wrap) is preferred per Pitfall 6 — `withSentryConfig` validates `org`/`project` at build time even when DSN is empty, so the unconditional wrap would require setting unused env vars in CI.

---

### `Frontend/package.json` (config) — APPEND

**Existing block** (lines 11-30, dependencies):
```json
"dependencies": {
  "@base-ui/react": "^1.3.0",
  "@hookform/resolvers": "^5.2.2",
  ...
  "zod": "^4.3.6"
},
```

**Add:**
```json
"@sentry/nextjs": "^10.51.0",
```

(insert alphabetically — between `@hookform/resolvers` and `@tanstack/react-query`.)

---

### `docker-compose.yml` (config) — MODIFY

**Existing `api` env block** (lines 28-41):
```yaml
environment:
  ASPNETCORE_ENVIRONMENT: Production
  ASPNETCORE_URLS: http://+:8080
  ConnectionStrings__DefaultConnection: Host=db;Port=5432;Database=belegpilot;...
  Jwt__Secret: ${JWT_SECRET}
  ...
  Anthropic__ApiKey: ${ANTHROPIC_API_KEY}
  Anthropic__Model: ${ANTHROPIC_MODEL:-claude-sonnet-4-5}        ← CHANGE
  Anthropic__MaxTokens: ${ANTHROPIC_MAX_TOKENS:-1024}              ← config-that-goes-nowhere (RESEARCH.md A7) — leave alone
  Anthropic__CostPerClassification: ${ANTHROPIC_COST_PER_CLASSIFICATION:-1}
  RUN_MIGRATIONS: "true"
```

**Modify** (D-02):
- Line 38: change `claude-sonnet-4-5` → `claude-haiku-4-5`.

**Add new env vars** (D-13/D-16, RESEARCH.md Pattern 2):
```yaml
Sentry__Dsn: ${SENTRY_DSN_BACKEND:-}
Sentry__Environment: production
```

**Existing `web` env block** (lines 52-54):
```yaml
environment:
  NODE_ENV: production
  BACKEND_API_URL: http://api:8080
```

**Add (D-16):**
```yaml
NEXT_PUBLIC_SENTRY_ENABLED: ${NEXT_PUBLIC_SENTRY_ENABLED:-false}
NEXT_PUBLIC_SENTRY_DSN: ${NEXT_PUBLIC_SENTRY_DSN:-}
```

The `${... :-false}` default ensures Phase 1 stays disabled even if `.env` is missing the var. Phase 6 flips this on after the consent banner ships.

---

### `.env.example` (config) — MODIFY

**Existing block** (lines 17-21):
```
# ── Anthropic AI ──────────────────────────────────────────────────────────────
ANTHROPIC_API_KEY=sk-ant-...
ANTHROPIC_MODEL=claude-sonnet-4-5      ← CHANGE
ANTHROPIC_MAX_TOKENS=1024
ANTHROPIC_COST_PER_CLASSIFICATION=1
```

**Modify:**
- Line 19: change `claude-sonnet-4-5` → `claude-haiku-4-5`.

**Append new section** (per RESEARCH.md Pattern 2 lines 360-368):
```
# ── Sentry ────────────────────────────────────────────────────────────────────
# Backend DSN — EU region (sentry.eu.io). Leave blank to disable.
SENTRY_DSN_BACKEND=

# Frontend Sentry — disabled in Phase 1 until TTDSG cookie banner lands (Phase 6).
# Frontend DSN is a public symbol (ships in JS bundle). Backend DSN is private.
NEXT_PUBLIC_SENTRY_ENABLED=false
NEXT_PUBLIC_SENTRY_DSN=
```

---

### `.gitignore` (config) — APPEND

**Existing file** (full):
```
.env
.env.local
*.env.local

# Runtime storage
Backend/storage/
storage/

# Tesseract trained data (binary, large — install separately)
*.traineddata
```

**Append (D-05):**
```
# Backend API project storage (PII receipt PDFs)
Backend/src/TaxReader.Api/storage/

# MSBuild diagnostic logs / binlogs
build-diag*.txt
*.binlog
```

The existing `Backend/storage/` and `storage/` rules don't cover the API project's nested `storage/` subdirectory — this is the path where the leaked PDFs live (per CONTEXT.md D-04).

---

### `CLAUDE.md` (docs) — APPEND

**Add Anthropic model documentation** (per RESEARCH.md Pattern 5 Action 5). Recommended location: existing `## Project` section near the constraints block (per CONTEXT.md "Claude's Discretion" #5):
```markdown
**Anthropic model:** `claude-haiku-4-5` is the production default — ~10× cheaper and ~3-5× faster than Sonnet, sufficient for the 13-DE-category classification choice. Single source of truth lives in `Backend/src/TaxReader.Infrastructure/Configuration/AnthropicOptions.cs`. Override per-environment via `Anthropic__Model` env var.
```

**Sentry pointer (optional, planner discretion):** A one-liner in the same Operations subsection pointing at `.planning/phases/01-foundation-cleanup-ci/` for the Sentry setup details.

> **Drift flag** (RESEARCH.md line 970): `CLAUDE.md` line 4 still says "BelegPilot is an API-first system…" — this is an unrelated rebrand drift. Plan should leave it alone unless explicitly fixing rebrand strings (out of scope for Phase 1 per `<deferred>`).

---

### `README.md` (repo root) — NEW

**Analog:** `Backend/README.md` (`Backend/README.md:1-136`).

**Why this analog:** Same project, same audience (developers), same English convention. The existing Backend README has the right voice (concise, command-oriented) but is scoped to the .NET API. The repo-root README needs to introduce the full stack (frontend + backend + Caddy + Postgres) and be the single entry point.

**Tagline + Scope shape** (`Backend/README.md:1-19`) — adapt for the whole project:
```markdown
# TaxReader

TaxReader is an API-first .NET 10 project for uploading PDF receipts, parsing text-based receipts, classifying expense items, and calculating yearly category totals.

## Scope of this version

This first version focuses on:
- ...
```

**Run-with-Docker shape** (`Backend/README.md:61-69`):
```markdown
## Run with Docker Compose

\`\`\`bash
docker compose up --build
\`\`\`

The API will be available at:
- `http://localhost:8080`
```

**For repo-root README content** per CONTEXT.md D-12:
- Project tagline (one sentence)
- Prerequisites: .NET 10 SDK, Node 22+, Docker Desktop, Tesseract for non-container dev
- Quick start: `cp .env.example .env`, edit secrets, `docker compose up --build`, browse to `https://localhost`
- Links to `CLAUDE.md` + `.planning/codebase/` for deeper docs
- No screenshots

**Avoid duplicating** content from `Backend/README.md` and `Frontend/README.md` — link to them instead.

---

### `.github/workflows/ci.yml` (config, event-driven) — NEW

**Analog:** none (establishes new pattern).

**Authoritative source:** RESEARCH.md Pattern 8 (hygiene-check job) + Pattern 9 (full workflow skeleton). Both are verbatim-copyable.

**Critical structure:**
- 3 parallel jobs: `hygiene-check`, `backend-build-test`, `frontend-lint-build`
- Triggers: `pull_request: branches: [main]` + `push: branches: [main]`
- Concurrency group: `${{ github.workflow }}-${{ github.ref }}` with `cancel-in-progress: ${{ github.event_name == 'pull_request' }}` (cancel for PRs, never cancel main)
- `actions/setup-dotnet@v4` cache-dependency-path includes `Directory.Packages.props` per Pitfall 5
- `actions/setup-node@v4` cache `'npm'` keyed on `Frontend/package-lock.json`

**Branch protection** (D-10) — manual GitHub UI step (not in workflow file): require all 3 job names as required status checks on `main`; PRs required; no required reviewers (solo dev).

---

## Shared Patterns

### Pattern S1: File-scoped namespaces (always)

**Source:** `Backend/Directory.Build.props` (global setting) + every existing C# file.
**Apply to:** All new C# files (`SentryScrubbing.cs`, `AnthropicOptionsTests.cs`, `CorsConfigurationTests.cs`, `SentryScrubbingTests.cs`, `SerilogEnrichmentTests.cs`).

**Excerpt** (`OcrTextNormalizer.cs:3`):
```csharp
namespace TaxReader.Infrastructure.Services;
```

**Never** use block-scoped (`namespace Foo { ... }`) — drives a build warning under `<AnalysisLevel>latest</AnalysisLevel>`.

---

### Pattern S2: Structured logging with named placeholders

**Source:** CONVENTIONS.md "Logging" section + `ClaudeAiClassifier.cs` examples.
**Apply to:** `Program.cs` startup-log line (D-02), CORS warning (D-07), any future log lines this phase adds.

**Correct shape** (CONVENTIONS.md):
```csharp
logger.LogWarning("Anthropic API returned {Status}: {Body}", response.StatusCode, body);
```

**Wrong shape** (never used):
```csharp
logger.LogWarning($"Anthropic API returned {response.StatusCode}: {body}");  // ❌ string interpolation
```

---

### Pattern S3: `IOptions<T>` for typed configuration

**Source:** `Backend/src/TaxReader.Infrastructure/Configuration/JwtOptions.cs` + `AnthropicOptions.cs` + `TesseractOptions.cs`.
**Apply to:** Reading `AnthropicOptions` for the startup-log line (D-02). **NOT** Sentry — Sentry config flows through `SentryAspNetCoreOptions` directly via the `UseSentry(o => ...)` callback; no separate POCO needed (CONTEXT.md `<code_context>` "Established Patterns").

**Excerpt** (`JwtOptions.cs:1-12`):
```csharp
namespace TaxReader.Infrastructure.Configuration;

public class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Secret { get; set; } = string.Empty;
    ...
}
```

**Reading shape** (`Program.cs:39-41`, existing JWT pattern — copy for Anthropic startup-log):
```csharp
var jwtSection = builder.Configuration.GetSection(JwtOptions.SectionName);
builder.Services.Configure<JwtOptions>(jwtSection);
var jwtOptions = jwtSection.Get<JwtOptions>() ?? new JwtOptions();
```

For the post-build read (D-02 startup log):
```csharp
var resolvedAnthropicOptions = app.Services
    .GetRequiredService<Microsoft.Extensions.Options.IOptions<AnthropicOptions>>().Value;
app.Logger.LogInformation(
    "Anthropic configuration resolved: Model={Model}, CostPerClassification={Cost}",
    resolvedAnthropicOptions.Model,
    resolvedAnthropicOptions.CostPerClassification);
```

---

### Pattern S4: Test class shape — Theory + InlineData + FluentAssertions

**Source:** `Backend/tests/TaxReader.UnitTests/Infrastructure/Services/OcrTextNormalizerTests.cs`.
**Apply to:** All Phase 1 tests on pure helpers (`SentryScrubbingTests`, `AnthropicOptionsTests`).

**Excerpt** (`OcrTextNormalizerTests.cs:8-13`):
```csharp
[Theory]
[InlineData("", "")]
[InlineData("   ", "   ")]
public void Normalize_EmptyOrWhitespace_ReturnsUnchanged(string input, string expected)
{
    OcrTextNormalizer.Normalize(input).Should().Be(expected);
}
```

**Naming convention:** `Method_Scenario_Result` (CONVENTIONS.md / `CLAUDE.md`).

---

### Pattern S5: Test fixture shape — IDisposable constructor + Dispose

**Source:** `Backend/tests/TaxReader.UnitTests/Application/Commands/ConfirmClassificationHandlerTests.cs`.
**Apply to:** Phase 1 tests that build a stateful rig (`CorsConfigurationTests` with `WebApplicationFactory<Program>`, `SerilogEnrichmentTests` with handler scope).

**Constructor + Dispose pair** (`ConfirmClassificationHandlerTests.cs:21-31, 67`):
```csharp
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
...
public void Dispose() => _dbContext.Dispose();
```

For `WebApplicationFactory<Program>`-based tests, replace `AppDbContext` with `WebApplicationFactory<Program>` and dispose it in `Dispose()`. The `Program` class is automatically referenced as long as `TaxReader.Api` is a `ProjectReference` of the test project (it already is — see `TaxReader.UnitTests.csproj:25`).

---

### Pattern S6: `Async` suffix + `CancellationToken` on every async method

**Source:** Every handler in `Backend/src/TaxReader.Application/Commands/` and `Queries/`.
**Apply to:** No new async methods are added in Phase 1 — but if planner adds e.g. an async helper to `SentryScrubbing`, the suffix is mandatory.

**Excerpt** (`UploadReceiptFilesHandler.cs:35-37`):
```csharp
public async Task<Result<UploadReceiptFilesResponse>> HandleAsync(
    UploadReceiptFilesCommand command,
    CancellationToken cancellationToken = default)
```

`SentryScrubbing.Scrub` is **synchronous** (returns `SentryEvent?`) — that's correct because `BeforeSend` is synchronous in the SDK.

---

## No Analog Found

Files with no close match in the codebase (planner relies on RESEARCH.md patterns instead):

| File | Role | Data Flow | Reason |
|------|------|-----------|--------|
| `.github/workflows/ci.yml` | CI/CD config | event-driven | First CI workflow in repo. RESEARCH.md Pattern 9 is the source of truth. |
| `Frontend/instrumentation-client.ts` | Next.js convention file | event-driven | First Sentry integration. RESEARCH.md Pattern 3 (lines 376-449). Critical: filename is **NOT** `sentry.client.config.ts` — Next.js 16 deprecated that name. |
| `Frontend/instrumentation.ts` | Next.js convention file | event-driven | First Sentry integration. RESEARCH.md Pattern 3 (lines 451-466). |
| `Frontend/sentry.server.config.ts` | Sentry server init | event-driven | First Sentry integration. RESEARCH.md Pattern 3 (lines 468-488). |
| `Frontend/sentry.edge.config.ts` | Sentry edge init | event-driven | First Sentry integration. RESEARCH.md Pattern 3 (lines 490-502). |
| `README.md` (repo root) | top-level docs | static | `Backend/README.md` and `Frontend/README.md` exist (subsystem-scoped); the root file is structurally new. Borrow voice + sections from `Backend/README.md`. |
| `Backend/.dockerignore` | Docker config | static | `Frontend/.dockerignore` exists at the same layer position; copy the structure but populate with backend-specific exclusions (PII storage paths, MSBuild diagnostic files). |
| `CLAUDE.md` Operations subsection | docs section | static | New subsection. Append per RESEARCH.md Pattern 5 Action 5. |

---

## Metadata

**Analog search scope:**
- `Backend/src/TaxReader.Api/`
- `Backend/src/TaxReader.Application/Commands/`
- `Backend/src/TaxReader.Infrastructure/Services/`
- `Backend/src/TaxReader.Infrastructure/Configuration/`
- `Backend/tests/TaxReader.UnitTests/`
- `Frontend/` (root config files only — no new component files in Phase 1)
- Repo root (`docker-compose.yml`, `.gitignore`, `.env.example`, `CLAUDE.md`)

**Files scanned:** ~25 source files, ~10 test files, 4 config files, 2 documentation files.

**Pattern extraction date:** 2026-05-04

**Key insight for planner:**
- Phase 1 is **80% configuration / 20% code**. Most patterns to copy are config shapes, not algorithm shapes.
- The two non-trivial code additions (`SentryScrubbing.cs` + `LogContext.PushProperty` in `UploadReceiptFilesHandler.cs`) have strong analogs (`OcrTextNormalizer.cs` + the existing handler structure).
- The four "establishes new pattern" files (`ci.yml`, the 4 Sentry frontend configs) are 100% governed by RESEARCH.md Patterns 3, 8, 9 — planner can paste verbatim.
- No analog needed for the **disk-only delete** of `Backend/src/TaxReader.Api/storage/2026/04/` — it's a `rm -rf` on the filesystem, plus `.gitignore` and CI hygiene-check belt-and-suspenders.

---

*Phase: 01-foundation-cleanup-ci*
*Pattern mapping completed: 2026-05-04*
