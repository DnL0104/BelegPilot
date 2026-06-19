# External Integrations

**Analysis Date:** 2026-06-19

## APIs & External Services

**Anthropic AI Classification:**
- **Service:** Anthropic API (Claude model for receipt item classification)
- **What it's used for:** AI-powered categorization of receipt line items into German tax categories (13-category taxonomy)
- **SDK/Client:** `HttpClient` factory + custom JSON marshalling in `Backend/src/TaxReader.Infrastructure/Services/ClaudeAiClassifier.cs`
- **Base URL:** `https://api.anthropic.com/` (configured in `Backend/src/TaxReader.Infrastructure/DependencyInjection.cs` line 61)
- **Model:** `claude-haiku-4-5` (default, configurable via `Anthropic__Model` env var; specified in `Backend/src/TaxReader.Infrastructure/Configuration/AnthropicOptions.cs` line 10)
- **Auth:** Bearer token via `Anthropic__ApiKey` env var (required in production)
- **Timeout:** 60 seconds per request (configured in `Backend/src/TaxReader.Infrastructure/DependencyInjection.cs` line 62)
- **Token economy:** Each classification costs `CostPerClassification` tokens (default 1, configurable via `Anthropic__CostPerClassification` env var)
- **Configuration logging:** Resolved model and cost logged at startup (`Backend/src/TaxReader.Api/Program.cs` lines 278–283)

**Payment Processing (Stripe):**
- **Service:** Stripe payments platform
- **What it's used for:** User token (credit) purchases, subscription management, webhook handling for payment events
- **SDK/Client:** `Stripe.net 51.2.0` - Official Stripe .NET SDK
- **Implementation:** `Backend/src/TaxReader.Infrastructure/Services/StripePaymentProvider.cs` and `Backend/src/TaxReader.Infrastructure/Services/StripeWebhookHandler.cs`
- **Auth:** 
  - Secret key: `Stripe__SecretKey` env var (required; validated in `Backend/src/TaxReader.Infrastructure/Configuration/StripeOptionsValidator.cs`)
  - Publishable key: `Stripe__PublishableKey` env var (required; used by frontend)
  - Webhook secret: `Stripe__WebhookSecret` env var (required; validates incoming webhook signatures)
- **Configuration:**
  - `Stripe__DemoMode` (env var, default `false`) - Disables real Stripe calls for testing
  - `Stripe__AppBaseUrl` (env var, default `http://localhost:3000`) - Frontend URL for checkout/portal redirects
  - `Stripe__BusinessAddress` - German business address for invoices
  - `Stripe__KleinunternehmerNote` - German small business tax exemption note
  - `PricePacks` - Array of `(Credits, StripePriceId)` pairs for sale options (configured in `docker-compose.yml` via `Stripe__PricePacks` or via `StripeOptions` in code)
- **Security:** 
  - Validator prevents test keys in production (`Backend/src/TaxReader.Infrastructure/Configuration/StripeOptionsValidator.cs` line 41)
  - Startup logs warn if live key is used in non-production (`Backend/src/TaxReader.Api/Program.cs` lines 289–294)
- **Endpoints:**
  - `POST /payments/checkout-session` - Create Stripe checkout session
  - `POST /payments/portal-session` - Create Stripe customer portal session
  - `POST /payments/webhook` - Webhook ingestion (stripe events: payment success, refund, chargeback)
- **Entity Storage:** `Backend/src/TaxReader.Domain/Entities/Payment.cs` - Persists payment records locally (Stripe ID, amount, status)

## Data Storage

**Databases:**
- **PostgreSQL 17 Alpine** - Primary relational database
  - Connection: `ConnectionStrings__DefaultConnection` env var (format: `Host=<host>;Port=<port>;Database=<db>;Username=<user>;Password=<password>`)
  - Default connection in dev: `Host=localhost;Port=5432;Database=belegpilot;Username=postgres;Password=postgres`
  - Docker Compose production: `Host=db;Port=5432;Database=belegpilot;Username=${POSTGRES_USER:-postgres};Password=${POSTGRES_PASSWORD:-postgres}`
  - Client: **Entity Framework Core 10.0.4** with **Npgsql** provider
  - Schema: EF Core Code-First via migrations in `Backend/src/TaxReader.Infrastructure/Migrations/`
  - Naming convention: snake_case columns (via `EFCore.NamingConventions`)

**Background Job Storage:**
- **PostgreSQL (Hangfire.PostgreSql)** - Stores scheduled/completed jobs, queues, and job history
  - Same connection string as main DB
  - Schema: Auto-created by Hangfire (`PrepareSchemaIfNecessary: true` in `Backend/src/TaxReader.Infrastructure/DependencyInjection.cs` line 101)
  - Fallback: In-memory storage for tests (`Hangfire.MemoryStorage` when `Hangfire__UseInMemoryStorage=true`)

**File Storage:**
- **Local Filesystem** - Uploaded receipt files (PDFs, images)
  - Path: `UploadStorage__Path` env var (Docker default: `/var/lib/taxreader/uploads`)
  - Dev default: `Path.GetTempPath()/taxreader-uploads`
  - Implementation: `Backend/src/TaxReader.Infrastructure/Storage/FileSystemUploadBlobStore.cs`
  - Docker Compose volume: `taxreader_uploads:/var/lib/taxreader/uploads` (persists across container restarts)

**Caching:**
- **In-memory** - Application-level singleton stores (not external cache service):
  - Export token store: `Backend/src/TaxReader.Infrastructure/Services/ExportTokenStore.cs` (Singleton; lost on restart per `RESEARCH Pitfall 4`)
  - Tesseract engine pool: `Backend/src/TaxReader.Infrastructure/Services/TesseractEnginePool.cs` (Singleton, bounded channel)

## Authentication & Identity

**Auth Provider:**
- **Custom JWT** - In-app authentication (no external provider)
  - Implementation: `Backend/src/TaxReader.Infrastructure/Services/AuthService.cs` (register, login, refresh)
  - Token type: JWT Bearer tokens (access + refresh pair)
  - Signing: HS256 (HMAC-SHA256) with secret from `Jwt__Secret` env var
  - Issuer/Audience: `Jwt__Issuer` and `Jwt__Audience` env vars (both default to `"BelegPilot"`)
  - Access token expiry: `Jwt__AccessTokenExpirationMinutes` env var (default 60 min)
  - Refresh token expiry: `Jwt__RefreshTokenExpirationDays` env var (default 30 days)
  - Refresh token storage: PostgreSQL table `refresh_tokens` (salted + hashed with HMAC-SHA256 pepper from `RefreshToken__HashKey`)
  - Refresh token pepper: `RefreshToken__HashKey` env var - 32-byte Base64-encoded HMAC key; validated at startup in `Backend/src/TaxReader.Infrastructure/DependencyInjection.cs` line 46
  - Endpoints:
    - `POST /auth/register` - Create account + issue tokens + award 10 welcome tokens
    - `POST /auth/login` - Authenticate + issue tokens
    - `POST /auth/refresh` - Rotate token pair
    - `DELETE /auth/account` - Delete user + cascade all data

**Authorization:**
- **Single role model** - Authenticated vs. anonymous
  - Global enforcement: `/api/v1/*` requires bearer token (configured in `Backend/src/TaxReader.Api/Program.cs` line 153: `.RequireAuthorization()`)
  - Per-user data scoping: Handlers filter queries via `ICurrentUser.UserId` (injected from JWT `sub` claim)
  - Admin feature: Optional `Hangfire__SeedAdminEmails` env var; matched at startup to flip `IsAdmin=true` flag (configured in `Backend/src/TaxReader.Api/Program.cs` line 119)

## Monitoring & Observability

**Error Tracking:**
- **Sentry** - Error and performance monitoring
  - SDK: `Sentry ^6.4.1` (Core) + `Sentry.AspNetCore ^6.4.1` (backend); `@sentry/nextjs ^10.52.0` (frontend)
  - DSN Backend: `Sentry__Dsn` env var (empty DSN = no-op; configured in `Backend/src/TaxReader.Api/Program.cs` line 45)
  - DSN Frontend: `NEXT_PUBLIC_SENTRY_DSN` env var (only used if `NEXT_PUBLIC_SENTRY_ENABLED=true`)
  - Environment: Backend reads `Sentry__Environment` env var (falls back to `ASPNETCORE_ENVIRONMENT`); frontend reads `NEXT_PUBLIC_SENTRY_ENVIRONMENT` (if enabled)
  - PII handling: `SendDefaultPii = false` (backend); frontend uses `sentry-scrubber.ts` (path: `Frontend/src/lib/sentry-scrubber.ts`) to redact user data
  - Request body size: `MaxRequestBodySize = RequestSize.None` (no limit on captured bodies)
  - Scrubbing: Custom scrubber at `Backend/src/TaxReader.Infrastructure/Observability/SentryScrubbing.cs` removes sensitive data before transmission
  - Integration timing: Registered first in `Program.cs` (line 45) to catch DI-time exceptions
  - Frontend conditional wrapping: Sentry wrapped only when `NEXT_PUBLIC_SENTRY_ENABLED === "true"` (`Frontend/next.config.ts` line 49) to avoid build-time validation errors when env vars unset

**Logs:**
- **Serilog** - Structured logging to console
  - Configuration: `Backend/src/TaxReader.Api/appsettings.json` (log levels) + environment overrides via `Serilog__*` env vars
  - Sinks: Console output
  - Enrichers: `FromLogContext`, `WithEnvironmentName`
  - Named placeholders: All logs use `logger.LogWarning("Message {Placeholder}", value)` (structured, never interpolation)
  - Bootstrap logger: Created before host build (`Backend/src/TaxReader.Api/Program.cs` lines 29–31); final flush in `finally` block (line 169)
  - Configuration source: `UseSerilogRequestLogging()` middleware logs all HTTP requests

## CI/CD & Deployment

**Hosting:**
- **Docker Compose** - Self-hosted stack (`docker-compose.yml`)
- **Services exposed:**
  - Caddy (port 80, 443, 443/udp) - TLS termination, reverse proxy
  - PostgreSQL - Internal network only
  - .NET API - Internal network only (accessed via Caddy → Next.js rewrite)
  - Next.js - Internal network only (accessed via Caddy)

**Reverse Proxy:**
- **Caddy 2 Alpine** - TLS edge, security headers, compression
  - Config: `Caddyfile` (single block rewrites all traffic to Next.js on port 3000)
  - TLS: Automatic via Let's Encrypt (ACME) — `{$DOMAIN}` variable from env
  - Compression: zstd + gzip
  - Headers: HSTS, X-Content-Type-Options, X-Frame-Options, Referrer-Policy
  - Data volumes: `caddy-data:/data`, `caddy-config:/config` (persist certificates/config)

**CI Pipeline:**
- **Not detected** — No GitHub Actions, GitLab CI, or other CI service configured in repo
- **Local e2e testing:** Playwright (`Frontend/playwright.config.ts`) with Chrome Desktop; runs standalone or in CI with serial workers (auth rate limiter prevents parallelism)

## Environment Configuration

**Required env vars (Backend - Production):**
- `Jwt__Secret` - HS256 signing key (min 32 chars)
- `RefreshToken__HashKey` - 32-byte Base64 HMAC pepper
- `Anthropic__ApiKey` - Anthropic API key (sk-... format)
- `Stripe__SecretKey` - Stripe secret key (sk_live_... or sk_test_...)
- `Stripe__PublishableKey` - Stripe publishable key (pk_...)
- `Stripe__WebhookSecret` - Stripe webhook signing secret (whsec_...)
- `ConnectionStrings__DefaultConnection` - PostgreSQL connection string
- `POSTGRES_USER`, `POSTGRES_PASSWORD` - DB credentials (for docker-compose.yml)
- `DOMAIN` - Caddy domain (for HTTPS via Let's Encrypt)

**Optional env vars (Backend):**
- `CORS_ALLOWED_ORIGINS` - Comma-separated allowed origins (default: denies all in production if unset)
- `Sentry__Dsn` - Sentry error tracking DSN (empty = disabled)
- `Sentry__Environment` - Sentry environment name (fallback: `ASPNETCORE_ENVIRONMENT`)
- `Hangfire__SeedAdminEmails` - Comma-separated emails to promote to admin on startup
- `Tesseract__PoolSize` - OCR engine pool size (default 3)
- `Anthropic__Model` - Claude model override (default `claude-haiku-4-5`)
- `Anthropic__MaxTokens` - Max tokens per request (default 1024)
- `Anthropic__CostPerClassification` - Token cost per classification (default 1)
- `Stripe__DemoMode` - Disable real Stripe calls when `true` (default false)
- `Stripe__AppBaseUrl` - Frontend URL for Stripe redirects (default `http://localhost:3000`)
- `RUN_MIGRATIONS` - Auto-migrate DB on startup when `true` (recommended for containers)
- `UploadStorage__Path` - Receipt upload directory (docker-compose default: `/var/lib/taxreader/uploads`)

**Required env vars (Frontend - Build Time):**
- `BACKEND_API_URL` - Backend API base URL (default `http://localhost:5190`; Docker: `http://api:8080`)

**Optional env vars (Frontend - Build Time):**
- `NEXT_PUBLIC_SENTRY_ENABLED` - Enable Sentry error tracking (`true` to enable; default `false`)
- `NEXT_PUBLIC_SENTRY_DSN` - Sentry client-side DSN (only used if `NEXT_PUBLIC_SENTRY_ENABLED=true`)
- `SENTRY_ORG` - Sentry organization ID (only needed if Sentry enabled)
- `SENTRY_PROJECT` - Sentry project ID (only needed if Sentry enabled)

**Secrets location:**
- `.env` file at repo root (gitignored)
- Docker Compose reads from `.env` via interpolation (`${VAR_NAME:-default}` syntax)
- Kubernetes/cloud: Use native secret management (pass env vars at container runtime)
- Validation: `StripeOptionsValidator` and `RefreshTokenOptionsValidator` throw at startup if required secrets missing

## Webhooks & Callbacks

**Incoming:**
- **Stripe webhooks** - Stripe → Backend (`POST /payments/webhook`)
  - Endpoint: `Backend/src/TaxReader.Api/Endpoints/PaymentEndpoints.cs` - `MapPaymentEndpoints` extension
  - Handler: `Backend/src/TaxReader.Infrastructure/Services/StripeWebhookHandler.cs`
  - Signature validation: HMAC-SHA256 using `Stripe__WebhookSecret`
  - Events handled: `charge.succeeded`, `charge.refunded`, `charge.chargeback` (token grant/revoke jobs triggered)
  - Response: 200 OK on success, 400/401 on validation failure

**Outgoing:**
- **None configured**
- Note: Stripe payment events → internal jobs (grant/revoke tokens) via webhook ingestion only; no outbound webhooks to third parties

---

*Integration audit: 2026-06-19*
