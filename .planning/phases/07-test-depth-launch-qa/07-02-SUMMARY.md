---
phase: 07-test-depth-launch-qa
plan: "02"
subsystem: backend-tests
tags: [testing, auth, tokens, ai-classification, unit-tests, tdd]
dependency_graph:
  requires: []
  provides: [AuthServiceTests, TokenServiceTests, AiOnlyClassificationServiceTests]
  affects: [CI-fast-suite, D-01-gap-closure]
tech_stack:
  added: []
  patterns: [in-memory-ef-per-test, constructor-as-setup, IDisposable-dispose-pattern, Moq-Times-verification]
key_files:
  created:
    - Backend/tests/TaxReader.UnitTests/Services/AuthServiceTests.cs
    - Backend/tests/TaxReader.UnitTests/Services/TokenServiceTests.cs
    - Backend/tests/TaxReader.UnitTests/Services/AiOnlyClassificationServiceTests.cs
  modified: []
decisions:
  - "Used FirstAsync instead of FirstOrDefaultAsync for DB lookups in tests where the entity is guaranteed to exist — eliminates CS8602 nullability warnings and makes the intent clearer"
  - "Used null-forgiving operator (!) on Result<T>.Value after asserting IsSuccess — the type is nullable by design, and the preceding assertion makes the dereference safe"
  - "Seeded AutoConfirmThreshold=0.80 on the test user in AiOnlyClassificationServiceTests to avoid needing a nullable-threshold test path"
metrics:
  duration: "~12 minutes"
  completed: "2026-06-06T20:40:00Z"
  tasks_completed: 2
  files_created: 3
  files_modified: 0
---

# Phase 07 Plan 02: D-01 Service Backfill (AuthService + TokenService + AiOnlyClassificationService) Summary

Backfilled dedicated unit tests for the three highest-risk currently-untested money/security services. 17 new tests across 3 files, all fast (in-memory EF, no Docker), runnable on every PR.

## What Was Built

### AuthServiceTests (6 tests)
- Register happy-path: BCrypt hash stored, not plaintext
- Register duplicate email: exact German error "Ein Konto mit dieser E-Mail existiert bereits."
- Register short password: "Das Passwort muss mindestens 8 Zeichen lang sein."
- Login wrong password: "Ungültige E-Mail oder Passwort."
- Login unknown email: same German string as wrong password — no user enumeration (T-07-05 ASVS V2)
- Login correct password: BCrypt.Verify happy path, access token + mock refresh token returned

### TokenServiceTests (5 tests)
- GetOrCreateBalance new user: Balance=10, "Welcome bonus" transaction exists
- TryConsumeManyAsync insufficient: returns false, balance unchanged
- TryConsumeManyAsync sufficient: returns true, writes Consumption rows, Amount=-cost
- RefundManyAsync: writes Refund rows, balance restored
- AddTokensAsync non-positive amount: throws ArgumentOutOfRangeException (both 0 and -1)

### AiOnlyClassificationServiceTests (6 tests)
- AI not configured: all Unknown + "AI-Klassifizierung nicht konfiguriert." + no token deduction
- Insufficient tokens: all Unknown + "Keine Tokens verfügbar – bitte Credits aufladen." + ClassifyBatchAsync Times.Never
- AI returns Unbekannt: RefundManyAsync called with that item's ledger entry (Times.Once)
- AI throws: RefundManyAsync(all entries) + all Unknown + "AI-Fehler: ..."
- Confidence >= threshold: ClassificationStatus.Confirmed + reason starts "Auto-bestätigt"
- Confidence < threshold: ClassificationStatus.Suggested

## Commits

| Task | Commit | Files |
|------|--------|-------|
| Task 1: AuthServiceTests + TokenServiceTests | f76d3e7 | AuthServiceTests.cs, TokenServiceTests.cs |
| Task 2: AiOnlyClassificationServiceTests | cf581e4 | AiOnlyClassificationServiceTests.cs |

## Deviations from Plan

None — plan executed exactly as written.

The TDD framing for this plan is "backfill" (implementation already exists) so the RED phase was implicit: the test files didn't exist at all before this plan, so prior to writing them every behavior was uncovered. Tests pass (GREEN) on first run, confirming the existing implementation is correct.

## Threat Model Coverage

| Threat | Test | Result |
|--------|------|--------|
| T-07-05 Spoofing — LoginAsync user enumeration | `LoginAsync_UnknownEmail_ReturnsFailure` asserts same string as wrong password | Mitigated |
| T-07-06 Tampering — TokenService over-spend | `TryConsumeManyAsync_InsufficientBalance_ReturnsFalse` | Mitigated |
| T-07-07 Repudiation — refund-on-failure | `ClassifyItemsAsync_AiThrows_RefundsAllAndReturnsUnknown` + `_AiReturnsUnbekannt_RefundsThatItem` | Mitigated |
| T-07-08 Information Disclosure — test secrets | test-only placeholder secrets, never reach production | Accepted |

## Known Stubs

None.

## Threat Flags

None — no new network endpoints, auth paths, or file access introduced.

## Self-Check: PASSED

- [x] AuthServiceTests.cs exists at Backend/tests/TaxReader.UnitTests/Services/AuthServiceTests.cs
- [x] TokenServiceTests.cs exists at Backend/tests/TaxReader.UnitTests/Services/TokenServiceTests.cs
- [x] AiOnlyClassificationServiceTests.cs exists at Backend/tests/TaxReader.UnitTests/Services/AiOnlyClassificationServiceTests.cs
- [x] Commit f76d3e7 exists (AuthServiceTests + TokenServiceTests)
- [x] Commit cf581e4 exists (AiOnlyClassificationServiceTests)
- [x] `dotnet test --filter "FullyQualifiedName~ServiceTests"` → 26 passed, 0 failed
