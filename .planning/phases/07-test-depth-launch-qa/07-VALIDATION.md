---
phase: 7
slug: test-depth-launch-qa
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-06-05
---

# Phase 7 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Backend framework** | xUnit 2.9.2 + FluentAssertions 7 + Testcontainers.PostgreSql 4.x + Respawn 6.x (new integration project) |
| **Frontend framework** | Vitest 3 + @testing-library/react 16 (jsdom); Playwright 1.50 for E2E (new — none today) |
| **Config file** | Backend: new `TaxReader.IntegrationTests.csproj`; Frontend: `vitest.config.ts` + `playwright.config.ts` (Wave 0 installs) |
| **Quick run command** | `dotnet test Backend/tests/TaxReader.UnitTests` · `cd Frontend && npx vitest run` |
| **Full suite command** | `dotnet test Backend` (unit + integration) · `cd Frontend && npx vitest run && npx playwright test` |
| **Estimated runtime** | Unit ~seconds; Postgres integration + Playwright minutes (heavy CI job per D-03) |

---

## Sampling Rate

- **After every task commit:** Run the relevant quick command (xUnit unit tests / `vitest run`)
- **After every plan wave:** Run the full suite for the touched stack
- **Before `/gsd-verify-work`:** Full suite must be green
- **Max feedback latency:** unit/vitest < 60s; heavy integration suites run in the gated CI job

---

## Per-Task Verification Map

> Populated by gsd-planner during planning — one row per task mapping to QA-01..07 / OBS-03.

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| 07-01-01 | 01 | 1 | QA-01 | T-07-01 / — | Refresh-token replay rejected against real DB unique constraint | integration | `dotnet test Backend` | ❌ W0 | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] `TaxReader.IntegrationTests` project + Testcontainers/Respawn fixtures — QA-01
- [ ] `Frontend/vitest.config.ts` + test setup (jsdom, `@/*` alias, TanStack Query/axios mocks) — QA-02
- [ ] `Frontend/playwright.config.ts` + `webServer` against `next start` standalone in DE locale — QA-03

*Planner finalizes the exact Wave 0 file list per plan.*

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Native-speaker DE polish review | QA-04 | Linguistic nuance beyond the automated string guard | One-time launch review pass (NON-blocking per D-06) |
| Mobile photo-receipt upload from a real phone | QA-05 | Physical device camera + upload | Upload a photographed receipt end-to-end at sm/md |
| Final lawyer sign-off on AGB + Datenschutzerklärung | QA-07 | Legal judgement | Lawyer review; remove draft markers (HARD blocker per D-05) |
| AVV/DPA signing (Anthropic, Stripe, Sentry, BetterStack) | QA-07 | External counterparties | Operator signs all four (HARD blocker per D-05) |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 60s (fast tier)
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
