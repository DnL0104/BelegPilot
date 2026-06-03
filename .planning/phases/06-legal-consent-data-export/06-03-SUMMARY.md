---
phase: 06-legal-consent-data-export
plan: 03
subsystem: backend
tags: [audit-log, compliance, gdpr, leg-08, append-only, tdd]
dependency_graph:
  requires:
    - 05-commercial-surface-payments (DeleteAccountHandler, GrantTokensJob, RevokeTokensJob exist)
    - 02-01 (RefreshTokenService exists with replay detection)
  provides:
    - audit_log DB table (LEG-08)
    - IAuditLogger interface (consumed by 06-04 ExportUserDataJob for DataExportRequested/Downloaded events)
    - AuditLogEntry rows for five sensitive operations
  affects:
    - 06-04 (export bundle must include audit_log entries per D-15)
tech_stack:
  added:
    - AuditLogEntry entity (Domain)
    - AuditAction enum (Domain)
    - IAuditLogger interface (Application)
    - AuditLogger service (Infrastructure)
    - AuditLogEntryConfiguration EF config (Infrastructure, jsonb with ValueConverter)
    - AddAuditLog EF migration (20260603045456)
  patterns:
    - ValueConverter on Dictionary<string, object?> metadata — satisfies InMemory provider validation while Npgsql handles jsonb in production
    - SHA-256 HashEmail / HashUserId for PII minimization in audit metadata (T-06-12)
    - record-before-delete ordering: audit call fires before Users.Remove (T-06-11)
key_files:
  created:
    - Backend/src/TaxReader.Domain/Entities/AuditLogEntry.cs
    - Backend/src/TaxReader.Domain/Enums/AuditAction.cs
    - Backend/src/TaxReader.Application/Interfaces/IAuditLogger.cs
    - Backend/src/TaxReader.Infrastructure/Services/AuditLogger.cs
    - Backend/src/TaxReader.Infrastructure/Data/Configurations/AuditLogEntryConfiguration.cs
    - Backend/src/TaxReader.Infrastructure/Migrations/20260603045456_AddAuditLog.cs
    - Backend/tests/TaxReader.UnitTests/Application/AuditLoggerTests.cs
    - Backend/tests/TaxReader.UnitTests/Application/AuditAppendOnlyTests.cs
    - Backend/tests/TaxReader.UnitTests/Application/Commands/SaveClassificationRuleHandlerTests.cs
  modified:
    - Backend/src/TaxReader.Application/Interfaces/IAppDbContext.cs (add AuditLogEntries DbSet)
    - Backend/src/TaxReader.Infrastructure/Data/AppDbContext.cs (add AuditLogEntries property)
    - Backend/src/TaxReader.Infrastructure/DependencyInjection.cs (AddScoped<IAuditLogger, AuditLogger>)
    - Backend/src/TaxReader.Application/Commands/DeleteAccountHandler.cs (IAuditLogger, HashEmail, AccountDeleted)
    - Backend/src/TaxReader.Application/Jobs/GrantTokensJob.cs (IAuditLogger, TokensGranted)
    - Backend/src/TaxReader.Application/Jobs/RevokeTokensJob.cs (IAuditLogger, TokensRevoked)
    - Backend/src/TaxReader.Infrastructure/Services/RefreshTokenService.cs (IAuditLogger, RefreshTokenReplayDetected)
    - Backend/src/TaxReader.Application/Commands/SaveClassificationRuleHandler.cs (IAuditLogger, ClassificationRuleCreated)
    - Backend/tests/TaxReader.UnitTests/Auth/DeleteAccountHandlerTests.cs (mock + audit assertions)
    - Backend/tests/TaxReader.UnitTests/Jobs/GrantTokensJobTests.cs (mock + audit assertion)
    - Backend/tests/TaxReader.UnitTests/Jobs/RevokeTokensJobTests.cs (mock + audit assertions)
    - Backend/tests/TaxReader.UnitTests/Auth/ReplayDetectionTests.cs (mock + audit assertion)
    - Backend/tests/TaxReader.UnitTests/Auth/RefreshTokenServiceTests.cs (updated constructor)
    - Backend/tests/TaxReader.UnitTests/Auth/MultiDeviceTokenTests.cs (updated constructor)
    - Backend/tests/TaxReader.UnitTests/Auth/HmacPepperHashingTests.cs (updated constructor)
decisions:
  - "ValueConverter on audit_log.Metadata (JSON serialization string) enables InMemory provider validation; Npgsql transparently handles actual jsonb wire format in production"
  - "email_hash uses SHA-256 of lowercased email (not HMAC) — correlation tag only, no secret needed; satisfies DSGVO Art. 5(1)(c) and T-06-12"
  - "token_id_hash in RefreshTokenService reuses existing HashUserId(existing.Id) utility — same SHA-256 pattern, id is a Guid not a token value"
  - "AuditLogger has no ILogger injection — each call-site owns its own logger and the IAuditLogger contract is simple enough to not need logging at the write layer"
metrics:
  duration: "12 minutes"
  completed_date: "2026-06-03"
  tasks_completed: 2
  files_changed: 26
---

# Phase 06 Plan 03: Audit Log (LEG-08) Summary

**One-liner:** Append-only `audit_log` table backed by `IAuditLogger` interface + `AuditLogger` impl, wired into five sensitive-operation call sites with SHA-256 PII-minimized metadata and a migration that has no FK cascade on actor/subject.

## What Was Built

### Core Artifacts
- **`AuditLogEntry`** (Domain entity): `Guid Id`, `string Action`, `Guid? ActorUserId`, `Guid? SubjectUserId`, `Dictionary<string, object?> Metadata`, `DateTime CreatedAt`. No User navigation property — audit rows survive user deletion.
- **`AuditAction`** enum: `AccountDeleted`, `TokensGranted`, `TokensRevoked`, `RefreshTokenReplayDetected`, `ClassificationRuleCreated`, `DataExportRequested`, `DataExportDownloaded`.
- **`IAuditLogger`** (Application interface): `RecordAsync(action, actorUserId, subjectUserId, metadata, ct)`.
- **`AuditLogger`** (Infrastructure): `DbSet.Add(entry) + SaveChangesAsync`. No logger, no retry attributes.
- **EF Configuration**: `ToTable("audit_log")`, `HasColumnType("jsonb")` with a `ValueConverter<Dictionary<string,object?>, string>` for InMemory test compatibility, indexes on `subject_user_id` and `created_at`. No `HasOne`/`WithMany`, no cascade.
- **Migration `20260603045456_AddAuditLog`**: creates `audit_log` with uuid PK (`gen_random_uuid()`), action varchar(100), nullable actor/subject uuid, jsonb metadata, timestamptz created_at. No FK constraints on actor/subject.
- **DI**: `services.AddScoped<IAuditLogger, AuditLogger>()` added to `DependencyInjection.cs`.

### Call-Site Wirings (five)

| Handler/Job | AuditAction | Metadata | Insert Point |
|---|---|---|---|
| `DeleteAccountHandler` | `AccountDeleted` | `email_hash` (SHA-256 hex of lowercased email) | after `RevokeAllForUserAsync`, BEFORE `Users.Remove` |
| `GrantTokensJob` | `TokensGranted` | `credits` (int) | after `SaveChangesAsync` |
| `RevokeTokensJob` | `TokensRevoked` | `credits` (int) | after `SaveChangesAsync` |
| `RefreshTokenService` | `RefreshTokenReplayDetected` | `token_id_hash` (SHA-256 of token Guid) | after `SentrySdk.CaptureMessage`, before `RevokeAllForUserAsync` |
| `SaveClassificationRuleHandler` | `ClassificationRuleCreated` | `rule_id`, `category` | after `SaveChangesAsync`, before return |

### Tests (new + updated)

| File | Type | What It Tests |
|---|---|---|
| `AuditLoggerTests.cs` | New | RecordAsync writes row; null actor persists correctly |
| `AuditAppendOnlyTests.cs` | New | Structural-grep: no Remove/Delete on AuditLogEntries; no cascade in config |
| `SaveClassificationRuleHandlerTests.cs` | New | Audit call on success; no audit call on duplicate (409) |
| `DeleteAccountHandlerTests.cs` | Updated | Added audit mock; assert AccountDeleted once; new ordering test (record-before-delete) |
| `GrantTokensJobTests.cs` | Updated | Added audit mock; assert TokensGranted once |
| `RevokeTokensJobTests.cs` | Updated | Added audit mock; assert TokensRevoked once |
| `ReplayDetectionTests.cs` | Updated | Added audit mock; assert RefreshTokenReplayDetected once |
| `RefreshTokenServiceTests.cs`, `MultiDeviceTokenTests.cs`, `HmacPepperHashingTests.cs` | Updated | Updated constructor call to pass audit mock |

**Test results:** 262 passed, 5 skipped (infrastructure-only, pre-existing), 0 failed.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] EF InMemory provider rejects `Dictionary<string, object?>` with `HasColumnType("jsonb")`**
- **Found during:** Task 1 GREEN phase — AuditLoggerTests failed with `InvalidOperationException: The 'Dictionary<string, object>' property 'AuditLogEntry.Metadata' could not be mapped`
- **Issue:** EF InMemory validator does not support `HasColumnType("jsonb")` for complex types. In production, Npgsql handles the mapping; InMemory cannot.
- **Fix:** Added a `ValueConverter<Dictionary<string, object?>, string>` (JSON serialization) via `.HasConversion(metadataConverter)` before `.HasColumnType("jsonb")`. Npgsql in production transparently overrides the string representation with its own jsonb encoding. InMemory uses the string converter and round-trips the dictionary through JSON.
- **Files modified:** `AuditLogEntryConfiguration.cs`
- **Commit:** ffd34cc

**2. [Rule 1 - Bug] AuditAppendOnlyTests path computation was off by one level**
- **Found during:** Task 1 — `AuditLogEntryConfiguration_HasNoOnDeleteCascadeOrHasOneForeignKey` failed with "file not found"
- **Issue:** Test computed path using 5 `..` traversals from bin/Debug/net10.0 but the actual depth is 6 levels (Backend/tests/TaxReader.UnitTests/bin/Debug/net10.0).
- **Fix:** Changed to 6 `..` segments + `Path.GetFullPath()` for normalization.
- **Files modified:** `AuditAppendOnlyTests.cs`
- **Commit:** ffd34cc

## Known Stubs

None. All audit actions are wired to real production code. `DataExportRequested` and `DataExportDownloaded` enum values are defined for use in Plan 06-04 (ExportUserDataJob).

## Threat Flags

No new network endpoints, auth paths, or file access patterns introduced. Threat register T-06-10 through T-06-13 satisfied:
- T-06-10 (Tampering via Remove/Delete): AuditAppendOnlyTests structural-grep asserts zero Remove/RemoveRange/ExecuteDelete calls targeting AuditLogEntries
- T-06-11 (Repudiation via actor deletion): No FK constraint; nullable actor_user_id; actor reference survives user deletion
- T-06-12 (PII in metadata): SHA-256 email_hash/token_id_hash stored; acceptance criteria confirms no raw email/token in metadata
- T-06-13 (EF cascade wired by convention): No HasOne/WithMany in AuditLogEntryConfiguration; migration has no ForeignKey for actor/subject

## Self-Check: PASSED

All 9 created files exist on disk. Both task commits found in git log:
- `ffd34cc` — Task 1: entity + interface + impl + EF config + migration + DI + tests
- `44d545e` — Task 2: five call-site wirings + new/updated tests

Build: `dotnet build Backend` exits 0 (2 pre-existing NU1510 warnings, 0 errors)
Tests: `dotnet test Backend` exits 0 — 262 passed, 5 skipped, 0 failed
