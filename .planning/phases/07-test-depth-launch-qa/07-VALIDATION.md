---
phase: 7
slug: test-depth-launch-qa
status: planned
nyquist_compliant: true
wave_0_complete: false
created: 2026-06-05
updated: 2026-06-05
---

# Phase 7 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Backend unit framework** | xUnit 2.9.2 + FluentAssertions 7 + Moq + EFCore.InMemory (existing `TaxReader.UnitTests`) |
| **Backend integration framework** | xUnit + Testcontainers.PostgreSql 4.12.0 + Respawn 6.2.1 + WebApplicationFactory<Program> (new `TaxReader.IntegrationTests`) |
| **Frontend unit framework** | Vitest 3 + @testing-library/react 16 (jsdom) — new (none today) |
| **Frontend E2E framework** | Playwright 1.50 — new (none today) |
| **Config files** | Backend: new `TaxReader.IntegrationTests.csproj`; Frontend: `vitest.config.mts` + `vitest.setup.ts` + `playwright.config.ts` (all Wave 0 within plans 07-01/07-04/07-05) |
| **Quick run (fast tier)** | `dotnet test Backend/tests/TaxReader.UnitTests` · `cd Frontend && npx vitest run` |
| **Full suite (heavy tier)** | `dotnet test Backend/tests/TaxReader.IntegrationTests` · `cd Frontend && npx playwright test` |
| **Estimated runtime** | Unit/Vitest ~seconds; Postgres integration + Playwright minutes (gated heavy CI job per D-03) |

---

## Sampling Rate

- **After every task commit:** Run the relevant quick command (xUnit unit / `vitest run` / filtered integration)
- **After every plan wave:** Run the full fast suite for the touched stack
- **Before `/gsd-verify-work`:** Heavy suite (integration + Playwright) green on push-to-main
- **Max feedback latency:** unit/vitest < 60s; heavy suites run in the gated CI job (D-03)

---

## Per-Task Verification Map

> One row per automated task mapping to QA-01..07 / OBS-03.

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| 07-01-01 | 01 | 1 | QA-01 | T-07-04 | Test-only secrets ephemeral; project builds | build | `dotnet build tests/TaxReader.IntegrationTests` | ❌ W0 | ⬜ pending |
| 07-01-02 | 01 | 1 | QA-01 | T-07-01/02/03 | Refresh-token replay + payment idempotency + duplicate + cascade asserted against REAL Postgres constraints | integration | `dotnet test tests/TaxReader.IntegrationTests` | ❌ W0 | ⬜ pending |
| 07-02-01 | 02 | 1 | QA-01 (D-01) | T-07-05/06 | AuthService non-enumerating login + TokenService atomic ledger | unit | `dotnet test ...UnitTests --filter "AuthServiceTests\|TokenServiceTests"` | ❌ W0 | ⬜ pending |
| 07-02-02 | 02 | 1 | QA-01 (D-01) | T-07-07 | AiOnlyClassification refund-on-failure/Unknown + auto-confirm threshold | unit | `dotnet test ...UnitTests --filter AiOnlyClassificationServiceTests` | ❌ W0 | ⬜ pending |
| 07-03-01 | 03 | 1 | OBS-03 | T-07-09/11 | Health endpoints anonymous; no secret leak | build | `dotnet build src/TaxReader.Api` | ❌ W0 | ⬜ pending |
| 07-03-02 | 03 | 1 | OBS-03 | T-07-09/11 | 200/JSON "healthy", anonymous (not 401), no connection-string/secret in body | integration (WAF) | `dotnet test ...UnitTests --filter HealthEndpointTests` | ❌ W0 | ⬜ pending |
| 07-04-01 | 04 | 1 | QA-02 | T-07-12 | JWT shared-refresh-promise calls /auth/refresh exactly once | unit (Vitest) | `npx vitest run src/lib/format.test.ts src/lib/api-client.test.ts` | ❌ W0 | ⬜ pending |
| 07-04-02 | 04 | 1 | QA-02 | T-07-13 | Upload guard + classify-confirm German copy | component (Vitest) | `npx vitest run src/components/...test.tsx` | ❌ W0 | ⬜ pending |
| 07-05-01 | 05 | 2 | QA-03/QA-05 | — | Playwright config DE locale, standalone server, sm/md projects | config | `npx playwright test --list` | ❌ W0 | ⬜ pending |
| 07-05-02 | 05 | 2 | QA-03/QA-05 | T-07-15 | DE happy path register→export against real stack; per-user scoping | e2e | `npx playwright test` (heavy/local stack) | ❌ W0 | ⬜ pending |
| 07-06-01 | 06 | 3 | QA-04 | T-07-19 | DE guard fails bare toLocaleString; Vitest on every PR | CI guard | bash guard + `npx vitest run` | ❌ W0 | ⬜ pending |
| 07-06-02 | 06 | 3 | QA-01/02/03 | T-07-18 | Heavy job runs integration + E2E gated on push-main + run-heavy | CI | `npx js-yaml ci.yml` valid + heavy job run | ❌ W0 | ⬜ pending |
| 07-07-01 | 07 | 4 | QA-06/QA-07/OBS-03/QA-05 | T-07-21/22 | PITFALLS + go/no-go + ops docs authored | docs | `test -s` on the four docs + grep checks | ❌ W0 | ⬜ pending |
| 07-07-02 | 07 | 4 | QA-06/QA-07/OBS-03 | T-07-21/22/23 | Operator wires BetterStack keyword monitors + Sentry quiet-hours; sets go/no-go | manual (checkpoint) | operator UAT per 07-OPS-SETUP.md | n/a | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] `TaxReader.IntegrationTests` project + Testcontainers/Respawn fixtures + WAF factory — QA-01 (07-01 Task 1)
- [ ] `Frontend/vitest.config.mts` + `vitest.setup.ts` + `"test"` script + devDeps — QA-02 (07-04 Task 1)
- [ ] `Frontend/playwright.config.ts` + `e2e/` dir + browser install — QA-03/QA-05 (07-05 Task 1)
- [ ] Backend `/health` + `/api/v1/health` endpoints — OBS-03 (07-03 Task 1; did not exist)
- [ ] CI heavy job + DE-localization guard step — QA-04/D-03/D-07 (07-06)
- [ ] `PITFALLS.md` — QA-07 (07-07 Task 1)

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Native-speaker DE polish review | QA-04 | Linguistic nuance beyond the automated string guard | One-time launch review pass (NON-blocking per D-06); captured in 07-HUMAN-UAT.md |
| Mobile photo-receipt upload from a real phone | QA-05 | Physical device camera + upload | Upload a photographed receipt end-to-end at sm/md (viewport smoke is automated in 07-05; camera is manual) |
| Final lawyer sign-off on AGB + Datenschutzerklaerung | QA-07 | Legal judgement | Lawyer review; remove draft markers (HARD blocker per D-05) |
| AVV/DPA signing (Anthropic, Stripe, Sentry, BetterStack) | QA-07 | External counterparties | Operator signs all four (HARD blocker per D-05) |
| BetterStack monitors + status page + maintenance windows | OBS-03/QA-06 | External dashboard, no assumed provisioning API | Operator wires keyword monitors on "healthy" per 07-OPS-SETUP.md |
| Sentry quiet-hours alert rule | QA-06 | External dashboard config | Operator sets 23:00-07:00 HIGH-only per 07-OPS-SETUP.md |

---

## Validation Sign-Off

- [x] All tasks have `<automated>` verify or are explicit manual checkpoints with Wave 0 dependencies noted
- [x] Sampling continuity: no 3 consecutive automated tasks without an automated verify
- [x] Wave 0 covers all MISSING references (integration project, vitest, playwright, health endpoints, PITFALLS.md)
- [x] No watch-mode flags (`vitest run`, not `vitest`)
- [x] Feedback latency < 60s (fast tier: unit + vitest)
- [x] `nyquist_compliant: true` set in frontmatter

**Approval:** planned 2026-06-05
