---
phase: 6
slug: legal-consent-data-export
status: draft
nyquist_compliant: true
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
| **Quick run command** | `dotnet test Backend --filter "FullyQualifiedName~AuditLog\|FullyQualifiedName~Export"` |
| **Full suite command** | `dotnet test Backend` |
| **Frontend build smoke** | `cd Frontend && npm run build` |
| **Estimated runtime** | ~30 seconds (backend) + ~60s (frontend build) |

---

## Sampling Rate

- **After every task commit:** Run quick run command (backend) or frontend build smoke (frontend tasks)
- **After every plan wave:** Run full suite command + frontend build
- **Before `/gsd-verify-work`:** Full suite must be green + frontend build green
- **Max feedback latency:** 60 seconds (backend); frontend build ~60s

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| 06-01-T1 | 06-01 | 1 | LEG-01 | T-06-03 | Only static legal pages made public via PUBLIC_PATHS | build-visible | `cd Frontend && npm run build` | new (footer) | ⬜ pending |
| 06-01-T2 | 06-01 | 1 | LEG-01..04 | T-06-02 | Draft marker present on all 4 pages (grep) | build-visible + grep | `cd Frontend && npm run build` | existing+new | ⬜ pending |
| 06-01-T3 | 06-01 | 1 | LEG-01..04 | T-06-02 | Review-gate doc tracks Drafted→Live | structural | `test -f 06-LEGAL-REVIEW.md && grep Lawyer-reviewed` | new | ⬜ pending |
| 06-01-UAT | 06-01 | 1 | LEG-01..04 | T-06-02 | Lawyer review external; pages reachable unauth | manual UAT | browser | — | ⬜ pending |
| 06-02-T1 | 06-02 | 2 | LEG-05 | T-06-20 | Sentry gated on `NEXT_PUBLIC_SENTRY_ENABLED && hasSentryConsent()` | build-visible + grep | `cd Frontend && npm run build` | new+modified | ⬜ pending |
| 06-02-T2 | 06-02 | 2 | LEG-05 | T-06-21/T-06-23 | Fehleranalyse not pre-ticked; Notwendig disabled | build-visible + grep | `cd Frontend && npm run build` | new+modified | ⬜ pending |
| 06-02-UAT | 06-02 | 2 | LEG-05 | T-06-20/T-06-22 | Equal-prominence buttons; Sentry init on grant / close on revoke; no reload | manual UAT | browser + DevTools | — | ⬜ pending |
| 06-03-T1 | 06-03 | 1 | LEG-08 | T-06-10/T-06-12/T-06-13 | Append-only (no Remove/Delete); jsonb; no actor cascade; PII-min | unit | `dotnet test Backend --filter "FullyQualifiedName~AuditLog"` | new (Wave 0) | ⬜ pending |
| 06-03-T2 | 06-03 | 1 | LEG-08 | T-06-11/T-06-12 | 5 call sites record; record-before-delete; email/token hashed | unit | `dotnet test Backend` | existing+new (Wave 0) | ⬜ pending |
| 06-04-T1 | 06-04 | 2 | LEG-07 | T-06-43/T-06-44/T-06-46 | Per-user bundle; own audit rows only; no password hash; 24h purge | unit | `dotnet test Backend --filter "FullyQualifiedName~Export"` | new (Wave 0) | ⬜ pending |
| 06-04-T2 | 06-04 | 2 | LEG-07 | T-06-40/T-06-41/T-06-42/T-06-45 | Auth-required download; 403 on IDOR; one-time token; no path traversal | unit | `dotnet test Backend --filter "FullyQualifiedName~ExportDownload"` | new (Wave 0) | ⬜ pending |
| 06-04-T3 | 06-04 | 2 | LEG-07 | T-06-40 | Authenticated blob download (JWT carried); status polling | build-visible | `cd Frontend && npm run build` | new+modified | ⬜ pending |
| 06-04-UAT | 06-04 | 2 | LEG-07 | T-06-40/T-06-44 | End-to-end export + IDOR 403 + bundle contents | manual UAT | docker compose | — | ⬜ pending |
| 06-05-T1 | 06-05 | 1 | LEG-06 | T-06-30/T-06-31 | AVV checklist + DPA URLs coupled to Datenschutz | structural | `test -f 06-AVV-TRACKING.md && grep anthropic.com/legal/dpa` | new | ⬜ pending |
| 06-05-T2 | 06-05 | 1 | LEG-09 | T-06-32 | Marken search record classes 9+42 + decision | structural | `test -f 06-MARKEN-SEARCH.md && grep EUIPO` | new | ⬜ pending |
| 06-05-UAT | 06-05 | 1 | LEG-06/LEG-09 | T-06-30/T-06-32 | Operator signs AVVs + runs register lookups | operator HUMAN-ACTION | external | — | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

Backend new test files (created within the TDD tasks that produce the code, RED before GREEN):

- [ ] `Backend/tests/TaxReader.UnitTests/Application/AuditLoggerTests.cs` — LEG-08, RecordAsync writes a row (06-03 T1)
- [ ] `Backend/tests/TaxReader.UnitTests/Application/AuditAppendOnlyTests.cs` — LEG-08, no Update/Delete path + no cascade/HasOne in config (06-03 T1)
- [ ] `Backend/tests/TaxReader.UnitTests/Application/Commands/SaveClassificationRuleHandlerTests.cs` — LEG-08, rule-creation audit call (NEW — none exists) (06-03 T2)
- [ ] `Backend/tests/TaxReader.UnitTests/Application/ExportTokenStoreTests.cs` — LEG-07, register/get/invalidate/expire (06-04 T1)
- [ ] `Backend/tests/TaxReader.UnitTests/Jobs/ExportUserDataJobTests.cs` — LEG-07, zip contents + per-user audit scope + no password hash (06-04 T1)
- [ ] `Backend/tests/TaxReader.UnitTests/Jobs/ExportCleanupJobTests.cs` — LEG-07, 24h purge (06-04 T1)
- [ ] `Backend/tests/TaxReader.UnitTests/Application/ExportDownloadEndpointTests.cs` — LEG-07, ownership 403 + one-time invalidation (06-04 T2)

Backend existing test files modified (add IAuditLogger mock assertions — 06-03 T2):

- `Backend/tests/TaxReader.UnitTests/Auth/DeleteAccountHandlerTests.cs`
- `Backend/tests/TaxReader.UnitTests/Jobs/GrantTokensJobTests.cs`
- `Backend/tests/TaxReader.UnitTests/Jobs/RevokeTokensJobTests.cs`
- `Backend/tests/TaxReader.UnitTests/Auth/ReplayDetectionTests.cs` (refresh-token replay audit path)

Frontend: No frontend test framework is configured this milestone. LEG-05 (cookie banner equal-prominence, Sentry init/close on consent) and LEG-07 (export UX, IDOR) frontend behaviors are validated via manual UAT + `npm run build` smoke. Unchanged from prior phases.

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| AVV/DPA signed and on file (Anthropic, Stripe, Sentry, BetterStack) | LEG-06 | Operator action — external sign-off | Complete `06-AVV-TRACKING.md` checklist (06-05 UAT) |
| DPMA + EUIPO Marken register lookup | LEG-09 | Executor cannot query registers | Complete `06-MARKEN-SEARCH.md` (06-05 UAT) |
| Lawyer review of legal page copy | LEG-01..LEG-04 | External legal sign-off (final in Phase 7) | Complete `06-LEGAL-REVIEW.md` (06-01 UAT) |
| Cookie banner equal-prominence + Sentry init/close on consent + no reload | LEG-05 | No frontend test framework this milestone | Manual browser UAT (06-02 UAT, steps 1-5) |
| End-to-end export + IDOR 403 + bundle contents (no password hash, own audit rows only) | LEG-07 | UX + cross-user IDOR not unit-coverable in-process | Manual UAT (06-04 UAT, steps 1-6); backend job/cleanup/token/ownership ARE unit-tested |

---

## Validation Sign-Off

- [x] All tasks have `<automated>` verify or Wave 0 dependencies (auto tasks: build/unit; checkpoint tasks: manual UAT documented above)
- [x] Sampling continuity: no 3 consecutive auto tasks without automated verify (every auto task has a build or unit command)
- [x] Wave 0 covers all MISSING references (7 new + 4 modified backend test files enumerated)
- [x] No watch-mode flags (all commands are single-run)
- [x] Feedback latency < 60s (backend unit ~30s; frontend build ~60s)
- [x] `nyquist_compliant: true` set in frontmatter

**Approval:** ready for execution
