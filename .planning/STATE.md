---
gsd_state_version: 1.0
milestone: v1.0
milestone_name: milestone
status: executing
stopped_at: Phase 5 complete — UAT approved 2026-05-31. Ready for Phase 6.
last_updated: "2026-05-31T00:00:00.000Z"
last_activity: 2026-05-31 -- Phase 5 complete. All 4 plans executed (05-01 through 05-04). Stripe DemoMode smoke test passed (checkout flow, transaction history). PAY-01 through PAY-06 verified. 2 code review criticals fixed (invoice double-divide, consent passthrough).
progress:
  total_phases: 7
  completed_phases: 5
  total_plans: 19
  completed_plans: 19
  percent: 100
---

# Project State

## Project Reference

See: .planning/PROJECT.md (updated 2026-05-03)

**Core value:** Trustworthy classification — every line item correctly categorized into the right tax category, with reasoning the user can audit and override.
**Current focus:** Phase 06 — legal-consent-data-export

## Current Position

Phase: 03 (background-pipeline-tesseract-pool) — EXECUTING
Plan: 4 plans planned, plan-checker PASS (iteration 2), VERIFICATION.md drafted (Nyquist scaffolds in Plan 03-01 T1)
Status: Phase 03 COMPLETE — all 4 plans executed (03-01, 03-03, 03-02, 03-04)
Last activity: 2026-05-22 -- Phase 3 complete. 217 backend tests passing. Frontend build green. UAT checklist in 03-HUMAN-UAT.md pending manual operator sign-off.

Progress: ██████████ 100% of Phase 1 + Phase 2 + Phase 3

### Wave map

**Phase 1 (complete):**

- Wave 1: 01-01 (Hygiene + Anthropic alignment + CORS deny-all) — no deps — DONE
- Wave 2: 01-04 (Serilog enrichers + LogContext) — depends on 01-01 — DONE
- Wave 3: 01-03 (Sentry .NET + Next.js, EU residency, PII scrubbing) — depends on 01-01, 01-04 — DONE
- Wave 4: 01-02 (CI workflow + README) — depends on 01-01, 01-03, 01-04 — DONE

**Phase 2 (complete):**

- Wave 1: 02-01 (refresh_tokens table + RefreshTokenService — AUTH-01) — no deps — DONE
- Wave 2: 02-03 (AddRateLimiter + ForwardedHeaders — AUTH-03) — depends on 02-01 — DONE
- Wave 2: 02-02 (account-deletion password re-auth — AUTH-02) — depends on 02-01, 02-03 — DONE

**Phase 3 (planned, ready to execute):**

- Wave 1: 03-01 (Hangfire installation + dashboard auth + recurring jobs — PIPE-01) — no deps
- Wave 1: 03-03 (TesseractEnginePool + warmup — PIPE-04) — no deps (parallel with 03-01)
- Wave 2: 03-02 (ProcessReceiptFileJob + ClassifyBatchJob + 202 Accepted + status + cancel endpoints — PIPE-02, PIPE-03) — depends on 03-01, 03-03
- Wave 3: 03-04 (UploadErrorCatalog + frontend status hooks + 5-page UI polish — PIPE-05, PIPE-06) — depends on 03-02 — DONE

## Performance Metrics

**Velocity:**

- Total plans completed: 11
- Average duration: 22 min (skewed by 01-02's 74-min resumption-agent wall-clock; non-resumption avg = 11 min)
- Total execution time: 158 min

**By Phase:**

| Phase | Plans | Total  | Avg/Plan |
|-------|-------|--------|----------|
| 1 | 4 | - | - |
| 2 | 3 | 63 min | 21 min |

**Recent Trend:**

- Last 5 plans: 02-02 (30 min, 5 tasks, 9 files — fresh execution; 2 deviations including a Rule-1 WAF host-startup regression caused by Minimal API DELETE body-binding without [FromBody]), 02-03 (18 min, 4 tasks, 12 files — fresh execution; 3 Rule-1 auto-fixes), 02-01 (15 min, 6 tasks, 23 files — fresh execution), 01-02 (74 min, 2 tasks, 2 files — resumption agent), 01-03 (11 min, 2 tasks, 17 files)
- Trend: 02-02 spent most wall-clock time investigating the [FromBody] regression that broke every WAF-using test in the suite (CorsConfigurationTests + 5 RateLimiting test classes). Root-cause isolation by piecewise revert; one-attribute fix. Production code unaffected.

*Updated after each plan completion*

## Accumulated Context

### Decisions

Decisions are logged in PROJECT.md Key Decisions table.

Recent decisions affecting current work:

- Init: Audience broadened from teachers to "Anyone DE"
- Init: Output bar = PDF/CSV summary for manual transcription (no ELSTER/ERiC)
- Init: Stripe selected as payment provider (deferred research)
- Init: Hangfire chosen over Channel<T> for background-job pipeline
- Init: Rule + AI hybrid classification (load-bearing for Core Value)
- 01-01: AnthropicOptions.cs is the source of truth for the model default; compose + env + CLAUDE.md mirror it; startup-log canary surfaces drift
- 01-01: CORS non-Dev + unset CORS_ALLOWED_ORIGINS = empty Origins (deny-all) + Serilog warning
- 01-01: Backend/.dockerignore added to prevent leaked PDFs from re-entering the runtime image via COPY . .
- 01-01: WebApplicationFactory<Program> integration-test pattern established (test project now references TaxReader.Api)
- 01-04: Serilog enrichers wired via appsettings.json (FromLogContext + WithEnvironmentName); appsettings.Development.json deliberately unchanged (array-merge avoidance)
- 01-04: LogContext.PushProperty correlation scope established at long-running-handler boundary (UploadReceiptFilesHandler per-file block); JobId variant deferred to Phase 3 Hangfire boundary per D-18
- 01-04: Serilog.Enrichers.Environment owned by TaxReader.Api project; test project receives it transitively via existing ProjectReference
- 01-04: Source-level structural-grep test pattern (File.ReadAllText + Should().Contain) added as a load-bearing wiring guard for cross-cutting invariants no runtime test can express cleanly
- 01-03: Sentry SDK init is the FIRST builder.WebHost registration after CreateBuilder (Pitfall 1 — DI-time exceptions reach Sentry); SetBeforeSend (not deprecated BeforeSend) wired to SentryScrubbing.Scrub
- 01-03: PII scrubber (D-14) lives at Backend/src/TaxReader.Infrastructure/Observability/SentryScrubbing.cs (not Api/) — matches "Infrastructure implements external concerns" architectural rule + OcrTextNormalizer.cs analog; cost is one PackageReference + one FrameworkReference on the Infrastructure csproj
- 01-03: AllowedExtraKeys allow-list ({receipt_id, processing_run_id, request_id, job_id, phase}) actively wipes Extra keys not in the set — defence-in-depth so future Sentry.SetExtra("vendor", ...) cannot leak receipt content (D-14 #6 active enforcement, not just call-site contract)
- 01-03: Frontend Sentry stays OFF in Phase 1 (D-16) — instrumentation-client.ts (NOT deprecated sentry.client.config.ts) gates Sentry.init on NEXT_PUBLIC_SENTRY_ENABLED === "true"; Phase 6 LEG-05 cookie banner flips the flag
- 01-03: Conditional withSentryConfig in next.config.ts (Pitfall 6) — production builds work without SENTRY_ORG/SENTRY_PROJECT in Phase 1 CI because the wrap is skipped when the flag is off
- 01-02: GitHub Actions CI workflow (`.github/workflows/ci.yml`) is the first ever CI in the repo — three parallel jobs (`hygiene-check`, `backend-build-test`, `frontend-lint-build`) on PRs to main and pushes to main; CPM-aware cache key includes `Backend/Directory.Packages.props` (RESEARCH.md Pitfall 5 mitigation)
- 01-02: Branch protection on main is manual-pending per D-10 + plan instruction — operator runs the GitHub UI workflow after first PR merges and CI registers the three job names; auto-enabling repo security via gh CLI is explicitly out of executor scope
- 01-02: Top-level `README.md` is English (not German) per D-12 — convention boundary between dev tooling (English, matches existing `Backend/README.md` analog) and end-user UI (German Sie-form). No screenshots, no CI badge until branch protection is enabled
- 01-02: Local empty `storage/` dir is benign — gitignored by Plan 01-01, zero tracked files, never reaches CI runner via `actions/checkout@v4`. Plan's local-side smoke fails on directory presence; CI-side check on tracked files passes. Documented as local-vs-CI delta, NOT a regression
- 02-01: RefreshTokenService stays HTTP-context-free (Pitfall 8) — UA/IP are method parameters; Minimal API endpoint binds HttpContext directly and extracts them. No IHttpContextAccessor injection.
- 02-01: RevokeAllForUserAsync detects provider by name (`Database.ProviderName == "Microsoft.EntityFrameworkCore.InMemory"`) — production runs ExecuteUpdateAsync; in-memory tests fall back to load-and-mutate. No InMemory package dependency leaked into production Infrastructure.
- 02-01: Sentry message body is the unique searchable token ("Refresh token replay detected"); Serilog warning uses a different phrasing so the verification grep returns exactly one match. Both fire together on every replay event.
- 02-01: EF migration manually reordered to CreateTable-first then DropColumn-last (EF scaffolds the reverse) per D-15 + RESEARCH Pattern 3.
- 02-01: AuthService.RegisterAsync now SaveChanges before refreshTokenService.IssueAsync — refresh_tokens.user_id FK requires the user row to exist first.
- 02-03: Pipeline order pinned — `UseForwardedHeaders` FIRST (real client IP must resolve before any IP-partitioned read), `UseRateLimiter` between Serilog and Authentication. `ForwardedHeadersWiringTests` defends the invariant via three source-level structural-grep tests.
- 02-03: `auth-strict` is a single mixed-partition policy — JWT `sub` claim present → partition by `user:{sub}`, otherwise `ip:{RemoteIpAddress}`. Plan 02-02 can attach the same policy name to authenticated `/account` DELETE and get sub-partitioned behavior automatically.
- 02-03: `KnownIPNetworks` uses `System.Net.IPNetwork.Parse("172.16.0.0/12")` (.NET 10 BCL) — NOT the deprecated `Microsoft.AspNetCore.HttpOverrides.IPNetwork` (emits `ASPDEPR005`). `ForwardLimit = 1` is explicit security intent (defaults are right today but explicit code blocks future drift toward IP-spoofing windows).
- 02-03: `OnRejected` writes via `WriteAsJsonAsync(value, options: null, contentType: "application/problem+json", ct)` — the 4-arg overload — NOT the property setter pattern, which `WriteAsJsonAsync` clobbers back to `application/json`. RejectionStatusCode set as first line inside `AddRateLimiter` (default would be 503, misleading clients/monitoring).
- 02-03: WebApplicationFactory<Program> rate-limit tests serialized via `[CollectionDefinition(DisableParallelization = true)]` host (`RateLimiterTestCollection`) — `Program.cs` top-level statements break in parallel WAF runs. Pattern reusable for any future WAF integration test. Plan 02-02 should adopt the same `[Collection]` attribute.
- 02-02: DELETE-with-record-body in Minimal API requires explicit `[FromBody]` attribute on the parameter — without it, the host short-circuits at WebApplicationFactory bootstrap time with ObjectDisposedException. Affects ANY future endpoint that body-binds on a verb other than POST/PUT. The cost is one attribute + one using directive.
- 02-02: BCrypt.Net-Next now PackageReference'd on TaxReader.Application (was Infrastructure-only). Justified because handlers live in Application/Commands and BCrypt is a pure library (no IO/network), so the "Infrastructure implements external concerns" rule is not violated.
- 02-02: Frontend uses raw `axios.delete` (NOT the shared `api` instance) for `deleteAccount` so user-error 401s surface inline rather than triggering the shared refresh-interceptor's logout flow. Pattern reusable for any endpoint where a 401 represents a user error rather than session expiry.
- 02-02: Mock.Callback used to assert call ordering (revoke-before-delete D-13) by capturing the user count at the moment the callback fires — cleaner than Mock.Sequence and compatible with EF in-memory's deferred SaveChanges. Pattern reusable for any handler that needs to prove a side-effect runs before a DB mutation.
- 02-CR-01: HMAC pepper validation is fail-fast via `RefreshTokenOptionsValidator` + `ValidateOnStart()` — rejects empty/non-Base64/wrong-length `HashKey`. API refuses to boot without a valid 32-byte pepper, eliminating the silent empty-key HMAC degradation the original code shipped with.
- 02-CR-02: Minimal API DELETE endpoint manually invokes `IValidator<DeleteAccountRequest>.ValidateAsync` before calling the handler. FluentValidation is NOT auto-invoked by Minimal APIs; this pattern (`Results.ValidationProblem(errors)` for grouped per-property German messages) should be replicated on any future endpoint that wants validator-driven 400 responses.
- 03-04: UploadErrorCatalog placed in Application/Common — zero infrastructure deps; raw ex.Message NEVER flows to processing_runs.error_message or HTTP response body (D-21 invariant verified by structural-grep test).
- 03-04: shadcn Alert created manually (npx shadcn@latest add alert is interactive-only); matched base-nova style using cva + data-slot pattern identical to badge/skeleton/card components.
- 03-04: Upload form fully rewritten for 202 Accepted shape; old synchronous UploadReceiptFilesResponse removed; ReceiptFileCard stack drives per-file polling.
- 03-04: ClassifyBatchJob cancellation test updated to use CancellationTokenSource.Callback() — token must be cancelled DURING the classify call (not before) so EF idempotency AnyAsync() query succeeds before IsCancellationRequested is checked.

### Pending Todos

- **Operator (manual)** — Enable branch protection on `main` after first PR carrying CI workflow merges and the three job names register with GitHub. Full configuration in `.planning/phases/01-foundation-cleanup-ci/01-02-SUMMARY.md` "Pending Operator Action" section. Not blocking Phase 1 verification but required for full satisfaction of Phase 1 Success Criterion #1 ("checks BLOCK merges", not just "checks RUN").
- **Phase 2 manual UAT** — 4 items in `02-HUMAN-UAT.md` (real-IP-through-Caddy burn, upload-concurrency timing, account-deletion dialog UX, Postgres migration `Up()` against real Postgres 17). Run `/gsd-verify-work 2` when ready to test against a live docker compose stack.
- **Phase 2 polish** — 9 WARNING + 6 INFO items remain in `02-REVIEW.md` (German localization on "User not found", typed error discriminator for 401 vs 404, ExpiresAt `<=` boundary, etc.). Bundle into a `/gsd-quick` polish pass when convenient.

### Blockers/Concerns

None yet.

## Deferred Items

| Category | Item | Status | Deferred At |
|----------|------|--------|-------------|
| *(none)* | | | |

## Session Continuity

Last session: 2026-05-31T00:00:00Z
Stopped at: Session resumed — proceeding to execute Phase 5, Plan 05-03 (/billing page)
Resume file: None
