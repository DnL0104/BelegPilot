# Phase 1: Foundation Cleanup + CI - Context

**Gathered:** 2026-05-04
**Status:** Ready for planning

<domain>
## Phase Boundary

Establish CI/CD with build-test-lint gates, observability via Sentry + structured logging, and a clean working tree — so every later phase in this hardening milestone can be verified.

In scope: CI workflow + branch protection, hygiene cleanup (`storage/`, `build-diag.txt`, model alignment, CORS, `.gitignore`), Sentry integration (.NET + Next.js, EU residency, PII scrubbing), Serilog enrichers + correlation IDs in long-running handlers, top-level `README.md`.

Out of scope (later phases own these):
- `refresh_tokens` table / rotation (Phase 2)
- `AddRateLimiter` policies (Phase 2)
- Hangfire + `JobId` correlation (Phase 3)
- Vitest/Playwright frontend tests (Phase 7)
- Postgres integration tests via Testcontainers (Phase 7)
- BetterStack uptime monitors + Sentry alert tuning against real traffic (Phase 7 / OBS-03 / QA-06)
- TTDSG cookie banner / consent gating wire-up (Phase 6)

</domain>

<decisions>
## Implementation Decisions

### Anthropic model alignment (FND-02)
- **D-01:** `claude-haiku-4-5` becomes the single documented production default. Reason: ~10× cheaper and ~3-5× faster than Sonnet, sufficient for the (initially 8, later 13) DE-category classification choice; keeps the token-economy margin generous.
- **D-02:** Lock-in mechanism = single source of truth + startup-log. The code default in `Backend/src/TaxReader.Infrastructure/Configuration/AnthropicOptions.cs` is the source of truth (`"claude-haiku-4-5"`). `docker-compose.yml:38` and `.env.example:19` are updated to match. `CLAUDE.md` documents the choice. On startup, the API logs the resolved `Anthropic__Model` value (info-level) so any drift is visible in Sentry/logs without throwing.
- **D-03:** No startup-time hard guard or allow-list; no required-config strip. Keep configurability — the existing `AnthropicOptions.cs` comment "Override in appsettings for higher accuracy" stays valid as an escape hatch for future per-environment experiments.

### Hygiene cleanup (FND-01, FND-03)
- **D-04:** Delete `Backend/src/TaxReader.Api/storage/2026/04/` (contains 2 real receipt PDFs — PII left over from local dev). Files are untracked, so `git rm` is not needed; just delete from disk.
- **D-05:** Extend `.gitignore` to cover the actually-affected paths: `Backend/src/TaxReader.Api/storage/`, `build-diag*.txt`, `*.binlog`. The current rules (`storage/`, `Backend/storage/`) miss the API project's nested `storage/` subdirectory.
- **D-06:** Add a dedicated CI hygiene step (`hygiene-check` job) that fails the build if `storage/`, `Backend/storage/`, `Backend/src/TaxReader.Api/storage/`, `build-diag*.txt`, or `*.binlog` files appear in the tree. Belt-and-suspenders alongside `.gitignore`. Satisfies Success Criterion #2 (`CI fails if reintroduced`).
- **D-07:** CORS production fail-mode for FND-03: when `CORS_ALLOWED_ORIGINS` is unset AND environment is **not** Development, register a deny-all CORS policy (no `WithOrigins` call). Log a warning at startup. Drop the `localhost:3000` fallback from the non-Dev branch in `Program.cs:108-110`. Browsers in production speak to Caddy (same-origin) so this is mostly inert — but the code stops being misleading.

### CI workflow design (FND-04)
- **D-08:** Single `.github/workflows/ci.yml` with parallel jobs: `hygiene-check`, `backend-build-test`, `frontend-lint-build`. Built-in caching via `actions/setup-dotnet` and `actions/setup-node`. Triggers: pull requests targeting `main` + pushes to `main`. Concurrency group keyed on `${{ github.workflow }}-${{ github.ref }}` with `cancel-in-progress: true` for PRs (not for `main`).
- **D-09:** Backend test scope = existing `TaxReader.UnitTests` only (in-memory EF). No Postgres service spun up in CI. Phase 7's `QA-01` (Testcontainers + Respawn integration tests) will add a separate `integration-tests` job later. Avoids speculative CI infrastructure.
- **D-10:** Branch protection on `main`: PRs required (no direct push), required status checks = `hygiene-check` + `backend-build-test` + `frontend-lint-build`, no required reviewers (solo dev), no signed-commit requirement, no linear-history requirement. Tuned for solo-dev velocity without sacrificing the merge-blocking guarantee.
- **D-11:** No CI secrets needed in Phase 1 — unit tests use in-memory EF and don't call Anthropic. Sentry DSN is a public symbol (frontend bundle ships it) so no secret needed for builds. `ANTHROPIC_API_KEY`, `JWT_SECRET`, etc. become CI secrets only when integration tests / E2E land in Phase 7.

### README (FND-05)
- **D-12:** Top-level `README.md`, English (matches code/comment language), brief structure: project tagline → prerequisites (.NET 10 SDK, Node 22+, Docker Desktop, Tesseract for non-container dev with macOS/Linux install hints) → quick start (`cp .env.example .env`, edit secrets, `docker compose up --build`, browse to `https://localhost`) → links to `CLAUDE.md` + `.planning/codebase/` for deeper docs. No screenshots. The end-user product surface is German, but dev docs stay English to match the existing codebase convention.

### Sentry integration (OBS-01)
- **D-13:** Sentry Developer Free tier on the EU region (Frankfurt — `sentry.eu.io`). 5k errors / 10k perf units per month is sufficient for the 100–500 paying user target with the quiet alert baseline below. EU residency is a hard DSGVO requirement.
- **D-14:** PII scrubbing posture = default-deny + small allow-list. In `BeforeSend` / `BeforeSendTransaction`:
  - Strip request bodies entirely
  - Strip query strings except an explicit allow-list (`page`, `pageSize`, `year`, `format`)
  - Strip HTTP headers except `User-Agent`
  - Mask URL path segments matching a UUID pattern to `:id`
  - Strip user email; keep a hash of the user ID as `user.id_hash`
  - Strip raw receipt content, item descriptions, vendor names, classification reasoning text from any captured event
- **D-15:** Alert routing = email-only to the solo-dev address. Two starting rules: (a) **new error type** with 1h cooldown, (b) **sustained rate** ≥ 10 events/min for ≥ 5 min. **No** page-on-first-error. No Slack/PagerDuty in this milestone. Phase 7 (`QA-06`) retunes against real-traffic baseline.
- **D-16:** Frontend Sentry stays **disabled in production** until Phase 6 wires the TTDSG consent banner. `sentry-init.ts` calls `Sentry.init({...})` only when `process.env.NEXT_PUBLIC_SENTRY_ENABLED === "true"`, which we leave unset/`false` in `docker-compose.yml`'s `web` service for Phase 1. Backend Sentry runs unconditionally (server-internal errors aren't browser data). When Phase 6 lands, the consent banner flips a runtime flag that gates `Sentry.init`.

### Correlation IDs in long-running handlers (OBS-02)
- **D-17:** Backend-internal correlation only. Add `Enrichers.FromLogContext()` and `Enrichers.WithEnvironmentName()` to the Serilog config (registered via `appsettings.json` so `ReadFrom.Configuration` picks them up). The existing `UseSerilogRequestLogging` already attaches ASP.NET Core's `RequestId`. No frontend changes, no W3C `traceparent` header, no `X-Correlation-Id` custom header.
- **D-18:** Inside `UploadReceiptFilesHandler.HandleAsync`, wrap the per-file processing block with `using (LogContext.PushProperty("ReceiptFileId", receiptFileId))`. Wrap any nested call (extraction, parsing, classification) so every log line emitted within the scope carries the ID. Phase 3's `ProcessReceiptFileJob` will add the second `LogContext.PushProperty("JobId", jobId)` at that time — explicitly NOT pre-wired now.
- **D-19:** Correlation ID surface = Serilog only. No Sentry tag wiring (frontend Sentry is off; backend Sentry will see `RequestId` already via the integration's HTTP context). No `X-Request-Id` HTTP response header. Reconsider in Phase 6 once frontend Sentry is gated and live.

### Claude's Discretion
- Exact Serilog console output template (timestamp format, color, JSON vs plain). Default to a readable plain-text dev template + structured JSON in production via `appsettings.{Environment}.json`.
- Exact Sentry SDK package selection (.NET: `Sentry.AspNetCore`; frontend: `@sentry/nextjs`) and minor-version pinning policy.
- Hygiene-check shell snippet implementation (single `bash -c "test ! -e ... && ... "` step vs a tiny script).
- Whether to add a `setup-dotnet` cache-key suffix that includes `Directory.Packages.props` hash (likely yes — central package management means that's the cache invalidation signal).
- Where in `CLAUDE.md` to document the Anthropic model choice (likely the existing "Project" section or a new "Operations" subsection).

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Project context
- `.planning/PROJECT.md` — Vision, Core Value (trustworthy classification), constraints (3-month timeline, solo dev, scale 100–500 users), Key Decisions table
- `.planning/REQUIREMENTS.md` — v1 requirements list; FND-01 through FND-05 + OBS-01 / OBS-02 are this phase's deliverables; traceability table at the bottom
- `.planning/ROADMAP.md` — Phase 1 entry with success criteria; downstream phase ordering and dependencies

### Codebase intel
- `.planning/codebase/STACK.md` — Tech stack inventory (.NET 10 / EF Core 10 / Next.js 16 / Caddy / Anthropic / Tesseract); package versions; configuration sources; Anthropic model mismatch documented
- `.planning/codebase/CONCERNS.md` — Concerns inventory (analysis 2026-04-29). This phase fixes concerns #1 (no CI), #3 (`build-diag.txt`), #4 (`storage/`), #6 (model mismatch), #14 (CORS), #18 (no README), #20 (no correlation enrichment)
- `.planning/codebase/CONVENTIONS.md` — Backend (file-scoped namespaces, primary-constructor DI, `Result<T>`, `Async` suffix) + frontend (RHF + Zod, `cn()`, German user-facing strings) conventions
- `.planning/codebase/ARCHITECTURE.md` — Layer rules (Domain has zero deps; Application defines interfaces; Infrastructure implements; API thin); long-running handler context (`UploadReceiptFilesHandler`); `IOptions<T>` pattern for configuration

### Files this phase will touch (read before editing)
- `Backend/src/TaxReader.Api/Program.cs` — Serilog config, CORS policy, JWT setup, endpoint mapping
- `Backend/src/TaxReader.Api/appsettings.json` + `appsettings.Development.json` — Serilog enricher list, log template
- `Backend/src/TaxReader.Infrastructure/Configuration/AnthropicOptions.cs` — Model default
- `Backend/src/TaxReader.Application/Commands/UploadReceiptFiles/UploadReceiptFilesHandler.cs` — Add `LogContext.PushProperty("ReceiptFileId", id)` scope
- `docker-compose.yml` — Update `Anthropic__Model` default; add Sentry env vars (DSN, env, release)
- `.env.example` — Update `ANTHROPIC_MODEL` default; add Sentry DSN placeholder
- `.gitignore` — Add `Backend/src/TaxReader.Api/storage/`, `build-diag*.txt`, `*.binlog`
- `Frontend/src/app/layout.tsx` (or `Frontend/sentry.client.config.ts` per `@sentry/nextjs` convention) — Conditional `Sentry.init` gated on `NEXT_PUBLIC_SENTRY_ENABLED`
- `CLAUDE.md` — Document Anthropic model choice + Sentry setup

### External docs (read during research)
- `https://docs.sentry.io/platforms/dotnet/guides/aspnetcore/` — `Sentry.AspNetCore` setup (`UseSentry` on host, `BeforeSend` filter)
- `https://docs.sentry.io/platforms/javascript/guides/nextjs/` — `@sentry/nextjs` SDK; client-only init pattern; `BeforeSend` filter
- `https://docs.sentry.io/security-legal-pii/scrubbing/server-side-scrubbing/` — Server-side data scrubbers (defense in depth on top of `BeforeSend`)
- `https://serilog.net/` + `https://github.com/serilog/serilog-aspnetcore` — `LogContext`, `UseSerilogRequestLogging`, enricher list
- `https://docs.github.com/en/actions/automating-builds-and-tests/building-and-testing-net` — Reference workflow for `actions/setup-dotnet` + central package management caching
- `https://docs.github.com/en/repositories/configuring-branches-and-merges-in-your-repository/defining-the-mergeability-of-pull-requests/about-protected-branches` — Branch protection rules

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- **Serilog setup** (`Program.cs:18-33`): bootstrap logger + `UseSerilog((ctx, cfg) => cfg.ReadFrom.Configuration(ctx.Configuration))` — enricher additions land in `appsettings.json`, not Program.cs.
- **`UseSerilogRequestLogging` middleware** (`Program.cs:122`): already attaches ASP.NET Core's `RequestId` to every request-scoped log. Don't re-implement; just add `Enrichers.FromLogContext()` so `LogContext.PushProperty` calls work.
- **`AnthropicOptions`** (`Backend/src/TaxReader.Infrastructure/Configuration/AnthropicOptions.cs`): `IOptions<T>` POCO with `SectionName = "Anthropic"`. Default model lives in the property initializer; that's the canonical write site for D-01.
- **`UploadReceiptFilesHandler`** (`Backend/src/TaxReader.Application/Commands/UploadReceiptFiles/UploadReceiptFilesHandler.cs`): the only currently long-running handler — Phase 3 will move work into `ProcessReceiptFileJob`. `ReceiptFileId` push site is the per-file `foreach` body.
- **`ICurrentUser`** (Scoped): exposes `UserId` if D-14's allow-listed `user.id_hash` Sentry tag wants the user reference.
- **`ExceptionHandlingMiddleware`** (`Backend/src/TaxReader.Api/Middleware/ExceptionHandlingMiddleware.cs`): top-level error → ProblemDetails translator; Sentry's ASP.NET Core integration sits ahead of it in the pipeline and captures the unhandled exception before this middleware turns it into a response.

### Established Patterns
- **`IOptions<T>` for config** with `SectionName` constants — Sentry options follow the same pattern (new `SentryOptions.cs` in `Infrastructure/Configuration/` if needed beyond what `Sentry.AspNetCore` provides).
- **Environment variables `__`-nested** (`Anthropic__Model`, `Jwt__Secret`) — Sentry DSN becomes `Sentry__Dsn` in compose.
- **Central Package Management** (`Backend/Directory.Packages.props`): NuGet versions live there, per-project `<PackageReference>` carries no version. Add Sentry packages there.
- **Result<T> for error handling** — Sentry captures don't throw; they're side-effects of the failure path that already flows through `Result<T>.Failure`.

### Integration Points
- **Sentry .NET hook**: `builder.WebHost.UseSentry(o => { o.Dsn = ...; o.BeforeSend = ...; })` — added in `Program.cs` between `WebApplication.CreateBuilder` and `builder.Services.AddInfrastructure`.
- **Sentry Next.js hook**: `@sentry/nextjs` adds `sentry.client.config.ts` + `sentry.server.config.ts` + `sentry.edge.config.ts` at the Frontend root. Wrap `Sentry.init` in a feature-flag check for D-16.
- **Hygiene CI step**: shell command checks for files in working tree post-checkout — runs before any build step in the workflow so a fast-fail surfaces immediately.
- **CORS deny-all path**: `Program.cs:107-110` `policy.WithOrigins(...)` is the line to delete in the non-Dev branch; replace with no-op (the AddDefaultPolicy still needs to be called so the middleware doesn't error, but no origins means no responses pass the CORS check).
- **`docker-compose.yml` rebrand note**: container names + database name still say `belegpilot-*` / `belegpilot` (lines 3, 13, 23, 31, 47, 59); this phase doesn't fix that — flagged in `<deferred>` for a follow-up hygiene pass.

</code_context>

<specifics>
## Specific Ideas

- "Solo dev with paging-style alerting expectation" (PROJECT.md constraint) — alerts must be quiet enough that the dev believes them when they fire. Two-rule baseline (new-error 1h cooldown + sustained-rate gate) is the floor, retuned in Phase 7.
- "Code/compose mismatch" (CONCERNS.md #6) — the failure mode is silent-cost-divergence: dev runs Haiku, prod runs Sonnet, the dev never realizes. The startup-log line documenting the resolved model is the user-visible signal preventing recurrence.
- Receipts contain real user PII (names, addresses, vendor lists, item descriptions). Default-deny PII scrubbing in Sentry is the GDPR-safe default; we re-enable specific fields when we confirm they're safe.
- Frontend code/comments are English; user-facing copy is German (`Sie`-form). README is dev-facing → English. The convention boundary lives at the user-visible surface, not the dev tooling.

</specifics>

<deferred>
## Deferred Ideas

- **Stripe / payment-provider env vars + multi-environment safety** — Phase 5 (PAY-06)
- **W3C `traceparent` browser → backend trace propagation** — Possible Phase 6/7 follow-up once frontend Sentry is consent-gated and live; not load-bearing for OBS-02 today
- **Sentry Slack / PagerDuty integration** — Out of scope here; reconsider after Phase 7's alert-rule retuning if the email signal is too noisy or too quiet
- **OpenTelemetry / distributed tracing across API ↔ Hangfire** — Out of scope; Phase 3's `JobId` LogContext push covers the diagnostic need at this scale
- **Dev-machine pre-commit hooks (husky/lefthook)** — Solo dev doesn't need it; CI hygiene step is sufficient
- **macOS/Linux `start.sh` / `stop.sh` equivalents to `start.ps1`** — `docker compose up --build` from the README is the documented path; CONCERNS.md #19 stays in the backlog
- **Container/database rebrand from `belegpilot-*` / `belegpilot` to `taxreader-*` / `taxreader`** — Cosmetic; touching `POSTGRES_DB` requires a migration plan. Backlog.
- **Sentry release tagging + source maps + commits-since-last-release** — Useful in Phase 7 once release cadence is real; out of scope for the bare-minimum "errors received with PII scrubbed" success criterion
- **Sentry Performance / tracing / session replay** — Adds cost (perf units) + PII surface (replay especially); out of scope
- **Backend Sentry test endpoint (`/sentry-debug`)** — Not strictly required; if added, gate behind `if (env.IsDevelopment())` and keep it inside `Program.cs` rather than a permanent endpoint

</deferred>

---

*Phase: 01-foundation-cleanup-ci*
*Context gathered: 2026-05-04*
