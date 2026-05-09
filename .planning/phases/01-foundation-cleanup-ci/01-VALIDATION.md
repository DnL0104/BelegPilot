---
phase: 1
slug: foundation-cleanup-ci
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-05-04
---

# Phase 1 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.
> Source: `01-RESEARCH.md` § Validation Architecture.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit 2.9.2 + FluentAssertions 7.0.0 + Moq 4.20.72 |
| **Config file** | `Backend/tests/TaxReader.UnitTests/TaxReader.UnitTests.csproj` |
| **Quick run command** | `dotnet test Backend --filter "FullyQualifiedName~Phase1" --no-restore` |
| **Full suite command** | `dotnet test Backend --configuration Release` |
| **Estimated runtime** | ~30s quick / ~2-3 min full + frontend lint+build |

Frontend has no test framework in Phase 1 (Vitest/Playwright land in Phase 7). Phase 1 frontend verification is `cd Frontend && npm run lint && npm run build` succeeds.

---

## Sampling Rate

- **After every task commit:** Run `dotnet build Backend && dotnet test Backend --filter "FullyQualifiedName~Phase1"` (~30s)
- **After every plan wave:** Run `dotnet test Backend --configuration Release` + `cd Frontend && npm run lint && npm run build` (~2-3 min)
- **Before `/gsd-verify-work`:** Full suite must be green; CI workflow green on PR; manual smoke of `docker compose up --build` to verify startup log line and Sentry init no-op when DSN unset
- **Max feedback latency:** ~30 seconds (per-task) / ~3 min (per-wave)

---

## Per-Task Verification Map

> Filled in by gsd-planner during plan generation. Each task in each PLAN.md should map back to one or more rows here.

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| 1-XX-YY | TBD | TBD | FND-01 | hygiene leak | repo tree free of `storage/` and `build-diag*.txt` | smoke (CI) | hygiene-check job in `.github/workflows/ci.yml` | ❌ W0 | ⬜ pending |
| 1-XX-YY | TBD | TBD | FND-02 | model drift | startup log emits resolved Anthropic model; default = `claude-haiku-4-5` everywhere | unit + smoke | `dotnet test Backend --filter "AnthropicOptionsTests.Default_Model_IsHaiku4_5"` | ❌ W0 | ⬜ pending |
| 1-XX-YY | TBD | TBD | FND-03 | CORS misconfig | non-Dev env without `CORS_ALLOWED_ORIGINS` denies cross-origin requests | unit (WebApplicationFactory) | `dotnet test Backend --filter "CorsConfigurationTests.Production_NoOrigins_DeniesAll"` | ❌ W0 | ⬜ pending |
| 1-XX-YY | TBD | TBD | FND-04 | unverified merge | three CI jobs are merge-blocking on `main` | manual-only | inspect first PR's checks page; verify "required" badge | n/a | ⬜ pending |
| 1-XX-YY | TBD | TBD | FND-05 | onboarding miss | `README.md` at repo root references compose, env, browser URL | smoke | `test -f README.md && grep -q "docker compose up --build" README.md && grep -q ".env.example" README.md` | ❌ W0 | ⬜ pending |
| 1-XX-YY | TBD | TBD | OBS-01 | PII leak | Sentry `BeforeSend` strips request body, query (allow-list), headers (allow-list), UUID path mask, user.id_hash, receipt-content fields | unit | `dotnet test Backend --filter "SentryScrubbingTests"` | ❌ W0 | ⬜ pending |
| 1-XX-YY | TBD | TBD | OBS-02 | log correlation | Serilog config loads `WithEnvironmentName` enricher; upload handler emits log lines with `ReceiptFileId` property in scope | unit | `dotnet test Backend --filter "SerilogEnrichmentTests"` + handler-side `LogContext.PushProperty` assertion | ❌ W0 | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] `Backend/tests/TaxReader.UnitTests/Configuration/AnthropicOptionsTests.cs` — covers FND-02 (default model assertion)
- [ ] `Backend/tests/TaxReader.UnitTests/Cors/CorsConfigurationTests.cs` — covers FND-03 (deny-all in production); requires `WebApplicationFactory<Program>` test rig
- [ ] `Backend/tests/TaxReader.UnitTests/Observability/SentryScrubbingTests.cs` — covers OBS-01 (six scrubber rules: request body, query allow-list, headers allow-list, UUID path mask, user.id_hash, receipt-content fields)
- [ ] `Backend/tests/TaxReader.UnitTests/Observability/SerilogEnrichmentTests.cs` — covers OBS-02 (config + handler-side `LogContext` assertion)
- [ ] `Backend/tests/TaxReader.UnitTests/Helpers/TestLoggerProvider.cs` (or reuse existing helpers under `Backend/tests/TaxReader.UnitTests/Helpers/` if present) — captures log events for inspection during enrichment + scope tests
- [ ] Add `Microsoft.AspNetCore.Mvc.Testing` to `Backend/Directory.Packages.props` and the test csproj — required for FND-03 integration-flavoured test (`WebApplicationFactory<Program>`)

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| CI checks are merge-blocking on `main` | FND-04 | GitHub branch-protection settings live in repo admin UI, not in code | Open repo Settings → Branches → branch protection rule for `main`; verify three required status checks (`hygiene-check`, `backend-build-test`, `frontend-lint-build`) are listed and "Require branches to be up to date" is on |
| Sentry receives a real error event with PII scrubbed | OBS-01 (Success Criterion #4) | Requires real Sentry account + DSN configured in production | After deploy: trigger a known error path (e.g., a 500 from a malformed request); inspect Sentry event in EU dashboard; confirm body, query (except allow-list), headers (except `User-Agent`) and any UUID-shaped path segments are stripped/masked |
| Startup log emits resolved Anthropic model | FND-02 (Success Criterion #3) | Requires running container | `docker compose up --build` and grep startup logs for the resolved `Anthropic.Model` value; assert it matches `.env`/compose default |
| New developer can run `docker compose up --build` from `README.md` | FND-05 (Success Criterion #6) | Onboarding-style verification | Fresh clone → `cp .env.example .env` → edit secrets → `docker compose up --build` → browser reaches `https://localhost`; document any friction in PR review |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 30s per task / 3 min per wave
- [ ] `nyquist_compliant: true` set in frontmatter (after planner finalizes per-task map)

**Approval:** pending
