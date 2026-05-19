# Phase 3: Background Pipeline + Tesseract Pool - Research

**Researched:** 2026-05-19
**Domain:** Hangfire background jobs on .NET 10 / Postgres, Tesseract engine pool via `Channel<T>`, HttpOnly cookie dashboard auth, real-time status polling
**Confidence:** HIGH (Hangfire setup, Channel pool, AutomaticRetry semantics, TanStack Query polling) / MEDIUM (Hangfire dashboard anti-forgery posture, mid-Anthropic cancellation behavior under load)

## Summary

This phase replaces the synchronous upload pipeline with Hangfire 1.8.23 background jobs backed by Postgres (`Hangfire.PostgreSql` 1.21.1), restructures Tesseract OCR around a bounded `Channel<TesseractEngine>` pool, and adds a status-polling + cancel endpoint so the SPA can render progress without holding open a 30s+ HTTP request. All 23 D-XX decisions in `03-CONTEXT.md` are locked — the planner uses these as prescriptions, not options.

The single most consequential discovery: **Hangfire's built-in Batches feature is paywalled (Hangfire.Pro)**. D-01's parent-then-classify topology must be built on the free `ContinueJobWith` API or hand-rolled continuation polling. Greg Kedzierski's "Job chaining and batching in C#/.NET with Hangfire (Pro and Free)" article and the official docs both confirm Continuations work fine for sequential 1-parent-1-child chains; for N-parents-then-1-classify (our topology), the planner should hand-roll a "barrier" approach: each `ProcessReceiptFileJob` writes its terminal state to `processing_runs`, and the last finishing job enqueues `ClassifyBatchJob` for the upload. Concrete pattern documented in §Architecture Patterns.

The second non-trivial finding: **`TesseractEngine` is documented as not thread-safe AND object pooling has historically caused SEHException + memory leaks** (`charlesw/tesseract` issue #291). D-16/D-17's `Channel<TesseractEngine>` design mitigates this by guaranteeing one engine = one concurrent caller (single ownership at acquire time), but the quarantine-and-replace path (D-19) is load-bearing — engines that throw `TesseractException` must be disposed, not returned to the pool. Concrete acquire/release/quarantine pattern in §Code Examples.

The third actionable finding: **Hangfire 1.6.20+ ships built-in CSRF anti-forgery on the dashboard**, automatically wired via `Microsoft.AspNetCore.Antiforgery` in ASP.NET Core. Our SameSite=Strict + HttpOnly + Path=/hangfire cookie posture (D-10) inherits the request, so the anti-forgery middleware sees the user identity and emits tokens correctly. No `IgnoreAntiforgeryTokenAttribute` needed. Documented in §Common Pitfalls #5.

**Primary recommendation:** Adopt `Hangfire.Core 1.8.23` + `Hangfire.AspNetCore 1.8.23` + `Hangfire.PostgreSql 1.21.1` exactly. Use `ContinueJobWith` for the simplest D-01 parent-then-classify pattern (single parent ProcessReceiptFileJob with continuation isn't enough for N parents — see hand-rolled barrier pattern below). Use `Channel<TesseractEngine>` with bounded capacity = `PoolSize` (default 3), eager warmup via `IHostedService`, and explicit dispose-on-exception. Status polling at 2s via TanStack Query `refetchInterval` that returns `false` on terminal status (idiomatic v5 pattern). HttpOnly `tr_access` cookie set from the endpoint layer (NOT `AuthService` — preserves Phase 2 02-01 invariant).

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

**Job topology & AI batching (PIPE-02)**

- **D-01:** Job topology = parent `ProcessReceiptFileJob` (per file) → `ClassifyBatchJob` (per upload). The per-file job handles extract + parse + per-file DB writes (deterministic, retryable). After every file's parse completes, a single `ClassifyBatchJob` runs ONE Anthropic call across all parsed items in the upload, preserving today's `UploadReceiptFilesHandler.cs:173-202` wallclock win (Haiku roundtrip ~1s vs N×1s). Coordination uses Hangfire's `IBackgroundJobClient` + `ContinueJobWith` (or an awaiter that polls the per-file jobs for completion before enqueueing the classify-batch).
- **D-02:** Token pre-charge fires inside `ClassifyBatchJob` at the moment item count is known — preserves the existing `AiOnlyClassificationService.cs:49-62` "pre-charge whole batch + per-item refund for Unknowns" pattern exactly. Cancellation before `ClassifyBatchJob` starts → nothing charged. Cancellation during the Anthropic call → full batch refund via the existing `AiOnlyClassificationService.cs:71-75` "AI failure" branch. The 402-on-insufficient-tokens UX still happens — just deferred from upload time to classify time; the per-file status surfaces `errorCode = "InsufficientTokens"` when the pre-charge fails.
- **D-03:** `POST /receipt-files` 202 response body = `{ files: [{ receiptFileId, jobId, fileName }] }`. Per-file polling via `GET /receipt-files/{id}/status` (D-13). No `uploadBatchId` concept introduced; frontend computes batch-level progress client-side from the per-file states. Matches the existing per-file card layout in `upload-form.tsx`.
- **D-04:** Hangfire retry policy = tiered:
  - `ProcessReceiptFileJob`: 3 retries with backoff ~30s / 2m / 5m via `[AutomaticRetry(Attempts = 3)]`. Transient PdfPig/Tesseract/IO errors are real and idempotent (`UploadReceiptFilesHandler` already removes-and-recreates on retry per `ContentHash`).
  - `ClassifyBatchJob`: 0 Hangfire retries via `[AutomaticRetry(Attempts = 0)]`. The existing "refund + mark Unknown" branch handles AI failures gracefully; we don't want 3× the token-refund churn or 3× the Anthropic load.
- **D-05:** `LogContext.PushProperty("JobId", jobId)` scope wraps the body of both jobs at their entry points — fulfills the Phase 1 D-18 reservation. Push only IDs (non-PII); never vendor names, item descriptions, or user emails. Sentry tags inherit via the existing scope-propagation set up in Phase 1 D-14.
- **D-06:** `ProcessingStatus` enum gains two values: `Queued` (between enqueue and worker pick-up) and `Cancelled` (terminal). New numeric order: `Pending=0, Queued=1, Extracting=2, Parsing=3, Classifying=4, Completed=5, Failed=6, Cancelled=7`. EF migration `AddQueuedAndCancelledProcessingStatuses` updates the enum mapping. `Pending` is retained for code paths that haven't enqueued yet (sub-1s window during the 202 response building); the worker observing `Pending` immediately transitions it to `Queued` then `Extracting`.

**Hangfire dashboard auth (PIPE-01)**

- **D-07:** Admin gate = JWT `role` claim backed by a `User.IsAdmin` column. New `bool IsAdmin` column on `users` (NOT NULL default false); EF migration `AddIsAdminToUsers`. `AuthService` adds `"role":"admin"` to the access JWT when `IsAdmin` is true. Generalizes to any future role-gated endpoint without introducing a second mechanism. Refresh tokens stay opaque (no payload).
- **D-08:** First-admin bootstrap = migration-time seed via env var. New env `Hangfire__SeedAdminEmails=csv` read by an idempotent startup `SeedAdminUsers` step (runs after `RUN_MIGRATIONS=true` applies the migration). Sets `IsAdmin=true` for matching `User.Email` rows. Safe to re-run; works on fresh installs and existing DBs; documented in `.env.example` with a generation hint.
- **D-09:** Claim refresh policy = access-token only; demotion takes effect within 60 min (next access-token refresh). The `role` claim is added to the access JWT in `AuthService.LoginAsync` and `AuthService.RefreshAsync`. The Hangfire dashboard filter reads claims from `HttpContext.User`. Refresh tokens carry no role payload. Acceptable because admin demotion is rare and not security-critical at the 100–500 user target.
- **D-10:** Browser credentials transport for `/hangfire` = JWT in HttpOnly cookie set at login. `AuthService.LoginAsync` and `AuthService.RefreshAsync` set `tr_access` cookie (HttpOnly, Secure, SameSite=Strict, Path=/hangfire, expires with the access JWT TTL of 60 min). localStorage still holds the same token for the SPA — one auth scheme, two transports. `/auth/logout` (or any clear-session path the SPA invokes) explicitly clears the cookie. The Hangfire `IDashboardAuthorizationFilter` reads the cookie, validates the JWT using the existing `Jwt__Secret`, and checks the `role` claim.

**Cancellation, polling & refunds (PIPE-03)**

- **D-11:** Cancellable states = any non-terminal state (`Pending`, `Queued`, `Extracting`, `Parsing`, `Classifying`). Hangfire's `IJobCancellationToken` propagates into the job; `Tesseract.ExtractTextAsync`, `PdfPig.ExtractTextAsync`, and `ClaudeAiClassifier.ClassifyBatchAsync` all observe the `CancellationToken` already. Mid-Anthropic cancel = best-effort abort (HttpClient cancellation), full refund via the existing failure branch.
- **D-12:** Refund accounting = all-or-nothing per file. Cancel before `ClassifyBatchJob` starts → no charge fired, no refund needed. Cancel during `ClassifyBatchJob` → the Anthropic abort returns before per-item ledger commits, so the existing `AiOnlyClassificationService.cs:71-75` "refund all" branch runs. One ledger entry per cancellation; auditable; cannot be abused by "upload 10, cancel 1" gaming (which would refund only the cancelled file, not previously-completed ones).
- **D-13:** Status endpoint = `GET /receipt-files/{id}/status` returning `{ status, updatedAt, errorCode?, errorMessage? }`:
  - `status`: ProcessingStatus enum value (string-serialized)
  - `updatedAt`: ISO-8601 UTC
  - `errorCode`: stable enum for the frontend to switch on (`NoTextExtracted`, `ParserMissing`, `AiUnavailable`, `InsufficientTokens`, `Cancelled`, `Unknown`) — present when status is `Failed` or `Cancelled`
  - `errorMessage`: German display string — present when `errorCode` is present
  - Polling cadence: every 2s while status is non-terminal; stop on `Completed`, `Failed`, `Cancelled`. No progress percentage (steps aren't usefully linearizable).
- **D-14:** Cancel endpoint = `POST /receipt-files/{id}/cancel` returning `204 No Content` on success, `409 Conflict` when the file is already terminal, `404 Not Found` when the file doesn't belong to the user. Idempotent (cancelling an already-Cancelled file returns 204). Implementation: `BackgroundJob.Delete(jobId)` for queued jobs, `CancellationTokenSource` signalling for in-flight jobs. The job observes cancellation, marks `ProcessingStatus.Cancelled`, runs refund (D-12), exits.
- **D-15:** Worker recovery on container restart = trust Hangfire's invisibility timeout (~30 min default for the worker heartbeat). `ProcessReceiptFileJob` is idempotent: the existing `UploadReceiptFilesHandler` `ContentHash`-based duplicate detection + the "remove existing non-Processed file and retry" branch in `UploadReceiptFilesHandler.cs:74-80` make re-runs safe. No bespoke startup sweep. Documented as an implicit dependency on Hangfire's worker-liveness model; revisit only if real-traffic shows orphans surviving the invisibility window.

**Tesseract pool design (PIPE-04)**

- **D-16:** Pool size = configurable, default 3. New `TesseractOptions.PoolSize` property + `Tesseract__PoolSize` env var. Sized to typical concurrent-OCR-2-or-3 at the 100–500 user target. Hangfire `WorkerCount` aligned to the same value via shared config or explicit registration — never more workers than engines, so engine starvation is impossible.
- **D-17:** Pool implementation = `Channel<TesseractEngine>` (bounded, single-channel). `IImageTextExtractor` implementation calls `Channel.Reader.ReadAsync(jobCancellationToken)` to acquire, `Channel.Writer.TryWrite(engine)` to release. Hangfire's `IJobCancellationToken` is the only thing that aborts an acquire wait — no artificial timeouts, no synthetic "pool full" errors at the OCR layer. The pool implementation lives in `Backend/src/TaxReader.Infrastructure/Services/TesseractEnginePool.cs` (renamed from `TesseractImageTextExtractor.cs`); the old class is removed.
- **D-18:** Engine warmup = eager at startup via `IHostedService`. `TesseractEnginePoolWarmupService.StartAsync` creates all `PoolSize` engines before the host signals Ready (and before `/health` returns 200). Adds ~`PoolSize × 100ms` (~300ms at default) to container boot. First OCR pays no init cost. Predictable steady-state latency; appropriate for a Docker Compose deploy that restarts rarely.
- **D-19:** Engine failure handling = quarantine + replace on exception. Each OCR call wraps the `engine.Process(image)` in try/catch. On `TesseractException` or `OutOfMemoryException`, the engine is `Dispose()`d and NOT returned to the channel. A pool-side hosted service (or the next-acquire path) detects the count drop and creates a replacement engine on the same thread that observed the failure (cheap — ~100ms). Logs at `Warning` with engine-id; structured event so Sentry's "new error type" rule can baseline.
- **D-20:** Tesseract config knobs stay = `EngineMode.LstmOnly` + `PageSegMode.SingleBlock` + 2400px downsample carry over from `TesseractImageTextExtractor.cs:60-72,119-123`. Image-downsampling math, OCR-text normalization via `OcrTextNormalizer.Normalize`, and the German+English language pack stay identical. Only the engine lifecycle changes.

**Cross-cutting (PIPE-05, PIPE-06) — Claude's Discretion within stated conventions**

- **D-21:** German error catalog (PIPE-05) location = `Backend/src/TaxReader.Application/Common/UploadErrorCatalog.cs` (or analog). Maps known exception types to `(errorCode, germanMessage)` pairs surfaced via D-13's status response. Raw exception messages NEVER appear in HTTP body or `processing_runs.error_message`; they go to Serilog only via `logger.LogError(ex, "{ErrorCode} during {Step} for ReceiptFile {Id}", ...)`. Fall-through for unknown exceptions: `errorCode = "Unknown"`, `errorMessage = "Verarbeitung fehlgeschlagen — bitte erneut versuchen oder Support kontaktieren."`. The exact catalog content (which exception types, which strings) is planner-decided per the existing German `Sie`-form convention.
- **D-22:** Empty/loading/error UI patterns (PIPE-06) = reuse existing shadcn primitives (`Skeleton`, `Alert`, `AlertCircle`, the toast pattern via `sonner`). No new UI primitives introduced. Pages affected: `upload/page.tsx`, `receipts/page.tsx` (list), `receipts/[id]/page.tsx` (detail), `dashboard/page.tsx`, `reports/page.tsx`. Polling for in-flight status uses TanStack Query's `refetchInterval` set per D-13's 2s cadence with terminal-state stop. Per-file-card placeholders in `upload-form.tsx:52-58` get a real status badge plus the `errorMessage` text from the polling response. Exact wording, copy length, spacing decisions are planner/executor discretion within the German `Sie`-form convention from `CONVENTIONS.md`.

**Recurring cleanup jobs (PIPE-01) — Claude's Discretion within stated scope**

- **D-23:** Recurring jobs registered at startup via `RecurringJob.AddOrUpdate`:
  1. Expired refresh tokens cleanup — daily at 03:00 UTC; `DELETE FROM refresh_tokens WHERE expires_at < now() - INTERVAL '7 days'` (7-day grace beyond expiry so audit queries still work briefly). Fulfills the Phase 2 D-16 deferred handoff.
  2. Abandoned `Failed` jobs cleanup — weekly; removes Hangfire-internal `Failed`-state job metadata older than 30 days via `BackgroundJobClient.Delete(...)` over a Hangfire monitoring-API query. `ProcessingRun` rows are kept (DB audit), only Hangfire's job table is pruned.
  3. `ProcessingRun` retention — none in Phase 3; defer to Phase 6 (LEG-08 audit log decides retention policy uniformly across audit-relevant tables).
  Exact cron expressions, log volumes, and idempotency guards are planner-decided.

### Claude's Discretion

- Exact `IBackgroundJobClient` invocation pattern (`Enqueue` + `ContinueJobWith` vs `BatchJob` extension package vs custom continuation poll) for D-01's parent/child topology — **research recommendation: hand-rolled barrier (see Architecture Patterns) because Hangfire Batches are paywalled**
- Whether `ProcessReceiptFileJob` and `ClassifyBatchJob` are class-typed or static-method-typed Hangfire targets (likely class-typed with DI to match the established handler-injection pattern) — **research recommendation: class-typed, matches existing handler idiom**
- Hangfire dashboard's `DashboardOptions.Authorization` filter chain (single filter or composed; whether to include a "no anonymous" hard reject) — **research recommendation: single `HangfireAdminAuthFilter`; multiple filters are AND-combined per docs.hangfire.io**
- Status enum string serialization (PascalCase vs snake_case in JSON) — **research recommendation: PascalCase (matches existing enum convention; no `JsonStringEnumConverter` retrofit)**
- Whether `GET /receipt-files/{id}/status` lives on `ReceiptFileEndpoints.cs` (current home) or a dedicated `ProcessingStatusEndpoints.cs` — **research recommendation: `ReceiptFileEndpoints.cs` (cohesion; resource-oriented)**
- Tesseract engine warmup order (parallel vs serial) — likely serial to avoid I/O contention loading the same language data files — **research recommendation: serial (D-18's ~300ms budget already assumes serial; `Task.WhenAll` would race on filesystem reads)**
- Whether the cookie is set via `Response.Cookies.Append` in `AuthService` or in the endpoint layer (likely endpoint layer to keep `AuthService` HTTP-context-free per Phase 2 02-01 invariant) — **research recommendation: endpoint layer (preserves invariant; `AuthService` returns the JWT string + TTL, endpoint sets cookie)**

### Deferred Ideas (OUT OF SCOPE)

- CSRF posture for Hangfire dashboard POST actions (requeue, delete) — Hangfire ships built-in anti-forgery, our SameSite=Strict + HttpOnly cookie covers the threat model
- Audit logging of dashboard actions (who requeued / deleted which job, when) — fold into Phase 6 LEG-08 audit_log
- Rate-limit policy on `/hangfire` path — admin tool, low volume; the global 60/min IP limit is enough
- SPA logout flow to clear the `tr_access` cookie — Phase 3 adds endpoint to clear cookie
- SSE / long-poll for status push — 2s polling is fine at scale; defer until BetterStack shows polling cost
- `ProcessingRun` retention policy — defer to Phase 6 LEG-08
- Per-route concurrency limit on `POST /receipts/{id}/reclassify` — still deferred
- PdfPig zero-words → Tesseract fallback (CONCERNS.md #11) — important but separate-PR-scope; likely Phase 4
- Worker autoscaling / dynamic pool sizing — single container handles target scale
- Hangfire batches (Hangfire.Pro extension) — paid; build coordination on free core
- Two-phase token ledger (reserve → commit/refund) — overkill at this scale
- OpenTelemetry tracing across HTTP → Hangfire boundary — Phase 1 D-19 deferred

</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| PIPE-01 | Hangfire installed with Postgres storage; dashboard at `/hangfire` auth-gated to admin role; recurring cleanup jobs registered (expired refresh tokens, abandoned `Failed` jobs) | §Standard Stack (Hangfire 1.8.23 + Hangfire.PostgreSql 1.21.1 + Hangfire.AspNetCore 1.8.23), §Architecture Patterns (Pattern 1: Hangfire bootstrap, Pattern 6: Dashboard auth filter), §Code Examples (Hangfire registration, IDashboardAuthorizationFilter, RecurringJob.AddOrUpdate cron) |
| PIPE-02 | `ProcessReceiptFileJob` running the extract → parse → classify pipeline as a Hangfire background job; `POST /receipt-files` returns `202 Accepted` with jobIds; token pre-charge + per-item refund pattern preserved | §Architecture Patterns (Pattern 2: Parent/Child topology with hand-rolled barrier; Pattern 3: Tiered AutomaticRetry attribute), §Code Examples (ProcessReceiptFileJob + ClassifyBatchJob skeletons; AiOnlyClassificationService reuse) |
| PIPE-03 | `GET /receipt-files/{id}/status` for frontend polling; `POST /receipt-files/{id}/cancel` for explicit cancellation; status reflects (Queued, Extracting, Parsing, Classifying, Completed, Failed, Cancelled) | §Architecture Patterns (Pattern 4: Cancellation propagation via CancellationToken; Pattern 5: Status endpoint + TanStack Query refetchInterval), §Code Examples (Cancel endpoint, status DTO, polling hook) |
| PIPE-04 | `TesseractEnginePool` (configurable size, default 3-5) using `Channel<TesseractEngine>`; replaces Singleton + lock pattern in `TesseractImageTextExtractor` | §Architecture Patterns (Pattern 7: Channel<TesseractEngine> pool; Pattern 8: Eager warmup IHostedService; Pattern 9: Quarantine-and-replace), §Code Examples (Pool acquire/release; quarantine pattern) |
| PIPE-05 | User-friendly German error messages on upload failure — known exception types mapped to safe strings; raw exceptions logged to Serilog only, never returned in HTTP body or persisted in `processing_runs.error_message` | §Architecture Patterns (Pattern 10: UploadErrorCatalog), §Code Examples (catalog skeleton with German strings, ErrorCode enum) |
| PIPE-06 | Empty / loading / error states implemented across upload page, receipts list page, receipt detail page, dashboard, reports — no blank-screen-of-thinking states | §Architecture Patterns (Pattern 11: shadcn Skeleton + Alert empty/loading/error patterns), §Code Examples (TanStack Query polling hook with terminal-state stop) |

</phase_requirements>

## Project Constraints (from CLAUDE.md)

- **Architecture Rules:** Domain layer ZERO dependencies; Application defines interfaces only; Infrastructure implements; API thin; Application does NOT reference Infrastructure
- **Patterns FORBIDDEN:** Repository pattern, AutoMapper, MediatR, stored procedures, exceptions for control flow
- **Patterns REQUIRED:** Primary constructors for DI, records for DTOs/commands, `Result<T>` for error handling, file-scoped namespaces, always pass `CancellationToken`, `Async` suffix on every async method, structured logging with named placeholders (never string interpolation in log templates)
- **Configuration:** `IOptions<T>` with `SectionName` constant; `__`-nested env vars; `appsettings.json` + `appsettings.Development.json` + env-var precedence
- **EF Core:** snake_case via `UseSnakeCaseNamingConvention`; one `IEntityTypeConfiguration<T>` per entity; cascade delete for cleanup
- **Localization:** German `Sie`-form for user-facing strings (`Result<T>.Failure`, German error catalog, frontend UI copy). Dev docs / code comments stay English.
- **GSD enforcement:** Phase work goes through `/gsd-execute-phase`; no direct repo edits outside GSD workflow

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Hangfire bootstrap, server, dashboard registration | API (`Program.cs`) | Infrastructure (`AddInfrastructure` DI) | Pipeline order is API-layer concern; storage config is Infrastructure |
| `IDashboardAuthorizationFilter` (JWT validation + role check) | API (`HangfireAdminAuthFilter`) | — | Filter reads `HttpContext` — must live in API layer; Application/Infra HTTP-free |
| `ProcessReceiptFileJob`, `ClassifyBatchJob` classes | Application (`Jobs/`) | Infrastructure (Hangfire `BackgroundJobActivator`) | Job classes are CQRS-style handlers in Application; Hangfire DI activator wires the scope |
| `IBackgroundJobClient` abstraction | Application (interface) | Infrastructure (Hangfire wrapper) | Keeps Application Hangfire-free (per architecture rule); Infra implements |
| Hangfire `RecurringJob.AddOrUpdate` for cleanup | API (registration on startup) | Application (job classes) | Registration sits next to other startup wiring; job logic in Application |
| Status DTO + polling endpoint | API endpoint + Application query handler | — | Standard request/response cycle; not background work |
| Cancel endpoint | API endpoint + Application command handler | Infrastructure (Hangfire `BackgroundJob.Delete`) | Endpoint thin; handler uses `IBackgroundJobClient` to signal Hangfire |
| `TesseractEnginePool` (Channel + acquire/release) | Infrastructure (replaces `TesseractImageTextExtractor`) | — | OCR is external concern; pool is Infrastructure mechanic |
| `TesseractEnginePoolWarmupService` (IHostedService) | Infrastructure | API (`AddHostedService` registration) | Hosted services live with the dependency they warm up |
| `UploadErrorCatalog` (exception → German string) | Application (`Common/`) | — | Pure mapping logic; no external deps |
| `tr_access` cookie set / clear | API endpoint layer (AuthEndpoints) | — | HTTP-context-bound; preserves AuthService HTTP-free invariant |
| `User.IsAdmin` column + `role` claim minting | Domain (entity) + Infrastructure (AuthService claims) | — | Entity shape is Domain; claim construction is Infra (signs JWT) |
| Status polling hook | Frontend (`use-receipt-files.ts` or new hook) | — | Standard TanStack Query consumer |
| Empty/loading/error UI states | Frontend (page-level components) | — | shadcn primitives at page boundary |

## Standard Stack

### Core

| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| `Hangfire.Core` | `1.8.23` | Background-job engine | `[VERIFIED: nuget.org]` Latest stable as of 2026-02-05. Targets .NET Standard 1.3+; computed support through .NET 10. Industry-standard .NET job framework. |
| `Hangfire.AspNetCore` | `1.8.23` | ASP.NET Core integration (dashboard, `AddHangfireServer`, DI scopes) | `[VERIFIED: nuget.org]` Released 2026-02-05; depends on `Hangfire.Core`. Provides `IGlobalConfigurationBuilder.UseAspNetCoreLogging`, `app.UseHangfireDashboard`, `context.GetHttpContext()` for auth filters. |
| `Hangfire.PostgreSql` | `1.21.1` | Postgres storage provider | `[VERIFIED: nuget.org]` Latest stable as of 2026-02-11. Requires Npgsql 6.0+ (we have 10.0.1 transitively via `Npgsql.EntityFrameworkCore.PostgreSQL` 10.0.1 — `[VERIFIED: Backend/Directory.Packages.props]`). Built-in schema creation on first connection. |

### Supporting

| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| `System.Threading.Channels` | (BCL, .NET 10) | Bounded `Channel<TesseractEngine>` | `[VERIFIED: learn.microsoft.com]` Built into BCL; no NuGet ref needed. `Channel.CreateBounded<T>(capacity)` is the canonical fixed-size pool primitive on .NET 10. |
| `Tesseract` | `5.2.0` (already in `Directory.Packages.props`) | OCR engine wrapper | `[VERIFIED: Backend/Directory.Packages.props line 32]` Existing dep; no version change. Used by new `TesseractEnginePool`. |
| `System.IdentityModel.Tokens.Jwt` | `8.12.1` (already in `Directory.Packages.props`) | JWT validation in dashboard filter | `[VERIFIED: Backend/Directory.Packages.props line 14]` Already present for AuthService; reused for `HangfireAdminAuthFilter` token validation. |
| `Microsoft.AspNetCore.Antiforgery` | (BCL, .NET 10) | CSRF protection on Hangfire dashboard POSTs | `[VERIFIED: docs.hangfire.io]` Hangfire 1.6.20+ auto-wires this in ASP.NET Core via DI lookup; no explicit ref. |

### Alternatives Considered

| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| `Hangfire.PostgreSql` | `Hangfire.SqlServer` | Would require running SQL Server — out of stack (we have Postgres 17) |
| `Channel<TesseractEngine>` | `ConcurrentBag<TesseractEngine>` + `SemaphoreSlim` | `[CITED: learn.microsoft.com/en-us/dotnet/core/extensions/channels]` Channels offer async waits + backpressure built-in; ConcurrentBag requires hand-rolled semaphore. D-17 picks Channel; ConcurrentBag pattern is the legacy Microsoft Learn recipe but predates Channels |
| Hand-rolled barrier for D-01 | `Hangfire.Pro` Batches API | `[VERIFIED: hangfire.io/pro]` Batches feature is paywalled — solo-dev pre-revenue can't justify Pro license; hand-rolled barrier is standard practice for free Hangfire |
| `IJobCancellationToken` | `CancellationToken` (since Hangfire 1.7.0) | `[VERIFIED: docs.hangfire.io/.../using-cancellation-tokens.html]` Modern Hangfire (1.7+) supports plain `CancellationToken` — fully async, safe in tight loops. Use this, NOT the legacy `IJobCancellationToken`. |
| `Hangfire.AspNetCore` v1.7.x | v1.8.23 | v1.8 adds `CompatibilityLevel.Version_180` (recommended for new installs) + `UseSimpleAssemblyNameTypeSerializer` + `UseRecommendedSerializerSettings` per `[CITED: docs.hangfire.io/.../upgrade-guides/upgrading-to-hangfire-1.8.html]` |

**Installation:**

Add to `Backend/Directory.Packages.props`:
```xml
<PackageVersion Include="Hangfire.Core" Version="1.8.23" />
<PackageVersion Include="Hangfire.AspNetCore" Version="1.8.23" />
<PackageVersion Include="Hangfire.PostgreSql" Version="1.21.1" />
```

Add `<PackageReference Include="Hangfire.AspNetCore" />` to `Backend/src/TaxReader.Api/TaxReader.Api.csproj` (Hangfire.Core comes transitively).

Add `<PackageReference Include="Hangfire.PostgreSql" />` to `Backend/src/TaxReader.Infrastructure/TaxReader.Infrastructure.csproj` (registers storage in `DependencyInjection.cs`).

**Version verification (run before locking in `Directory.Packages.props`):**

```bash
# Confirm published versions and dates at planning time
curl -s https://api.nuget.org/v3-flatcontainer/hangfire.core/index.json | jq '.versions[-1]'
curl -s https://api.nuget.org/v3-flatcontainer/hangfire.aspnetcore/index.json | jq '.versions[-1]'
curl -s https://api.nuget.org/v3-flatcontainer/hangfire.postgresql/index.json | jq '.versions[-1]'
```

`[VERIFIED: nuget.org]` Hangfire.Core 1.8.23 published 2026-02-05; Hangfire.AspNetCore 1.8.23 published 2026-02-05; Hangfire.PostgreSql 1.21.1 published 2026-02-11.

## Architecture Patterns

### System Architecture Diagram

```
                                                                                    
  Browser (SPA)                                                                     
      │                                                                              
      │  multipart POST /api/v1/receipt-files                                       
      ▼                                                                              
  Caddy :443  ──proxy──►  api:8080                                                   
                              │                                                      
                              ▼                                                      
           ┌─────────────────────────────────────┐                                  
           │ UploadReceiptFilesHandler           │                                  
           │  (Application)                       │                                  
           │  • SHA-256 dedup per UserId          │                                  
           │  • Insert ReceiptFile (Queued)       │                                  
           │  • Insert ProcessingRun (Pending)    │                                  
           │  • backgroundJobs.Enqueue            │                                  
           │     ProcessReceiptFileJob × N        │                                  
           └────────────┬─────────────────────────┘                                  
                        │ 202 Accepted { files:[{receiptFileId, jobId, fileName}] } 
                        ▼                                                            
                  Hangfire (Postgres-backed) queue                                   
                        │                                                            
                        ▼                                                            
           ┌─────────────────────────────────────┐                                   
           │ ProcessReceiptFileJob (Application) │                                   
           │  [AutomaticRetry(Attempts = 3,      │                                   
           │     DelaysInSeconds = [30,120,300])]│                                   
           │  • LogContext.PushProperty(JobId)   │                                   
           │  • Status: Queued → Extracting      │                                   
           │  • PdfPig OR TesseractEnginePool    │                                   
           │  • Parsing → AmazonParser/Eduki/Gen │                                   
           │  • Insert Receipt + ReceiptItems    │                                   
           │  • Check if last → enqueue Classify │                                   
           └────────────┬─────────────────────────┘                                  
                        │ all N parents complete                                     
                        ▼                                                            
           ┌─────────────────────────────────────┐                                   
           │ ClassifyBatchJob (Application)      │                                   
           │  [AutomaticRetry(Attempts = 0)]     │                                   
           │  • LogContext.PushProperty(JobId)   │                                   
           │  • TokenService.TryConsumeManyAsync │                                   
           │  • ClaudeAiClassifier.ClassifyBatch │                                   
           │  • Per-Unknown refund               │                                   
           │  • Status: Classifying → Completed  │                                   
           └────────────┬─────────────────────────┘                                  
                        │                                                            
                                                                                    
   Frontend (separately):                                                            
       GET /receipt-files/{id}/status  ◄── TanStack Query refetchInterval 2s         
       POST /receipt-files/{id}/cancel ◄── stops on terminal state                   
                                                                                    
   Caddy :443  ──proxy──►  api:8080/hangfire                                         
       (HttpOnly tr_access cookie validated by IDashboardAuthorizationFilter)        
                                                                                    
   TesseractEnginePool (Singleton, Infrastructure):                                  
       Channel<TesseractEngine>(capacity = PoolSize, default 3)                      
       ↑ acquire via Reader.ReadAsync(ct)                                            
       ↓ release via Writer.TryWrite (or dispose-and-replace on TesseractException)  
       eager warmup via TesseractEnginePoolWarmupService : IHostedService            
```

### Component Responsibilities

| Component | File | Responsibility |
|-----------|------|---------------|
| `UploadReceiptFilesHandler` (REWRITTEN) | `Backend/src/TaxReader.Application/Commands/UploadReceiptFilesHandler.cs` | SHA-256 dedup, insert `ReceiptFile`/`ProcessingRun` in `Queued`/`Pending`, enqueue jobs, return 202 payload. NO more extract/parse/classify in the request thread. |
| `ProcessReceiptFileJob` (NEW) | `Backend/src/TaxReader.Application/Jobs/ProcessReceiptFileJob.cs` | Per-file extract + parse + persist; check-if-last-and-enqueue-classify barrier pattern |
| `ClassifyBatchJob` (NEW) | `Backend/src/TaxReader.Application/Jobs/ClassifyBatchJob.cs` | Single Anthropic call for all parsed items in upload; refund on AI failure/cancel; mark all per-file runs Completed |
| `IBackgroundJobClient` (NEW abstraction) | `Backend/src/TaxReader.Application/Interfaces/IBackgroundJobClient.cs` | Application port; methods `EnqueueAsync<TJob>(...)`, `DeleteAsync(jobId, ct)`, `IsAlreadyRunning(jobId)` |
| `HangfireBackgroundJobClient` (NEW) | `Backend/src/TaxReader.Infrastructure/Services/HangfireBackgroundJobClient.cs` | Adapter to Hangfire's `IBackgroundJobClient` + `IRecurringJobManager` |
| `TesseractEnginePool` (NEW, replaces `TesseractImageTextExtractor`) | `Backend/src/TaxReader.Infrastructure/Services/TesseractEnginePool.cs` | `Channel<TesseractEngine>` acquire/release; quarantine on exception; implements `IImageTextExtractor` |
| `TesseractEnginePoolWarmupService` (NEW) | `Backend/src/TaxReader.Infrastructure/Services/TesseractEnginePoolWarmupService.cs` | `IHostedService` eager engine creation at boot |
| `HangfireAdminAuthFilter` (NEW) | `Backend/src/TaxReader.Api/Hangfire/HangfireAdminAuthFilter.cs` | `IDashboardAuthorizationFilter`; reads `tr_access` cookie, validates JWT against `Jwt__Secret`, checks `role == "admin"` |
| `SeedAdminUsersHostedService` (NEW) | `Backend/src/TaxReader.Infrastructure/Services/AdminBootstrap/SeedAdminUsersHostedService.cs` | Reads `Hangfire__SeedAdminEmails`, sets `IsAdmin=true` on matching `User.Email` rows after migration |
| `GetReceiptFileStatusHandler` (NEW) | `Backend/src/TaxReader.Application/Queries/GetReceiptFileStatusHandler.cs` | Reads `ProcessingRun` by `ReceiptFileId`, filters by `currentUser.UserId`, builds D-13 DTO from `Status` + `UpdatedAt` + (if Failed/Cancelled) `ErrorCode` from new `processing_runs.error_code` column |
| `CancelReceiptFileHandler` (NEW) | `Backend/src/TaxReader.Application/Commands/CancelReceiptFileHandler.cs` | Validates user owns file, validates non-terminal, calls `IBackgroundJobClient.DeleteAsync`, marks `Cancelled`, exits |
| `UploadErrorCatalog` (NEW) | `Backend/src/TaxReader.Application/Common/UploadErrorCatalog.cs` | Pure static mapper: `Exception ex → (string errorCode, string germanMessage)` |

### Recommended Project Structure

```
Backend/src/TaxReader.Application/
├── Commands/
│   ├── UploadReceiptFilesHandler.cs    (REWRITTEN: 202 + enqueue)
│   └── CancelReceiptFileHandler.cs     (NEW)
├── Queries/
│   └── GetReceiptFileStatusHandler.cs  (NEW)
├── Jobs/                                (NEW DIRECTORY)
│   ├── ProcessReceiptFileJob.cs
│   └── ClassifyBatchJob.cs
├── Common/                              (NEW DIRECTORY)
│   └── UploadErrorCatalog.cs
├── Interfaces/
│   └── IBackgroundJobClient.cs          (NEW)
└── DTOs/
    ├── UploadAcceptedResponse.cs        (NEW: 202 body shape)
    └── ReceiptFileStatusDto.cs          (NEW: D-13 polling response)

Backend/src/TaxReader.Infrastructure/
├── Services/
│   ├── TesseractEnginePool.cs           (NEW, replaces TesseractImageTextExtractor)
│   ├── TesseractEnginePoolWarmupService.cs  (NEW IHostedService)
│   ├── HangfireBackgroundJobClient.cs   (NEW)
│   └── AdminBootstrap/
│       └── SeedAdminUsersHostedService.cs  (NEW IHostedService)
├── Configuration/
│   └── TesseractOptions.cs              (UPDATED: add PoolSize)
└── Migrations/
    ├── ……_AddIsAdminToUsers.cs           (NEW)
    └── ……_AddQueuedAndCancelledProcessingStatuses.cs  (NEW)

Backend/src/TaxReader.Api/
├── Hangfire/                            (NEW DIRECTORY)
│   └── HangfireAdminAuthFilter.cs
├── Endpoints/
│   ├── ReceiptFileEndpoints.cs           (UPDATED: 202 + status + cancel; drops upload-concurrency rate limit)
│   └── AuthEndpoints.cs                  (UPDATED: set/clear tr_access cookie at login/refresh/logout)
└── Program.cs                            (UPDATED: AddHangfire, AddHangfireServer, dashboard, RecurringJob.AddOrUpdate)

Backend/src/TaxReader.Domain/
├── Entities/
│   ├── User.cs                          (UPDATED: add bool IsAdmin)
│   └── ProcessingRun.cs                 (UPDATED: add ErrorCode column)
└── Enums/
    └── ProcessingStatus.cs              (UPDATED: add Queued, Cancelled per D-06)
```

### Pattern 1: Hangfire bootstrap on .NET 10 + Postgres

**What:** Minimum-viable wiring per `docs.hangfire.io/.../aspnet-core-applications.html`.
**When to use:** Once, at the API `Program.cs` boot path.

```csharp
// Backend/src/TaxReader.Infrastructure/DependencyInjection.cs (excerpt)

// Source: https://docs.hangfire.io/en/latest/configuration/using-postgresql.html (verified Hangfire.PostgreSql 1.21.1)
services.AddHangfire(config => config
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UseSerilogLogProvider() // pipes Hangfire's internal logs into our Serilog sink
    .UsePostgreSqlStorage(c => c.UseNpgsqlConnection(
        configuration.GetConnectionString("DefaultConnection")),
        new PostgreSqlStorageOptions
        {
            PrepareSchemaIfNecessary = true, // first-boot schema creation
            QueuePollInterval = TimeSpan.FromSeconds(1), // faster than 15s default for snappy UX
            InvisibilityTimeout = TimeSpan.FromMinutes(30) // D-15: matches Hangfire default; explicit for clarity
        }));

var poolSize = configuration.GetValue<int>("Tesseract:PoolSize", 3);
services.AddHangfireServer(options =>
{
    options.WorkerCount = poolSize; // D-16: WorkerCount aligned with pool size — never more workers than engines
    options.Queues = new[] { "default" };
    options.CancellationCheckInterval = TimeSpan.FromSeconds(2); // poll storage every 2s for cancellation signals
});
```

**Pitfall:** Hangfire's schema creation runs on the FIRST connection, NOT through EF migrations (`[CITED: hangfire-postgres/Hangfire.PostgreSql README]`). With `RUN_MIGRATIONS=true` and Phase 3's two new EF migrations (`AddIsAdminToUsers`, `AddQueuedAndCancelledProcessingStatuses`), EF runs first via `dbContext.Database.MigrateAsync()` in `Program.cs:298-303`, then Hangfire's first DB call (during `AddHangfireServer` initialization) creates its own `hangfire.*` schema. The schemas don't conflict; Hangfire uses its own schema (default `hangfire`). Document this ordering invariant.

### Pattern 2: Parent/Child topology — hand-rolled barrier (D-01)

**What:** N `ProcessReceiptFileJob` parents must all finish before 1 `ClassifyBatchJob` runs.
**When to use:** When Hangfire Pro Batches is unavailable (paid tier).

**The problem with `ContinueJobWith`:** Hangfire's free `ContinueJobWith(parentJobId, ...)` accepts a SINGLE parent ID. It doesn't natively support "fan-in" of N parents to 1 child. Workarounds documented at `gregkedzierski.com/essays/dotnet-job-chaining-and-batching-with-hangfire`:
1. Chain `ContinueJobWith` calls sequentially (Job A → Job B → Job C → Classify) — turns parallel parents into serial work. Defeats the wallclock-win design.
2. Hand-rolled "barrier" in the parent jobs.

**Recommended pattern (barrier):**

```csharp
// Backend/src/TaxReader.Application/Jobs/ProcessReceiptFileJob.cs (sketch)
public class ProcessReceiptFileJob(
    IAppDbContext dbContext,
    IPdfTextExtractor pdfExtractor,
    IImageTextExtractor imageExtractor, // bound to TesseractEnginePool
    IEnumerable<IReceiptParser> parsers,
    IBackgroundJobClient jobClient,
    ILogger<ProcessReceiptFileJob> logger)
{
    [AutomaticRetry(Attempts = 3, DelaysInSeconds = new[] { 30, 120, 300 })] // D-04
    public async Task HandleAsync(
        Guid receiptFileId,
        Guid uploadBatchId,    // logical batch id minted in UploadReceiptFilesHandler
        int batchSize,         // total parents the barrier is waiting for
        CancellationToken cancellationToken)
    {
        using var _ = LogContext.PushProperty("JobId", receiptFileId); // D-05
        // ... extract + parse + persist ...
        // (mark this run as parse-complete in DB)

        // Barrier: count completed parents in this batch
        var completedCount = await dbContext.ProcessingRuns
            .Where(r => r.ReceiptFile.UploadBatchId == uploadBatchId
                     && (r.Status == ProcessingStatus.Parsing || r.Status >= ProcessingStatus.Classifying))
            .CountAsync(cancellationToken);

        if (completedCount >= batchSize)
        {
            // I'm the last one — enqueue the classify-batch
            await jobClient.EnqueueAsync<ClassifyBatchJob>(
                j => j.HandleAsync(uploadBatchId, CancellationToken.None),
                cancellationToken);
        }
    }
}
```

**Race-condition handling:** If two parents finish near-simultaneously, both may read `completedCount == batchSize` and both enqueue `ClassifyBatchJob`. Mitigation: make `ClassifyBatchJob` idempotent via a unique constraint on `(upload_batch_id, status_in_classifying_or_higher)` checked at job entry. Alternative: use Postgres advisory lock around the count + enqueue.

**Simpler alternative if planner accepts serial classify:** chain `ContinueJobWith` from a "tail" sentinel parent — but this requires deterministic ordering of parent enqueue, which the upload handler doesn't naturally guarantee. The barrier pattern is more robust.

### Pattern 3: Tiered `AutomaticRetry` attribute (D-04)

**What:** Per-job retry policy with custom delays.
**When to use:** On every Hangfire job class entry point.

```csharp
// Source: https://docs.hangfire.io/en/latest/background-processing/dealing-with-exceptions.html
// Source: github.com/HangfireIO/Hangfire/blob/main/src/Hangfire.Core/AutomaticRetryAttribute.cs

[AutomaticRetry(Attempts = 3, DelaysInSeconds = new[] { 30, 120, 300 })]
public async Task HandleAsync(Guid receiptFileId, ..., CancellationToken ct) { ... }

[AutomaticRetry(Attempts = 0)] // disable retries
public async Task HandleAsync(Guid uploadBatchId, CancellationToken ct) { ... }
```

`DelaysInSeconds` is array-of-int (seconds). For ProcessReceiptFileJob: `[30, 120, 300]` = 30s / 2m / 5m. `[VERIFIED: github.com/HangfireIO/Hangfire/blob/main/src/Hangfire.Core/AutomaticRetryAttribute.cs]` Last element repeats if `Attempts > delays.Length`; here 3 attempts × 3 delays so it's exactly aligned.

`OnAttemptsExceeded`: defaults to `Fail` (move to Failed state). Hangfire's `IElectStateFilter` integration auto-runs without extra config.

### Pattern 4: Cancellation propagation (D-11)

**What:** Use `CancellationToken` parameter on job methods (Hangfire 1.7+ native support); pass through to `HttpClient.SendAsync`, `PdfPig.ExtractTextAsync`, `Tesseract.Process`.

```csharp
// Source: https://docs.hangfire.io/en/latest/background-methods/using-cancellation-tokens.html
// "Starting from Hangfire 1.7.0, it's possible to use a regular CancellationToken class."
public async Task HandleAsync(
    Guid receiptFileId, ..., CancellationToken cancellationToken) // ← Hangfire injects, polls every 2s
{
    // Pass to every awaitable:
    var rawText = await pdfExtractor.ExtractTextAsync(stream, cancellationToken);
    var classifications = await aiClassifier.ClassifyBatchAsync(descriptions, cancellationToken);
}
```

Hangfire polls storage every `CancellationCheckInterval` (default 5s; we set 2s above) to see if the job's parent `BackgroundJob.Delete(jobId)` was called. When it sees cancellation, it throws `OperationCanceledException` from any `Task.Delay(ct)` call OR — for code that doesn't `await Task.Delay` — at the next checkpoint set by `ct.ThrowIfCancellationRequested()`.

**Mid-Anthropic cancellation:** `HttpClient.SendAsync(request, ct)` respects `ct` (`[CITED: learn.microsoft.com]`). If the cancel arrives mid-request, the socket is closed; the call throws `TaskCanceledException` (subtype of `OperationCanceledException`). Our existing `AiOnlyClassificationService.cs:71-75` catches `Exception` and refunds — that branch handles the cancel naturally. `[ASSUMED]` Anthropic charges for the partial inference even though the network connection drops; we eat that cost (token economy is pass-through).

**Mid-Tesseract cancellation:** `engine.Process(image)` is synchronous and DOES NOT observe `CancellationToken`. The job runs OCR inside `Task.Run(() => RunOcr(bytes), ct)` (already done at `TesseractImageTextExtractor.cs:33`); the `ct` cancels the wait-for-task-completion, but Tesseract's native call continues until it finishes natively. This is acceptable — typical receipt OCR is < 3s; cancellation tolerates that latency.

### Pattern 5: Status endpoint + TanStack Query refetchInterval (D-13)

**What:** `GET /receipt-files/{id}/status` returns terminal-aware DTO; frontend polls at 2s and stops on terminal state.

**Backend DTO + handler:**
```csharp
// Backend/src/TaxReader.Application/DTOs/ReceiptFileStatusDto.cs
public record ReceiptFileStatusDto(
    ProcessingStatus Status,
    DateTime UpdatedAt,
    string? ErrorCode,
    string? ErrorMessage);

// Backend/src/TaxReader.Application/Queries/GetReceiptFileStatusHandler.cs
public class GetReceiptFileStatusHandler(IAppDbContext db, ICurrentUser currentUser)
{
    public async Task<Result<ReceiptFileStatusDto>> HandleAsync(Guid receiptFileId, CancellationToken ct)
    {
        var run = await db.ProcessingRuns
            .Where(r => r.ReceiptFileId == receiptFileId && r.ReceiptFile.UserId == currentUser.UserId)
            .OrderByDescending(r => r.StartedAt)
            .FirstOrDefaultAsync(ct);

        if (run is null)
            return Result<ReceiptFileStatusDto>.Failure("Datei nicht gefunden."); // → 404 at endpoint

        return Result<ReceiptFileStatusDto>.Success(new ReceiptFileStatusDto(
            run.Status,
            run.CompletedAt ?? run.StartedAt,
            run.ErrorCode, // new column added in PIPE-05's migration
            run.ErrorMessage));
    }
}
```

**Frontend hook (TanStack Query v5 idiomatic pattern):**
```typescript
// Source: https://tanstack.com/query/latest/docs/framework/react/guides/polling
// Frontend/src/hooks/use-receipt-file-status.ts
import { useQuery } from "@tanstack/react-query";

const TERMINAL_STATUSES = new Set(["Completed", "Failed", "Cancelled"]);

export function useReceiptFileStatus(id: string | null) {
  return useQuery({
    queryKey: ["receipt-file-status", id],
    queryFn: () => getReceiptFileStatus(id!),
    enabled: !!id,
    // v5 signature: refetchInterval receives Query object, returns ms-number OR false-to-stop
    refetchInterval: (query) => {
      const status = query.state.data?.status;
      if (!status) return 2000; // first call before data
      return TERMINAL_STATUSES.has(status) ? false : 2000;
    },
  });
}
```

**Polling cost back-of-envelope (D-22, sanity check):** 100–500 paying users, peak concurrent active = ~50. Each upload = 1–10 files, polled every 2s while non-terminal. Average upload-resolution time ~10s with Hangfire. Steady-state QPS = (50 users × 5 files × 5 polls/file) / 60s ≈ 21 req/s on the status endpoint. Negligible — Postgres + EF handles this trivially with the existing `(receipt_file_id, started_at)` index. Defer SSE/long-poll until BetterStack shows real cost.

### Pattern 6: `IDashboardAuthorizationFilter` reading `tr_access` cookie (D-10)

**What:** Validate JWT from HttpOnly cookie, check `role` claim.

```csharp
// Backend/src/TaxReader.Api/Hangfire/HangfireAdminAuthFilter.cs
// Source: https://docs.hangfire.io/en/latest/configuration/using-dashboard.html
// Source: https://mahdi.medium.com/authorizing-hangfire-dashboard-in-net-web-api-using-jwt-tokens-e13c880cf002

public class HangfireAdminAuthFilter(IOptions<JwtOptions> jwtOptions) : IDashboardAuthorizationFilter
{
    private readonly JwtOptions _jwt = jwtOptions.Value;

    public bool Authorize(DashboardContext context)
    {
        var httpContext = context.GetHttpContext();
        // Read tr_access cookie set by AuthEndpoints at login/refresh
        if (!httpContext.Request.Cookies.TryGetValue("tr_access", out var token)
            || string.IsNullOrEmpty(token))
        {
            return false;
        }

        var handler = new JwtSecurityTokenHandler();
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

        try
        {
            var principal = handler.ValidateToken(token, validationParameters, out _);
            var role = principal.FindFirst("role")?.Value;
            return role == "admin";
        }
        catch
        {
            // Token invalid / expired / wrong signature → unauthorized
            return false;
        }
    }
}
```

**Registration:**
```csharp
// Program.cs (in API layer, AFTER UseAuthentication / UseAuthorization)
app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = new[] { new HangfireAdminAuthFilter(jwtOptionsAccessor) },
    DisplayStorageConnectionString = false, // safety: don't leak DefaultConnection in the UI
    DashboardTitle = "TaxReader Background Jobs"
});
```

**Multiple filters semantics (verified):** `[CITED: docs.hangfire.io]` "the access will be granted only if _all of them_ return `true`" — AND logic. Single filter is fine for our case.

**Anti-forgery (per `[CITED: docs.hangfire.io/...] + discuss.hangfire.io/csrf-prevention-in-hangfire`):** Hangfire 1.6.20+ auto-wires `Microsoft.AspNetCore.Antiforgery` via DI lookup. Our SameSite=Strict cookie means cross-site POSTs never carry the token; no further config needed. Don't add `IgnoreAntiforgeryTokenAttribute`.

### Pattern 7: `Channel<TesseractEngine>` pool (D-16, D-17, D-19)

**What:** Bounded channel as a thread-safe object pool with async waits.

```csharp
// Backend/src/TaxReader.Infrastructure/Services/TesseractEnginePool.cs
// Source: https://learn.microsoft.com/en-us/dotnet/core/extensions/channels
// Source: https://devblogs.microsoft.com/dotnet/an-introduction-to-system-threading-channels/

public sealed class TesseractEnginePool : IImageTextExtractor, IDisposable
{
    private readonly TesseractOptions _options;
    private readonly ILogger<TesseractEnginePool> _logger;
    private readonly Channel<TesseractEngine> _channel;
    private int _engineCount; // tracks live engines (for replacement on quarantine)

    public TesseractEnginePool(IOptions<TesseractOptions> options, ILogger<TesseractEnginePool> logger)
    {
        _options = options.Value;
        _logger = logger;
        _channel = Channel.CreateBounded<TesseractEngine>(new BoundedChannelOptions(_options.PoolSize)
        {
            FullMode = BoundedChannelFullMode.Wait, // shouldn't happen — we only Write what we Read
            SingleReader = false, // many concurrent callers acquire
            SingleWriter = false  // many concurrent callers release
        });
    }

    /// <summary>Called by TesseractEnginePoolWarmupService at startup. Idempotent.</summary>
    public void Initialize()
    {
        for (var i = 0; i < _options.PoolSize; i++)
        {
            var engine = CreateEngine();
            if (_channel.Writer.TryWrite(engine))
                Interlocked.Increment(ref _engineCount);
            else
                engine.Dispose(); // bounded channel rejected — race during init
        }
        _logger.LogInformation("Tesseract pool warmed up with {Count} engines", _engineCount);
    }

    public async Task<string> ExtractTextAsync(
        Stream imageStream,
        string mediaType,
        CancellationToken cancellationToken = default)
    {
        using var ms = new MemoryStream();
        await imageStream.CopyToAsync(ms, cancellationToken);
        var imageBytes = ms.ToArray();

        // Acquire — cancellation respects Hangfire's job CT
        var engine = await _channel.Reader.ReadAsync(cancellationToken);
        var quarantined = false;
        try
        {
            return await Task.Run(() => RunOcr(engine, imageBytes), cancellationToken);
        }
        catch (TesseractException ex)
        {
            quarantined = true;
            _logger.LogWarning(ex, "Tesseract engine threw — quarantining and replacing");
            throw; // bubble up; ProcessReceiptFileJob's AutomaticRetry can re-attempt with a fresh engine
        }
        catch (OutOfMemoryException ex)
        {
            quarantined = true;
            _logger.LogError(ex, "Tesseract engine OOM — quarantining and replacing");
            throw;
        }
        finally
        {
            if (quarantined)
            {
                // Dispose dead engine + spawn replacement (D-19)
                engine.Dispose();
                Interlocked.Decrement(ref _engineCount);
                try
                {
                    var replacement = CreateEngine();
                    if (_channel.Writer.TryWrite(replacement))
                        Interlocked.Increment(ref _engineCount);
                    else
                        replacement.Dispose();
                }
                catch (Exception spawnEx)
                {
                    _logger.LogError(spawnEx, "Failed to spawn replacement engine; pool size dropped to {Count}", _engineCount);
                }
            }
            else
            {
                // Return healthy engine
                _channel.Writer.TryWrite(engine);
            }
        }
    }

    private TesseractEngine CreateEngine()
    {
        var path = Path.IsPathRooted(_options.TessDataPath)
            ? _options.TessDataPath
            : Path.Combine(AppContext.BaseDirectory, _options.TessDataPath);
        var engine = new TesseractEngine(path, _options.Language, EngineMode.LstmOnly);
        engine.DefaultPageSegMode = PageSegMode.SingleBlock; // D-20: preserve existing config
        return engine;
    }

    private string RunOcr(TesseractEngine engine, byte[] imageBytes)
    {
        // ... identical body to existing TesseractImageTextExtractor.RunOcr but no lock ...
        // (downsample to 2400px max edge, engine.Process, OcrTextNormalizer.Normalize)
    }

    public void Dispose()
    {
        _channel.Writer.Complete();
        while (_channel.Reader.TryRead(out var engine))
            engine.Dispose();
    }
}
```

### Pattern 8: Eager warmup `IHostedService` (D-18)

```csharp
// Backend/src/TaxReader.Infrastructure/Services/TesseractEnginePoolWarmupService.cs
// Source: https://learn.microsoft.com/en-us/aspnet/core/fundamentals/host/hosted-services

public class TesseractEnginePoolWarmupService(
    IImageTextExtractor pool, // Singleton, same instance the OCR callers use
    ILogger<TesseractEnginePoolWarmupService> logger) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (pool is TesseractEnginePool concretePool)
        {
            var sw = Stopwatch.StartNew();
            concretePool.Initialize(); // serial — ~PoolSize × 100ms
            logger.LogInformation("Tesseract pool warmup complete in {Ms}ms", sw.ElapsedMilliseconds);
        }
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

// Registration in DependencyInjection.cs:
services.AddSingleton<TesseractEnginePool>();
services.AddSingleton<IImageTextExtractor>(sp => sp.GetRequiredService<TesseractEnginePool>());
services.AddHostedService<TesseractEnginePoolWarmupService>();
```

The `IHostedService.StartAsync` runs BEFORE `/health` returns 200, so a load balancer probing health waits for warmup. At PoolSize=3 default, this is ~300ms — negligible.

### Pattern 9: `RecurringJob.AddOrUpdate` for cleanup (D-23)

```csharp
// In Program.cs, after app.Build() and DI is available:
// Source: https://docs.hangfire.io/en/latest/background-methods/performing-recurrent-tasks.html

using (var scope = app.Services.CreateScope())
{
    var recurringJobs = scope.ServiceProvider.GetRequiredService<IRecurringJobManager>();

    // Daily 03:00 UTC: expired refresh tokens (D-23 #1)
    recurringJobs.AddOrUpdate<RefreshTokenCleanupJob>(
        "refresh-tokens-cleanup",
        job => job.HandleAsync(CancellationToken.None),
        "0 3 * * *", // standard 5-field cron: minute hour day month day-of-week
        new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });

    // Weekly Sunday 04:00 UTC: prune old Failed Hangfire jobs (D-23 #2)
    recurringJobs.AddOrUpdate<HangfireFailedJobCleanupJob>(
        "hangfire-failed-cleanup",
        job => job.HandleAsync(CancellationToken.None),
        "0 4 * * 0",
        new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });
}
```

**Critical:** `DisableConcurrentExecution` attribute on cleanup job classes prevents double-execution if a previous run is still in progress when the schedule triggers (`[CITED: pedrocons.com/.../how-to-schedule-recurring-jobs-in-net-using-hangfire-and-postgresql]`):

```csharp
[DisableConcurrentExecution(timeoutInSeconds: 600)]
[AutomaticRetry(Attempts = 0)] // cleanup runs daily; failed run retries next day
public class RefreshTokenCleanupJob(IAppDbContext db) { ... }
```

### Pattern 10: `UploadErrorCatalog` (D-21)

```csharp
// Backend/src/TaxReader.Application/Common/UploadErrorCatalog.cs
public static class UploadErrorCatalog
{
    public static (string ErrorCode, string GermanMessage) Classify(Exception ex) => ex switch
    {
        // Domain-specific
        NoTextExtractedException => ("NoTextExtracted",
            "Aus dieser Datei konnte kein Text gelesen werden. "
            + "Tipp: Digitale Rechnungen (z.B. Amazon) bitte als PDF hochladen, nicht als Screenshot."),
        ParserNotFoundException => ("ParserMissing",
            "Format der Datei wird derzeit nicht unterstützt."),
        InsufficientTokensException => ("InsufficientTokens",
            "Keine Tokens verfügbar – bitte Credits aufladen."),
        OperationCanceledException => ("Cancelled",
            "Vorgang abgebrochen."),

        // Anthropic / AI failures
        HttpRequestException httpEx when httpEx.Message.Contains("Anthropic") => ("AiUnavailable",
            "KI-Klassifizierung derzeit nicht verfügbar — bitte später erneut versuchen."),

        // Fallback
        _ => ("Unknown",
            "Verarbeitung fehlgeschlagen — bitte erneut versuchen oder Support kontaktieren.")
    };
}
```

Used at job-failure boundary:
```csharp
catch (Exception ex)
{
    var (errorCode, germanMessage) = UploadErrorCatalog.Classify(ex);
    logger.LogError(ex, "{ErrorCode} during ProcessReceiptFileJob for ReceiptFile {ReceiptFileId}",
        errorCode, receiptFileId);
    run.ErrorCode = errorCode;
    run.ErrorMessage = germanMessage; // safe German string only; ex.Message never persisted
    run.Status = ProcessingStatus.Failed;
    await dbContext.SaveChangesAsync(CancellationToken.None);
    throw; // let AutomaticRetry attempt retries; on final failure Hangfire moves to Failed state
}
```

### Pattern 11: Empty/loading/error UI (D-22)

Reuse existing shadcn primitives from `Frontend/src/components/ui/`:
- `Skeleton` for in-flight rows (`receipts-table.tsx` while polling shows `Queued`/`Extracting`/`Parsing`/`Classifying`)
- `Alert` + `AlertCircle` (lucide-react) for terminal-error states (Failed/Cancelled with `errorMessage`)
- `sonner` toast for "Vorgang abgebrochen" / "Cancellation failed" after the cancel mutation resolves
- Disabled-cancel-button while `state in TERMINAL_STATUSES`

No new UI primitives. Per-page state machine:

| Page | Empty | Loading | Error |
|------|-------|---------|-------|
| `upload/page.tsx` | "Noch keine Belege hochgeladen." | Skeleton card per file while status is non-terminal | Inline `Alert` with German `errorMessage` |
| `receipts/page.tsx` | "Noch keine Belege erfasst." | Spinner header while any row is non-terminal (existing `useReceipts` polling pattern) | Per-row badge "Fehlgeschlagen" with tooltip showing `errorMessage` |
| `receipts/[id]/page.tsx` | (impossible — page wouldn't load) | `Skeleton` over receipt-item table | Full-page `Alert` if status terminal-failed |
| `dashboard/page.tsx` | "Noch keine Daten." with `EmptyState` (existing pattern) | Skeleton on stat cards | `Alert` per failed widget |
| `reports/page.tsx` | "Keine bestätigten Belege im Jahr {year}." | Skeleton on chart | `Alert` if export fails |

### Anti-Patterns to Avoid

- **`ConcurrentBag<TesseractEngine>` + `SemaphoreSlim`** — predates `Channel<T>`; reinvents what `Channel.CreateBounded` does idiomatically. `[VERIFIED: learn.microsoft.com]` Channels supersede this pattern for new code.
- **Returning a dead `TesseractEngine` to the channel** — re-using a quarantined engine causes the next caller to throw inside Tesseract native code (often as `SEHException` per `[CITED: github.com/charlesw/tesseract/issues/228]`). Always Dispose on exception path; replace via the same code path.
- **Setting `tr_access` cookie inside `AuthService`** — breaks the Phase 2 02-01 invariant "RefreshTokenService stays HTTP-context-free" (extended to AuthService by symmetry). Set the cookie in the API endpoint layer via `Response.Cookies.Append`.
- **Calling Hangfire's `BackgroundJob.Enqueue` directly from Application** — would force Application to reference Hangfire. Wrap in `IBackgroundJobClient` interface in Application; implement in Infrastructure.
- **Adding `JsonStringEnumConverter` globally** — would silently change every existing enum endpoint serialization. The status DTO can opt in via `[JsonConverter(typeof(JsonStringEnumConverter))]` on the property if PascalCase strings are wanted.
- **Running EF migrations AFTER `AddHangfireServer`** — Hangfire's schema creation needs Postgres available; EF's `MigrateAsync` runs the migrations our entities need. Order matters: `MigrateAsync` first, then `AddHangfireServer` (which lazily creates schema on first connection). Current `Program.cs:298-303` runs `MigrateAsync` after the `app.Build()`, which is correct.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Background-job retry with backoff | Custom polling loop + exception catch + `Task.Delay` | `[AutomaticRetry(Attempts=N, DelaysInSeconds=[...])]` | Hangfire handles persistence, retry count tracking, idempotency, and crash recovery |
| Recurring scheduler (cleanup) | `Timer` + `IHostedService` with `Task.Delay` | `RecurringJob.AddOrUpdate` with cron | Survives container restart, deduplicates concurrent runs (`DisableConcurrentExecution`), exposes in dashboard |
| Job persistence across container restart | Custom `pending_jobs` table | Hangfire's invisibility timeout + Postgres storage | Battle-tested at high throughput; idempotency contract documented |
| Pool of expensive objects | Custom `BlockingCollection<T>` or `SemaphoreSlim` + `ConcurrentBag<T>` | `Channel.CreateBounded<T>` | Async-first, built into BCL, backpressure semantics documented |
| JWT validation in dashboard filter | Custom token decoder + signature verifier | `JwtSecurityTokenHandler.ValidateToken` (already used in Program.cs:64-79) | Bug-resistant; uses same secret + validation params as the API itself |
| Cron parser | Custom interval-string parser | Hangfire's built-in `Cron` helpers + standard 5-field cron expressions | Tested across timezones; `RecurringJobOptions.TimeZone` integrates with .NET's `TimeZoneInfo` |
| HTTP→Job correlation ID | Bespoke header propagation | `LogContext.PushProperty("JobId", id)` + Serilog enrichers (already wired Phase 1 D-17) | Same logs surface to Sentry via Phase 1 D-14 |
| German error string lookup | Inline switch in every catch site | `UploadErrorCatalog.Classify(ex)` (Pattern 10) | One place to audit messages; matches `Result<T>` convention |

**Key insight:** Hangfire's value-add IS exactly the things you'd otherwise hand-roll (retry, persistence, scheduling, dashboard). Lean on the framework; don't reinvent.

## Runtime State Inventory

> Phase 3 is mostly greenfield code with config additions. This section catalogues incidental runtime state introduced.

| Category | Items Found | Action Required |
|----------|-------------|------------------|
| Stored data | (a) New Hangfire schema in Postgres — auto-created on first connection via `PrepareSchemaIfNecessary=true`; (b) New `users.is_admin` column (NOT NULL DEFAULT false) and new `processing_runs.error_code` column added via EF migrations; (c) Two new `ProcessingStatus` enum values (`Queued=1, Cancelled=7`) — `processing_runs.status` int column re-mapped per D-06's new numeric order | Code edit + data migration. Note: D-06 reorders existing enum values (`Extracting=1` → `=2`, etc.). Existing rows with `status=1` will be mis-interpreted as `Queued` after migration. Mitigation: a one-shot UPDATE statement in the migration's `Up()` method renumbers existing rows BEFORE the enum is reordered conceptually. Planner MUST verify this migration is correct on a populated DB. |
| Live service config | `Tesseract__PoolSize` (new), `Hangfire__SeedAdminEmails` (new) — these live in `docker-compose.yml` and `.env`. `docker-compose.yml` is in git; `.env` is gitignored. Cookie-related env vars: NONE; `tr_access` cookie config is code-level. | Code edit (compose.yml + .env.example committed; `.env` is operator's responsibility) |
| OS-registered state | None — no Windows services, no scheduled tasks, no systemd units affected by this phase | None |
| Secrets/env vars | `Hangfire__SeedAdminEmails` (new) is a list of admin emails, not a secret per se; visible in compose; no rotation needed | Documentation in `.env.example` and `OPERATIONS.md` if it exists (it doesn't yet) |
| Build artifacts / installed packages | New NuGet packages (`Hangfire.Core`, `Hangfire.AspNetCore`, `Hangfire.PostgreSql`) — central package management in `Directory.Packages.props`. NO container image changes — Hangfire is pure managed code, no native deps. Tesseract is unchanged (apt-installed in `Backend/Dockerfile` per `[VERIFIED: Backend/Dockerfile lines 12-18]`). | Standard `dotnet restore` + `docker compose build api` cycle |

**Critical migration ordering issue (call out for planner):** The `ProcessingStatus` enum reorder in D-06 (existing `Extracting=1` becomes `Queued=1`, etc.) means a naïve `[Column<Status>]` mapping change WILL silently corrupt existing `processing_runs.status` rows. The migration `AddQueuedAndCancelledProcessingStatuses` must:
1. Read existing rows
2. UPDATE: `Failed (was=5)` → `Failed (new=6)`, `Completed (was=4)` → `Completed (new=5)`, `Classifying (was=3)` → `Classifying (new=4)`, `Parsing (was=2)` → `Parsing (new=3)`, `Extracting (was=1)` → `Extracting (new=2)`. Process in descending order to avoid colliding with old `Queued`/`Cancelled` placeholders (there are none — both are new).
3. Now safe to map `Queued=1`, `Cancelled=7`.

Alternatively (cleaner): append new values WITHOUT renumbering — `Pending=0, Extracting=1, Parsing=2, Classifying=3, Completed=4, Failed=5, Queued=6, Cancelled=7`. This contradicts D-06's stated numeric order but preserves DB integrity without an in-place renumber. **Planner should re-confirm D-06 numeric order with user, or accept the appended-values variant.** This is a `[ASSUMED]` recommendation — D-06's "new numeric order" wording suggests intent for clean ordering, but the data-migration cost may justify accepting non-sequential ordering.

## Common Pitfalls

### Pitfall 1: Hangfire schema migration ordering with `RUN_MIGRATIONS=true`

**What goes wrong:** Two schema-management systems (EF migrations, Hangfire's `PrepareSchemaIfNecessary`) run independently. If `AddHangfireServer` initializes before EF's `MigrateAsync`, Hangfire connects, creates its schema, then EF migrations run separately. No conflict (separate schemas) — but the ordering is fragile.
**Why it happens:** `Program.cs` order: `builder.Services.AddInfrastructure` (registers Hangfire services but doesn't start them) → `app.Build()` → `MigrateAsync` (line 302) → `app.UseHangfireDashboard` and the implicit `AddHangfireServer`-spun BackgroundJobServer starts on first DI resolution.
**How to avoid:** Confirm via code review that `dbContext.Database.MigrateAsync()` runs BEFORE `app.UseHangfireDashboard` and BEFORE any code that resolves `IBackgroundJobClient`. Current Phase 3 plan: EF migrations at `Program.cs:298-303`, Hangfire dashboard registration AFTER that, no Hangfire job enqueueing until endpoints start receiving requests. Order is correct as-is.
**Warning signs:** First-boot fails with "table users does not exist" inside Hangfire's job-deserialization path → migration ran AFTER Hangfire started.

### Pitfall 2: Hand-rolled barrier double-enqueueing the classify job

**What goes wrong:** Two `ProcessReceiptFileJob` parents finish near-simultaneously, both read `completedCount == batchSize`, both enqueue `ClassifyBatchJob`. Token pre-charge fires twice → user double-charged.
**Why it happens:** Read-then-enqueue is not atomic; concurrent parents race.
**How to avoid:** Either (a) `pg_advisory_xact_lock(upload_batch_id::bigint)` around the count-and-enqueue, or (b) `ClassifyBatchJob` checks `processing_runs.status` for any items already in `Classifying` state and exits if so. Idempotency at the job entry is the simpler safer approach.
**Warning signs:** Double-spent tokens after a busy upload; Hangfire dashboard shows 2 `ClassifyBatchJob` entries for the same `upload_batch_id`.

### Pitfall 3: Cancellation racing with terminal-state transitions

**What goes wrong:** User clicks cancel just as `ClassifyBatchJob` finishes the Anthropic call. `BackgroundJob.Delete(jobId)` returns success (Hangfire purges the job record), but the in-flight Task continues to write `Status=Completed` and commits tokens. User sees "Cancelled" briefly then "Completed" with tokens debited.
**Why it happens:** Cancel checks "is the job not terminal yet" then schedules deletion; the worker may have already moved past `cancellationToken.IsCancellationRequested` check.
**How to avoid:** In the cancel handler, FIRST mark `processing_runs.status = Cancelled` (no-op if already terminal), THEN call `IBackgroundJobClient.DeleteAsync`. The job, on its next `cancellationToken.ThrowIfCancellationRequested()` or `Task.Delay(ct)`, sees the cancel. Worst case (cancel arrived during Anthropic call): the AI call's existing `try/catch` will see `TaskCanceledException`, run the refund branch in `AiOnlyClassificationService.cs:71-75`, and exit. Status is already `Cancelled` per the cancel handler's update; the job's "mark Completed" doesn't fire (it's after the catch).
**Warning signs:** Tokens charged on a status-Cancelled run; per-user disputes about cancel.

### Pitfall 4: Caddy reverse-proxy strips/rewrites cookie attributes for `/hangfire`

**What goes wrong:** `tr_access` cookie set with `Path=/hangfire` on the API server; Caddy proxies the response through to the browser. By default, Caddy does NOT rewrite cookie paths (`[CITED: caddy.community/t/reverse-proxy-cookie-set-and-read/3669]`). The browser sees `Path=/hangfire` and only sends the cookie on `GET /hangfire/*` requests.
**Why it happens:** This is the desired behavior — but if any redirect chain (login flow) returns the user to a non-`/hangfire` path, the cookie is set but invisible from `/` until the user navigates to `/hangfire`.
**How to avoid:** Set `tr_access` cookie at login (immediately, regardless of where the user navigates next). Verify in dev that `document.cookie` is empty (because HttpOnly) but `curl -b cookies.txt https://localhost/hangfire` works after login. Test via the integration WAF: a successful `/auth/login` followed by `GET /hangfire` should not require a separate auth flow.
**Warning signs:** Login appears to succeed, SPA works fine, but `/hangfire` returns 401 because the cookie wasn't set yet (login response only set localStorage tokens, not the cookie).

### Pitfall 5: Hangfire CSRF anti-forgery blocking dashboard POST actions

**What goes wrong:** Clicking "Requeue" or "Delete" on the Hangfire dashboard returns 403.
**Why it happens:** Hangfire 1.6.20+ enables CSRF anti-forgery by default in ASP.NET Core via `Microsoft.AspNetCore.Antiforgery`. The dashboard's POST routes are protected.
**How to avoid:** Most cases this Just Works — Hangfire auto-wires the antiforgery token from the cookie set on the first dashboard GET. Our SameSite=Strict cookie posture supports this (same-origin POST inherits the cookie). Verify: open `/hangfire`, refresh, requeue a Failed job → 200, not 403.
**Warning signs:** 403 on POST `/hangfire/jobs/.../requeue`. Solution if it persists: add `Filters = new[] { new IgnoreAntiforgeryTokenAttribute() }` to `DashboardOptions` — but only if CSRF posture is otherwise covered (it is, via SameSite=Strict).

### Pitfall 6: `Channel.Writer.TryWrite` returning false silently

**What goes wrong:** Pool replacement engine fails to enter the channel (e.g., during a race where another release happens at the same time). Engine is leaked OR the pool size drops permanently.
**Why it happens:** Bounded channel rejects writes when capacity is hit. `TryWrite` returns false; the engine reference is lost. Or, the replacement raced with someone else's write and was rejected.
**How to avoid:** Always check `TryWrite` return value. If false, `engine.Dispose()` the engine and log a warning. Track `_engineCount` via `Interlocked` and log when it drifts below `PoolSize` for steady-state monitoring.
**Warning signs:** OCR throughput drops over hours; "Tesseract pool warmup complete" log line says 3 engines but later acquire times grow unboundedly.

### Pitfall 7: `WorkerCount > PoolSize` causes silent contention

**What goes wrong:** Hangfire `WorkerCount = 20` (default!) with `PoolSize = 3` means 17 of 20 workers can block indefinitely on `Channel.Reader.ReadAsync` while waiting for an OCR engine. Throughput collapses to 3-parallel.
**Why it happens:** Default `BackgroundJobServerOptions.WorkerCount` is `Math.Min(20, Environment.ProcessorCount * 5)`. We must override.
**How to avoid:** Explicit `options.WorkerCount = poolSize` in `AddHangfireServer` (Pattern 1). Documented at the registration site.
**Warning signs:** Hangfire dashboard shows many jobs in "Processing" state but few completing per minute; logs show many "acquiring Tesseract engine" entries blocked on ReadAsync.

### Pitfall 8: `ProcessingRun.Status` enum reorder corrupts existing rows

**What goes wrong:** D-06 reorders the enum (`Extracting=1` → `Queued=1`, etc.). Existing `processing_runs` rows have integer status values that no longer map to the same name. A row stored as `status=1` ("Extracting" pre-migration) is read as "Queued" post-migration.
**Why it happens:** EF maps enum-to-int by value; the values changed. No DB-level safeguard catches this — the column is just `integer`.
**How to avoid:** Either (a) append new values without renumbering (`Queued=6, Cancelled=7`) — safe but contradicts D-06's stated order, or (b) write an in-migration UPDATE that renumbers existing rows in descending order (5→6, 4→5, 3→4, 2→3, 1→2, leaving 0 unchanged) BEFORE the enum reordering takes effect at the EF layer. See "Runtime State Inventory" above.
**Warning signs:** Post-migration, all in-flight uploads appear "Queued" in the UI even though they were previously "Extracting" / "Parsing"; receipts already completed show wrong status.

## Code Examples

Verified patterns from official sources (collected above in Architecture Patterns). Additional snippets:

### Setting the `tr_access` cookie in the endpoint layer

```csharp
// Backend/src/TaxReader.Api/Endpoints/AuthEndpoints.cs
// AuthService stays HTTP-context-free per Phase 2 02-01 invariant

auth.MapPost("/login", async (
    LoginRequest request,
    IAuthService authService,
    IOptions<JwtOptions> jwtOptions,
    HttpContext httpContext,
    CancellationToken ct) =>
{
    var userAgent = httpContext.Request.Headers.UserAgent.ToString();
    var ip = httpContext.Connection.RemoteIpAddress?.ToString();

    var result = await authService.LoginAsync(request, userAgent, ip, ct);
    if (result.IsFailure)
        return Results.Json(new { error = result.Error }, statusCode: 401);

    // D-10: set tr_access cookie for /hangfire dashboard
    httpContext.Response.Cookies.Append("tr_access", result.Value.AccessToken, new CookieOptions
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
.RequireRateLimiting("auth-strict");

// Logout endpoint clears the cookie
auth.MapPost("/logout", (HttpContext httpContext) =>
{
    httpContext.Response.Cookies.Delete("tr_access", new CookieOptions
    {
        Path = "/hangfire" // must match Set-Cookie Path for browser to clear it
    });
    return Results.NoContent();
});
```

### The cancel endpoint (D-14)

```csharp
// Backend/src/TaxReader.Api/Endpoints/ReceiptFileEndpoints.cs (excerpt — NEW endpoint)

receiptFiles.MapPost("/{id:guid}/cancel", async (
    Guid id,
    CancelReceiptFileHandler handler,
    CancellationToken ct) =>
{
    var result = await handler.HandleAsync(id, ct);
    if (result.IsSuccess) return Results.NoContent(); // 204
    return result.Error switch
    {
        "NotFound" => Results.NotFound(new { error = "Datei nicht gefunden." }),
        "TerminalState" => Results.Conflict(new { error = "Verarbeitung bereits abgeschlossen." }),
        _ => Results.BadRequest(new { error = result.Error })
    };
})
.WithName("CancelReceiptFile")
.WithSummary("Cancel an in-flight receipt-file processing job");

// Handler:
public class CancelReceiptFileHandler(
    IAppDbContext db,
    IBackgroundJobClient jobClient,
    ICurrentUser currentUser)
{
    public async Task<Result<bool>> HandleAsync(Guid receiptFileId, CancellationToken ct)
    {
        var file = await db.ReceiptFiles
            .Include(f => f.ProcessingRuns)
            .Where(f => f.Id == receiptFileId && f.UserId == currentUser.UserId)
            .FirstOrDefaultAsync(ct);

        if (file is null)
            return Result<bool>.Failure("NotFound");

        var latestRun = file.ProcessingRuns.OrderByDescending(r => r.StartedAt).First();
        if (IsTerminal(latestRun.Status))
            return Result<bool>.Failure("TerminalState");

        // 1. Mark Cancelled FIRST (so concurrent job doesn't flip to Completed)
        latestRun.Status = ProcessingStatus.Cancelled;
        latestRun.CompletedAt = DateTime.UtcNow;
        latestRun.ErrorCode = "Cancelled";
        latestRun.ErrorMessage = "Vorgang abgebrochen.";
        await db.SaveChangesAsync(ct);

        // 2. Then signal Hangfire to terminate the job
        await jobClient.DeleteAsync(file.JobId, ct); // new column: receipt_files.job_id

        return Result<bool>.Success(true);
    }

    private static bool IsTerminal(ProcessingStatus s) =>
        s == ProcessingStatus.Completed || s == ProcessingStatus.Failed || s == ProcessingStatus.Cancelled;
}
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| `IJobCancellationToken.ThrowIfCancellationRequested()` | Plain `CancellationToken` parameter on job methods | Hangfire 1.7.0 (2019) | Fully async; safe in tight loops; passes naturally to `HttpClient`, EF, etc. Per `[CITED: docs.hangfire.io/.../using-cancellation-tokens.html]` |
| Hangfire schema management via separate migration tool | `PrepareSchemaIfNecessary=true` first-connection auto-create | Hangfire.PostgreSql 1.20.13+ | Simplifies first-boot; explicit opt-out via `false` only if you want to manage Hangfire schema in your own EF migrations |
| `WithFilter(new AutomaticRetryAttribute(...))` chained on `AddHangfire` config | `[AutomaticRetry(Attempts=N, DelaysInSeconds=[...])]` attribute on job class | Hangfire 1.6+ | Per-job retry policy is more discoverable than global filter chains |
| `ConcurrentBag<T>` + `SemaphoreSlim` for object pools | `Channel.CreateBounded<T>(capacity)` | .NET Core 2.1+ | Async-first; backpressure semantics built-in; per `[CITED: devblogs.microsoft.com/dotnet/an-introduction-to-system-threading-channels/]` |
| Hangfire dashboard auth via `app.UseHangfireDashboard("...", new DashboardOptions { Authorization = [...] })` | (unchanged) | Hangfire 1.6.20 added built-in CSRF; auto-wires in ASP.NET Core | Just works; no `IgnoreAntiforgeryTokenAttribute` needed for same-origin admin flow |

**Deprecated/outdated:**
- Storing engine pool in `ConcurrentBag` with manual `SemaphoreSlim` — superseded by `Channel.CreateBounded` since .NET Core 2.1 (the SDK we have)
- `Hangfire.Pro` Batches for fan-in coordination — NOT needed; hand-rolled barrier is standard for free-tier Hangfire
- Setting `Sentry.OptionsBeforeSend` (deprecated) — Phase 1 D-14 already moved to `SetBeforeSend`; new error events from Phase 3 inherit

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | Anthropic charges for partial inference even when HttpClient cancels mid-call | Pattern 4, mid-Anthropic cancellation | Token economy footnote; low impact at scale (single-digit cancel events per day expected). Verify if cancellation becomes frequent. |
| A2 | Hangfire.PostgreSql 1.21.1 is fully compatible with Npgsql 10.0.1 (we have transitively) | Standard Stack | Build failure or runtime exception during first DB call. Test in Plan 03-01 by running `dotnet build` + smoke test against a real Postgres container. |
| A3 | D-06's numeric reorder of `ProcessingStatus` is intended; user accepts data migration cost | Runtime State Inventory, Pitfall 8 | If user wanted simple appended values (`Queued=6, Cancelled=7`), the in-migration UPDATE statement is unnecessary work. Planner should re-confirm with user during plan-checker review. |
| A4 | The hand-rolled barrier (Pattern 2) is acceptable to user; not preferring Hangfire.Pro Batches license | Pattern 2 | If user wants the cleaner Batches API, planner must surface a paid-license decision. |
| A5 | Caddy will not strip the `tr_access` cookie attributes (Path=/hangfire, SameSite=Strict) | Pitfall 4 | Cookie may not be set as intended; integration test (Plan 03-01) must verify cookie roundtrip via a real Caddy hop. |
| A6 | The Phase 3 polling cost (~21 req/s steady-state at 100–500 user scale) is acceptable | Pattern 5 polling cost analysis | If real traffic is heavier than estimated, BetterStack (Phase 7) will surface the cost; deferred per Deferred Ideas. |
| A7 | `TesseractEngine` failures (`TesseractException`, OOM) are rare enough that quarantine-and-replace at ~100ms per replacement doesn't degrade end-user UX | Pattern 7 D-19 | If engine failures cluster (e.g., a sequence of malformed images), pool can shrink to zero between replacements. Mitigation in code via the `_engineCount` invariant log line; production-monitor via Sentry. |
| A8 | Tesseract 5.2.0 from `charlesw/tesseract` does NOT fundamentally panic when running across a `Channel<T>`-mediated single-ownership pool, contrary to the historical SEHException reports | Pattern 7 + Pitfall 6 | If single-ownership Channel pool still hits SEHException, fallback is to keep the current Singleton+lock pattern (D-16/D-17 design fails) and document throughput limit. Verify in Plan 03-03 with a stress test (10 concurrent uploads). |

**If this table is empty:** N/A — 8 assumptions need confirmation. A3 and A8 are highest-risk and should be addressed before plan execution begins.

## Open Questions

1. **D-06 numeric reorder — accept data migration cost or use appended values?**
   - What we know: D-06 specifies `Pending=0, Queued=1, Extracting=2, …, Cancelled=7`.
   - What's unclear: Whether the user/planner wants the in-place renumber migration (with the UPDATE statement risk in Pitfall 8) vs the simpler appended-values approach (`Queued=6, Cancelled=7`).
   - Recommendation: Plan checker should re-prompt with the data-migration cost spelled out. Default to appended values unless user explicitly wants the clean order.

2. **JobId column on `receipt_files` or `processing_runs`?**
   - What we know: D-14's cancel endpoint needs to delete a Hangfire job by ID; the ID must persist somewhere queryable.
   - What's unclear: Whether `receipt_files.hangfire_job_id` or `processing_runs.hangfire_job_id` is the natural home. The cancel handler in Code Examples assumes `receipt_files.JobId`. ProcessingRun may be a better fit (it tracks the actual job execution; receipt_files is more identity-of-the-file).
   - Recommendation: Add `hangfire_job_id` column to `processing_runs`. The latest run carries the cancel target.

3. **TesseractEngine concurrency — confirm single-ownership-via-Channel works**
   - What we know: Single ownership semantics + bounded Channel = no two concurrent threads touch the same engine.
   - What's unclear: Whether Tesseract's documented "not thread-safe" extends to "fails inside a process even with single-thread access at a time" (the historical SEHException pattern reported by `charlesw/tesseract` users).
   - Recommendation: Smoke-test in Plan 03-03 with a 10-image concurrent upload. Fall back to Singleton+lock + decorate it with a German error if pool fails.

4. **Polling on receipts list page — global or per-row enabled?**
   - What we know: D-22 requires the receipts list to reflect live status while any row is non-terminal.
   - What's unclear: Whether to add one polling hook per non-terminal row (N TanStack Query instances) or one batched polling endpoint returning the status array for visible rows.
   - Recommendation: Per-row hook (Pattern 5 reused); TanStack Query dedupes and batches under the hood; simpler to reason about cancel/terminal logic per row.

5. **Anti-forgery and `IgnoreAntiforgeryTokenAttribute` — confirm same-origin POST works without it**
   - What we know: Hangfire 1.6.20+ auto-wires CSRF via `Microsoft.AspNetCore.Antiforgery`. SameSite=Strict cookie + same-origin admin posture should satisfy it without the bypass attribute.
   - What's unclear: Whether the dashboard's "Requeue" / "Delete" buttons in Hangfire 1.8.23 work as expected through Caddy without `IgnoreAntiforgeryTokenAttribute`.
   - Recommendation: Manual UAT in Plan 03-01: log in as admin, attempt to requeue a Failed job, confirm 200 not 403. If 403, add the attribute and document why.

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| PostgreSQL | Hangfire storage (shares the `belegpilot` DB via separate `hangfire` schema) | ✓ | 17 (docker-compose.yml line 3) | — |
| .NET 10 SDK | Build | ✓ | 10.0.0+ (`<TargetFramework>net10.0</TargetFramework>` in `Backend/Directory.Build.props`) | — |
| Docker Compose v2 | Local development + production | ✓ | (assumed installed per `Backend/Dockerfile` + `docker-compose.yml`) | — |
| Tesseract OCR with deu+eng | `TesseractEnginePool` engine init | ✓ | Container: `tesseract-ocr-deu`+`tesseract-ocr-eng` apt packages (Backend/Dockerfile lines 12-18) | None — image-OCR receipts blocked if absent; documented behavior at `TesseractImageTextExtractor.cs:94-101` |
| Caddy 2-alpine | TLS edge + reverse proxy to `/hangfire` | ✓ | docker-compose.yml line 58 | — |
| Anthropic API access | `ClassifyBatchJob` AI call | ✓ (assumed — Phase 1 D-01 locked the model) | `claude-haiku-4-5` | Already handled: `aiClassifier.IsConfigured` check at `AiOnlyClassificationService.cs:35`; everything → Unknown if missing |

**Missing dependencies with no fallback:** None.

**Missing dependencies with fallback:** None — the existing fallbacks (Tesseract missing → German error from `TesseractImageTextExtractor.cs:94-101`; Anthropic missing → all-Unknown classification) continue to work in Phase 3.

## Validation Architecture

### Test Framework

| Property | Value |
|----------|-------|
| Framework | xUnit 2.9.2 + FluentAssertions 7.0.0 + Moq 4.20.72 (existing — `[VERIFIED: Backend/tests/TaxReader.UnitTests/TaxReader.UnitTests.csproj`) |
| Config file | `Backend/tests/TaxReader.UnitTests/TaxReader.UnitTests.csproj` (no separate `xunit.runner.json`) |
| Quick run command | `dotnet test Backend/tests/TaxReader.UnitTests --filter "FullyQualifiedName~Pipeline"` |
| Full suite command | `dotnet test Backend` |

WebApplicationFactory<Program> integration tests use the existing serialization pattern (`RateLimiterTestCollection` `[CollectionDefinition(DisableParallelization = true)]`) — `[VERIFIED: Backend/tests/TaxReader.UnitTests/RateLimiting/RateLimiterTestCollection.cs:11-15]`. Phase 3 should reuse this pattern: introduce `HangfireTestCollection` (or reuse `RateLimiterTestCollection` if dashboard tests are also WAF-based).

### Phase Requirements → Test Map

| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| PIPE-01 | Hangfire dashboard returns 401 to anonymous | integration (WAF) | `dotnet test --filter "FullyQualifiedName~HangfireDashboardAnonymousReturns401"` | ❌ Wave 0 |
| PIPE-01 | Dashboard returns 200 with admin JWT in `tr_access` cookie | integration (WAF) | `dotnet test --filter "FullyQualifiedName~HangfireDashboardAdminCookieReturns200"` | ❌ Wave 0 |
| PIPE-01 | Dashboard returns 401 with valid JWT lacking admin role | integration (WAF) | `dotnet test --filter "FullyQualifiedName~HangfireDashboardNonAdminReturns401"` | ❌ Wave 0 |
| PIPE-01 | RecurringJob.AddOrUpdate registers refresh-token-cleanup job at startup | source-level structural-grep | `dotnet test --filter "FullyQualifiedName~RecurringJobsBootstrap"` | ❌ Wave 0 |
| PIPE-01 | `SeedAdminUsersHostedService` flips `IsAdmin=true` for matching emails | unit | `dotnet test --filter "FullyQualifiedName~SeedAdminUsersHostedService"` | ❌ Wave 0 |
| PIPE-02 | `POST /receipt-files` returns 202 with `{ files: [{receiptFileId, jobId, fileName}] }` | integration (WAF) | `dotnet test --filter "FullyQualifiedName~UploadReceiptFilesReturns202"` | ❌ Wave 0 |
| PIPE-02 | `ProcessReceiptFileJob.HandleAsync` extract+parse+persist for a PDF | unit | `dotnet test --filter "FullyQualifiedName~ProcessReceiptFileJob_Pdf_PersistsReceipt"` | ❌ Wave 0 |
| PIPE-02 | Barrier: last completing parent enqueues `ClassifyBatchJob` exactly once | unit | `dotnet test --filter "FullyQualifiedName~ProcessReceiptFileJob_LastParent_EnqueuesClassify"` | ❌ Wave 0 |
| PIPE-02 | Race: two concurrent parents do NOT double-enqueue ClassifyBatchJob | unit | `dotnet test --filter "FullyQualifiedName~ProcessReceiptFileJob_RaceCondition_SingleClassifyEnqueue"` | ❌ Wave 0 |
| PIPE-02 | `ClassifyBatchJob` preserves AiOnlyClassificationService token pre-charge + refund pattern | unit | `dotnet test --filter "FullyQualifiedName~ClassifyBatchJob_TokenRefundOnAiFailure"` | ❌ Wave 0 |
| PIPE-02 | `[AutomaticRetry(Attempts=3, DelaysInSeconds=[30,120,300])]` attribute applied to `ProcessReceiptFileJob.HandleAsync` | source-level structural-grep | `dotnet test --filter "FullyQualifiedName~ProcessReceiptFileJob_HasRetryAttribute"` | ❌ Wave 0 |
| PIPE-02 | `[AutomaticRetry(Attempts=0)]` on `ClassifyBatchJob.HandleAsync` | source-level structural-grep | `dotnet test --filter "FullyQualifiedName~ClassifyBatchJob_HasNoRetryAttribute"` | ❌ Wave 0 |
| PIPE-03 | `GET /receipt-files/{id}/status` returns D-13 DTO for non-terminal state | integration (WAF) | `dotnet test --filter "FullyQualifiedName~GetReceiptFileStatus_NonTerminal_ReturnsDto"` | ❌ Wave 0 |
| PIPE-03 | `GET /receipt-files/{id}/status` returns 404 when file not owned by user | integration (WAF) | `dotnet test --filter "FullyQualifiedName~GetReceiptFileStatus_ForeignUser_Returns404"` | ❌ Wave 0 |
| PIPE-03 | `POST /receipt-files/{id}/cancel` returns 204 + marks Cancelled | integration (WAF) | `dotnet test --filter "FullyQualifiedName~CancelReceiptFile_NonTerminal_Returns204"` | ❌ Wave 0 |
| PIPE-03 | `POST /receipt-files/{id}/cancel` returns 409 on terminal state | integration (WAF) | `dotnet test --filter "FullyQualifiedName~CancelReceiptFile_Terminal_Returns409"` | ❌ Wave 0 |
| PIPE-03 | `POST /receipt-files/{id}/cancel` returns 404 for foreign-user file | integration (WAF) | `dotnet test --filter "FullyQualifiedName~CancelReceiptFile_ForeignUser_Returns404"` | ❌ Wave 0 |
| PIPE-03 | Cancellation during job execution refunds tokens via `AiOnlyClassificationService.cs:71-75` branch | unit | `dotnet test --filter "FullyQualifiedName~ClassifyBatchJob_CancelDuringAiCall_RefundsAll"` | ❌ Wave 0 |
| PIPE-04 | `TesseractEnginePool` capacity-3 with 5 concurrent acquires queues 2 | unit | `dotnet test --filter "FullyQualifiedName~TesseractEnginePool_FiveConcurrentAcquires_QueuesTwo"` | ❌ Wave 0 |
| PIPE-04 | `TesseractEnginePool` quarantines engine on `TesseractException` and spawns replacement | unit | `dotnet test --filter "FullyQualifiedName~TesseractEnginePool_QuarantinesOnException"` | ❌ Wave 0 |
| PIPE-04 | `TesseractEnginePoolWarmupService.StartAsync` creates PoolSize engines | unit | `dotnet test --filter "FullyQualifiedName~TesseractEnginePoolWarmup_StartAsync_CreatesPoolSize"` | ❌ Wave 0 |
| PIPE-04 | Old `TesseractImageTextExtractor` class is removed from the codebase | source-level structural-grep | `dotnet test --filter "FullyQualifiedName~TesseractImageTextExtractorRemoved"` | ❌ Wave 0 |
| PIPE-04 | Hangfire `WorkerCount` matches `TesseractOptions.PoolSize` (D-16 alignment) | source-level structural-grep | `dotnet test --filter "FullyQualifiedName~HangfireWorkerCountMatchesPoolSize"` | ❌ Wave 0 |
| PIPE-05 | `UploadErrorCatalog.Classify(new NoTextExtractedException())` returns "NoTextExtracted" + German string | unit | `dotnet test --filter "FullyQualifiedName~UploadErrorCatalog_NoTextExtracted_ReturnsGermanString"` | ❌ Wave 0 |
| PIPE-05 | `UploadErrorCatalog.Classify(new Exception("foo"))` returns "Unknown" + German fallback | unit | `dotnet test --filter "FullyQualifiedName~UploadErrorCatalog_UnknownException_ReturnsFallback"` | ❌ Wave 0 |
| PIPE-05 | Raw exception message NEVER appears in `processing_runs.error_message` | source-level structural-grep | `dotnet test --filter "FullyQualifiedName~ProcessReceiptFileJob_NoExceptionMessageLeak"` | ❌ Wave 0 |
| PIPE-05 | Status endpoint D-13 response includes `errorCode` field for Failed/Cancelled states | integration (WAF) | `dotnet test --filter "FullyQualifiedName~StatusDto_Failed_IncludesErrorCode"` | ❌ Wave 0 |
| PIPE-06 | Frontend: `useReceiptFileStatus` hook stops polling on terminal status | manual UAT (frontend has no Vitest yet per CONCERNS.md #2 — defer to Phase 7 QA-02) | (manual: open DevTools Network tab, upload file, observe polling stops on Completed) | N/A — manual UAT |
| PIPE-06 | Upload form replaces "processing" spinner with status badge once polling resolves | manual UAT | (manual: upload + observe card state transition) | N/A — manual UAT |
| PIPE-06 | Receipts list shows skeleton for in-flight rows; alert for failed rows | manual UAT | (manual: visit /receipts during/after upload) | N/A — manual UAT |

### Sampling Rate

- **Per task commit:** `dotnet test Backend/tests/TaxReader.UnitTests --filter "FullyQualifiedName~Pipeline | FullyQualifiedName~Hangfire | FullyQualifiedName~Tesseract"` (~5–10s expected)
- **Per wave merge:** `dotnet test Backend` (full backend suite, ~30s expected post-Phase-2 baseline)
- **Phase gate:** Full backend suite green + manual UAT items in `03-HUMAN-UAT.md` (analogous to Phase 2's `02-HUMAN-UAT.md`) before `/gsd-verify-work 3`

### Wave 0 Gaps

- [ ] `Backend/tests/TaxReader.UnitTests/Pipeline/` — new directory for `ProcessReceiptFileJob`, `ClassifyBatchJob`, `UploadErrorCatalog`, `CancelReceiptFileHandler`, `GetReceiptFileStatusHandler` tests
- [ ] `Backend/tests/TaxReader.UnitTests/Hangfire/` — new directory for `HangfireAdminAuthFilter`, dashboard auth WAF tests, `RecurringJobsBootstrap` source-grep tests, `SeedAdminUsersHostedService` unit tests
- [ ] `Backend/tests/TaxReader.UnitTests/Infrastructure/Tesseract/` — new directory for `TesseractEnginePool`, `TesseractEnginePoolWarmupService` tests
- [ ] `Backend/tests/TaxReader.UnitTests/Helpers/HangfireTestFactory.cs` — analog of `RateLimitTestFactory` but wires a fake Hangfire job storage (in-memory) for WAF tests that exercise dashboard auth without spinning up Postgres
- [ ] `Backend/tests/TaxReader.UnitTests/Helpers/TestDataFactory.cs` — add `CreateAdminUser(...)` helper that returns a User with `IsAdmin = true`
- [ ] `Backend/tests/TaxReader.UnitTests/Pipeline/PipelineTestCollection.cs` — `[CollectionDefinition(DisableParallelization = true)]` for WAF tests (parallel `WebApplicationFactory<Program>` instances break `Program.cs` top-level-statements per `[VERIFIED: Backend/tests/TaxReader.UnitTests/RateLimiting/RateLimiterTestCollection.cs:11-15]`)
- [ ] Frontend Vitest setup is OUT OF SCOPE for Phase 3 (deferred to Phase 7 QA-02); PIPE-06 frontend tests are manual UAT items per the table above

## Security Domain

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V2 Authentication | yes | JWT bearer (Phase 2 carries forward); new `tr_access` cookie reuses same secret (D-10) |
| V3 Session Management | yes | HttpOnly + Secure + SameSite=Strict + Path=/hangfire on `tr_access` (D-10); cookie cleared on logout |
| V4 Access Control | yes | Per-user data scoping in `GetReceiptFileStatusHandler` + `CancelReceiptFileHandler` (`f.UserId == currentUser.UserId`); admin gate via `User.IsAdmin` + `role` claim (D-07, D-09) |
| V5 Input Validation | yes | FluentValidation (existing) on new `CancelReceiptFileCommand`; `Guid` route parameters strongly typed |
| V6 Cryptography | yes | HMAC-SHA256 for JWT signing (existing `Jwt__Secret`); same key used for `tr_access` cookie validation — single shared secret, no new crypto surface |
| V7 Error Handling | yes | German error catalog (D-21) explicitly designed for "no raw exception leakage" — addresses CONCERNS.md #12 |
| V12 Logging | yes | `LogContext.PushProperty("JobId", id)` (D-05) + Phase 1 D-14 Sentry PII allow-list applies; raw receipt content + vendor names + user emails NEVER logged |

### Known Threat Patterns for {Hangfire + Postgres + .NET 10 + cookie auth}

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| CSRF on Hangfire dashboard POST (Requeue / Delete) | Tampering | Hangfire 1.6.20+ auto-wires `Microsoft.AspNetCore.Antiforgery`; SameSite=Strict cookie blocks cross-origin POST (`[CITED: discuss.hangfire.io/csrf-prevention-in-hangfire/4944]`) |
| JWT cookie leak via XSS | Information disclosure | HttpOnly attribute prevents JavaScript access; Sentry frontend disabled in Phase 3 (per Phase 1 D-16) so no session-replay risk |
| Cookie replay across sites (CSRF-via-cookie) | Spoofing | SameSite=Strict prevents browser sending cookie on cross-site navigation; Path=/hangfire constrains attack surface |
| Cookie scope expansion attack (set Domain=.com) | Spoofing | We use `Domain` unset → host-only cookie; only sent to the exact host that set it (`[CITED: dchost.com/blog/.../cookies-that-behave-samesitelax-strict-secure-and-httponly]`) |
| Job parameter tampering (Hangfire job arguments serialized in DB) | Tampering | Hangfire serializes job method arguments to JSON in `hangfire.job.arguments`; DB row tampering would require Postgres compromise (same attack surface as the rest of the app — outside Phase 3 scope) |
| Hangfire dashboard exposing connection string in UI | Information disclosure | `DashboardOptions.DisplayStorageConnectionString = false` (Pattern 6) |
| Token pre-charge bypass via job arg tampering | Tampering / Elevation | `ClassifyBatchJob` always re-reads token balance from DB inside the job; never trusts a `prechargedAmount` job argument |
| Cancel endpoint abuse to free pool engines | DoS | Per-user rate limit (Phase 2 D-09 global 60/min IP applies); cancel-then-reupload + cancel-then-reupload doesn't bypass token economy because cancel before classify = no charge anyway |
| OCR engine OOM-DoS via giant image upload | DoS | Existing file-size validator (`Backend/src/TaxReader.Application/Validators/UploadReceiptFilesValidator.cs` if applicable; check) + 2400px downsample (D-20) limits memory; quarantine-and-replace (D-19) survives a single OOM |
| Anti-forgery token theft via referrer leak | Tampering | Caddy security header `Referrer-Policy: strict-origin-when-cross-origin` (existing per `[CITED: .planning/codebase/INTEGRATIONS.md]`) strips referrer to origin only |

**Security review checklist for plan execution:**
- [ ] `HangfireAdminAuthFilter` validates JWT lifetime (`ValidateLifetime = true`) — an expired access token must NOT pass dashboard auth even if cookie still present
- [ ] `IsAdmin` defaults to `false` in migration's column default
- [ ] `SeedAdminUsers` ONLY flips `IsAdmin = true`, NEVER flips back to false (admin removal is manual SQL — documented in OPERATIONS.md or commit message)
- [ ] Cancel endpoint: 404 for foreign-user file (per-user scoping check); not 401 — leaks existence of the file otherwise
- [ ] Status endpoint: same per-user scoping; 404 not 401 on foreign-user lookup

## Sources

### Primary (HIGH confidence)

- **Hangfire official documentation** — https://docs.hangfire.io/en/latest/
  - `getting-started/aspnet-core-applications.html` — minimal viable setup
  - `configuration/using-postgresql.html` — Postgres-specific options (`PrepareSchemaIfNecessary`, `QueuePollInterval`, `InvisibilityTimeout`)
  - `configuration/using-dashboard.html` — `IDashboardAuthorizationFilter` + `GetHttpContext` + multiple-filter AND semantics
  - `background-methods/using-cancellation-tokens.html` — `CancellationToken` since Hangfire 1.7.0 (modern, async)
  - `background-processing/dealing-with-exceptions.html` — `[AutomaticRetry]` semantics, `DelaysInSeconds`, `Attempts`
  - `background-methods/performing-recurrent-tasks.html` — `RecurringJob.AddOrUpdate`, cron expressions, `DisableConcurrentExecution`
  - `upgrade-guides/upgrading-to-hangfire-1.8.html` — `CompatibilityLevel.Version_180`, `UseSimpleAssemblyNameTypeSerializer`, `UseRecommendedSerializerSettings`
- **NuGet registry** — https://www.nuget.org/packages/Hangfire.Core (1.8.23, 2026-02-05), https://www.nuget.org/packages/Hangfire.AspNetCore (1.8.23, 2026-02-05), https://www.nuget.org/packages/Hangfire.PostgreSql (1.21.1, 2026-02-11)
- **Microsoft Learn — Channels** — https://learn.microsoft.com/en-us/dotnet/core/extensions/channels — bounded channels, `BoundedChannelFullMode`, `SingleReader`/`SingleWriter` semantics
- **.NET Blog — Channels intro** — https://devblogs.microsoft.com/dotnet/an-introduction-to-system-threading-channels/ — patterns for async producer/consumer
- **TanStack Query v5 docs** — https://tanstack.com/query/latest/docs/framework/react/guides/polling — `refetchInterval` function signature, terminal-state stopping
- **Existing TaxReader codebase**
  - `[VERIFIED: Backend/Directory.Packages.props lines 1-41]` — Central Package Management setup
  - `[VERIFIED: Backend/src/TaxReader.Infrastructure/DependencyInjection.cs lines 1-73]` — DI registration pattern
  - `[VERIFIED: Backend/src/TaxReader.Api/Program.cs lines 1-327]` — middleware order, JWT setup, FluentValidation, EF migrations on boot
  - `[VERIFIED: Backend/src/TaxReader.Application/Commands/UploadReceiptFilesHandler.cs lines 1-234]` — current synchronous loop
  - `[VERIFIED: Backend/src/TaxReader.Infrastructure/Services/AiOnlyClassificationService.cs lines 1-137]` — token pre-charge + refund logic
  - `[VERIFIED: Backend/src/TaxReader.Infrastructure/Services/TesseractImageTextExtractor.cs lines 1-137]` — OCR config + Singleton+lock pattern
  - `[VERIFIED: Backend/tests/TaxReader.UnitTests/RateLimiting/RateLimiterTestCollection.cs lines 1-16]` — WAF-test serialization pattern

### Secondary (MEDIUM confidence)

- **Greg Kedzierski article on Hangfire chaining/batching** — https://gregkedzierski.com/essays/dotnet-job-chaining-and-batching-with-hangfire/ — confirms Batches are paywalled, documents free-tier patterns. Cross-verified with `[CITED: hangfire.io/pro]` confirming Batches is a Pro feature.
- **Mahdi Taghizadeh / Anthony Sapountzis Medium articles** — JWT-in-cookie patterns for Hangfire dashboard. Cross-verified against `[CITED: docs.hangfire.io/.../using-dashboard.html]`.
- **`charlesw/tesseract` GitHub issues** (#291, #228, #281, #4281) — historical reports of SEHException + thread-safety constraints on TesseractEngine. Single-source community reports, hence MEDIUM. Verified against the existing `TesseractImageTextExtractor.cs` Singleton+lock comment which corroborates: "Tesseract itself is NOT thread-safe — concurrent calls are serialised via `_gate`".
- **Hangfire 1.6.20 release notes** — https://www.hangfire.io/blog/2018/07/21/hangfire-1.6.20.html — anti-forgery rollout. Confirms ASP.NET Core auto-wiring.

### Tertiary (LOW confidence)

- WebSearch results on Hangfire CSRF disable patterns — `IgnoreAntiforgeryTokenAttribute` exists; community usage documented but not on docs.hangfire.io directly. Flagged for verification in Plan 03-01 manual UAT.
- Anthropic mid-request cancellation cost behavior (charges for partial vs full inference) — A1 in Assumptions Log; behavior depends on Anthropic's current production billing model which we haven't verified for Phase 3.

## Metadata

**Confidence breakdown:**
- Standard stack (Hangfire 1.8.23 / Hangfire.PostgreSql 1.21.1 / Hangfire.AspNetCore 1.8.23): HIGH — NuGet-verified, dates within 3 months of 2026-05-19, compatibility with .NET 10 explicit
- Architecture patterns (parent/child barrier, retry attribute, cancellation, pool, status polling, dashboard auth): HIGH — verified against multiple official sources (docs.hangfire.io, learn.microsoft.com, tanstack.com)
- Channel<TesseractEngine> single-ownership safety with `charlesw/tesseract` 5.2.0: MEDIUM — historical SEHException reports + existing TaxReader Singleton+lock pattern suggest viability under single-ownership, but the pool model is new; needs Plan 03-03 stress test to confirm. See A8.
- Pitfalls (especially #1, #5, #8): HIGH for #1, #5; MEDIUM for #8 (data migration ordering depends on D-06 final interpretation)
- Validation Architecture tests: HIGH — patterns derived from existing Phase 2 test infrastructure (`RateLimiterTestCollection`, `RateLimitTestFactory`, source-level structural-grep tests)

**Research date:** 2026-05-19
**Valid until:** 2026-06-19 (30 days for the Hangfire stack — stable enough to lock; revisit if Hangfire 1.9 ships with breaking changes before plan execution)
