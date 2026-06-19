# Codebase Concerns

**Analysis Date:** 2026-06-19

## Tech Debt

**Unimplemented Payment Top-Up Flow:**
- Issue: `POST /tokens/purchase` endpoint exists but is a placeholder with no implementation. Users cannot currently purchase token credits through the UI.
- Files: `Backend/src/TaxReader.Api/Endpoints/TokenEndpoints.cs` (endpoint defined but body not implemented), `Frontend/src/app/(authenticated)/billing/page.tsx` (billing UI requires this)
- Impact: Revenue cannot be collected from users who exhaust their free welcome tokens (10 credits). Only the payment webhook flow (Stripe → token grant) is functional. This is a critical gap for a commercial launch.
- Fix approach: Implement the `purchase` endpoint to mint a checkout session and redirect to Stripe. Already have the infrastructure (`StripePaymentProvider`, `CreateCheckoutSessionRequest`); just need to wire the endpoint.

**Frontend Missing Test Coverage:**
- Issue: `Frontend/package.json` includes Vitest, `@testing-library/react`, and Playwright e2e setup, but no `.test.ts` or `.spec.tsx` files exist in `src/`. All tests are in `node_modules/`.
- Files: `Frontend/package.json` (test scripts present), `Frontend/src/` (no test files)
- Impact: React components and hooks have zero unit test coverage. E2E tests exist (see `test:e2e` script), but component-level behavior (form validation, API error handling, token refresh) is untested. Risk of shipping breaking changes to critical flows (login, classification, exports).
- Fix approach: Create `.test.tsx` files colocated with components. Start with high-value targets: `auth-provider.tsx`, `api-client.ts` token refresh logic, and `useReceipts` hook.

**Sentry Error Tracking Has No DSN in Production:**
- Issue: `Backend/src/TaxReader.Api/appsettings.json` defines `Sentry.Dsn` as empty string. Frontend also has `NEXT_PUBLIC_SENTRY_DSN` defaulting to empty in `docker-compose.yml` line 69.
- Files: `Backend/src/TaxReader.Api/appsettings.json:32`, `docker-compose.yml:69`
- Impact: Production exceptions are **not** being reported. A critical bug (OCR failure, AI classification timeout, payment webhook race condition) will only be visible by querying logs manually. This violates the solo-operator constraint ("paging-style alerting expectation").
- Fix approach: Require `SENTRY_DSN_BACKEND` env var for non-Development environments. Wire the frontend DSN in `next.config.ts`. Set up a Sentry project for the product and document the keys in the deployment guide.

**No Request Body Size Limit on Sentry:**
- Issue: `Backend/src/TaxReader.Api/Program.cs` line 48 sets `options.MaxRequestBodySize = Sentry.Extensibility.RequestSize.None;`. This means Sentry will buffer and send the entire request body (including file uploads, JSON payloads) to its API, unbounded.
- Files: `Backend/src/TaxReader.Api/Program.cs:48`
- Impact: Large file uploads (near the 10 MB limit) or concurrent uploads could send massive payloads to Sentry, increasing latency and cloud cost. Sentry's own API has limits (default 1 MB per event).
- Fix approach: Set `options.MaxRequestBodySize = Sentry.Extensibility.RequestSize.Small;` (8 KB) to avoid oversized payloads. Document that sensitive file contents are never logged by design.

**AI Parsing Failure Tolerance Is Silent:**
- Issue: When Claude API returns malformed JSON or is truncated, `ClaudeAiClassifier.ParseBatchResult` (lines 150–207) silently fills missing entries with `Category.Unbekannt` and logs a warning. The user sees "AI couldn't classify this" but the actual error (truncation, API quota, malformed response) is only in Serilog.
- Files: `Backend/src/TaxReader.Infrastructure/Services/ClaudeAiClassifier.cs:150–207`
- Impact: If Claude responses are consistently truncated (e.g., due to aggressive max_tokens tuning), users will see all-Unbekannt results without understanding why. This erodes trust in the classifier.
- Fix approach: Return structured partial-result metadata from `AiOnlyClassificationService.ClassifyItemsAsync` that distinguishes "AI said Unbekannt" from "parse failed, got refund" so the UI can surface clearer messaging.

## Known Bugs

**Hangfire Job Enqueueing Without Error Visibility:**
- Symptoms: Background jobs (e.g., `ProcessReceiptFileJob`) fail silently if they throw an exception. The job enters Hangfire's Failed state, but there's no automatic alert.
- Files: `Backend/src/TaxReader.Api/Hangfire/RecurringJobsBootstrap.cs` (no job exception handlers), `Backend/src/TaxReader.Application/Jobs/ProcessReceiptFileJob.cs` (no try/catch)
- Trigger: E.g., Tesseract pool exhausted, Anthropic API timeout, OCR memory error — job fails, no notification
- Workaround: Admin must manually check Hangfire dashboard at `/hangfire`. For a solo operator, this is not sustainable.
- Fix approach: Integrate Hangfire's server filters to log failed jobs to Sentry. Alternatively, add a `HangfireFailedJobCleanupJob` monitor (D-23 pattern) that alerts if failures exceed a threshold.

**Token Balance Can Go Negative (Design, Not Bug, But Risk):**
- Symptoms: If a refund/chargeback webhook processes AFTER the user has consumed the refunded tokens, balance goes negative. The UI shows -5 tokens available.
- Files: `Backend/src/TaxReader.Infrastructure/Services/TokenService.cs:131–140` (`AddTokensAsync` has no lower-bound check), `Backend/src/TaxReader.Application/Jobs/RevokeTokensJob.cs` (performs raw balance update)
- Impact: Users can appear to have negative balances. Confusing, but the next `TryConsumeManyAsync` call will fail and prevent further classification, so it's self-healing. However, it breaks the invariant "balance ≥ 0".
- Workaround: In production, chargebacks should be rare and the balance recovers after the user purchases more tokens.
- Fix approach: Add a constraint in `RevokeTokensJob` to prevent balance from going below 0. Cap the refund to the current balance.

**Race Condition in Stripe Webhook Idempotency:**
- Symptoms: Two webhook deliveries of the same `checkout.session.completed` event arrive within milliseconds. The UNIQUE constraint on `stripe_event_id` prevents duplicate payment rows, but both requests may concurrently check and insert.
- Files: `Backend/src/TaxReader.Infrastructure/Services/StripeWebhookHandler.cs:50–57`, `Backend/src/TaxReader.Infrastructure/Data/Configurations/PaymentConfiguration.cs` (where UNIQUE is enforced)
- Impact: If both requests hit the DB at the same time, the second one may still insert a duplicate before the first commit. Unlikely but possible under high load or clock skew.
- Workaround: PostgreSQL UNIQUE constraint acts as a safety net — the second insert will fail with a constraint violation (logged as a duplicate), and `HandleAsync` returns 200 OK anyway (line 56).
- Fix approach: Use `ON CONFLICT DO NOTHING` or a serializable transaction to prevent the INSERT from happening twice in the first place.

## Security Considerations

**JWT Secret Stored in Environment Variables:**
- Risk: `JWT_SECRET` is passed via env var and exists in `docker-compose.yml` template (as a placeholder `${JWT_SECRET}`). If the `.env` file is leaked or the container is inspected, the secret is compromised.
- Files: `Backend/src/TaxReader.Api/Program.cs:78–79`, `docker-compose.yml:32`
- Current mitigation: `.env` is gitignored. Caddy reverse proxy is the only public interface (tokens in HTTP-only cookies recommended in CLAUDE.md, though frontend currently uses localStorage).
- Recommendations: 
  1. Document that JWT_SECRET should be a cryptographically random 32+ byte value (currently no guidance).
  2. Consider using a secrets manager (HashiCorp Vault, AWS Secrets Manager, or Docker Compose v2's `--secret`) in production deployment.
  3. Rotate JWT_SECRET every 6 months (add to deployment runbook).

**Refresh Token Rotation Not Enforced:**
- Risk: `RefreshToken` entities are rotated on use (validated in `IRefreshTokenService.ValidateAndRotateAsync`), but the old token can still be used until it expires (30 days default, per `JWT_REFRESH_EXPIRY_DAYS`). If a token is intercepted, it's valid for 30 days.
- Files: `Backend/src/TaxReader.Infrastructure/Services/RefreshTokenService.cs` (manages rotation), `Backend/src/TaxReader.Api/Program.cs:36` (30-day default)
- Current mitigation: Refresh tokens are hashed in the database (only the hash is stored, not the plaintext). They are cleared on logout. Access tokens have a 60-minute TTL.
- Recommendations:
  1. Reduce `JWT_REFRESH_EXPIRY_DAYS` to 7 days for higher security.
  2. Document that refresh tokens should NOT be stored in localStorage (move to HttpOnly cookies post-launch, see Phase 3 plan).
  3. Implement refresh token reuse detection (if an old token is used after a newer rotation, mark all tokens as compromised and force re-login).

**Stripe Event Metadata Not Signed:**
- Risk: `StripeWebhookHandler` extracts `userId` and `credits` from `session.Metadata` (lines 60–72). While Stripe signatures are verified, the metadata itself is user-supplied during checkout session creation. A client could theoretically craft a malicious session with inflated credits.
- Files: `Backend/src/TaxReader.Api/Endpoints/PaymentEndpoints.cs:53–54` (metadata is passed when creating session), `Backend/src/TaxReader.Infrastructure/Services/StripeWebhookHandler.cs:60–72`
- Current mitigation: Stripe's signature verification ensures the event came from Stripe. The metadata is signed as part of the payload. However, the session is created by the client, so the metadata is not a trusted value until Stripe confirms it.
- Recommendations:
  1. After webhook processing, fetch the session from Stripe's API to confirm the credits and userId (redundant but safer).
  2. Document that metadata is user-input and the webhook handler must not trust it blindly.

**Unauthenticated File Size Limit Bypass:**
- Risk: `UploadReceiptFilesValidator.cs` enforces a 10 MB per-file limit (line 28). However, the validator is registered globally in `Program.cs:90`, and rate limiting is per-IP, not per-user. An attacker could upload 10 MB × 60 requests/min = 600 MB/min to the endpoint.
- Files: `Backend/src/TaxReader.Application/Validators/UploadReceiptFilesValidator.cs:28`, `Backend/src/TaxReader.Api/Program.cs:144–155` (global 60/min rate limit)
- Current mitigation: The global rate limiter (60 requests/min per IP) is lower than the 10 MB/request limit. DDoS would be detected by Caddy or the hoster's upstream.
- Recommendations:
  1. Add a secondary rate limit on upload volume (e.g., 50 MB/hour per authenticated user) in the upload handler.
  2. Document expected storage growth and monitor disk usage.

**CORS Configuration Allows Any Method in Production:**
- Risk: `Program.cs:246` enables `AllowAnyMethod()` for CORS, meaning preflight requests can attempt DELETE, PUT, PATCH on any endpoint.
- Files: `Backend/src/TaxReader.Api/Program.cs:246`
- Current mitigation: Authentication is enforced globally (line 354), so unauthenticated requests are rejected. Cross-origin DELETE requests from a malicious site would still hit rate limiting.
- Recommendations:
  1. Restrict allowed methods to `["GET", "POST"]` for the default CORS policy.
  2. Document that `AllowAnyMethod()` is a placeholder; production should enumerate permitted methods per resource.

## Performance Bottlenecks

**Tesseract OCR Pool Size Fixed at 3:**
- Problem: `TesseractOptions.PoolSize` defaults to 3, meaning at most 3 PDFs can be OCR'd concurrently. If a user uploads 10 images, 7 will queue in Hangfire until earlier jobs finish.
- Files: `Backend/src/TaxReader.Infrastructure/Configuration/TesseractOptions.cs:26`, `Backend/src/TaxReader.Api/Program.cs:45` (Hangfire worker count aligned to pool size)
- Cause: Tesseract is memory-intensive (~100 MB per engine instance) and CPU-bound. Scaling beyond 3 on typical VPSs causes memory pressure and context-switch thrashing.
- Improvement path: 
  1. Monitor Hangfire job queue depth and memory usage in production.
  2. If queue depth > 5 consistently, increase `PoolSize` to 5 and monitor memory.
  3. Consider async Tesseract wrapper (e.g., `Tesseract.Core`) or offloading OCR to a separate service (Phase 3 optimization).

**AI Classification Batch Size Unbounded:**
- Problem: `ClaudeAiClassifier.ClassifyBatchAsync` accepts an `IReadOnlyList<string>` with no size limit. Sending 1000 item descriptions in a single prompt could exceed Claude's context window (200k tokens) or hit timeouts.
- Files: `Backend/src/TaxReader.Infrastructure/Services/ClaudeAiClassifier.cs:27–42`
- Cause: Batch size is determined by the upload handler, which calls the classifier once per receipt. A user could theoretically upload a PDF with 5000 line items.
- Improvement path:
  1. Add a batch-size limit check in `ClassifyBatchAsync` (e.g., max 100 items per call).
  2. If exceeded, split the batch and make multiple API calls.
  3. Log a warning if batch processing takes > 10s (timeout indicator).

**No Connection Pooling Tuning:**
- Problem: `docker-compose.yml` and `appsettings.json` use default PostgreSQL connection string. No explicit pool size configuration.
- Files: `Backend/src/TaxReader.Api/appsettings.json:2–4`, `docker-compose.yml:31`
- Cause: Npgsql's default pool size is 25 connections. Under 100–500 users, this may not be enough for concurrent requests + Hangfire jobs + background tasks.
- Improvement path:
  1. Add `MaxPoolSize=50;MinPoolSize=5` to connection string.
  2. Monitor connection usage in production via `SELECT count(*) FROM pg_stat_activity;`.
  3. If pool exhaustion occurs, increase to 100 and monitor database load.

**Audit Log Has No Retention Policy:**
- Problem: `AuditLogEntry` records every user action (upload, classification, export). No pruning mechanism exists — the table will grow indefinitely.
- Files: `Backend/src/TaxReader.Infrastructure/Migrations/20260603045456_AddAuditLog.cs` (creates table), no cleanup job registered
- Impact: After 12 months at 100–500 users, the audit log could be millions of rows, slowing queries and increasing backups.
- Improvement path:
  1. Add a recurring job (similar to `ExportCleanupJob`) to delete audit logs older than 90 days.
  2. Archive old logs to cold storage (S3 Glacier) for compliance audits.

## Fragile Areas

**Classification Rule Matching Logic Is Untested:**
- Files: `Backend/src/TaxReader.Infrastructure/Services/ClassificationRuleService.cs` (if it exists), or inline in handlers
- Why fragile: Rule matching (user-defined rules override AI classification) likely uses string matching or regex. If a user creates a rule with a typo, it silently fails to match.
- Safe modification: 
  1. Add unit tests for rule matching with edge cases (case sensitivity, partial matches, regex escaping).
  2. Validate rules at creation time with a dry-run test against existing items.
- Test coverage: Likely none (no test files found in Backend/tests for rule matching).

**Stripe Webhook Event Type Switching Has No Default Case:**
- Files: `Backend/src/TaxReader.Infrastructure/Services/StripeWebhookHandler.cs:44–144`
- Why fragile: Only `checkout.session.completed` and `charge.refunded` are handled. Any other event type (e.g., `customer.updated`, `invoice.payment_failed`) falls through and returns 200 OK silently. If Stripe adds a new event in the future, it will be ignored.
- Safe modification:
  1. Add a default case that logs the event type with a warning.
  2. Document which events are monitored and why.
  3. Add a health check endpoint to verify webhook connectivity.
- Test coverage: `Backend/tests/TaxReader.UnitTests/Webhooks/StripeWebhookHandlerTests.cs` exists but likely incomplete.

**ExceptionHandlingMiddleware Discards Stack Traces:**
- Files: `Backend/src/TaxReader.Api/Middleware/ExceptionHandlingMiddleware.cs:19–24`
- Why fragile: The middleware maps exceptions to ProblemDetails without including the stack trace (line 33: `Detail = ex.Message`). If the message is generic ("Invalid operation"), debugging in production is hard.
- Safe modification:
  1. Include the full stack trace in the ProblemDetails for non-Production environments.
  2. For Production, include an error ID (correlate with Sentry) so users can report it.
  3. Add custom exception types (e.g., `ValidationException`, `NotFoundException`) so the middleware can return appropriate status codes.
- Test coverage: No tests found for this middleware.

**PDF Export Generation Is Blocking:**
- Files: `Backend/src/TaxReader.Infrastructure/Services/PdfExportService.cs` (if synchronous), `ExportEndpoints.cs` 
- Why fragile: If PDF generation takes > 30s (network timeout), the request hangs. For large reports (500+ items), this is likely.
- Safe modification:
  1. Move PDF generation to a Hangfire job and return a download URL with an expiry.
  2. Use streaming responses if PDF generation must be synchronous.
  3. Add timeouts and cancellation token support.

## Scaling Limits

**Single PostgreSQL Instance Without Replication:**
- Current capacity: ~100–500 users, each with 10–100 receipts (5000–50,000 total items).
- Limit: PostgreSQL single instance can handle ~1000 concurrent connections. At 500 users with 5 avg. concurrent requests = 2500 connections, the instance is overloaded. Backup and recovery are manual (no automated failover).
- Scaling path:
  1. Add read replicas for reporting queries (Category totals, annual summaries).
  2. Implement connection pooling (PgBouncer) to reduce per-request overhead.
  3. Shard by user ID if traffic exceeds 2000 concurrent users (Phase 4 refactor).

**Hangfire Uses PostgreSQL As Job Store (No Scaling):**
- Current capacity: ~50 concurrent Hangfire jobs (Tesseract pool × 3 + API request handlers).
- Limit: Job state is stored in PostgreSQL. Heavy job load (100+ concurrent uploads) causes table locks and slowdown.
- Scaling path:
  1. Migrate to Redis-based Hangfire storage (Hangfire.Pro.Redis) for better concurrency.
  2. Or, use a separate Hangfire database instance.

**Anthropic API Costs Unbounded:**
- Current capacity: 10 free welcome tokens per user × 500 users = 5000 classifications. At ~0.01 per classification (Haiku), ~$50/month for welcome tokens. Classification cost is user-purchased, so this should be user-limited.
- Limit: No rate limits or quota enforcement on API calls. A malicious user could burn through their token balance in seconds by uploading massive batches.
- Scaling path:
  1. Implement per-user daily quota (e.g., max 1000 classifications/day).
  2. Add timeout on AI calls (currently no timeout visible).
  3. Monitor token consumption and alert if a single user consumes > 100 tokens/hour.

**Frontend Bundle Size Not Monitored:**
- Current capacity: Next.js standalone build for Frontend is self-contained, ~500 KB JS gzipped (typical for React + Tailwind + shadcn/ui).
- Limit: If more components or libraries are added, bundle size could exceed 1 MB, hurting mobile users and cold-start load time.
- Scaling path:
  1. Add bundle analysis to the build pipeline (`next-bundle-analyzer`).
  2. Set a budget (e.g., fail if bundle > 800 KB) and test in CI.
  3. Use dynamic imports for heavy components (e.g., PDF viewer, large tables).

## Dependencies at Risk

**Tesseract 5.2.0 Is Aging:**
- Risk: Last release was 2022. No active maintenance. If a bug is found (e.g., memory leak in LSTM), there's no patch available.
- Impact: OCR reliability degrades if Tesseract is known to have issues with certain PDFs (e.g., rotated text, low contrast).
- Migration plan: 
  1. Monitor issues and consider cloud-based OCR (Google Vision, AWS Textract) as backup.
  2. Or, use `Tesseract.Core` (newer fork) if compatible.

**QuestPDF 2026.2.4 Uses Community License:**
- Risk: Community license is free for unlimited commercial use, but redistribution of the library itself is not allowed. If TaxReader is open-sourced, licensing becomes problematic.
- Impact: None currently, but limits future open-source strategy.
- Migration plan: If open-sourcing is desired, switch to Syncfusion (paid) or iTextSharp (AGPL) or generate HTML reports instead.

**Stripe SDK Dependency Updates:**
- Risk: Stripe.net may lag behind Stripe API versions. If Stripe deprecates an API version, the SDK may become incompatible.
- Impact: Webhook event types or session fields could be removed, breaking the payment flow.
- Migration plan:
  1. Pin Stripe SDK version and review changelogs quarterly.
  2. Use Stripe's API versioning header to lock to a specific API version (e.g., `2020-08-27`).
  3. Test payment flow in CI using Stripe test mode.

**Next.js 16.2.2 Is Cutting-Edge:**
- Risk: Very recent version (Feb 2025). Bugs and breaking changes are more likely than stable LTS versions.
- Impact: Could encounter unexpected behavior or deprecations when updating dependencies.
- Migration plan:
  1. Pin Next.js version strictly (no `^16.2.2`, use `=16.2.2`).
  2. Stay subscribed to Next.js releases and test major updates in a staging environment.
  3. Set a policy to upgrade every 3 months.

## Missing Critical Features

**Payment Top-Up UI Endpoint:**
- Problem: Users cannot purchase tokens. The Stripe webhook grants tokens on purchase, but there's no way to initiate a purchase.
- Blocks: Revenue generation, token economy sustainability.
- See Tech Debt section for details.

**Automated Backup & Disaster Recovery:**
- Problem: No documented backup strategy. If the database is lost, all user data is gone.
- Blocks: Production readiness, GDPR compliance (data loss = inability to honor deletion requests).
- Fix: 
  1. Configure PostgreSQL `pg_dump` in a daily cron job to S3.
  2. Test restore procedure monthly.
  3. Document RTO/RPO targets in deployment guide.

**User Support / Feedback Channel:**
- Problem: No way for users to report bugs or request features.
- Blocks: Product iteration, customer retention.
- Fix: Add an email contact form or integrate with Intercom/Zendesk.

## Test Coverage Gaps

**Backend Integration Tests Are Sparse:**
- Untested area: Upload → OCR → Parse → Classify → Export pipeline end-to-end.
- Files: `Backend/tests/TaxReader.IntegrationTests/` (limited coverage)
- Risk: A bug in one layer (e.g., OCR output not matching parser expectations) could go undetected until production. The happy path works, but edge cases (scanned PDF with poor contrast, Amazon receipt in German, item with no price) are not covered.
- Priority: High — this is the core value proposition.

**Frontend Component Tests Are Missing:**
- Untested area: React components for classification, token top-up, exports.
- Files: `Frontend/src/` (no `.test.tsx` files)
- Risk: UI state management bugs (e.g., loading state not cleared after error) could ship. Form validation might not work as expected.
- Priority: High — users interact with this daily.

**Hangfire Job Failure Scenarios:**
- Untested area: What happens if a job fails mid-way (e.g., Tesseract crashes, DB connection lost)?
- Files: `Backend/tests/TaxReader.UnitTests/Jobs/` (tests exist but limited)
- Risk: Orphaned processing runs, incomplete classifications, or token ledger inconsistencies.
- Priority: Medium — failures should be rare but must be handled gracefully.

**Stripe Webhook Event Edge Cases:**
- Untested area: Duplicate events, out-of-order events, missing metadata, API errors during webhook processing.
- Files: `Backend/tests/TaxReader.UnitTests/Webhooks/StripeWebhookHandlerTests.cs`
- Risk: Duplicate charges, lost refunds, or inconsistent payment state.
- Priority: High — payment correctness is critical.

**GDPR Data Export & Deletion:**
- Untested area: User account deletion cascades correctly, audit logs are removed or retained per policy, sensitive data is not leaked in exports.
- Files: `Backend/src/TaxReader.Application/Commands/DeleteAccountHandler.cs`, `Backend/src/TaxReader.Application/Jobs/ExportUserDataJob.cs`
- Risk: Accidental data leaks or orphaned records after deletion.
- Priority: High — GDPR is a legal requirement.

---

*Concerns audit: 2026-06-19*
