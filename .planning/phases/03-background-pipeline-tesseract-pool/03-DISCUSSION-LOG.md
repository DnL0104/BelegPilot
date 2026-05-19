# Phase 3: Background Pipeline + Tesseract Pool - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-05-18
**Phase:** 03-background-pipeline-tesseract-pool
**Areas discussed:** Job topology & AI batching, Hangfire dashboard auth, Cancellation/polling/refunds, Tesseract pool design

---

## Job topology & AI batching

### Q1: Job model — how to move the cross-receipt batched Anthropic call to Hangfire

| Option | Description | Selected |
|--------|-------------|----------|
| Job-per-file, AI per file | One ProcessReceiptFileJob per file; each does extract+parse+classify in its own Anthropic call. Simplest topology, independent retries, but a 10-file upload costs 10 AI roundtrips instead of 1. | |
| Parent + classify-batch child | ProcessReceiptFileJob (per file) handles extract+parse and fans into ClassifyBatchJob (per upload) that runs ONE Anthropic call across all parsed items. Preserves today's wallclock win. | ✓ |
| Job-per-file with greedy windowing | Each per-file job pushes items onto a queue; a Classifier worker drains it every ~500ms with batched calls. More machinery but reusable beyond a single upload. | |

**User's choice:** Parent + classify-batch child (Recommended)
**Notes:** Preserves the Core-Value-protecting cross-receipt batching invariant — consistent classification across items uploaded together.

### Q2: Token pre-charge timing

| Option | Description | Selected |
|--------|-------------|----------|
| Charge at ClassifyBatch start | Pre-charge happens inside ClassifyBatchJob after parse, preserving the per-item cost model exactly. | ✓ |
| Estimate at upload, reconcile after parse | Per-file estimate at HTTP upload endpoint; reconcile inside ClassifyBatchJob. Lets us 402-reject at 202-time. | |
| Charge per file at job start | Per-file pre-charge before extract; spreads reconciliation across N jobs. | |

**User's choice:** Charge at ClassifyBatch start (Recommended)
**Notes:** Surfaces InsufficientTokens via the status polling endpoint instead of at HTTP 202 time.

### Q3: 202 Accepted response shape

| Option | Description | Selected |
|--------|-------------|----------|
| Per-file array | `{ files: [{ receiptFileId, jobId, fileName }] }`. Frontend polls per file. Matches current upload-form per-file cards. | ✓ |
| Per-batch with embedded files | `{ uploadBatchId, files: [...] }` + a batch-status endpoint. Tighter polling but introduces an UploadBatch concept. | |
| Per-file array + batch convenience | Both. Most flexible, most surface area. | |

**User's choice:** Per-file array (Recommended)
**Notes:** Frontend computes batch-level progress client-side from per-file states.

### Q4: Hangfire retry policy on transient failures

| Option | Description | Selected |
|--------|-------------|----------|
| Tiered: extract/parse retry, AI no-retry | ProcessReceiptFileJob gets 3 retries (idempotent); ClassifyBatchJob gets 0 (existing refund branch handles it). | ✓ |
| Uniform 3 retries everywhere | Simpler mental model; AI failures get 3× the refund churn. | |
| No retries; user-driven retry | Maximum control; transient Postgres deadlocks become user-facing failures unnecessarily. | |

**User's choice:** Tiered: extract/parse retry, AI no-retry (Recommended)

---

## Hangfire dashboard auth

### Q1: How to gate /hangfire (no admin role/claim exists today)

| Option | Description | Selected |
|--------|-------------|----------|
| Env-var-allow-listed user IDs | Hangfire__AdminUserIds CSV. Lowest blast radius; doesn't generalize. | |
| JWT 'role' claim | User.IsAdmin column + role claim in access JWT. Generalizes to future role-gated endpoints. | ✓ |
| BasicAuth + separate password | Independent auth via env-var credentials. Cleanest separation; second mechanism. | |
| Disable in production entirely | Dashboard only in dev/staging. Minimal surface; loses prod debugging. | |

**User's choice:** JWT 'role' claim (Recommended)

### Q2: First-admin bootstrap

| Option | Description | Selected |
|--------|-------------|----------|
| Migration-time seed via env var | Idempotent startup step reads Hangfire__SeedAdminEmails, sets IsAdmin on matching rows. | ✓ |
| Manual SQL after migration | Operator runs UPDATE after migration. Zero bootstrap code. | |
| First-registered-user gets admin | Self-bootstrapping; risky if anyone else registers first. | |

**User's choice:** Migration-time seed via env var (Recommended)

### Q3: Where the 'role' claim gets added

| Option | Description | Selected |
|--------|-------------|----------|
| Access token only | Claim in access JWT only; demotion takes effect within 60 min (next refresh). | ✓ |
| Refresh both, re-read on every refresh | DB hit per refresh; demotion faster. | |
| Tighter still — dashboard re-reads DB every request | DB hit per dashboard page; overkill for a low-traffic admin surface. | |

**User's choice:** Access token only (Recommended)

### Q4: Browser credentials transport for /hangfire

| Option | Description | Selected |
|--------|-------------|----------|
| JWT in HttpOnly cookie at login | tr_access cookie set at login/refresh; dashboard filter reads cookie. localStorage retained for SPA. One auth scheme, two transports. | ✓ |
| Separate /admin login that mints a short cookie | Stronger isolation; second login UX surface. | |
| Query-string token (one-time link) | Most secure (no long-lived cookie); most machinery. | |

**User's choice:** JWT in HttpOnly cookie at login (Recommended)

---

## Cancellation, polling & refunds

### Q1: Cancellable states

| Option | Description | Selected |
|--------|-------------|----------|
| Cancel any non-terminal state | Queued/Extracting/Parsing/Classifying all cancellable via IJobCancellationToken. ProcessingStatus gets Queued + Cancelled values. | ✓ |
| Cancel only Queued | Cancel rejected once a job has started executing. Trades user control for implementation simplicity. | |
| Cancel Queued + Extracting only | Practical middle ground; classification not interruptible. | |

**User's choice:** Cancel any non-terminal state (Recommended)

### Q2: Refund accounting on cancel

| Option | Description | Selected |
|--------|-------------|----------|
| All-or-nothing per file | Cancel before classify = no charge fired; cancel during classify = full batch refund via existing "AI failure" branch. | ✓ |
| Per-item granular | Partial-result handling; "pay for what you got". More accurate, more code. | |
| Full refund of whole upload batch | User-friendly; abusable (upload 10, cancel after 9 classify). Rejected. | |

**User's choice:** All-or-nothing per file (Recommended)

### Q3: Status polling shape

| Option | Description | Selected |
|--------|-------------|----------|
| Minimal status + progress hint | `{ status, updatedAt, errorCode?, errorMessage? }`. errorCode = stable enum for frontend; errorMessage = German display string. 2s polling. | ✓ |
| Status + step timestamps | Per-step transition timestamps for step indicator UI. More wire + UI surface. | |
| Status only, longer poll interval | Minimal everything, 5s polling. Failure UX lags. | |

**User's choice:** Minimal status + progress hint (Recommended)

### Q4: Worker recovery on container restart

| Option | Description | Selected |
|--------|-------------|----------|
| Hangfire's built-in invisibility timeout | Trust Hangfire's 30-min worker-heartbeat re-enqueue. ProcessReceiptFileJob is idempotent. No startup sweep. | ✓ |
| Startup sweep marks orphans Failed | Mark orphans Failed with German "Vorgang durch Neustart unterbrochen" message; user-driven retry. | |
| Startup sweep re-enqueues | Explicit re-enqueue at startup. Costs: distinguishing crash-orphans from cancel-orphans. | |

**User's choice:** Hangfire's built-in invisibility timeout (Recommended)

---

## Tesseract pool design

### Q1: Pool size

| Option | Description | Selected |
|--------|-------------|----------|
| Configurable, default 3 | TesseractOptions.PoolSize + Tesseract__PoolSize env. Sized to target scale; aligned with Hangfire WorkerCount. | ✓ |
| Fixed 5 | Maximum headroom; ~50 MB extra memory. | |
| Auto-size to ProcessorCount | Adapts to host; loses predictability across deploys. | |

**User's choice:** Configurable, default 3 (Recommended)

### Q2: Engine warmup

| Option | Description | Selected |
|--------|-------------|----------|
| Eager at startup | IHostedService creates all engines before /health Ready. Adds ~300ms boot; predictable steady state. | ✓ |
| Lazy on first acquire | Faster boot; first OCR pays init cost. | |
| Mixed: 1 eager + rest lazy | Compromise; saves boot time but second concurrent OCR pays init. | |

**User's choice:** Eager at startup (Recommended)

### Q3: Acquisition timeout

| Option | Description | Selected |
|--------|-------------|----------|
| Block on Hangfire cancellation token | Wait indefinitely on Channel.Reader.ReadAsync(jobCancellationToken). Only Hangfire abort breaks the wait. | ✓ |
| 30-second hard timeout | Bounds worst-case latency; transient slowness becomes retries. | |
| Configurable timeout | TesseractOptions.AcquireTimeoutSeconds. Maximum flexibility; more knobs. | |

**User's choice:** Block on Hangfire cancellation token (Recommended)

### Q4: Engine failure mode

| Option | Description | Selected |
|--------|-------------|----------|
| Quarantine + replace on exception | Dispose broken engine; create replacement. Self-healing under sporadic failures. | ✓ |
| Return + log; let it fail next time | Bad in cascade scenarios; one bad engine poisons multiple jobs. | |
| Quarantine; manual restart to recover | Permanent quarantine, page operator when pool empties. Good for surfacing bugs, bad under transient load. | |

**User's choice:** Quarantine + replace on exception (Recommended)

---

## Claude's Discretion

Captured as D-21 (German error catalog), D-22 (empty/loading/error UI patterns), D-23 (recurring cleanup jobs) in CONTEXT.md. The user opted to wrap up after the four selected areas; PIPE-05 and PIPE-06 fall to planner/executor discretion within the German Sie-form convention and existing shadcn primitive patterns. PIPE-01 recurring cleanup list (expired refresh tokens, abandoned Failed jobs, ProcessingRun retention deferred to Phase 6) is captured but exact cron expressions / retention windows are planner-decided.

Additional Claude's-discretion items called out inline in CONTEXT.md `<decisions>`:
- Exact IBackgroundJobClient invocation pattern (Enqueue + ContinueJobWith vs custom continuation poll)
- Class-typed vs static-method-typed Hangfire job targets
- DashboardOptions.Authorization filter composition
- Status enum string serialization (PascalCase vs snake_case)
- Endpoint home for GET /receipt-files/{id}/status (existing ReceiptFileEndpoints vs dedicated)
- Tesseract engine warmup ordering (parallel vs serial)
- Cookie-setting code location (AuthService vs endpoint layer)

## Deferred Ideas

Captured in CONTEXT.md `<deferred>`. Key items:

- CSRF posture for Hangfire dashboard POST actions (SameSite=Strict covers our threat model at scale)
- Audit logging of dashboard actions → fold into Phase 6 LEG-08 audit_log
- Rate-limit policy on /hangfire path → global IP limit is enough
- SPA logout endpoint to clear tr_access cookie (minor footprint, picked up in this phase)
- SSE / long-poll for status push (defer until polling cost is shown to be a real problem)
- ProcessingRun retention policy → defer to Phase 6 LEG-08 uniform audit retention
- PdfPig zero-words → Tesseract fallback (CONCERNS.md #11) → Phase 4 polish
- Hangfire batches (Hangfire.Pro paid extension) → build coordination on OSS core
- Two-phase token ledger (reserve → commit/refund) → revisit when PAY-* introduces real-money flows
- OpenTelemetry tracing across HTTP → Hangfire boundary → JobId LogContext push is enough at this scale
- Worker autoscaling / dynamic pool sizing → not needed at target scale
