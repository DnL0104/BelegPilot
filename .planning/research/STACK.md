# Stack Research

**Domain:** DE B2C tax-receipt SaaS — hardening additions to existing .NET 10 + Next.js 16 stack
**Researched:** 2026-05-03
**Confidence:** HIGH for picks tied to existing stack constraints; MEDIUM-HIGH for payment provider (Stripe well-known; DE-market specifics evolving)

> **Scope note:** This document covers ADDITIVE choices for the hardening milestone. The existing stack (.NET 10, EF Core, PostgreSQL 17, Next.js 16, shadcn/ui, Caddy, Anthropic, Tesseract) is already documented in `.planning/codebase/STACK.md` and is **not under reconsideration**. Every recommendation below is something we install on top.

---

## Recommended Stack — Additions

### Payments

| Technology | Version | Purpose | Why Recommended |
|------------|---------|---------|-----------------|
| **Stripe** | API `2025-10-29.acacia` (or current) | Payment processing, subscription/one-time, invoicing | Best-documented for .NET; full SEPA support; Stripe Tax handles DE VAT collection; mature webhook patterns; ~300-500 LOC for checkout + webhooks + invoicing |
| **Stripe.net** | latest 47.x | .NET SDK | Official, actively maintained, ASP.NET Core middleware patterns documented |
| **Stripe Tax** | service add-on | DE VAT registration + compliant tax line on invoices | Avoids hand-rolling UStG / OSS registration logic |
| **Stripe Invoicing** | included | Generates DE-compliant Rechnungen (PDF) for purchases | Required by §14 UStG for B2B; expected by B2C buyers; saves building a PDF-invoice generator |

**Why Stripe over alternatives (for THIS product):**
- The user is a solo dev with a 3-month deadline. Mollie is cheaper on DE-only methods (Sofort, giropay) but has a smaller .NET community, fewer English docs, and a less-mature invoicing/tax stack — meaning more glue code.
- SumUp is in-person + simple online; not built for SaaS subscription/credit-pack flows.
- SEPA-direct (own provider, own DD mandates) is technically possible but requires hand-rolled invoicing, dunning, refund handling — easily 2-3 weeks of work for a solo dev to do badly.
- The existing token-economy ledger is a perfect fit for Stripe's webhook-driven `payment_intent.succeeded` → grant-tokens pattern.

**Key gotchas:**
- Stripe Tax requires registering for VAT collection in DE; small-vendor exemption (Kleinunternehmer §19 UStG) is not supported by Stripe Tax — if user qualifies as Kleinunternehmer they handle invoices manually with a "no VAT shown" line.
- Webhook signature verification is non-negotiable (use `Stripe.EventUtility.ConstructEvent`); replay protection via idempotency keys.
- Test mode and live mode use different keys — environment separation matters (see ARCHITECTURE).
- Refunds must reverse token grants atomically — design the ledger to support negative entries (the existing `TokenTransaction` is well-suited).

### Error Tracking & Paging

| Technology | Version | Purpose | Why Recommended |
|------------|---------|---------|-----------------|
| **Sentry** | SaaS + `Sentry.AspNetCore` 5.x, `@sentry/nextjs` 9.x | Error tracking, performance monitoring, paging via integrations | Free tier covers 5k errors/mo (sufficient at hundreds-of-users scale); EU data residency available (must opt-in at signup); first-class .NET + Next.js SDKs; integrates with PagerDuty, Slack, Telegram, email |
| **GlitchTip** (alt) | 5.x self-hosted | Drop-in Sentry-compatible | Self-host on the existing Caddy stack if Sentry SaaS GDPR posture is unacceptable; same SDK works |

**Why Sentry over alternatives:**
- BetterStack Logs/Sentry-equivalent is excellent but newer with smaller .NET community.
- Self-hosted GlitchTip is a viable Plan B but adds an extra service to ops; defer unless legal review insists.
- Both Sentry + GlitchTip offer Source Map upload for Next.js stack-traces.

**Key gotchas:**
- Configure `SendDefaultPii = false` on the .NET SDK and scrub user data in `BeforeSend` — receipts contain PII.
- Disable session-replay on Next.js side, or it will record receipt-list pages with PII.

### Uptime Monitoring

| Technology | Purpose | Notes |
|------------|---------|-------|
| **BetterStack Uptime** | External health checks + status page | Free tier: 10 monitors, 3-min interval — enough for `/health`, `/api/v1/health`, plus 8 more if needed; DE/EU vantage points; status page included; integrates to Telegram/email/Slack |
| **/health endpoint** | Internal — already implicit in ASP.NET Core | Add explicit `/health` (DB ping) + `/health/ready` (DB + Anthropic config valid) |

**Why BetterStack over UptimeRobot:**
- UptimeRobot's free tier is 5-min, BetterStack is 3-min.
- Status page on BetterStack is included; UptimeRobot charges.
- BetterStack's incident timeline UX is better for a solo dev.

### Background Jobs (.NET 10)

| Technology | Version | Purpose | Why Recommended |
|------------|---------|---------|-----------------|
| **Hangfire** | 1.8.x | Background-job orchestration, persistent queue, dashboard | Single-process Docker compose deploy; Postgres-backed (no Redis needed); built-in retry, scheduled jobs, dashboard at `/hangfire` for solo-dev troubleshooting; widely used in .NET community |
| **Hangfire.PostgreSql** | 1.20.x | PG storage adapter | Reuses the existing Postgres instance |

**Why Hangfire over alternatives:**
- `Channel<T>` + `IHostedService` (in-process queue): zero persistence — a job lost if the container restarts mid-job. Acceptable only for fire-and-forget low-stakes work; receipt processing crosses that line because users paid tokens for it.
- Quartz.NET: excellent but verbose; no built-in dashboard; larger learning curve for the same outcome.
- Hangfire is the pragmatic pick. The dashboard alone is worth the integration effort for a solo dev who needs to debug at 3am without redeploying.

**Key gotchas:**
- `BackgroundJob.Enqueue` runs jobs as `JobActivator`-resolved instances — DI scope semantics are different from HTTP requests; use `IServiceScopeFactory` if you need a `IAppDbContext` per job.
- The Hangfire dashboard endpoint must be auth-gated (`[Authorize]` on `app.UseHangfireDashboard("/hangfire", new DashboardOptions { Authorization = ... })`); never expose it.
- Migrations: Hangfire creates its own schema (`hangfire`) — separate from the app's `public` schema; both must be migrated on container start.

### Rate Limiting

| Technology | Version | Purpose | Why Recommended |
|------------|---------|---------|-----------------|
| **ASP.NET Core built-in `AddRateLimiter`** | .NET 10 | Per-IP, per-user rate limits on auth + upload endpoints | Built-in to the framework, no extra dependency, supports fixed-window / sliding-window / token-bucket / concurrency policies; sufficient for hundreds-of-users scale |

**Why built-in over `AspNetCoreRateLimit`:**
- The package was created before ASP.NET Core had a built-in rate limiter; the built-in one supersedes it for new code.
- One less dependency to track for security updates.

**Recommended policies:**
- `/auth/login`, `/auth/register`: fixed-window, 5 req/min per IP.
- `/auth/refresh`: fixed-window, 30 req/min per user.
- `/receipt-files` (upload): concurrency limiter, 2 in-flight per user.
- Global: 60 req/min per IP across all endpoints (defense in depth).

### Frontend Testing

| Technology | Version | Purpose | Why Recommended |
|------------|---------|---------|-----------------|
| **Vitest** | 3.x | Unit & integration test runner | Fast, Vite-native; works seamlessly with Next.js 16 + React 19; same API surface as Jest, fewer config headaches |
| **@testing-library/react** | 16.x | DOM-level component testing | Standard pairing with Vitest; encourages user-behavior tests over implementation details |
| **@testing-library/user-event** | 14.x | Realistic user interaction simulation | Required for `userEvent.type()`, `.click()`, `.upload()` semantics |
| **MSW (Mock Service Worker)** | 2.x | Mock TanStack Query API calls | Standard pairing for testing components that hit `/api/v1/*` |
| **Playwright** | 1.50.x | E2E browser tests | DE/EN locale tests, multi-browser, single tool covers Chromium/Firefox/Webkit; better DX than Cypress for this stack |

**Why Vitest over Jest:**
- Faster on cold start.
- Native ESM (Next.js 16 / React 19 increasingly assume ESM).
- Identical assertion API (`expect()` works the same), so RTL / MSW examples translate 1:1.

**Why Playwright over Cypress:**
- Tests cross-browser without extra licensing.
- First-class TypeScript.
- Trace viewer for solo-dev debugging is best-in-class.
- The CI step is `npx playwright install --with-deps` once and you have headless Chromium/Firefox/Webkit.

**Test coverage targets (interim, not blocking):**
- Vitest unit: hooks (auth, upload state), utility functions.
- Vitest component: forms (login, register, upload), classification confirm/override flow.
- Playwright E2E: register → login → upload-receipt → see-classification → confirm → see-report flow as one happy-path test.

### PostgreSQL Integration Tests

| Technology | Version | Purpose | Why Recommended |
|------------|---------|---------|-----------------|
| **Testcontainers.PostgreSql** | 4.x | Spin up real Postgres 17 in Docker for tests | Catches FK/cascade/snake-case-naming bugs that EF in-memory misses |
| **Respawn** | 6.x | Reset DB between tests without re-creating | Faster than starting a fresh container per test |

**Why this pairing:**
- The current test suite uses `UseInMemoryDatabase` which doesn't enforce FKs and silently passes tests that would fail in production (concern #15).
- Testcontainers + Respawn is the standard .NET pairing — TC for the container, Respawn for fast cleanup between tests.
- One Postgres container per test class keeps run-time reasonable.

**Key gotchas:**
- CI must have Docker available (GitHub-hosted runners do; self-hosted may not).
- Containers are slow to start (~3-5s) — use `[Collection]` to share a container across a test class.

### Logging & Correlation

| Technology | Version | Purpose | Why Recommended |
|------------|---------|---------|-----------------|
| **Serilog** | already in stack | Structured logging | Already configured (`Serilog.AspNetCore`); just enrich |
| **Serilog.Enrichers.Environment** | 3.x | Add machine name, environment | Helps when correlating between dev/stage/prod logs |
| **Serilog.Enrichers.CorrelationId** | 3.x | RequestId already added by `UseSerilogRequestLogging`; this adds W3C trace correlation | Lets a request traversing API → background job stay correlated |
| **OpenTelemetry .NET** | 1.10.x | Tracing across HTTP + DB + Anthropic + Hangfire | Optional but cheap; if Sentry's tracing isn't enough, OTel exports to free Grafana Tempo / Sentry Performance |

**Recommendation:**
- Phase 1: Just Serilog enrichers + `LogContext.PushProperty("ReceiptFileId", id)` inside long-running handlers (concern #20).
- Phase 2 (only if needed): Add OpenTelemetry traces for cross-service correlation.

### OCR Scaling

| Approach | Verdict |
|----------|---------|
| **Pool of `TesseractEngine` instances** | ✓ Recommended for hundreds-of-users scale |
| **Switch to AWS Textract / Google Vision** | ✗ Don't — adds US data residency, GDPR posture cost, per-call billing |
| **PaddleOCR self-hosted** | ✗ Don't yet — adds Python dep + container; revisit only if Tesseract accuracy on real receipts proves insufficient |

**Recommended pattern:**
- Replace the Singleton `TesseractImageTextExtractor` with a pool of 3-5 `TesseractEngine` instances behind a `Channel<TesseractEngine>` rented per OCR call.
- Combined with background-job processing (off-request), this eliminates the upload-blocking scenario.
- Document the pool size as configurable; tune at deploy time based on observed CPU.

---

## Installation Summary

```bash
# Backend (.NET) — add to Backend/Directory.Packages.props
# Stripe.net 47.0.0
# Hangfire 1.8.18
# Hangfire.AspNetCore 1.8.18
# Hangfire.PostgreSql 1.20.10
# Sentry.AspNetCore 5.4.0
# OpenTelemetry.Extensions.Hosting 1.10.0   (optional)
# Serilog.Enrichers.Environment 3.0.1
# Testcontainers.PostgreSql 4.1.0           (test-only)
# Respawn 6.2.1                              (test-only)

# Frontend
cd Frontend
npm install -D vitest @vitest/ui @testing-library/react @testing-library/user-event @testing-library/jest-dom jsdom
npm install -D msw
npm install -D @playwright/test
npm install @sentry/nextjs
```

---

## Alternatives Considered

| Recommended | Alternative | When to Use Alternative |
|-------------|-------------|-------------------------|
| Stripe | Mollie | If DE-only methods (Sofort, giropay) dominate buyer intent and the integration cost is acceptable; revisit at v2 |
| Stripe | SEPA-direct (no provider) | Never for this product — no hand-rolled invoicing, dunning, chargebacks |
| Sentry | GlitchTip self-hosted | If legal review demands all error data stay in self-hosted infra |
| BetterStack Uptime | UptimeRobot | If status page is unnecessary and 5-min interval is fine |
| Hangfire | Quartz.NET | If recurring-cron-style scheduling dominates and the dashboard isn't needed |
| Hangfire | Channel<T> + IHostedService | Only for low-stakes fire-and-forget; never for paid work |
| Built-in RateLimiter | AspNetCoreRateLimit | Never for new code |
| Vitest | Jest | Existing Jest project being migrated |
| Playwright | Cypress | Team already invested in Cypress |
| Testcontainers | Raw Docker via Compose-in-test | Highly customized DB setup |
| TesseractEngine pool | AWS Textract | If accuracy on hand-scanned receipts becomes a quality blocker |
| TesseractEngine pool | Google Cloud Vision OCR | Same — accuracy escape hatch only |

---

## What NOT to Use

| Avoid | Why | Use Instead |
|-------|-----|-------------|
| `Channel<T>`-only background jobs | Lost on restart; users paid tokens | Hangfire (Postgres-backed) |
| EF in-memory provider for integration tests | Doesn't enforce FK / cascade / naming | Testcontainers.PostgreSql |
| `AspNetCoreRateLimit` package | Superseded by built-in (.NET 7+) | `AddRateLimiter` |
| Direct SEPA mandate handling | Hand-rolled invoicing, dunning, chargebacks | Stripe with SEPA Direct Debit |
| Cypress (for new projects) | Single-browser tier on free; lock-in | Playwright |
| `console.log` for backend errors | Already replaced by Serilog | Sentry + Serilog |
| Self-hosted Anthropic-equivalent | No serious OSS alternative on classification quality | Stay on Anthropic + plan for Art. 28 AVV |

---

## Stack Patterns by Variant

**If buying-volume of DE-only methods (Sofort/giropay) > 50% of intent:**
- Add Mollie alongside Stripe; Stripe for cards/SEPA, Mollie for local methods
- Adds ~2 weeks of dual-provider plumbing — only do this after launch when data is in

**If Kleinunternehmer (§19 UStG) status applies:**
- Don't enable Stripe Tax; instead emit invoices with the "Kleinunternehmer — keine Umsatzsteuer ausgewiesen" footer
- Switch to Stripe Tax automatically when revenue threshold crossed (€22k/year, currently €25k from 2024)

**If GDPR posture demands self-hosted error tracking:**
- Use GlitchTip on the existing Caddy stack instead of Sentry SaaS
- Adds one Docker service, ~256MB RAM, separate Postgres DB

---

## Version Compatibility

| Package A | Compatible With | Notes |
|-----------|-----------------|-------|
| Stripe.net 47 | .NET 10 | First-class .NET 10 support since 47.x |
| Hangfire 1.8 | .NET 10 + EF Core 10 + Npgsql 10 | Use Hangfire.PostgreSql 1.20+ for Npgsql 10 compat |
| Sentry.AspNetCore 5 | .NET 10 | Configure `Hub.UseDefault = true` to share scope across DI |
| Vitest 3 | Next.js 16 + React 19 | Use `@vitejs/plugin-react` 5.x for React 19 |
| Playwright 1.50 | Next.js 16 standalone | Run against the standalone server, not `next dev` |
| Testcontainers 4 | EF Core 10 migrations | Apply migrations programmatically per test fixture |

---

## Sources

- Stripe Docs — `stripe.com/docs/payments/sepa-debit`, `stripe.com/docs/tax`, `stripe.com/docs/api/idempotent_requests`
- Hangfire Docs — `docs.hangfire.io/en/latest/`
- ASP.NET Core 10 Rate Limiting — `learn.microsoft.com/aspnet/core/performance/rate-limit`
- Sentry .NET Docs — `docs.sentry.io/platforms/dotnet/`
- Testcontainers for .NET — `dotnet.testcontainers.org/modules/postgres/`
- Existing project: `.planning/codebase/STACK.md` (existing stack constraints), `.planning/codebase/CONCERNS.md` (gaps each addition closes)

---
*Stack research for: TaxReader hardening milestone (DE commercial launch)*
*Researched: 2026-05-03*
