# External Integrations

**Analysis Date:** 2026-04-29

## External APIs

### Anthropic Claude API
- **Purpose:** AI-driven classification of receipt items into German tax-relevant categories
- **Client:** `Backend/src/TaxReader.Infrastructure/Services/ClaudeAiClassifier.cs`
- **Wired up:** `Backend/src/TaxReader.Infrastructure/DependencyInjection.cs:34-38` — `AddHttpClient<IAiClassifier, ClaudeAiClassifier>` with `BaseAddress = https://api.anthropic.com/`, 60-second timeout
- **Endpoint used:** `POST v1/messages` (Anthropic Messages API, version `2023-06-01`)
- **Model selection:** Configurable via `Anthropic__Model` env var; backend default is `claude-haiku-4-5` (`AnthropicOptions.cs`); docker-compose default is `claude-sonnet-4-5`
- **Auth header:** `x-api-key: <ANTHROPIC_API_KEY>` (env var; format `sk-ant-...`)
- **Request shape:** `{ model, max_tokens, system, messages: [{role, content}] }` with `JsonPropertyName` attributes for snake_case
- **Token budget per call:** `max(256, items * 100)` — sized for batch JSON output
- **Batching strategy:** Single `ClassifyBatchAsync(IReadOnlyList<string>)` call across all items in an upload batch (cross-receipt) — see `UploadReceiptFilesHandler.cs:159-180` for batching logic
- **Failure modes handled:**
  - HTTP non-2xx → throws `HttpRequestException` with friendly message extracted from `error.message`
  - Malformed JSON in response → falls back to all-Unknown classifications
  - Truncated batch (model returns fewer items than requested) → missing entries filled with `Category.Unknown`
- **Fallback:** When `ANTHROPIC_API_KEY` is missing (`IsConfigured == false`), all items left as `Category.Unknown` with reason `"AI-Klassifizierung nicht konfiguriert."` (see `AiOnlyClassificationService.cs:35-39`)
- **Cost accounting:** Token-system pre-charges `Anthropic__CostPerClassification` (default 1) per item before the call; refunds per-item Unknowns and full-batch on AI failure (`AiOnlyClassificationService.cs:54-75`)

## Databases

### PostgreSQL 17
- **Image:** `postgres:17-alpine` (`docker-compose.yml:3`)
- **Database name:** `belegpilot` (created by Postgres container)
- **Schema management:** EF Core 10 migrations in `Backend/src/TaxReader.Infrastructure/Migrations/`
- **Migrations to date** (chronological):
  - `20260406153622_InitialCreate` — base schema
  - `20260410153742_AddTokenSystem` — token economy tables
  - `20260412095923_AddAuthAndUserScoping` — User entity and per-user data scoping
  - `20260416105937_AddAutoConfirmThreshold` — `users.auto_confirm_threshold` column
  - `20260420055623_RemoveStoragePath` — drop persisted storage path; PDFs no longer kept on disk
  - `20260420091512_AddTokenTransactionUserFk` — FK from `token_transactions` to `users`
- **Naming convention:** `EFCore.NamingConventions` snake_case (`DependencyInjection.cs:22`) — entity `ReceiptFile` → table `receipt_files`
- **DbContext:** `Backend/src/TaxReader.Infrastructure/Data/AppDbContext.cs` — implements `IAppDbContext` so Application layer doesn't depend on EF directly
- **Configurations:** Per-entity `IEntityTypeConfiguration<T>` classes in `Backend/src/TaxReader.Infrastructure/Data/Configurations/` auto-applied via `ApplyConfigurationsFromAssembly` (`AppDbContext.cs:23`)
- **Connection string:** `ConnectionStrings__DefaultConnection` env var; format `Host=db;Port=5432;Database=belegpilot;Username=...;Password=...`
- **Auto-migration on boot:** Controlled by `RUN_MIGRATIONS=true` env var or `Development` environment (`Program.cs:137-149`)
- **Networking:** Postgres container exposes no host ports (`docker-compose.yml:17`) — internal Docker network only
- **Healthcheck:** `pg_isready` every 5s (`docker-compose.yml:12-16`); API `depends_on: db: condition: service_healthy`
- **Volume:** Named volume `postgres-data` mounted at `/var/lib/postgresql/data`

## Authentication & Identity

### JWT Bearer (Self-Issued)
- **Issuer:** `BelegPilot` (configurable via `Jwt__Issuer`)
- **Audience:** `BelegPilot` (configurable via `Jwt__Audience`)
- **Algorithm:** HMAC-SHA256 with shared secret (`Jwt__Secret`, ≥32 chars, `RandomNumberGenerator`-generated)
- **Access token claims:** `sub` (user GUID), `email`, `name` (display name), `jti` (random GUID)
- **Access token lifetime:** `Jwt__AccessTokenExpirationMinutes` (default 60)
- **Refresh token:** Random 64-byte base64 string stored on `users.refresh_token` with `users.refresh_token_expires_at` (`Backend/src/TaxReader.Infrastructure/Services/AuthService.cs:151-152`)
- **Refresh lifetime:** `Jwt__RefreshTokenExpirationDays` (default 30)
- **Clock skew:** `TimeSpan.FromSeconds(30)` (`Program.cs:56`)
- **Wired up:** `Program.cs:43-58` (`AddJwtBearer` with `TokenValidationParameters`)
- **Default authorization:** Every `/api/v1/*` route is protected (`Program.cs:153`); auth endpoints opt-out with `.AllowAnonymous()` inside `AuthEndpoints.cs`
- **CurrentUser abstraction:** `ICurrentUser` (`Backend/src/TaxReader.Application/Interfaces/ICurrentUser.cs`) reads `sub` claim from `IHttpContextAccessor`; implementation `Backend/src/TaxReader.Api/Services/CurrentUser.cs`

### Password Hashing
- **Library:** `BCrypt.Net-Next` 4.0.3 — used in `AuthService.cs:44` (`HashPassword`) and `:94` (`Verify`)
- **Default work factor:** library default (10) — not overridden

## Local OCR / PDF / Document Stack

### PdfPig (PDF text extraction)
- **Service:** `Backend/src/TaxReader.Infrastructure/Services/PdfPigTextExtractor.cs` — implements `IPdfTextExtractor`
- **Strategy:** Custom bounding-box-based line reconstruction (`ExtractPageText`): groups words by Y-coordinate within 3-point tolerance, sorts top-to-bottom + left-to-right
- **Lifetime:** Scoped (`DependencyInjection.cs:27`)

### Tesseract (image OCR)
- **Library:** `Tesseract` 5.2.0 — local, no API costs
- **Service:** `Backend/src/TaxReader.Infrastructure/Services/TesseractImageTextExtractor.cs` — implements `IImageTextExtractor`
- **Lifetime:** **Singleton** (`DependencyInjection.cs:45`) — `TesseractEngine` is expensive to construct (~10MB language data + LSTM init); reused across requests with internal locking (Tesseract not thread-safe)
- **Languages:** `deu+eng` (German + English)
- **Mode:** LSTM-only
- **Tessdata path:** Configurable via `Tesseract__DataPath` env var; production container ships `tesseract-ocr-deu` and `tesseract-ocr-eng` apt packages (`Backend/Dockerfile:12-18`)
- **Supported image MIME types:** `image/jpeg`, `image/png`, `image/webp` — gated in `UploadReceiptFilesHandler.cs:29-34`

### QuestPDF (export)
- **Library:** `QuestPDF` 2026.2.4 (Community license)
- **Service:** `Backend/src/TaxReader.Infrastructure/Services/PdfExportService.cs`
- **Purpose:** Generates German-localized PDF tax-summary export
- **Companion:** `CsvExportService.cs` produces CSV variant

## Reverse Proxy / Edge

### Caddy 2
- **Image:** `caddy:2-alpine` (`docker-compose.yml:58`)
- **Config:** `Caddyfile` at repo root (mounted read-only into container)
- **Domain:** `${DOMAIN}` env var (defaults to `localhost`); Caddy automatically provisions Let's Encrypt cert for non-localhost domains
- **Routing:** All traffic → `web:3000` (Next.js); Next.js rewrites `/api/v1/*` to `api:8080` server-to-server (see `Frontend/next.config.ts`)
- **Compression:** `zstd gzip`
- **Security headers:**
  - `Strict-Transport-Security: max-age=31536000; includeSubDomains`
  - `X-Content-Type-Options: nosniff`
  - `X-Frame-Options: DENY`
  - `Referrer-Policy: strict-origin-when-cross-origin`
- **Ports:** Only Caddy exposes ports — `80`, `443`, `443/udp` (HTTP/3) (`docker-compose.yml:63-66`)
- **Volumes:** `caddy-data` (certs), `caddy-config`

## Frontend → Backend Wiring
- **Path:** Frontend uses relative `baseURL: "/api/v1"` (`Frontend/src/lib/api-client.ts:17`)
- **Rewrite:** `Frontend/next.config.ts` rewrites `/api/v1/*` → `${BACKEND_API_URL}/api/v1/*` (server-side proxy)
- **Env var:** `BACKEND_API_URL` (default `http://localhost:5190` for dev; `http://api:8080` in compose)
- **CORS:** Backend allows `http://localhost:3000` in dev; `CORS_ALLOWED_ORIGINS` env var (comma-separated) for production
- **Auth flow:** Bearer JWT in `Authorization` header via Axios request interceptor; on 401 a single in-flight refresh attempt is shared across concurrent requests (`api-client.ts:41-73`)
- **Refresh token storage:** `localStorage.refreshToken` (browser-only); access token kept in module-scoped variable (not persisted)

## Webhooks / Background Jobs / Queues

**None.** All processing is synchronous within the upload request:
- Upload → extract → parse → classify (single AI call) → persist
- No message broker, no background worker, no scheduled jobs
- See `CONCERNS.md` for upload latency implications under large batches

## Logging Sinks

### Serilog
- **Sink:** Console only (`Program.cs:18-20`)
- **Configuration source:** `appsettings.json` / `appsettings.Development.json` via `ReadFrom.Configuration`
- **Request logging:** `app.UseSerilogRequestLogging()` (`Program.cs:122`)
- **No external log sink** (no Seq, no Loki, no Datadog) — production observability relies on `docker logs`

---

*External integrations analysis: 2026-04-29*
