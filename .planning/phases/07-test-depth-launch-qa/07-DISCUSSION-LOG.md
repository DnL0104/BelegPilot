# Phase 7: Test Depth + Launch QA - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions captured in 07-CONTEXT.md — this log preserves the discussion.

**Date:** 2026-06-05
**Phase:** 07-test-depth-launch-qa
**Mode:** discuss (default, batched)
**Areas discussed:** Test coverage ambition, CI model for slow tests, Launch go/no-go gate, Localization audit method

## Gray Areas Presented

User selected all four offered gray areas to discuss. Tooling was treated as locked by REQUIREMENTS.md (Testcontainers 4.x, Respawn 6.x, Vitest 3, Playwright 1.50, BetterStack, Sentry), so discussion focused on ambition, gating, and CI mechanics.

## Questions & Decisions

### Test coverage ambition
- Options: Critical paths + risk backfill (recommended) / Named critical paths only / Comprehensive backfill
- **Selected:** Critical paths + risk backfill → D-01 / D-02
- Note: high-risk untested services (AuthService, AiOnlyClassificationService, TokenService) added; PdfPig/Tesseract/exports/ClaudeAiClassifier deferred.

### CI model for slow tests
- Options: Separate heavy job, gated (recommended) / Every PR / Nightly schedule only
- **Selected:** Separate heavy job, gated → D-03 / D-04
- Note: Postgres-integration + Playwright on push-to-main (+ optional PR label); Vitest stays on every PR.

### Launch go/no-go gate (multiSelect — hard blockers)
- Options: Automated suites green / Lawyer sign-off (QA-07) / Real legal data + AVVs / Native review + prior UAT
- **Selected as HARD blockers:** Automated suites green, Lawyer sign-off, Real legal data + AVVs → D-05
- **Left non-blocking (tracked):** Native-speaker review + prior-phase (P2/3/4) UAT → D-06

### Localization audit method
- Options: Automated guard + manual pass (recommended) / Manual checklist only / Automated guard only
- **Selected:** Automated guard + manual pass → D-07
- Note: extend the 06-07 hygiene-check bash pattern + assert Intl.NumberFormat('de-DE') EUR; one-time native-speaker pass at launch (non-blocking per D-06).

## Claude's Discretion / Surfaced
- Exact Testcontainers/Respawn/Playwright/Vitest wiring left to research + planning (D-08 monitoring details too).
- Flagged that `PITFALLS.md` (referenced by QA-07) does not exist yet and must be authored this phase.
- Flagged that `codebase/TESTING.md` is stale (predates CI + WebApplicationFactory tests).

## Deferred Ideas
- Comprehensive backend backfill (PdfPig, Tesseract, exports, ClaudeAiClassifier) → backlog / future hardening.

## Folded Todos
None — no pending todos matched this phase.
