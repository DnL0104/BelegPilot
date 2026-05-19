---
phase: 3
slug: background-pipeline-tesseract-pool
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-05-19
---

# Phase 3 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.
> Detailed Phase Requirements → Test Map lives in `03-RESEARCH.md` § "Validation Architecture".

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit 2.9.2 + FluentAssertions 7.0.0 + Moq 4.20.72 (existing) |
| **Config file** | `Backend/tests/TaxReader.UnitTests/TaxReader.UnitTests.csproj` |
| **Quick run command** | `dotnet test Backend/tests/TaxReader.UnitTests --filter "FullyQualifiedName~Pipeline\|FullyQualifiedName~Hangfire\|FullyQualifiedName~Tesseract"` |
| **Full suite command** | `dotnet test Backend` |
| **Estimated runtime** | ~5–10s quick / ~30s full |

WAF integration tests reuse the `[Collection]` serialization pattern from Phase 2 (`RateLimiterTestCollection` `[CollectionDefinition(DisableParallelization = true)]`). Phase 3 introduces `PipelineTestCollection` (and/or `HangfireTestCollection`) on the same template — parallel `WebApplicationFactory<Program>` instances break `Program.cs` top-level statements.

---

## Sampling Rate

- **After every task commit:** Run `dotnet test Backend/tests/TaxReader.UnitTests --filter "FullyQualifiedName~Pipeline|FullyQualifiedName~Hangfire|FullyQualifiedName~Tesseract"`
- **After every plan wave:** Run `dotnet test Backend`
- **Before `/gsd-verify-work`:** Full backend suite must be green + manual UAT items in `03-HUMAN-UAT.md` (analog of `02-HUMAN-UAT.md`)
- **Max feedback latency:** 10s for quick runs

---

## Per-Task Verification Map

Filled in by planner (gsd-planner) during plan generation. Each plan's tasks reference the test mappings from `03-RESEARCH.md` § "Validation Architecture" (Phase Requirements → Test Map). The planner MUST attach an `<automated>` verify command to each task that mutates production code, choosing the appropriate filter expression from the research test map (e.g. `--filter "FullyQualifiedName~ProcessReceiptFileJob_Pdf_PersistsReceipt"`).

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| _to be populated by gsd-planner_ | | | | | | | | | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

Stub test files / fixtures planner MUST create in plan 03-01 (or earliest plan in dependency order) before any other plan can run:

- [ ] `Backend/tests/TaxReader.UnitTests/Pipeline/` — new directory for `ProcessReceiptFileJob`, `ClassifyBatchJob`, `UploadErrorCatalog`, `CancelReceiptFileHandler`, `GetReceiptFileStatusHandler` tests
- [ ] `Backend/tests/TaxReader.UnitTests/Hangfire/` — new directory for `HangfireAdminAuthFilter` WAF tests, `RecurringJobsBootstrap` source-grep tests, `SeedAdminUsersHostedService` unit tests
- [ ] `Backend/tests/TaxReader.UnitTests/Infrastructure/Tesseract/` — new directory for `TesseractEnginePool`, `TesseractEnginePoolWarmupService` tests
- [ ] `Backend/tests/TaxReader.UnitTests/Helpers/HangfireTestFactory.cs` — fake Hangfire job storage (in-memory) so dashboard-auth WAF tests don't need a real Postgres
- [ ] `Backend/tests/TaxReader.UnitTests/Helpers/TestDataFactory.cs` — add `CreateAdminUser(...)` helper returning a `User` with `IsAdmin = true`
- [ ] `Backend/tests/TaxReader.UnitTests/Pipeline/PipelineTestCollection.cs` — `[CollectionDefinition(DisableParallelization = true)]` for WAF tests (parallel `WebApplicationFactory<Program>` instances break top-level statements per `RateLimiterTestCollection`)

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| `useReceiptFileStatus` hook stops polling on terminal status | PIPE-06 | Frontend has no Vitest yet per CONCERNS.md #2; deferred to Phase 7 QA-02 | DevTools Network tab → upload → observe polling stops on `Completed`/`Failed`/`Cancelled` |
| Upload form replaces "processing" spinner with status badge once polling resolves | PIPE-06 | Frontend not test-instrumented | Upload file → observe per-card transition through Queued → Extracting → Parsing → Classifying → Completed |
| Receipts list shows skeleton for in-flight rows; alert for failed rows | PIPE-06 | Frontend not test-instrumented | Visit `/receipts` during/after upload, confirm shadcn `Skeleton` + `Alert` primitives render |
| Caddy reverse-proxy correctly forwards `tr_access` cookie + does not strip `Path=/hangfire` attribute | PIPE-01 | Caddy behavior only observable end-to-end | `docker compose up` → login as admin → visit `https://localhost/hangfire` → confirm 200 response |
| Hangfire dashboard CSRF anti-forgery tokens render correctly for admin POST actions (requeue/delete) | PIPE-01 | Anti-forgery is browser-side cookie + form-token interaction | Admin login → dashboard → click "Requeue" on a Failed job → confirm POST succeeds |
| Postgres migration `AddIsAdminToUsers` + `AddQueuedAndCancelledProcessingStatuses` apply cleanly against real Postgres 17 | PIPE-01, PIPE-02 | EF in-memory tests don't exercise migrations (CONCERNS.md #15) | `docker compose up --build` with `RUN_MIGRATIONS=true` against fresh + existing-data DB |
| Mid-Anthropic-call cancellation observably aborts the HTTP request (best-effort) | PIPE-03 | HttpClient cancellation behavior under real network conditions | Upload large multi-item batch → cancel during the `Classifying` status → confirm tokens refunded and status becomes `Cancelled` |
| Tesseract pool quarantines a corrupt engine without taking down the worker | PIPE-04 | Corrupt-engine path requires malformed image + concurrent pressure | Upload a malformed image while 2+ other OCR jobs run; confirm engine quarantined per Sentry warning + pool replenishes |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 10s for quick runs
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
