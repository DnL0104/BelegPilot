# Concerns / Technical Debt / Risk Areas

**Analysis Date:** 2026-04-29

> Risk-ranked. Items higher up are higher priority — they either block evolution, leak secrets, or threaten production reliability.

---

## High — production reliability or security

### 1. No CI / no automated build & test gate
- **Evidence:** No `.github/workflows/`, no `azure-pipelines.yml`, no `.gitlab-ci.yml`. Tests run only on developer machines.
- **Why it hurts:** Regressions are caught at code-review time at best, post-merge at worst. The "Frontend has no tests at all" gap below compounds this — there is no automated pre-merge signal that anything works.
- **Suggested fix:** Add a workflow that runs `dotnet build`, `dotnet test`, `npm install`, `npm run lint`, and `npm run build` on every PR. Treat lint and test failures as merge-blocking.

### 2. Frontend has zero automated tests
- **Evidence:** `Frontend/package.json` has no test runner; no `__tests__/`, no `*.test.ts(x)`, no Vitest/Jest/Playwright/Cypress in dependencies.
- **Why it hurts:** All user-visible behavior — login flow, JWT refresh, upload mixed-success rendering, classification confirmation, dashboard aggregation — is unvalidated except by manual click-testing. The token-refresh logic in [api-client.ts](Frontend/src/lib/api-client.ts:41) (shared in-flight refresh promise, `_retry` flag, fallback to login) is non-trivial and fragile under refactor.
- **Suggested fix:** Add Vitest + React Testing Library for hook/component unit tests; Playwright for the upload-flow happy path and login/logout.

### 3. `build-diag.txt` (1.8 MB) committed at repo root
- **Evidence:** [build-diag.txt](build-diag.txt) is 1,809,432 bytes, dated 2026-04-15, listed as untracked in `git status`. The size suggests `dotnet build /bl` or similar diagnostic output captured during a build investigation.
- **Why it hurts:** Hides whatever was being investigated, bloats repo if accidentally committed, and may contain absolute developer paths or other low-grade leaks.
- **Suggested fix:** Investigate the original cause if the build issue is unresolved; otherwise add `*.binlog` and `build-diag*.txt` to `.gitignore` and delete the file.

### 4. `storage/` directory committed at repo root and in API output
- **Evidence:** Top-level `storage/` exists; `Backend/src/TaxReader.Api/storage/2026/04/` exists in the working tree. Note the `RemoveStoragePath` migration (`20260420055623_RemoveStoragePath`) — the database column was deliberately removed because PDFs are no longer persisted. The directories appear to be leftovers.
- **Why it hurts:** Real receipt PDFs that were uploaded during local testing may still sit on disk. They will get checked in if anyone runs `git add .`. Receipt PDFs typically contain names, addresses, and order numbers — i.e. PII.
- **Suggested fix:** Confirm the directories are no longer used (search for any code that writes to disk under `storage/`); add `storage/` to `.gitignore` (or verify it is already there); delete the directories.

### 5. Hardcoded Tesseract path comment + Windows-specific dev assumption
- **Evidence:** `Backend/src/TaxReader.Infrastructure/Configuration/TesseractOptions.cs` references `C:/Program Files/Tesseract-OCR/tessdata` in a comment (per STACK.md). Tessdata path is configured via `appsettings.Development.json` to a relative `tessdata` folder. Both `Backend/src/TaxReader.Api/tessdata/` and `Backend/src/TaxReader.Api/bin/Debug/net10.0/tessdata/` exist in the working tree.
- **Why it hurts:** New developers on macOS/Linux must install Tesseract themselves and the dev story is unclear from the README (there isn't one). The container build (`Backend/Dockerfile`) installs `tesseract-ocr-deu`+`tesseract-ocr-eng` via apt, so production is fine — only local dev is ambiguous.
- **Suggested fix:** Document the dev requirement in a `Backend/README.md` or top-level setup section; consider shipping a small subset of `tessdata` for tests rather than depending on system install.

### 6. `Anthropic__Model` default mismatch between code and compose
- **Evidence:**
  - Backend `AnthropicOptions.cs` defaults `Model` to `claude-haiku-4-5`
  - `docker-compose.yml:38` passes `${ANTHROPIC_MODEL:-claude-sonnet-4-5}`
- **Why it hurts:** Cost and accuracy diverge depending on whether `ANTHROPIC_MODEL` is set. Local dev (no compose env) → cheap Haiku. Self-hosted compose stack → 10× more expensive Sonnet by default. A user reading `appsettings.json` and confirming "we use Haiku" may be wrong about the production reality.
- **Suggested fix:** Pick one default and align both files; document the choice in `CLAUDE.md` or `INTEGRATIONS.md`.

### 7. Token-purchase endpoint without payment provider
- **Evidence:** `Frontend/src/lib/api-client.ts:288-291` exposes `purchaseTokens(amount)` → `POST /tokens/purchase`. No Stripe / PayPal / payment-provider dependency exists in either side.
- **Why it hurts:** Either the endpoint is a stub (a user can mint themselves arbitrary tokens via the API), or the docs are out of sync. Without payment integration, this is effectively a self-serve free-credit faucet.
- **Suggested fix:** Audit the implementation in `TokenService.cs` and `Backend/src/TaxReader.Api/Endpoints/TokenEndpoints.cs`. If it really does grant tokens without payment, gate it behind dev-only authorization until the payment integration ships.

---

## Medium — fragility, missing safety nets

### 8. Synchronous upload pipeline blocks the request
- **Evidence:** `UploadReceiptFilesHandler.HandleAsync` does extraction (PdfPig / Tesseract), parsing, and a Claude API call (60s timeout) all inside the HTTP request lifecycle. No background queue.
- **Why it hurts:** A 10-receipt batch where each Tesseract OCR takes ~3s + a 5s Claude roundtrip → 35s+ request. Mobile networks, browser timeouts (30s default for many stacks), and Caddy/Next.js intermediaries can interrupt. The user sees a generic failure even though some receipts were already saved (the handler `await`s every step inside the loop, so partial DB writes survive but the HTTP response is dropped).
- **Suggested fix:** Move pipeline to a background job (Hangfire, Quartz.NET, or simple `Channel<T>` + `BackgroundService`); return `202 Accepted` with a job ID; have the frontend poll. Acceptable interim: cap batch size in the validator.

### 9. Tesseract Singleton with internal lock = serial OCR under contention
- **Evidence:** `DependencyInjection.cs:40-45` notes "Tesseract is not thread-safe" and uses `Singleton` + internal locking. Multiple concurrent users uploading images will queue.
- **Why it hurts:** Throughput collapses under load. Each Tesseract call holds the lock; a 10-image upload batch from one user effectively blocks any other user's image upload.
- **Suggested fix:** Either pool a small set of `TesseractEngine` instances (Singleton holding `ConcurrentBag<TesseractEngine>`), or process image OCR off-request as part of the background-job migration in #8. Document the throughput limit in the meantime.

### 10. Refresh token persisted as a single column on `users`
- **Evidence:** `AuthService.cs:74-75, :120-121` writes `user.RefreshToken = refreshToken; user.RefreshTokenExpiresAt = ...`. One token per user. Issuing a new refresh token overwrites the old.
- **Why it hurts:** A user logged in on two devices will silently log out the older device every refresh. Also rules out refresh-token rotation auditing (you can't see when/where tokens were used). A leaked refresh token is single-use detectable only by the legitimate user being randomly logged out.
- **Suggested fix:** Move to a `refresh_tokens` table (id, user_id, token_hash, expires_at, revoked_at, last_used_at, user_agent) and store only the hash. This also makes "log out everywhere" a one-statement update.

### 11. Empty-state PdfPig fallback uses page-default text ordering
- **Evidence:** `PdfPigTextExtractor.cs:27-28` returns `page.Text` (PdfPig's default extractor) when the page has zero `GetWords()` results. The default extractor is known to produce inconsistent column ordering on multi-column PDFs.
- **Why it hurts:** Receipts that come out as image-only PDFs will hit the zero-words branch, return raw text, and parser regexes that work on the bounding-box-reconstructed layout will silently fail.
- **Suggested fix:** Detect zero-words and route to Tesseract OCR via the `IImageTextExtractor` (rasterize the page and OCR) rather than falling back to `page.Text`.

### 12. Error message leakage in upload failure
- **Evidence:** `UploadReceiptFilesHandler.cs:154-155, :153` writes `$"Processing failed: {ex.Message}"` directly into the HTTP response and the DB. Internal exception messages can include stack-pointer-like text or PdfPig/Tesseract internals.
- **Why it hurts:** Information disclosure to API consumers; uglier UX than necessary; persisted forever in `processing_runs.error_message`.
- **Suggested fix:** Map known exception types to user-friendly German strings; log the raw exception via Serilog; return only a generic "Verarbeitung fehlgeschlagen" externally. Keep the technical detail in the log, not the HTTP body.

### 13. No rate limiting / no anti-abuse
- **Evidence:** `Program.cs` does not call `AddRateLimiter`. No middleware enforces login-attempt throttling, registration throttling, or per-user upload rate caps.
- **Why it hurts:** Login/register endpoints are publicly reachable (`.AllowAnonymous`); BCrypt.Verify is intentionally slow so brute force is *somewhat* slowed, but credential stuffing is not stopped. Upload endpoint accepts any number of files of any size.
- **Suggested fix:** Add `AddRateLimiter` with a fixed-window or sliding-window policy on `/auth/*` and a separate concurrency limiter on `/receipt-files`. Cap upload count (e.g. 20 files) and per-file size in the validator.

### 14. CORS policy permissive in non-development
- **Evidence:** `Program.cs:108-110` — when `CORS_ALLOWED_ORIGINS` is unset *and* environment is **not** Development, the policy still calls `WithOrigins("http://localhost:3000")`. That isn't an open wildcard, but it is a non-obvious default that allows any process listening on `localhost:3000` to call the API as long as it can reach it. In a self-hosted compose stack the browser only ever speaks to Caddy, not directly to the API, so this is mostly inert — but the code is confusing and easy to break.
- **Suggested fix:** In production-like environments where `CORS_ALLOWED_ORIGINS` is unset, *deny* all origins (or drop CORS entirely since same-origin via Caddy is the path).

### 15. No integration tests for the EF / PostgreSQL layer
- **Evidence:** All tests use `UseInMemoryDatabase`. No `Testcontainers.PostgreSql` or similar.
- **Why it hurts:** EF in-memory provider does not enforce FK constraints, does not respect snake_case naming convention behavior at the SQL level, and won't catch raw SQL or `FromSqlRaw` typos. Migrations are not applied — schema drift between `OnModelCreating` configuration and the migration history can go unnoticed.
- **Suggested fix:** Add a small set of integration tests using `Testcontainers.PostgreSql`, focused on (a) duplicate-detection round-tripping, (b) cascade deletes, (c) at least one `dotnet ef database update` smoke-test against a clean container.

---

## Low — house-keeping, polish

### 16. `ClassificationRule` entity defined but unused
- **Evidence:** Domain entity `ClassificationRule.cs`, EF configuration `ClassificationRuleConfiguration.cs`, and table `classification_rules` exist. But `DependencyInjection.cs` registers `IClassificationService` as `AiOnlyClassificationService` only; no rule-based classifier is wired up. `CLAUDE.md` mentions "Phase 1: Rule-based" / "Phase 2: AI", but the implementation skipped Phase 1.
- **Why it hurts:** Confusing for new contributors; carries a table that takes up schema space and slows future migrations. `TestDataFactory.CreateRule(...)` exists but is unused outside its own factory.
- **Suggested fix:** Either implement a rule-based path or drop the entity + table in a future migration and remove the factory.

### 17. `appsettings.json` and `Development.json` not inspected here but coexist with env-var overrides
- **Evidence:** Multiple config sources (`appsettings*.json` + env vars with `__` separators). `docker-compose.yml` injects all secrets via env vars; local dev relies on `appsettings.Development.json`.
- **Why it hurts:** Two places to keep in sync. A change in `appsettings.json` may not match the env-var-driven production reality (see #6).
- **Suggested fix:** Document which keys are env-driven only and which can live in `appsettings.json`. Consider stripping `appsettings.json` to defaults that are safe to commit (no secrets, no keys).

### 18. README and onboarding gap
- **Evidence:** No top-level `README.md`. `CLAUDE.md` exists for AI agents but has no human onboarding section ("how to run locally"). `Frontend/AGENTS.md` warns about Next.js drift but offers no link to the relevant docs.
- **Why it hurts:** New contributors and future-you must reverse-engineer Docker Compose + .env to get the stack up.
- **Suggested fix:** Short top-level `README.md` covering: required tools, env vars (link `.env.example`), `docker compose up --build`, where to point a browser. Move build steps from `start.ps1` into the README so non-Windows devs see them.

### 19. PowerShell-only orchestration scripts
- **Evidence:** `start.ps1`, `stop.ps1` at repo root. No bash equivalents.
- **Why it hurts:** macOS/Linux contributors copy commands manually. The shell environment in this session is bash-friendly, so contributors are likely already mixed-platform.
- **Suggested fix:** Add `start.sh` / `stop.sh` or — better — replace both with Make targets (`make up`, `make down`, `make logs`) that are platform-agnostic.

### 20. No structured trace correlation across requests
- **Evidence:** Serilog console only; no `CorrelationId` enrichment or W3C trace context.
- **Why it hurts:** When a multi-step upload fails midway, finding all log lines for that request requires guessing on timestamps + filename. A single `RequestId` enricher would make this trivial.
- **Suggested fix:** `UseSerilogRequestLogging` already attaches `RequestId`; add `Enrichers.FromLogContext()` and explicitly push correlation context inside long-running handlers (e.g. `using (LogContext.PushProperty("ReceiptFileId", id))`).

---

## Inventory of `TODO` / `FIXME` markers
None detected via `grep -E 'TODO|FIXME|HACK|XXX'` across `*.cs`, `*.ts`, `*.tsx`. The codebase relies on the issue tracker (or this document) for tracking debt rather than in-code markers — which is fine, but means once forgotten, a concern is genuinely forgotten.

---

*Concerns analysis: 2026-04-29*
