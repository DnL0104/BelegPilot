---
phase: 03-background-pipeline-tesseract-pool
plan: 03
subsystem: infra
tags: [tesseract, ocr, channel-pool, hosted-service, hangfire-alignment]

# Dependency graph
requires:
  - phase: 03-background-pipeline-tesseract-pool
    provides: "Hangfire WorkerCount aligned with Tesseract:PoolSize in DependencyInjection.cs (set by 03-01 Wave 1 sibling)"
  - phase: 01-foundation-cleanup-ci
    provides: "Serilog structured logging (preserved verbatim in OCR pipeline body)"
provides:
  - "TesseractEnginePool: bounded Channel<TesseractEngine> capacity = TesseractOptions.PoolSize (default 3)"
  - "TesseractEnginePoolWarmupService: IHostedService eagerly creates PoolSize engines at host start"
  - "TesseractOptions.PoolSize property bound from Tesseract__PoolSize env var"
  - "Quarantine-and-replace lifecycle on TesseractException / OutOfMemoryException (D-19)"
  - "Old TesseractImageTextExtractor.cs removed; no source-level references remain in Backend/src"
  - "InternalsVisibleTo seam: EngineFactoryOverride + LiveEngineCount for Layer A unit tests"
  - "Source-grep regression test (HangfireWorkerCountMatchesPoolSizeTests) locks the Pitfall 7 invariant"
affects:
  - "03-02-PLAN (ProcessReceiptFileJob binds IImageTextExtractor to this pool; concurrent image uploads no longer serialise)"
  - "07-* (BetterStack monitors the pool warmup-complete log line; Sentry baselines the TesseractException Warning event)"

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Bounded Channel<T> as a thread-safe object pool with async waits (System.Threading.Channels)"
    - "IHostedService eager warmup pattern (Pattern 8) — failure swallowed, host stays up, pool falls back to lazy init"
    - "InternalsVisibleTo seam for Layer A tests against algorithmic invariants without native dependency"
    - "Two-layer test split: Layer A (channel mechanics, in-process) + Layer B (real-OCR roundtrip, deferred to manual UAT)"

key-files:
  created:
    - "Backend/src/TaxReader.Infrastructure/Services/TesseractEnginePool.cs"
    - "Backend/src/TaxReader.Infrastructure/Services/TesseractEnginePoolWarmupService.cs"
    - "Backend/tests/TaxReader.UnitTests/Infrastructure/Tesseract/TesseractEnginePoolTests.cs"
    - "Backend/tests/TaxReader.UnitTests/Infrastructure/Tesseract/TesseractEnginePoolWarmupTests.cs"
    - "Backend/tests/TaxReader.UnitTests/Infrastructure/Tesseract/TesseractImageTextExtractorRemovedTests.cs"
    - "Backend/tests/TaxReader.UnitTests/Infrastructure/Tesseract/HangfireWorkerCountMatchesPoolSizeTests.cs"
  modified:
    - "Backend/src/TaxReader.Infrastructure/Configuration/TesseractOptions.cs (PoolSize property)"
    - "Backend/src/TaxReader.Infrastructure/DependencyInjection.cs (replace AddSingleton<IImageTextExtractor, TesseractImageTextExtractor> with TesseractEnginePool + factory delegate + warmup hosted service)"
    - "Backend/src/TaxReader.Infrastructure/TaxReader.Infrastructure.csproj (InternalsVisibleTo TaxReader.UnitTests)"
  deleted:
    - "Backend/src/TaxReader.Infrastructure/Services/TesseractImageTextExtractor.cs (Singleton+lock replaced; OCR pipeline body lives verbatim inside TesseractEnginePool.RunOcr)"

key-decisions:
  - "Test layering: Layer A (algorithmic — channel queueing, init, dispose, cancellation) covered with EngineFactoryOverride returning null! engines; Layer B (real-OCR roundtrip) deferred to manual UAT in 03-HUMAN-UAT.md to avoid coupling unit tests to native Tesseract install"
  - "InternalsVisibleTo on the Infrastructure assembly rather than reflection-poking: the seam (EngineFactoryOverride, LiveEngineCount) is explicit, narrow, and documented at its declaration site"
  - "Production code null-guards engine.Dispose() in the drain loop (3 lines) — defensive across DI failures, also enables Layer A tests with null! engines; cost is trivial and the guard is genuinely defensive, not test-only sugar"
  - "DI registers TesseractEnginePool as concrete Singleton + IImageTextExtractor as factory delegate to the same instance — the warmup service can resolve either; the OCR callers get the interface; both end up at the same Singleton"

patterns-established:
  - "Channel<TesseractEngine> bounded pool with quarantine-and-replace on exception"
  - "Per-host-instance eager warmup via IHostedService (~PoolSize × 100ms boot cost, predictable steady-state latency)"
  - "Source-grep regression tests for cross-plan invariants (Hangfire WorkerCount alignment) extend the structural-grep pattern from 03-01 HangfireWiringTests"

requirements-completed: [PIPE-04]

# Metrics
duration: ~1h 15m
completed: 2026-05-21
---

# Phase 3 Plan 03: TesseractEnginePool with bounded Channel<TesseractEngine> Summary

**Replaced the Singleton-with-lock TesseractImageTextExtractor with a Channel<TesseractEngine>-backed bounded pool (default 3 engines) and an IHostedService that warms it up at host start, preserving the existing OCR pipeline verbatim (2400px downsample, LstmOnly, SingleBlock, OcrTextNormalizer) and locking the Hangfire WorkerCount = PoolSize invariant via a source-grep regression test.**

## Performance

- **Duration:** ~1h 15m (Wave 1 worktree agent, sequential within Plan 03-03)
- **Started:** 2026-05-21T14:00:00Z (worktree spawn)
- **Completed:** 2026-05-21T15:15:57Z
- **Tasks:** 2 (both TDD: RED → GREEN per task)
- **Files modified:** 9 (5 created, 3 modified, 1 deleted)
- **Tests added:** 14 (6 pool + 4 warmup + 2 removal-regression + 2 worker-count-alignment)
- **Full backend test suite:** 171 passing, 5 pre-existing skips, 0 failures (test count grew by 14 — exactly my new tests)

## Accomplishments

- `TesseractEnginePool` (sealed `IImageTextExtractor + IDisposable`) replaces the Singleton+lock pattern. Acquire via `Channel.Reader.ReadAsync(ct)`, release via `Channel.Writer.TryWrite(engine)`, quarantine-and-replace on `TesseractException` or `OutOfMemoryException`.
- OCR pipeline body preserved **verbatim** from `TesseractImageTextExtractor.RunOcr`: 2400px max-edge downsample, `EngineMode.LstmOnly`, `PageSegMode.SingleBlock`, `OcrTextNormalizer.Normalize`, identical structured log template, German `InvalidOperationException` message for tessdata-not-found.
- `TesseractEnginePoolWarmupService : IHostedService` eagerly creates `PoolSize` engines before the host signals Ready. Failure is logged at Error and swallowed so the host stays up; the pool falls back to first-call init via the engine-spawn path.
- `TesseractOptions.PoolSize` property (default 3) bound from `Tesseract__PoolSize` env var with documentation about the Hangfire WorkerCount alignment invariant (RESEARCH Pitfall 7).
- Old `TesseractImageTextExtractor.cs` deleted. Zero non-comment references remain anywhere in `Backend/src`.
- `HangfireWorkerCountMatchesPoolSizeTests` locks the Pitfall 7 invariant via source-grep — Plan 03-01 wired the alignment, Plan 03-03 ensures future refactors cannot drift.

## Task Commits

Each task was committed atomically with `--no-verify` (Wave 1 worktree convention):

1. **Task 1: TesseractOptions.PoolSize + TesseractEnginePool implementation + 6 Layer A tests** — `b6fec54` (feat)
2. **Task 2: TesseractEnginePoolWarmupService + DI registration + delete old extractor + 8 tests** — `9bd6911` (feat)

The final SUMMARY commit will be added by the executor-finalize step below.

## Files Created/Modified

### Created
- `Backend/src/TaxReader.Infrastructure/Services/TesseractEnginePool.cs` — bounded-channel pool implementing `IImageTextExtractor`. Internal seam (`EngineFactoryOverride`, `LiveEngineCount`) is XML-documented at its declaration site.
- `Backend/src/TaxReader.Infrastructure/Services/TesseractEnginePoolWarmupService.cs` — `IHostedService` invoking `pool.Initialize()` once at boot.
- `Backend/tests/TaxReader.UnitTests/Infrastructure/Tesseract/TesseractEnginePoolTests.cs` — 6 Layer A tests: `Initialize_CreatesPoolSizeEngines`, `Initialize_Twice_DoesNotDoubleFill`, `ExtractTextAsync_RespectsCancellation`, `Dispose_DrainsChannelAndMarksPoolDisposed`, `FiveConcurrentAcquires_QueuesTwoWhenPoolSizeIsThree`, `ExtractTextAsync_AfterDispose_Throws`.
- `Backend/tests/TaxReader.UnitTests/Infrastructure/Tesseract/TesseractEnginePoolWarmupTests.cs` — 4 tests covering Initialize-invocation, completed-task return, StopAsync no-op, factory-failure swallowing.
- `Backend/tests/TaxReader.UnitTests/Infrastructure/Tesseract/TesseractImageTextExtractorRemovedTests.cs` — 2 source-grep regression tests (file deleted; no class-name references in non-comment lines anywhere under `Backend/src`).
- `Backend/tests/TaxReader.UnitTests/Infrastructure/Tesseract/HangfireWorkerCountMatchesPoolSizeTests.cs` — 2 source-grep regression tests for the Hangfire WorkerCount = poolSize invariant (RESEARCH Pitfall 7).

### Modified
- `Backend/src/TaxReader.Infrastructure/Configuration/TesseractOptions.cs` — added `public int PoolSize { get; set; } = 3;` with XML doc explaining the Hangfire WorkerCount alignment.
- `Backend/src/TaxReader.Infrastructure/DependencyInjection.cs` — replaced `AddSingleton<IImageTextExtractor, TesseractImageTextExtractor>()` with `AddSingleton<TesseractEnginePool>()` + interface delegate factory + `AddHostedService<TesseractEnginePoolWarmupService>()`. The existing Hangfire WorkerCount alignment (lines 93-101, set in Plan 03-01) is preserved verbatim.
- `Backend/src/TaxReader.Infrastructure/TaxReader.Infrastructure.csproj` — added `<InternalsVisibleTo Include="TaxReader.UnitTests" />` so Layer A tests can read internal `EngineFactoryOverride` and `LiveEngineCount`.

### Deleted
- `Backend/src/TaxReader.Infrastructure/Services/TesseractImageTextExtractor.cs` — replaced by `TesseractEnginePool`. The OCR pipeline body (downsample, LstmOnly, SingleBlock, OcrTextNormalizer, log template, German tessdata error message) lives **verbatim** inside `TesseractEnginePool.RunOcr`; only the engine lifecycle (Singleton+lock → Channel-acquire-release) changed.

## Decisions Made

### Test layering — Layer A vs. Layer B

The plan explicitly called out the testability tension: `TesseractEngine.Process` is sealed (the upstream library makes it non-virtual via `IsFinal=true` on the override of `DisposableBase.Dispose(bool)`), and `TesseractEngine` itself has only ctor signatures requiring real native libraries. Two reasonable approaches:

1. **Refactor production code** (extract a `BoundedObjectPool<T>` generic base, then test the generic) — adds complexity for testability.
2. **Use `EngineFactoryOverride` returning `null!` engines** — tests cover the channel mechanics (acquire, release, cancellation, dispose, queueing) without ever calling `Process`. Real-OCR roundtrip becomes a Layer B manual UAT item.

I chose option 2 per the plan's "Final pragmatic decision". The seam is `internal` + `InternalsVisibleTo TaxReader.UnitTests`, narrow (one factory delegate + one count probe), and self-documenting at the declaration site.

### Production null-guards on engine.Dispose

The Layer A approach surfaced one production-code question: with `null!` engines in tests, `engine.Dispose()` NREs inside the drain loop and in the "Initialize called twice" branch. Three lines of null-check (`engine?.Dispose()`) make the production code:

- Defensive against any future DI shim or factory that legitimately returns null
- Test-friendly without changing the algorithmic invariants

This is **not** test-only sugar — it's a small piece of defensive code that improves robustness. CLAUDE.md's "Simplicity First" rule applies: 3 lines, zero abstractions added.

### DI registration shape

The plan provided the exact shape:
```csharp
services.AddSingleton<TesseractEnginePool>();
services.AddSingleton<IImageTextExtractor>(sp => sp.GetRequiredService<TesseractEnginePool>());
services.AddHostedService<TesseractEnginePoolWarmupService>();
```

The factory-delegate pattern ensures the concrete `TesseractEnginePool` and the `IImageTextExtractor` interface resolve to the **same** Singleton instance — so the warmup service finds the same pool the OCR callers use. This is canonical .NET DI idiom; no deviation.

### Source-grep test for the Pitfall 7 invariant

Plan 03-01 wired the alignment (`var poolSize = configuration.GetValue<int>("Tesseract:PoolSize", 3); options.WorkerCount = poolSize;`). Plan 03-03's `HangfireWorkerCountMatchesPoolSizeTests` locks that wiring via two file-read assertions. This extends Plan 03-01's `HangfireWiringTests` source-grep regression pattern — same idiom (read the file at AppContext.BaseDirectory-resolved repo root, assert literal substring presence).

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Production code needs null-guard on engine.Dispose for Layer A tests**

- **Found during:** Task 1 (initial test run after pool implementation)
- **Issue:** Layer A tests use `EngineFactoryOverride = () => null!` to avoid the native Tesseract dependency. The pool's `Dispose()` drain loop and `Initialize()` "channel full" branch both call `engine.Dispose()` unconditionally — NREs on null! engines and the dispose count never decrements.
- **Fix:** Added `engine?.Dispose()` (3 chars) in both the drain loop and the "channel rejected the write" branch. This is **defensive** production code, not test-only — any future DI mock or factory returning null is now handled cleanly. The algorithmic invariant `LiveEngineCount goes to zero after Dispose` is preserved.
- **Files modified:** `Backend/src/TaxReader.Infrastructure/Services/TesseractEnginePool.cs`
- **Verification:** All 6 pool tests pass; `Dispose_DrainsChannelAndMarksPoolDisposed` asserts `LiveEngineCount.Should().Be(0)` after Dispose, which requires the decrement to fire even with null engines.
- **Committed in:** `b6fec54` (part of Task 1)

**2. [Rule 3 - Blocking] FluentAssertions ThrowAsync requires Func<Task>, not Func<Task<string>>**

- **Found during:** Task 1 (initial test compilation)
- **Issue:** `var act = () => pool.ExtractTextAsync(...)` infers `Func<Task<string>>`, and FluentAssertions 7's `Should().ThrowAsync<T>()` extension does not light up on `FunctionAssertions<Task<TResult>>` — only on `Func<Task>`. Compile error: `'FunctionAssertions<?>' enthält keine Definition für 'ThrowAsync'`.
- **Fix:** Wrapped the call in `Func<Task> act = async () => await pool.ExtractTextAsync(...);` so the assertion sees `Func<Task>`. Applied to both `Dispose_DrainsChannelAndMarksPoolDisposed` and `ExtractTextAsync_AfterDispose_Throws`. No new precedent — this is the canonical FluentAssertions 7 pattern for async exception assertions; the codebase had no prior async-throws assertions, so the pattern lands here for the first time.
- **Files modified:** `Backend/tests/TaxReader.UnitTests/Infrastructure/Tesseract/TesseractEnginePoolTests.cs`
- **Verification:** All 6 pool tests compile and pass.
- **Committed in:** `b6fec54` (part of Task 1)

---

**Total deviations:** 2 auto-fixed (both Rule 3 - Blocking)
**Impact on plan:** Both were necessary mechanical adjustments — the test pattern existed nowhere else in the codebase yet, so the FluentAssertions 7 idiom landed for the first time here, and the production null-guard is genuinely defensive (improving robustness, not just test-friendliness). No scope creep.

## Issues Encountered

- **Initial test run had 2 failures, 4 passes** — the failures pointed me at the null-engine NRE behaviour, which led directly to the production null-guard deviation. Net effect: the failure surfaced the fix on the first attempt; no iterative debugging needed.

## Threat Surface Scan

No new network endpoints, auth paths, or schema changes introduced. The pool replaces an existing OCR service surface 1:1; the threat register entries T-03-11 through T-03-15 (DoS via giant image, malformed image, info disclosure, pool starvation, misconfig) are unchanged in scope from the plan and all `mitigate`/`accept` dispositions are preserved:

- **T-03-11** (giant image OOMs engine, `mitigate`) — the 2400px max-edge downsample is preserved verbatim inside `TesseractEnginePool.RunOcr`. The `OutOfMemoryException` quarantine path is wired and exercised algorithmically (the throw-on-Process path would trigger the same finally-block, though the verifying Layer A test cannot reach `Process` without a real engine; deferred to Layer B manual UAT).
- **T-03-12** (malformed image, `mitigate`) — `catch (TesseractException ex)` branch is present and the quarantine-and-replace finally-block runs unconditionally.
- **T-03-13** (info disclosure via logs, `accept`) — log templates do not include image bytes or filenames; the tessdata-path log is operator-controlled config, not PII.
- **T-03-14** (pool starvation, `mitigate`) — replacement spawn wraps its own try/catch; failure is logged at Error with the current pool count. Manual recovery via container restart is the documented operational lever.
- **T-03-15** (operator misconfig of PoolSize, `accept`) — solo-dev operational responsibility; container resource limits in docker-compose are the throttle.

## Manual UAT Deferred (next agent / human)

These require a real Caddy + Postgres + Tesseract native env and are out of scope for Layer A automated coverage. Capture in `03-HUMAN-UAT.md` when that file lands:

1. **Layer B real-OCR roundtrip** — `docker compose up --build` → upload a real image PDF/PNG → verify OCR text extracted matches the pre-Phase-3 output for the same image. The boot log must show `"Tesseract pool warmup complete in ~300ms"` (Info, written by the warmup service).
2. **10-concurrent-upload throughput** — 10 image uploads from one user should complete in roughly `OCR-time × ceil(10 / PoolSize)` ≈ 4 × OCR-time at default PoolSize=3, not 10× OCR-time. This is the headline win the pool ships.
3. **Malformed-image quarantine verification** — upload a corrupted image; verify Sentry receives a single Warning event (`Tesseract engine threw TesseractException — quarantining and replacing`); verify subsequent uploads still succeed (pool replenished to PoolSize).
4. **Engine-init-failure degradation** — set `Tesseract__PoolSize=3` against a container missing tessdata; verify the warmup logs Error but the host stays up; verify the first OCR upload returns the German `InvalidOperationException` message (`"OCR-Engine nicht verfügbar..."`).

## Next Phase Readiness

- **Plan 03-02 (PIPE-02) is fully unblocked on the OCR-pool dependency.** `ProcessReceiptFileJob` will inject `IImageTextExtractor` and get the pool transparently — no DI changes needed; image-receipt jobs will run up to PoolSize-concurrent without serialising on a single engine.
- **Plan 03-04 (status polling + cancel) is unaffected** — the pool's `Channel.Reader.ReadAsync(cancellationToken)` already honours Hangfire's job CancellationToken, so a cancelled job aborts the OCR acquire-wait cleanly.
- **Hangfire WorkerCount = PoolSize invariant is locked** by `HangfireWorkerCountMatchesPoolSizeTests` — any future refactor that decouples them will fail CI loudly. This extends Plan 03-01's `HangfireWiringTests` source-grep pattern.

## Self-Check

Verified after writing SUMMARY:

- `Backend/src/TaxReader.Infrastructure/Configuration/TesseractOptions.cs` — contains `public int PoolSize { get; set; } = 3`. FOUND.
- `Backend/src/TaxReader.Infrastructure/Services/TesseractEnginePool.cs` — contains `Channel.CreateBounded<TesseractEngine>`, `EngineMode.LstmOnly`, `PageSegMode.SingleBlock`, `OcrTextNormalizer.Normalize`, `Interlocked.Increment(ref _engineCount)`, `Interlocked.Decrement(ref _engineCount)`, `catch (TesseractException`, `catch (OutOfMemoryException`, `"OCR done: {Chars} chars in {Ms} ms"`. FOUND.
- `Backend/src/TaxReader.Infrastructure/Services/TesseractEnginePoolWarmupService.cs` — contains `IHostedService`, `concretePool.Initialize()`, `LogError(ex, "Tesseract pool warmup failed`. FOUND.
- `Backend/src/TaxReader.Infrastructure/Services/TesseractImageTextExtractor.cs` — DELETED. CONFIRMED.
- `Backend/src/TaxReader.Infrastructure/DependencyInjection.cs` — contains `AddSingleton<TesseractEnginePool>()`, `GetRequiredService<TesseractEnginePool>()`, `AddHostedService<TesseractEnginePoolWarmupService>()`. FOUND.
- Source-grep over `Backend/src/**/*.cs` (excluding comment lines) — 0 non-comment references to `TesseractImageTextExtractor`. CONFIRMED.
- `Backend/src/TaxReader.Infrastructure/DependencyInjection.cs` — contains `configuration.GetValue<int>("Tesseract:PoolSize"` and `options.WorkerCount = poolSize`. FOUND (set by 03-01, preserved by 03-03).
- Commits `b6fec54`, `9bd6911` — both reachable via `git log`. FOUND.
- `dotnet build Backend` — 0 errors, 2 pre-existing warnings (Microsoft.Extensions.Http NU1510). PASS.
- `dotnet test Backend/tests/TaxReader.UnitTests --filter "TesseractEnginePool|TesseractEnginePoolWarmup|TesseractImageTextExtractorRemoved|HangfireWorkerCountMatchesPoolSize"` — 14 of 14 tests pass. PASS.
- Full backend test suite — 171 passing, 5 pre-existing skips, 0 failures. PASS.

## Self-Check: PASSED

---
*Phase: 03-background-pipeline-tesseract-pool*
*Plan: 03*
*Completed: 2026-05-21*
