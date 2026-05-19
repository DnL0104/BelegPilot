# Phase 3: Background Pipeline + Tesseract Pool - Context

**Gathered:** 2026-05-18
**Status:** Ready for planning

<domain>
## Phase Boundary

Move the upload pipeline (extract → parse → classify) off the HTTP request lifecycle onto Hangfire background jobs so `POST /receipt-files` returns 202 Accepted within ~1s; replace the Tesseract Singleton-with-lock pattern with a small pooled engine set; deliver real-time per-file status polling with cancellation + token refund; surface user-friendly German error messages and empty/loading/error UI states across upload, receipts-list, receipt-detail, dashboard, and reports surfaces.

In scope: Hangfire (Postgres-backed) installation + dashboard auth gate + recurring cleanup jobs (PIPE-01); `ProcessReceiptFileJob` + parent `ClassifyBatchJob` + 202 Accepted response shape + status polling + cancellation (PIPE-02, PIPE-03); `TesseractEnginePool` replacing the Singleton+lock pattern (PIPE-04); German error catalog + UI states (PIPE-05, PIPE-06).

Out of scope (later phases own these):
- Rule + AI hybrid classification, `ClassificationRule` wiring — Phase 4 (CLASS-01, CLASS-02)
- 13-category enum expansion + PDF/CSV export updates — Phase 4 (CLASS-03)
- Stripe / payments / `/webhooks/stripe` rate limiting — Phase 5
- Audit-log entries for cancel / refund / refresh-token cleanup — Phase 6 (LEG-08)
- TTDSG cookie consent gating — Phase 6 (LEG-05)
- `ExportUserDataJob` self-serve data export — Phase 6 (LEG-07); leverages this phase's Hangfire infra
- PostgreSQL integration tests via Testcontainers — Phase 7 (QA-01)
- BetterStack uptime monitors / Sentry alert retuning — Phase 7 (OBS-03, QA-06)

</domain>

<decisions>
## Implementation Decisions

### Job topology & AI batching (PIPE-02)
- **D-01:** Job topology = **parent `ProcessReceiptFileJob` (per file) → `ClassifyBatchJob` (per upload)**. The per-file job handles extract + parse + per-file DB writes (deterministic, retryable). After every file's parse completes, a single `ClassifyBatchJob` runs ONE Anthropic call across all parsed items in the upload, preserving today's `UploadReceiptFilesHandler.cs:173-202` wallclock win (Haiku roundtrip ~1s vs N×1s). Coordination uses Hangfire's `IBackgroundJobClient` + `ContinueJobWith` (or an awaiter that polls the per-file jobs for completion before enqueueing the classify-batch).
- **D-02:** Token pre-charge fires inside `ClassifyBatchJob` at the moment item count is known — preserves the existing `AiOnlyClassificationService.cs:49-62` "pre-charge whole batch + per-item refund for Unknowns" pattern exactly. Cancellation before `ClassifyBatchJob` starts → nothing charged. Cancellation during the Anthropic call → full batch refund via the existing `AiOnlyClassificationService.cs:71-75` "AI failure" branch. The 402-on-insufficient-tokens UX still happens — just deferred from upload time to classify time; the per-file status surfaces `errorCode = "InsufficientTokens"` when the pre-charge fails.
- **D-03:** `POST /receipt-files` 202 response body = `{ files: [{ receiptFileId, jobId, fileName }] }`. Per-file polling via `GET /receipt-files/{id}/status` (D-13). No `uploadBatchId` concept introduced; frontend computes batch-level progress client-side from the per-file states. Matches the existing per-file card layout in `upload-form.tsx`.
- **D-04:** Hangfire retry policy = **tiered**:
  - `ProcessReceiptFileJob`: 3 retries with backoff ~30s / 2m / 5m via `[AutomaticRetry(Attempts = 3)]`. Transient PdfPig/Tesseract/IO errors are real and idempotent (`UploadReceiptFilesHandler` already removes-and-recreates on retry per `ContentHash`).
  - `ClassifyBatchJob`: 0 Hangfire retries via `[AutomaticRetry(Attempts = 0)]`. The existing "refund + mark Unknown" branch handles AI failures gracefully; we don't want 3× the token-refund churn or 3× the Anthropic load.
- **D-05:** `LogContext.PushProperty("JobId", jobId)` scope wraps the body of both jobs at their entry points — fulfills the Phase 1 D-18 reservation. Push only IDs (non-PII); never vendor names, item descriptions, or user emails. Sentry tags inherit via the existing scope-propagation set up in Phase 1 D-14.
- **D-06:** `ProcessingStatus` enum gains two values: `Queued` (between enqueue and worker pick-up) and `Cancelled` (terminal). New numeric order: `Pending=0, Queued=1, Extracting=2, Parsing=3, Classifying=4, Completed=5, Failed=6, Cancelled=7`. EF migration `AddQueuedAndCancelledProcessingStatuses` updates the enum mapping. `Pending` is retained for code paths that haven't enqueued yet (sub-1s window during the 202 response building); the worker observing `Pending` immediately transitions it to `Queued` then `Extracting`.

### Hangfire dashboard auth (PIPE-01)
- **D-07:** Admin gate = **JWT `role` claim backed by a `User.IsAdmin` column**. New `bool IsAdmin` column on `users` (NOT NULL default false); EF migration `AddIsAdminToUsers`. `AuthService` adds `"role":"admin"` to the access JWT when `IsAdmin` is true. Generalizes to any future role-gated endpoint without introducing a second mechanism. Refresh tokens stay opaque (no payload).
- **D-08:** First-admin bootstrap = **migration-time seed via env var**. New env `Hangfire__SeedAdminEmails=csv` read by an idempotent startup `SeedAdminUsers` step (runs after `RUN_MIGRATIONS=true` applies the migration). Sets `IsAdmin=true` for matching `User.Email` rows. Safe to re-run; works on fresh installs and existing DBs; documented in `.env.example` with a generation hint.
- **D-09:** Claim refresh policy = **access-token only; demotion takes effect within 60 min (next access-token refresh)**. The `role` claim is added to the access JWT in `AuthService.LoginAsync` and `AuthService.RefreshAsync`. The Hangfire dashboard filter reads claims from `HttpContext.User`. Refresh tokens carry no role payload. Acceptable because admin demotion is rare and not security-critical at the 100–500 user target.
- **D-10:** Browser credentials transport for `/hangfire` = **JWT in HttpOnly cookie set at login**. `AuthService.LoginAsync` and `AuthService.RefreshAsync` set `tr_access` cookie (HttpOnly, Secure, SameSite=Strict, Path=/hangfire, expires with the access JWT TTL of 60 min). localStorage still holds the same token for the SPA — one auth scheme, two transports. `/auth/logout` (or any clear-session path the SPA invokes) explicitly clears the cookie. The Hangfire `IDashboardAuthorizationFilter` reads the cookie, validates the JWT using the existing `Jwt__Secret`, and checks the `role` claim.

### Cancellation, polling & refunds (PIPE-03)
- **D-11:** Cancellable states = **any non-terminal state** (`Pending`, `Queued`, `Extracting`, `Parsing`, `Classifying`). Hangfire's `IJobCancellationToken` propagates into the job; `Tesseract.ExtractTextAsync`, `PdfPig.ExtractTextAsync`, and `ClaudeAiClassifier.ClassifyBatchAsync` all observe the `CancellationToken` already. Mid-Anthropic cancel = best-effort abort (HttpClient cancellation), full refund via the existing failure branch.
- **D-12:** Refund accounting = **all-or-nothing per file**. Cancel before `ClassifyBatchJob` starts → no charge fired, no refund needed. Cancel during `ClassifyBatchJob` → the Anthropic abort returns before per-item ledger commits, so the existing `AiOnlyClassificationService.cs:71-75` "refund all" branch runs. One ledger entry per cancellation; auditable; cannot be abused by "upload 10, cancel 1" gaming (which would refund only the cancelled file, not previously-completed ones).
- **D-13:** Status endpoint = `GET /receipt-files/{id}/status` returning `{ status, updatedAt, errorCode?, errorMessage? }`:
  - `status`: ProcessingStatus enum value (string-serialized)
  - `updatedAt`: ISO-8601 UTC
  - `errorCode`: stable enum for the frontend to switch on (`NoTextExtracted`, `ParserMissing`, `AiUnavailable`, `InsufficientTokens`, `Cancelled`, `Unknown`) — present when status is `Failed` or `Cancelled`
  - `errorMessage`: German display string — present when `errorCode` is present
  - Polling cadence: every 2s while status is non-terminal; stop on `Completed`, `Failed`, `Cancelled`. No progress percentage (steps aren't usefully linearizable).
- **D-14:** Cancel endpoint = `POST /receipt-files/{id}/cancel` returning `204 No Content` on success, `409 Conflict` when the file is already terminal, `404 Not Found` when the file doesn't belong to the user. Idempotent (cancelling an already-Cancelled file returns 204). Implementation: `BackgroundJob.Delete(jobId)` for queued jobs, `CancellationTokenSource` signalling for in-flight jobs. The job observes cancellation, marks `ProcessingStatus.Cancelled`, runs refund (D-12), exits.
- **D-15:** Worker recovery on container restart = **trust Hangfire's invisibility timeout** (~30 min default for the worker heartbeat). `ProcessReceiptFileJob` is idempotent: the existing `UploadReceiptFilesHandler` `ContentHash`-based duplicate detection + the "remove existing non-Processed file and retry" branch in `UploadReceiptFilesHandler.cs:74-80` make re-runs safe. No bespoke startup sweep. Documented as an implicit dependency on Hangfire's worker-liveness model; revisit only if real-traffic shows orphans surviving the invisibility window.

### Tesseract pool design (PIPE-04)
- **D-16:** Pool size = **configurable, default 3**. New `TesseractOptions.PoolSize` property + `Tesseract__PoolSize` env var. Sized to typical concurrent-OCR-2-or-3 at the 100–500 user target. Hangfire `WorkerCount` aligned to the same value via shared config or explicit registration — never more workers than engines, so engine starvation is impossible.
- **D-17:** Pool implementation = **`Channel<TesseractEngine>` (bounded, single-channel)**. `IImageTextExtractor` implementation calls `Channel.Reader.ReadAsync(jobCancellationToken)` to acquire, `Channel.Writer.TryWrite(engine)` to release. Hangfire's `IJobCancellationToken` is the only thing that aborts an acquire wait — no artificial timeouts, no synthetic "pool full" errors at the OCR layer. The pool implementation lives in `Backend/src/TaxReader.Infrastructure/Services/TesseractEnginePool.cs` (renamed from `TesseractImageTextExtractor.cs`); the old class is removed.
- **D-18:** Engine warmup = **eager at startup via `IHostedService`**. `TesseractEnginePoolWarmupService.StartAsync` creates all `PoolSize` engines before the host signals Ready (and before `/health` returns 200). Adds ~`PoolSize × 100ms` (~300ms at default) to container boot. First OCR pays no init cost. Predictable steady-state latency; appropriate for a Docker Compose deploy that restarts rarely.
- **D-19:** Engine failure handling = **quarantine + replace on exception**. Each OCR call wraps the `engine.Process(image)` in try/catch. On `TesseractException` or `OutOfMemoryException`, the engine is `Dispose()`d and NOT returned to the channel. A pool-side hosted service (or the next-acquire path) detects the count drop and creates a replacement engine on the same thread that observed the failure (cheap — ~100ms). Logs at `Warning` with engine-id; structured event so Sentry's "new error type" rule can baseline.
- **D-20:** Tesseract config knobs stay = **`EngineMode.LstmOnly` + `PageSegMode.SingleBlock` + 2400px downsample** carry over from `TesseractImageTextExtractor.cs:60-72,119-123`. Image-downsampling math, OCR-text normalization via `OcrTextNormalizer.Normalize`, and the German+English language pack stay identical. Only the engine lifecycle changes.

### Cross-cutting (PIPE-05, PIPE-06) — Claude's Discretion within stated conventions
- **D-21:** German error catalog (PIPE-05) location = **`Backend/src/TaxReader.Application/Common/UploadErrorCatalog.cs`** (or analog). Maps known exception types to `(errorCode, germanMessage)` pairs surfaced via D-13's status response. Raw exception messages NEVER appear in HTTP body or `processing_runs.error_message`; they go to Serilog only via `logger.LogError(ex, "{ErrorCode} during {Step} for ReceiptFile {Id}", ...)`. Fall-through for unknown exceptions: `errorCode = "Unknown"`, `errorMessage = "Verarbeitung fehlgeschlagen — bitte erneut versuchen oder Support kontaktieren."`. The exact catalog content (which exception types, which strings) is planner-decided per the existing German `Sie`-form convention.
- **D-22:** Empty/loading/error UI patterns (PIPE-06) = **reuse existing shadcn primitives** (`Skeleton`, `Alert`, `AlertCircle`, the toast pattern via `sonner`). No new UI primitives introduced. Pages affected: `upload/page.tsx`, `receipts/page.tsx` (list), `receipts/[id]/page.tsx` (detail), `dashboard/page.tsx`, `reports/page.tsx`. Polling for in-flight status uses TanStack Query's `refetchInterval` set per D-13's 2s cadence with terminal-state stop. Per-file-card placeholders in `upload-form.tsx:52-58` get a real status badge plus the `errorMessage` text from the polling response. Exact wording, copy length, spacing decisions are planner/executor discretion within the German `Sie`-form convention from `CONVENTIONS.md`.

### Recurring cleanup jobs (PIPE-01) — Claude's Discretion within stated scope
- **D-23:** Recurring jobs registered at startup via `RecurringJob.AddOrUpdate`:
  1. **Expired refresh tokens cleanup** — daily at 03:00 UTC; `DELETE FROM refresh_tokens WHERE expires_at < now() - INTERVAL '7 days'` (7-day grace beyond expiry so audit queries still work briefly). Fulfills the Phase 2 D-16 deferred handoff.
  2. **Abandoned `Failed` jobs cleanup** — weekly; removes Hangfire-internal `Failed`-state job metadata older than 30 days via `BackgroundJobClient.Delete(...)` over a Hangfire monitoring-API query. `ProcessingRun` rows are kept (DB audit), only Hangfire's job table is pruned.
  3. **`ProcessingRun` retention** — none in Phase 3; defer to Phase 6 (LEG-08 audit log decides retention policy uniformly across audit-relevant tables).
  Exact cron expressions, log volumes, and idempotency guards are planner-decided.

### Claude's Discretion (general)
- Exact `IBackgroundJobClient` invocation pattern (`Enqueue` + `ContinueJobWith` vs `BatchJob` extension package vs custom continuation poll) for D-01's parent/child topology
- Whether `ProcessReceiptFileJob` and `ClassifyBatchJob` are class-typed or static-method-typed Hangfire targets (likely class-typed with DI to match the established handler-injection pattern)
- Hangfire dashboard's `DashboardOptions.Authorization` filter chain (single filter or composed; whether to include a "no anonymous" hard reject)
- Status enum string serialization (PascalCase vs snake_case in JSON)
- Whether `GET /receipt-files/{id}/status` lives on `ReceiptFileEndpoints.cs` (current home) or a dedicated `ProcessingStatusEndpoints.cs`
- Tesseract engine warmup order (parallel vs serial) — likely serial to avoid I/O contention loading the same language data files
- Whether the cookie is set via `Response.Cookies.Append` in `AuthService` or in the endpoint layer (likely endpoint layer to keep `AuthService` HTTP-context-free per Phase 2 02-01 invariant)

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Project context
- `.planning/PROJECT.md` — Vision, Core Value (trustworthy classification), constraints (3-month timeline, solo dev, scale 100–500 users), "Background-job upload pipeline" key decision, Out of Scope boundary
- `.planning/REQUIREMENTS.md` — PIPE-01 through PIPE-06 are this phase's deliverables; full text under "Pipeline & Reliability"; traceability table at the bottom
- `.planning/ROADMAP.md` — Phase 3 entry with 7 success criteria and 4 plan stubs; Phase 4 dependency points

### Codebase intel
- `.planning/codebase/CONCERNS.md` — #8 (synchronous upload), #9 (Tesseract Singleton+lock), #11 (PdfPig zero-words fallback), #12 (error-message leakage) are the concerns this phase closes
- `.planning/codebase/ARCHITECTURE.md` — Layer rules (Domain has zero deps; Application defines interfaces; Infrastructure implements; API thin); `Result<T>` pattern; `ICurrentUser` abstraction; per-user data scoping idiom; upload pipeline shape pre-Hangfire
- `.planning/codebase/CONVENTIONS.md` — File-scoped namespaces, primary-constructor DI, `Result<T>` for errors, `Async` suffix, structured-logging named-placeholder rule, German `Sie`-form for user-facing copy, `IOptions<T>` config pattern, `__`-nested env vars
- `.planning/codebase/INTEGRATIONS.md` — JWT bearer config (60-min access, 30-day refresh, HmacSha256), Postgres + EF Core migration history, Caddy reverse-proxy posture, Anthropic API config
- `.planning/codebase/STACK.md` — Tesseract version (5.2.0) + LSTM-only mode + German/English language packs; .NET 10 runtime; PostgreSQL 17

### Prior-phase context (carries forward)
- `.planning/phases/01-foundation-cleanup-ci/01-CONTEXT.md` — D-18: `LogContext.PushProperty("JobId", jobId)` reserved for this phase; D-14: Sentry PII allow-list (`user.id_hash` permitted, raw receipt content forbidden); D-17: Serilog `Enrichers.FromLogContext()` already wired
- `.planning/phases/02-auth-rate-limit-hardening/02-CONTEXT.md` — D-07: upload-concurrency rate limiter retires when 202 Accepted lands; D-16: expired refresh-token cleanup deferred to Phase 3 (PIPE-01 picks it up — see D-23 above); D-06: `UseForwardedHeaders` first in pipeline (Hangfire dashboard filter inherits the real client IP); refresh-token table schema for the cleanup job
- `.planning/phases/02-auth-rate-limit-hardening/02-01-PLAN.md` § "RefreshTokenService stays HTTP-context-free" — `AuthService` invariant the cookie-setting code must respect (D-10)

### Files this phase will touch (read before editing)

#### Backend — Domain & Application
- `Backend/src/TaxReader.Domain/Entities/ProcessingRun.cs` — no shape change; new `ProcessingStatus` values used here
- `Backend/src/TaxReader.Domain/Enums/ProcessingStatus.cs` — add `Queued`, `Cancelled` values (D-06)
- `Backend/src/TaxReader.Domain/Entities/User.cs` — add `IsAdmin` bool property (D-07)
- `Backend/src/TaxReader.Application/Commands/UploadReceiptFilesHandler.cs` — gut the synchronous processing loop; the new handler only persists `ReceiptFile` rows in `Queued` state and enqueues `ProcessReceiptFileJob` per file + a `ClassifyBatchJob` parent. Returns 202 payload (D-03).
- `Backend/src/TaxReader.Application/Jobs/ProcessReceiptFileJob.cs` — NEW; per-file extract + parse logic moves here from `UploadReceiptFilesHandler`
- `Backend/src/TaxReader.Application/Jobs/ClassifyBatchJob.cs` — NEW; classify all parsed items in an upload in one Anthropic call (D-01); pre-charges via `ITokenService` (D-02)
- `Backend/src/TaxReader.Application/Interfaces/IBackgroundJobClient.cs` — NEW abstraction wrapping Hangfire's `IBackgroundJobClient` so Application stays Hangfire-free
- `Backend/src/TaxReader.Application/Common/UploadErrorCatalog.cs` — NEW; exception-to-(errorCode, germanMessage) mapping (D-21)
- `Backend/src/TaxReader.Application/Queries/GetReceiptFileStatusHandler.cs` — NEW; reads `ProcessingRun` for a `ReceiptFile`, returns D-13's response shape
- `Backend/src/TaxReader.Application/Commands/CancelReceiptFileHandler.cs` — NEW; per D-14 + D-11 + D-12
- `Backend/src/TaxReader.Application/Interfaces/IClassificationService.cs` — no shape change; called only from `ClassifyBatchJob` now

#### Backend — Infrastructure
- `Backend/src/TaxReader.Infrastructure/Services/TesseractImageTextExtractor.cs` — REMOVED (replaced)
- `Backend/src/TaxReader.Infrastructure/Services/TesseractEnginePool.cs` — NEW; `Channel<TesseractEngine>` pool (D-16, D-17, D-19); implements `IImageTextExtractor`
- `Backend/src/TaxReader.Infrastructure/Services/TesseractEnginePoolWarmupService.cs` — NEW; `IHostedService` for eager warmup (D-18)
- `Backend/src/TaxReader.Infrastructure/Configuration/TesseractOptions.cs` — add `PoolSize` property (D-16)
- `Backend/src/TaxReader.Infrastructure/Services/HangfireBackgroundJobClient.cs` — NEW; implements `IBackgroundJobClient`
- `Backend/src/TaxReader.Infrastructure/Services/AuthService.cs` — set `tr_access` HttpOnly cookie at login + refresh (D-10); add `role` claim from `User.IsAdmin` (D-07, D-09)
- `Backend/src/TaxReader.Infrastructure/Services/AdminBootstrap/SeedAdminUsersHostedService.cs` — NEW; reads `Hangfire__SeedAdminEmails` env var, sets `IsAdmin=true` on matching rows (D-08)
- `Backend/src/TaxReader.Infrastructure/Data/Configurations/UserConfiguration.cs` — map new `IsAdmin` column
- `Backend/src/TaxReader.Infrastructure/Data/Configurations/ProcessingRunConfiguration.cs` — no shape change; enum mapping picks up new values
- `Backend/src/TaxReader.Infrastructure/Migrations/` — TWO new migrations: `AddIsAdminToUsers`, `AddQueuedAndCancelledProcessingStatuses` (D-06 + D-07)
- `Backend/src/TaxReader.Infrastructure/DependencyInjection.cs` — remove Tesseract Singleton; register `TesseractEnginePool` Singleton + `TesseractEnginePoolWarmupService` `IHostedService`; register Hangfire (`AddHangfire`, `AddHangfireServer`) with Postgres storage; recurring jobs registered here per D-23

#### Backend — API
- `Backend/src/TaxReader.Api/Program.cs` — Hangfire dashboard registration at `/hangfire` with `DashboardOptions.Authorization = [new HangfireAdminAuthFilter(...)]`; reads `Hangfire__SeedAdminEmails` (passed via `IOptions`); recurring jobs registration moved to a dedicated `RecurringJobsBootstrap` invoked from here
- `Backend/src/TaxReader.Api/Hangfire/HangfireAdminAuthFilter.cs` — NEW; implements `IDashboardAuthorizationFilter`; reads `tr_access` cookie, validates JWT, checks `role` claim (D-10)
- `Backend/src/TaxReader.Api/Endpoints/ReceiptFileEndpoints.cs` — `POST /` returns `202 Accepted` with D-03 payload; new `GET /{id}/status` and `POST /{id}/cancel` endpoints; remove `.RequireRateLimiting("upload-concurrency")` per Phase 2 D-07's planned sunset (or downgrade to a lighter cap — planner decides)
- `Backend/src/TaxReader.Api/Endpoints/AuthEndpoints.cs` — set + clear `tr_access` cookie on login / refresh / (logout if exists)

#### Configuration
- `docker-compose.yml` — add `Tesseract__PoolSize`, `Hangfire__SeedAdminEmails` env vars on `api` service; ensure Postgres-storage connection string is reachable (already is — Hangfire reuses the same DB)
- `.env.example` — add `TESSERACT_POOLSIZE=3` and `HANGFIRE_SEEDADMINEMAILS=admin@example.com` placeholders with generation hints

#### Frontend
- `Frontend/src/lib/api-client.ts` — `uploadReceiptFiles` now returns the 202 payload `{ files: [...] }`; new `getReceiptFileStatus(id)` and `cancelReceiptFile(id)` exports
- `Frontend/src/hooks/use-receipt-files.ts` — `useUploadFiles` returns per-file IDs; new `useReceiptFileStatus(id)` hook using TanStack Query `refetchInterval` 2000ms with `enabled` set on non-terminal status (D-13); new `useCancelReceiptFile` mutation
- `Frontend/src/components/upload/upload-form.tsx` — placeholder cards stay until per-file polling resolves; status badge replaces "processing" spinner once a real status arrives; cancel button per card while non-terminal (D-22)
- `Frontend/src/app/(authenticated)/receipts/page.tsx` — list shows live status for in-flight files with auto-refresh while any non-terminal row exists
- `Frontend/src/app/(authenticated)/receipts/[id]/page.tsx` — handle in-flight (`Queued`/`Extracting`/`Parsing`/`Classifying`) state with skeletons; render error states from D-13 `errorMessage`
- `Frontend/src/app/(authenticated)/dashboard/page.tsx` and `Frontend/src/app/(authenticated)/reports/page.tsx` — empty/loading/error states per D-22

### External docs (read during research)
- `https://docs.hangfire.io/en/latest/background-methods/index.html` — `IBackgroundJobClient.Enqueue`, `ContinueJobWith`, job class signatures
- `https://docs.hangfire.io/en/latest/configuration/using-postgresql.html` — `Hangfire.PostgreSql` package + storage configuration
- `https://docs.hangfire.io/en/latest/configuration/using-dashboard.html` — `DashboardOptions.Authorization`, `IDashboardAuthorizationFilter` shape
- `https://docs.hangfire.io/en/latest/background-methods/dealing-with-exceptions.html` — `[AutomaticRetry]` attribute, retry attempts, backoff semantics (D-04)
- `https://docs.hangfire.io/en/latest/background-methods/passing-cancellation-tokens.html` — `IJobCancellationToken` patterns for D-11
- `https://learn.microsoft.com/en-us/dotnet/api/system.threading.channels.channel-1` — bounded `Channel<T>` for D-17
- `https://github.com/charlesw/tesseract` — `TesseractEngine` lifecycle, thread-safety (NOT thread-safe per the engine's docs — confirms D-16's pool model)
- `https://learn.microsoft.com/en-us/aspnet/core/fundamentals/host/hosted-services` — `IHostedService` for D-18 warmup
- `https://learn.microsoft.com/en-us/aspnet/core/security/anti-request-forgery` — CSRF posture for Hangfire dashboard POSTs (`requeue`/`delete`); flagged for planner review

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- **`UploadReceiptFilesHandler.cs:38-206`** — the entire current synchronous loop is the source material for `ProcessReceiptFileJob` + `ClassifyBatchJob` split. The cross-receipt batching logic (`pending` collection + single AI call at line 182) maps directly to `ClassifyBatchJob`. The per-file extract+parse loop (lines 52-167) maps to `ProcessReceiptFileJob`.
- **`AiOnlyClassificationService.cs:46-119`** — token pre-charge + per-item-refund-on-Unknown + full-refund-on-failure pattern is preserved verbatim inside `ClassifyBatchJob`. No changes to `ITokenService`. The "AI failure → refund all" branch (lines 71-75) is the exact path cancellation also takes (D-12).
- **`TesseractImageTextExtractor.cs:45-125`** — OCR logic (downsample, LstmOnly, SingleBlock, OcrTextNormalizer) is preserved verbatim inside `TesseractEnginePool.ExtractTextAsync`; only the engine lifecycle changes (Singleton+lock → Channel-acquire-release).
- **`AuthService.cs:80-95`** (login flow) — JWT minting site for D-09's `role` claim injection. The existing claim-set construction patterns are minimal; appending one more claim is a 2-line change.
- **`Result<T>` pattern** (`Backend/src/TaxReader.Domain/Common/Result.cs`) — every new handler returns `Result<T>`; the cancel endpoint maps `IsFailure(reason: "TerminalState")` → 409, `IsFailure(reason: "NotFound")` → 404.
- **`ICurrentUser`** (Scoped) — per-user data isolation in the new `CancelReceiptFileHandler` and `GetReceiptFileStatusHandler` (filter by `f.UserId == currentUser.UserId` on every query, same idiom as `DeleteReceiptFileHandler`).
- **`IOptions<TesseractOptions>`** — already wired in `DependencyInjection.cs:56`; adding `PoolSize` is a property + env var addition with the established `__`-nested pattern.
- **`LogContext.PushProperty` from Phase 1 D-18** — wrap both `ProcessReceiptFileJob.HandleAsync` and `ClassifyBatchJob.HandleAsync` bodies; the existing Serilog enricher (`FromLogContext`) already lifts these properties into structured log output.
- **TanStack Query `refetchInterval`** — existing `use-receipts.ts` hook pattern is the template for polling; just gate the interval on `data?.status` being non-terminal.
- **Frontend `sonner` toast pattern** (used throughout upload-form.tsx) — covers the "Vorgang abgebrochen" / "Cancellation failed" surfaces for the new cancel action.

### Established Patterns
- **`IOptions<T>` for config** with `SectionName` constants — `TesseractOptions` (existing) plus a possible `HangfireOptions` (`SeedAdminEmails`, retry counts if configurable) follow the same pattern.
- **`__`-nested env vars** — `Tesseract__PoolSize`, `Hangfire__SeedAdminEmails` follow.
- **Central Package Management** (`Backend/Directory.Packages.props`) — new packages: `Hangfire.AspNetCore`, `Hangfire.PostgreSql`. No package-management ceremony beyond adding to `.props`.
- **Per-user data scoping in handlers** — every new query/command filters by `userId`; never trust client-supplied identifiers. Applies to `GetReceiptFileStatusHandler` and `CancelReceiptFileHandler`.
- **German user-facing strings in `Result<T>.Failure`** — extends to D-21's error catalog and D-13's `errorMessage` field.
- **`AddHostedService<T>()`** — pattern for `TesseractEnginePoolWarmupService` and `SeedAdminUsersHostedService`; both run on `StartAsync` and exit cleanly.
- **`.RequireAuthorization()` group default + `.AllowAnonymous()` opt-out** (`Program.cs:153`) — `/hangfire` is NOT under `/api/v1` so the global group doesn't auto-apply; the `IDashboardAuthorizationFilter` is the sole auth path. New status + cancel endpoints inherit the standard `/api/v1` auth.

### Integration Points
- **Pipeline order in `Program.cs`** (preserved from Phase 2 D-06):
  1. `UseForwardedHeaders` (FIRST, Phase 2)
  2. `UseMiddleware<ExceptionHandlingMiddleware>`
  3. `UseCors`
  4. `UseSerilogRequestLogging`
  5. `UseRateLimiter` (Phase 2)
  6. `UseAuthentication`
  7. `UseAuthorization`
  8. `UseHangfireDashboard("/hangfire", new DashboardOptions { ... })` ← NEW; AFTER auth so the dashboard filter sees claims
  9. Endpoint mapping (`MapGroup("/api/v1")` plus the existing maps)
- **`AppDbContext`** — no schema-level Hangfire entanglement; Hangfire creates its own `hangfire.*` tables on first run via `Hangfire.PostgreSql`'s migration runner. EF migrations stay in the `public` schema; Hangfire's schema is separate and ignored by EF.
- **`ICurrentUser` in Application jobs** — Hangfire jobs do NOT run inside an HTTP request, so `ICurrentUser` (which reads from `HttpContext`) is not directly injectable. Job classes accept `Guid userId` as a method parameter (Hangfire serializes job arguments); the handlers internally re-create the equivalent of `currentUser.UserId` via that parameter. `AiOnlyClassificationService` currently depends on `ICurrentUser` (line 21) — needs a refactor so `ClassifyBatchJob` can pass the userId explicitly.
- **EF migration commands** (per `CLAUDE.md`): `dotnet ef migrations add AddIsAdminToUsers -p Backend/src/TaxReader.Infrastructure -s Backend/src/TaxReader.Api` and `dotnet ef migrations add AddQueuedAndCancelledProcessingStatuses -p Backend/src/TaxReader.Infrastructure -s Backend/src/TaxReader.Api`.
- **`docker-compose.yml`** — `api` service adds `Tesseract__PoolSize` + `Hangfire__SeedAdminEmails`; Hangfire dashboard is internal-only (Caddy maps `/hangfire` like any other path, no separate exposure).
- **Phase 2 upload-concurrency rate limit retirement** — `Backend/src/TaxReader.Api/Endpoints/ReceiptFileEndpoints.cs:48` `.RequireRateLimiting("upload-concurrency")` is removed (or replaced with a much looser policy like global 60/min IP); the synchronous-pipeline reason for it disappears with the 202 response.

</code_context>

<specifics>
## Specific Ideas

- **"Trustworthy classification > speed/UX polish" (PROJECT.md key decision)** — the existing single cross-receipt Anthropic batched call is a Core Value safeguard (consistent classification across items uploaded together). D-01's parent+classify-batch topology is non-negotiable for that reason; the simpler "job-per-file with one AI call each" option would have broken the batching invariant.
- **The 60-min access-token TTL is the tolerance window for admin demotion** (D-09). Acceptable today; if Phase 6 / LEG-08 audit-log work needs faster demotion, the dashboard filter can re-read `User.IsAdmin` from DB on each dashboard request (D-09 alternative #3) without re-issuing JWTs.
- **Hangfire's invisibility timeout (D-15) is the only crash-recovery mechanism we're committing to.** No bespoke orphan sweep. If real traffic shows orphans surviving the 30-min window, we add a sweep — but only against evidence. Documented as an implicit Hangfire dependency for future-us.
- **The `Channel<TesseractEngine>` pool is bounded by `PoolSize`** (D-17). If `Hangfire.WorkerCount > PoolSize`, jobs queue inside the channel — the wait is observable in logs. If `Hangfire.WorkerCount < PoolSize`, engines sit idle — wasted memory but no correctness issue. We align them deliberately in DI to make either misconfiguration loud.
- **The HttpOnly cookie + localStorage dual-storage** (D-10) is one of those decisions that costs explanation but no implementation complexity. Reviewers will ask "why two transports for the same token?" — the answer is "the SPA needs JS-readable for Axios interceptors; `/hangfire` needs cookie-borne for browser navigation; same JWT, same TTL, same signing secret".
- **Mid-Anthropic cancellation is best-effort** (D-11). `HttpClient.SendAsync` honors `CancellationToken` but the upstream model may have already produced output the network is racing to deliver. We treat the call as cancelled from the client's perspective and refund; we don't worry about the wasted Anthropic compute. The token economy is pass-through; we eat that cost on cancel.

</specifics>

<deferred>
## Deferred Ideas

- **CSRF posture for Hangfire dashboard POST actions** (requeue, delete) — Hangfire ships a built-in anti-forgery setup but the `tr_access` cookie + SameSite=Strict already covers the threat model for our scale. Revisit if Phase 7 lawyer review flags it.
- **Audit logging of dashboard actions** (who requeued / deleted which job, when) — fold into Phase 6 LEG-08 audit_log. The `AuditLogger` registered there can wrap `IDashboardAuthorizationFilter` (or an `IDashboardMonitor`) and capture the JWT subject + action.
- **Rate-limit policy on `/hangfire` path** — admin tool, low volume; the global 60/min IP limit (Phase 2 D-09) is enough. Revisit only if abuse surfaces.
- **SPA logout flow to clear the `tr_access` cookie** — the SPA currently doesn't have an explicit logout endpoint (just localStorage clear); Phase 3 adds `POST /auth/logout` (or extends `DELETE /auth/account`) to clear the cookie. Minor footprint.
- **SSE / long-poll for status push** — TanStack Query 2s polling is fine at 100–500 users with 1–10 files/upload (~peak 5 polls/s/user). Defer the protocol upgrade until BetterStack (Phase 7 OBS-03) shows polling cost is a real problem.
- **`ProcessingRun` retention policy** — recurring cleanup defers to Phase 6 (LEG-08 audit log decides retention uniformly across audit-relevant tables).
- **Per-route concurrency limit on `POST /receipts/{id}/reclassify`** (Phase 2 deferred to "post-launch if observed") — still deferred; Phase 3's 202-Accepted model doesn't apply to reclassify yet (it's a per-receipt sync call).
- **PdfPig zero-words → Tesseract fallback** (CONCERNS.md #11) — important but separate-PR-scope; Phase 3 keeps the existing extractor wiring. Likely a Phase 4 polish item once the rule-classifier work surfaces edge-case receipts where the fallback bites.
- **Worker autoscaling / dynamic pool sizing** — single Docker Compose api container handles target scale; no autoscaling required. Container resource limits in compose are the throttle if Tesseract pool growth becomes an issue.
- **Hangfire batches (Hangfire.Pro extension)** — the LGPL Hangfire.Pro batch API would make D-01's parent/child coordination cleaner but introduces a paid dependency. Defer; build the coordination ourselves on the open-source core.
- **Token-economy: tokens-debited-at-enqueue-but-not-charged-until-classify-time as a separate ledger state** — currently D-02's "charge at ClassifyBatch start" means the pre-charge IS the ledger event. A more accurate two-phase ledger (reserve → commit/refund) is overkill at this scale; revisit if the token economy becomes audit-heavy in Phase 5 (PAY-* introduces real-money flows).
- **OpenTelemetry tracing across HTTP → Hangfire boundary** — Phase 1 D-19 deferred; Phase 3's `JobId` LogContext push is sufficient at this scale.

</deferred>

---

*Phase: 03-background-pipeline-tesseract-pool*
*Context gathered: 2026-05-18*
