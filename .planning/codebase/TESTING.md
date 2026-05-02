# Testing

**Analysis Date:** 2026-04-29

## Frameworks & Tools

### Backend
- **Test runner:** xUnit 2.9.2 (`xunit.runner.visualstudio` 2.8.2)
- **Assertions:** FluentAssertions 7.0.0 (`result.Should().Be(...)`, `.Should().HaveCount(...)`)
- **Mocks:** Moq 4.20.72
- **In-memory DB:** `Microsoft.EntityFrameworkCore.InMemory` 10.0.4
- **Test SDK:** `Microsoft.NET.Test.Sdk` 17.12.0
- **Coverage:** `coverlet.collector` 6.0.4 (no enforced threshold; report not consumed by CI)
- **Test project:** `Backend/tests/TaxReader.UnitTests/TaxReader.UnitTests.csproj`

### Frontend
- **None.** No test framework configured in `Frontend/package.json`. No `__tests__/` directories, no `*.test.ts(x)` files, no Vitest/Jest/Playwright/Cypress dependencies.
- See `CONCERNS.md` for the full implication.

## Run Commands

```bash
dotnet test Backend                              # all tests
dotnet test Backend --filter FullyQualifiedName~UploadReceiptFilesHandlerTests
dotnet test Backend /p:CollectCoverage=true     # with coverage
```

## Test Layout

Tests mirror the production folder structure under `Backend/tests/TaxReader.UnitTests/`:

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
└── TestDataFactory.cs
Infrastructure/
├── Parsers/
│   ├── AmazonParserTests.cs
│   ├── EdukiParserTests.cs
│   └── GenericParserTests.cs
└── Services/
    └── OcrTextNormalizerTests.cs
```

## Naming Convention

`Method_Scenario_Result` (per `CLAUDE.md`). Examples drawn from `UploadReceiptFilesHandlerTests.cs`:

- `HandleAsync_ValidPdf_ProcessesAndReturnsReceiptDto`
- `HandleAsync_DuplicateFile_ReportsFailureWithoutAbortingBatch`
- `HandleAsync_NonSuccessfulDuplicateFile_RetrySucceeds`
- `HandleAsync_EmptyExtractedText_MarksFailedAndContinues`
- `HandleAsync_NoMatchingParser_MarksFailedAndContinues`
- `HandleAsync_ImageFile_UsesImageExtractor`
- `HandleAsync_BatchWithMixedOutcomes_ReturnsSuccessesAndFailures`

## Test Anatomy

### Standard handler test class shape
From `Backend/tests/TaxReader.UnitTests/Application/Commands/UploadReceiptFilesHandlerTests.cs`:

```csharp
public class UploadReceiptFilesHandlerTests : IDisposable
{
    private static readonly Guid TestUserId = Guid.Parse("aaaa...");

    private readonly AppDbContext _dbContext;
    private readonly Mock<IPdfTextExtractor> _pdfExtractorMock;
    private readonly Mock<IImageTextExtractor> _imageExtractorMock;
    private readonly Mock<IReceiptParser> _parserMock;
    private readonly Mock<IClassificationService> _classificationMock;
    private readonly Mock<ICurrentUser> _currentUserMock;
    private readonly UploadReceiptFilesHandler _handler;

    public UploadReceiptFilesHandlerTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContext = new AppDbContext(options);
        // ... set up mocks ...
        _currentUserMock.Setup(u => u.UserId).Returns(TestUserId);

        _handler = new UploadReceiptFilesHandler(
            _dbContext, _pdfExtractorMock.Object, ...);
    }

    [Fact]
    public async Task HandleAsync_ValidPdf_ProcessesAndReturnsReceiptDto()
    {
        SetupSuccessfulPipeline("Amazon.de Invoice 2025 Tinte EUR 9.99", "Amazon");

        var command = MakeCommand("test.pdf");
        var result = await _handler.HandleAsync(command);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Successful.Should().HaveCount(1);
        result.Value!.Successful[0].Receipt.Vendor.Should().Be("Amazon");

        var savedFile = await _dbContext.ReceiptFiles.FirstAsync();
        savedFile.Status.Should().Be(FileStatus.Processed);
    }

    public void Dispose() => _dbContext.Dispose();
}
```

**Key elements:**
- Constructor as setup (xUnit creates a fresh instance per test)
- `IDisposable` for `_dbContext.Dispose()` cleanup
- Fresh in-memory DB per test (`Guid.NewGuid().ToString()` as DB name)
- Private `SetupSuccessfulPipeline` helper to default mocks for the happy path
- Private `MakeCommand` factory for command construction

### Theories with `[InlineData]`
Used for parameterized scenarios:
```csharp
[Theory]
[InlineData(FileStatus.Failed)]
[InlineData(FileStatus.Processing)]
[InlineData(FileStatus.Uploaded)]
public async Task HandleAsync_NonSuccessfulDuplicateFile_RetrySucceeds(FileStatus stuckStatus)
```

### `TestDataFactory` (`Backend/tests/TaxReader.UnitTests/Helpers/TestDataFactory.cs`)
Static factory with sensible defaults; every parameter optional with named overrides. Example:
```csharp
public static Receipt CreateReceipt(
    Guid? id = null,
    Guid? receiptFileId = null,
    string vendor = "TestVendor",
    DateOnly? purchaseDate = null,
    decimal totalAmount = 29.99m)
```
Provides `CreateReceiptFile`, `CreateReceipt`, `CreateReceiptItem`, `CreateClassification`, `CreateRule`. Tests prefer the factory over inline construction so refactors of the entity surface stay in one place.

## Mocking Patterns

### Moq with `It.IsAny<>` defaults
```csharp
_pdfExtractorMock
    .Setup(e => e.ExtractTextAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
    .ReturnsAsync(extractedText);
```

### Per-call factory return for stateful entities
EF tracks identity, so returning the same `Receipt` instance from multiple `parser.Parse(...)` calls in a batch test breaks the second save. The fix:
```csharp
_parserMock.Setup(p => p.Parse(It.IsAny<string>(), It.IsAny<ReceiptFile>()))
    .Returns(() =>
    {
        var r = TestDataFactory.CreateReceipt(vendor: vendor);
        r.Items.Add(TestDataFactory.CreateReceiptItem());
        return r;
    });
```
The lambda fires per call → fresh `Guid` each time. Pattern used in `UploadReceiptFilesHandlerTests.SetupSuccessfulPipeline`.

### Verifying call counts
```csharp
_imageExtractorMock.Verify(
    e => e.ExtractTextAsync(It.IsAny<Stream>(), "image/jpeg", It.IsAny<CancellationToken>()),
    Times.Once);
_pdfExtractorMock.Verify(
    e => e.ExtractTextAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()),
    Times.Never);
```

## Database Strategy: In-Memory

Every handler test uses `UseInMemoryDatabase(Guid.NewGuid().ToString())`. Trade-offs:

**Pros:**
- Fast (no docker/postgres dependency for unit tests)
- Isolated per test (unique DB name per instance)
- No data cleanup needed

**Cons / known limitations:**
- In-memory provider does not enforce relational constraints exactly the same as PostgreSQL (no real FK enforcement; cascade delete behavior approximate)
- Snake-case naming convention does not change behavior here, but provider differences could mask issues only seen in real Postgres
- Migrations are not applied — schema is built from model metadata

There are **no integration tests** that hit real PostgreSQL. The `CONCERNS.md` document calls out the gap.

## Coverage Picture

### Covered well
- **Domain entities:** `ReceiptFileTests`, `ReceiptItemTests`, `ReceiptTests`, `ResultTests` — Domain layer is small and pure, easy to cover
- **Validators:** every validator has a corresponding `*Tests.cs`
- **Parsers:** `AmazonParserTests`, `EdukiParserTests`, `GenericParserTests` — text-fixture-driven `CanParse` and `Parse` tests
- **Upload pipeline:** `UploadReceiptFilesHandlerTests` exercises happy path, duplicates, retry-on-stuck, missing text, no parser, image vs PDF, mixed-outcome batching
- **Critical queries:** `GetAnnualSummaryHandlerTests`, `GetCategoryTotalsHandlerTests`
- **Confirm flow:** `ConfirmClassificationHandlerTests`
- **Mapping extensions:** `DtoMappingExtensionsTests`
- **OCR text normalizer:** `OcrTextNormalizerTests`

### Not covered
- **`AuthService`** — registration, login, refresh, BCrypt verification, refresh-token rotation: no test file
- **`ClaudeAiClassifier`** — Anthropic HTTP client, JSON parsing, fallback-on-malformed: no test file
- **`AiOnlyClassificationService`** — token pre-charge, refund-on-Unknown, refund-on-failure, auto-confirm threshold logic: no test file
- **`TokenService`** — atomic ledger operations: no test file
- **`PdfPigTextExtractor`** — bounding-box-line-reconstruction algorithm: no test file
- **`TesseractImageTextExtractor`** — singleton/locking behavior: no test file
- **`PdfExportService`** / **`CsvExportService`** — export formatting: no test file
- **Endpoint integration** — no `WebApplicationFactory<TEntryPoint>` tests; HTTP layer is uncovered
- **Frontend** — zero tests across all components, hooks, the api-client, and pages

## Running a Single Test

```bash
dotnet test Backend --filter FullyQualifiedName~UploadReceiptFilesHandlerTests.HandleAsync_ValidPdf_ProcessesAndReturnsReceiptDto
```

## CI

No CI configuration files detected at the repo root (`.github/workflows/` does not exist; no `azure-pipelines.yml`, no `.gitlab-ci.yml`). Tests are run only on developer machines today. See `CONCERNS.md` for the implication.

---

*Testing analysis: 2026-04-29*
