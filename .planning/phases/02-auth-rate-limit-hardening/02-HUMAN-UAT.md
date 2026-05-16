---
status: partial
phase: 02-auth-rate-limit-hardening
source: [02-VERIFICATION.md]
started: 2026-05-16T00:00:00Z
updated: 2026-05-16T00:00:00Z
---

## Current Test

[awaiting human testing]

## Tests

### 1. Real-IP-through-Caddy end-to-end
expected: 6th /auth/login from same client IP within 1 minute returns 429 with German body; Caddy access logs show real client IP (not 172.x docker-internal IP)
result: [pending]
notes: |
  Reverse-proxy hop cannot be simulated in WebApplicationFactory in-process (intentional [Fact(Skip)] on
  XForwardedFor_TrustedSubnet_ResolvesRealIp). Reproduce with:
    docker compose up --build
    for i in $(seq 1 6); do
      curl -i -H 'X-Forwarded-For: 1.2.3.4' \
        -H 'Content-Type: application/json' \
        -d '{"email":"x@y.de","password":"wrong"}' \
        https://localhost/api/v1/auth/login
    done
  Expect: first 5 → 401, 6th → 429 (German "Zu viele Anfragen.", Retry-After header).

### 2. Upload-concurrency limit (2 active + 4 queued)
expected: 7th concurrent POST to /api/v1/receipt-files from the same authenticated user returns 429 with German body; 3rd-6th queue until earlier upload completes
result: [pending]
notes: |
  WebApplicationFactory in-process timing is unreliable for concurrency-limiter assertions (2x intentional
  [Fact(Skip)] in UploadConcurrencyPolicyTests). Reproduce with real concurrent uploads against a running
  stack — see VALIDATION.md Manual-Only Verifications row 2.

### 3. Account-deletion dialog UX
expected: |
  Open /settings; click "Konto unwiderruflich löschen"; verify dialog shows password input + German prompt
  "Geben Sie zur Bestätigung Ihr Passwort ein."; typing wrong password surfaces "Ungültiges Passwort." inline
  without closing dialog; typing correct password closes dialog + redirects to /login
result: [pending]
notes: |
  Visual + interaction flow — automated component tests not in scope until Phase 7 QA-02 (Vitest). Per
  VALIDATION.md Manual-Only Verifications row 4.

### 4. Postgres migration Up() against real Postgres 17
expected: |
  psql -c "\d refresh_tokens" shows: id (uuid, default gen_random_uuid()), user_id (uuid, NOT NULL),
  token_hash (varchar(44), NOT NULL, UNIQUE), created_at/expires_at/revoked_at/last_used_at (timestamptz),
  user_agent (varchar(500)), ip_address (inet), replaced_by_token_id (uuid, nullable self-FK); users table
  no longer contains refresh_token / refresh_token_expires_at columns
result: [pending]
notes: |
  EF InMemory provider cannot run Postgres DDL — MigrationTests.cs is an explicit skip. Real
  Postgres-backed migration verification deferred to Phase 7 QA-01 (Testcontainers), but operator should
  run once now via `docker compose up db` + `dotnet ef database update` before merging.

## Summary

total: 4
passed: 0
issues: 0
pending: 4
skipped: 0
blocked: 0

## Gaps
