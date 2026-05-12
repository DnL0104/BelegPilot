---
phase: 02
slug: auth-rate-limit-hardening
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-05-12
---

# Phase 02 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit 2.9.2 + FluentAssertions 7.0.0 + Moq 4.20.72 + Microsoft.AspNetCore.Mvc.Testing 10.0.4 (already wired) |
| **Config file** | `Backend/tests/TaxReader.UnitTests/TaxReader.UnitTests.csproj` |
| **Quick run command** | `dotnet test Backend/tests/TaxReader.UnitTests --filter "FullyQualifiedName~RateLimiting|FullyQualifiedName~Auth|FullyQualifiedName~RefreshToken|FullyQualifiedName~DeleteAccount"` |
| **Full suite command** | `dotnet test Backend` |
| **Estimated runtime** | ~30 seconds (quick), ~90 seconds (full) |

---

## Sampling Rate

- **After every task commit:** Run quick command above
- **After every plan wave:** Run `dotnet test Backend`
- **Before `/gsd-verify-work`:** Full suite must be green
- **Max feedback latency:** 30 seconds (quick filter)

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| 02-01-W0a | 01 | 0 | AUTH-01 | — | Test infra ready | helper | `dotnet test Backend --filter "RateLimitTestFactory"` | ❌ W0 | ⬜ pending |
| 02-01-01 | 01 | 1 | AUTH-01 | T-02-03 | HMAC pepper deterministic + key-sensitive | unit | `dotnet test Backend --filter "HmacPepperHashingTests"` | ❌ W0 | ⬜ pending |
| 02-01-02 | 01 | 1 | AUTH-01 | T-02-02 | Rotation happy path: old token rejected, new accepted | integration | `dotnet test Backend --filter "RefreshTokenRotationTests.HappyPath_OldTokenRejected_NewTokenAccepted"` | ❌ W0 | ⬜ pending |
| 02-01-03 | 01 | 1 | AUTH-01 | T-02-02 | Replay detection revokes ALL user tokens + Sentry capture | integration | `dotnet test Backend --filter "ReplayDetectionTests.RevokedTokenPresented_RevokesAllTokens"` | ❌ W0 | ⬜ pending |
| 02-01-04 | 01 | 1 | AUTH-01 | — | Multi-device: two concurrent active tokens validate | integration | `dotnet test Backend --filter "MultiDeviceTokenTests.TwoActiveTokens_BothValidate"` | ❌ W0 | ⬜ pending |
| 02-01-05 | 01 | 1 | AUTH-01 | — | Migration shape: Up() creates table + drops columns | integration | `dotnet test Backend --filter "MigrationTests.Add_RefreshTokens_AndDropLegacy"` (smoke; real Postgres verify deferred to Phase 7 QA-01 Testcontainers) | ❌ W0 | ⬜ pending |
| 02-02-01 | 02 | 2 | AUTH-02 | T-02-04 | Correct password → 204 No Content + cascade delete | integration | `dotnet test Backend --filter "DeleteAccountTests.CorrectPassword_Returns204"` | ❌ W0 | ⬜ pending |
| 02-02-02 | 02 | 2 | AUTH-02 | T-02-04 | Wrong password → 401 + German "Ungültiges Passwort." | integration | `dotnet test Backend --filter "DeleteAccountTests.WrongPassword_Returns401_GermanError"` | ❌ W0 | ⬜ pending |
| 02-02-03 | 02 | 2 | AUTH-02 | T-02-02 | Refresh tokens revoked BEFORE user delete (defense-in-depth) | integration | `dotnet test Backend --filter "DeleteAccountTests.RevokesTokensBeforeDelete"` | ❌ W0 | ⬜ pending |
| 02-03-01 | 03 | 1 | AUTH-03 | T-02-05 | `UseForwardedHeaders` registered FIRST in pipeline | unit | `dotnet test Backend --filter "ForwardedHeadersWiringTests"` | ❌ W0 | ⬜ pending |
| 02-03-02 | 03 | 1 | AUTH-03 | T-02-05 | `KnownIPNetworks` contains Docker bridge `172.16.0.0/12` | unit | `dotnet test Backend --filter "ForwardedHeadersTests.KnownIPNetworksContainsDockerSubnet"` | ❌ W0 | ⬜ pending |
| 02-03-03 | 03 | 2 | AUTH-03 | T-02-01 | `auth-strict` 5/min on /login from one IP (6th = 429) | integration | `dotnet test Backend --filter "AuthStrictPolicyTests.SixthAttempt_Returns429"` | ❌ W0 | ⬜ pending |
| 02-03-04 | 03 | 2 | AUTH-03 | T-02-01 | `auth-strict` on /account partitioned by `sub` (two users get 5 each) | integration | `dotnet test Backend --filter "AuthStrictPolicyTests.TwoUsersOneIp_BothGetFiveAttempts"` | ❌ W0 | ⬜ pending |
| 02-03-05 | 03 | 2 | AUTH-03 | T-02-01 | `auth-refresh` 30/min per IP on /auth/refresh | integration | `dotnet test Backend --filter "AuthRefreshPolicyTests"` | ❌ W0 | ⬜ pending |
| 02-03-06 | 03 | 2 | AUTH-03 | T-02-06 | `upload-concurrency`: concurrency=2, queue=4 (7th = 429) | integration | `dotnet test Backend --filter "UploadConcurrencyPolicyTests"` | ❌ W0 | ⬜ pending |
| 02-03-07 | 03 | 2 | AUTH-03 | T-02-01 | Global 60/min per source IP | integration | `dotnet test Backend --filter "GlobalPolicyTests"` | ❌ W0 | ⬜ pending |
| 02-03-08 | 03 | 2 | AUTH-03 | T-02-07 | 429 response shape: German `application/problem+json` + `Retry-After` | integration | `dotnet test Backend --filter "RejectedResponseShapeTests"` | ❌ W0 | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

Wave 0 installs test scaffolding BEFORE any plan execution. None of the targeted files exist yet — all are stubs that pin behavior for the implementation tasks.

- [ ] `Backend/tests/TaxReader.UnitTests/Helpers/RateLimitTestFactory.cs` — `WebApplicationFactory<Program>` extension with seed user + access token issuance + short test windows (subsecond reset) for rate-limit tests
- [ ] `Backend/tests/TaxReader.UnitTests/Auth/HmacPepperHashingTests.cs` — stubs for AUTH-01 (deterministic, pepper-sensitive)
- [ ] `Backend/tests/TaxReader.UnitTests/Auth/RefreshTokenServiceTests.cs` — stubs for AUTH-01 (issue, validate, rotate happy path)
- [ ] `Backend/tests/TaxReader.UnitTests/Auth/ReplayDetectionTests.cs` — stubs for AUTH-01 (replay → revoke-all + Sentry capture)
- [ ] `Backend/tests/TaxReader.UnitTests/Auth/MultiDeviceTokenTests.cs` — stubs for AUTH-01 (two tokens, same user)
- [ ] `Backend/tests/TaxReader.UnitTests/Auth/MigrationTests.cs` — stub for AUTH-01 migration smoke (in-memory limitations noted)
- [ ] `Backend/tests/TaxReader.UnitTests/Auth/DeleteAccountHandlerTests.cs` — stubs for AUTH-02 (BCrypt verify, token revoke, cascade)
- [ ] `Backend/tests/TaxReader.UnitTests/RateLimiting/AuthStrictPolicyTests.cs` — stubs for AUTH-03 (login/register/account-delete 5/min)
- [ ] `Backend/tests/TaxReader.UnitTests/RateLimiting/AuthRefreshPolicyTests.cs` — stubs for AUTH-03 (refresh 30/min)
- [ ] `Backend/tests/TaxReader.UnitTests/RateLimiting/UploadConcurrencyPolicyTests.cs` — stubs for AUTH-03 (concurrency=2, queue=4)
- [ ] `Backend/tests/TaxReader.UnitTests/RateLimiting/GlobalPolicyTests.cs` — stubs for AUTH-03 (60/min)
- [ ] `Backend/tests/TaxReader.UnitTests/RateLimiting/RejectedResponseShapeTests.cs` — stubs for AUTH-03 (German 429 + Retry-After)
- [ ] `Backend/tests/TaxReader.UnitTests/RateLimiting/ForwardedHeadersTests.cs` — stubs for AUTH-03 (KnownIPNetworks resolution)
- [ ] `Backend/tests/TaxReader.UnitTests/RateLimiting/ForwardedHeadersWiringTests.cs` — source-level structural-grep test (pattern from existing `SerilogEnrichmentTests`)

**No new framework install required** — xUnit + `Microsoft.AspNetCore.Mvc.Testing` + InMemory DB are already in the test project. Tests follow `CorsConfigurationTests.cs` shape.

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Real-IP resolution through Caddy (end-to-end through docker compose) | AUTH-03 | Cannot reproduce reverse-proxy hop inside `WebApplicationFactory` | `docker compose up --build`; `curl -H "X-Forwarded-For: 1.2.3.4" https://localhost/api/v1/auth/login -d '{...}'` six times; verify 6th gets 429 in Caddy logs |
| Postgres migration `Up()` against real Postgres 17 | AUTH-01 | EF InMemory provider does not run Postgres DDL; full DDL verification deferred to Phase 7 QA-01 (Testcontainers) | `docker compose up db`; `dotnet ef database update -p Backend/src/TaxReader.Infrastructure -s Backend/src/TaxReader.Api`; `psql -c '\d refresh_tokens'`; verify columns match D-02 |
| Browser refresh-interceptor handling of replay-revoke (silent bounce to /login) | AUTH-01 D-04 | UX flow involves browser localStorage + axios in-flight retry dedupe | Login in two browsers; revoke browser A's token via DB; refresh browser A; verify silent bounce to /login without flash |
| Frontend dialog: password input replaces CONFIRM phrase | AUTH-02 D-11 | Visual + interaction test | Open `/settings`; click "Konto löschen"; verify dialog shows password input + German "Geben Sie zur Bestätigung Ihr Passwort ein."; verify 401 surfaces inline without closing dialog |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 30s (quick) / 90s (full)
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
