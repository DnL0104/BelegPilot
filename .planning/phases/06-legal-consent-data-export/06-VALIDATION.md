---
phase: 6
slug: legal-consent-data-export
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-06-02
---

# Phase 6 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit 2.9.2 + FluentAssertions 7 + Moq (backend); no frontend test framework configured |
| **Config file** | `Backend/tests/TaxReader.UnitTests/TaxReader.UnitTests.csproj` |
| **Quick run command** | `dotnet test Backend --filter "FullyQualifiedName~AuditLog|FullyQualifiedName~Export|FullyQualifiedName~Consent"` |
| **Full suite command** | `dotnet test Backend` |
| **Estimated runtime** | ~30 seconds |

---

## Sampling Rate

- **After every task commit:** Run quick run command
- **After every plan wave:** Run full suite command
- **Before `/gsd-verify-work`:** Full suite must be green
- **Max feedback latency:** 60 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| {filled by planner} | | | | | | | | | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] Audit-log entity + IAuditLogger test stubs for LEG-08
- [ ] Export bundle assembly test stubs for LEG-07
- [ ] Append-only enforcement test (no Update/Delete path) for LEG-08

*To be finalized by the planner against RESEARCH.md ## Validation Architecture.*

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| AVV/DPA signed and on file (Anthropic, Stripe, Sentry, BetterStack) | LEG-06 | Operator action — external sign-off | Complete `06-AVV-TRACKING.md` checklist |
| DPMA + EUIPO Marken register lookup | LEG-09 | Executor cannot query registers | Complete `06-MARKEN-SEARCH.md` |
| Lawyer review of legal page copy | LEG-01..LEG-04 | External legal sign-off (final in Phase 7) | Complete `06-LEGAL-REVIEW.md` |
| Cookie banner equal-prominence + Sentry init/close on consent | LEG-05 | No frontend test framework this milestone | Manual browser UAT |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 60s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
