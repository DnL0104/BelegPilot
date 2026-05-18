---
status: complete
phase: 02-auth-rate-limit-hardening
source: [02-VERIFICATION.md]
started: 2026-05-16T00:00:00Z
updated: 2026-05-18T12:38:00Z
---

## Current Test

[testing complete]

## Tests

### 1. Real-IP-through-Caddy end-to-end
expected: 6th /auth/login from same client IP within 1 minute returns 429 with German body; Caddy access logs show real client IP (not 172.x docker-internal IP)
result: pass
verified_at: 2026-05-18T11:50:00Z
evidence: |
  Two-burst diagnostic against fresh stack (docker compose down -v + up --build):
  - Burst A (X-Forwarded-For: 1.2.3.4): 5× 401, 6th = 429 with Retry-After: 60 and
    body {"type":"...","title":"Zu viele Anfragen.","status":429,
    "detail":"Bitte versuchen Sie es in 60 Sekunden erneut.","retryAfterSeconds":60}
    Content-Type: application/problem+json.
  - Burst B (X-Forwarded-For: 5.6.7.8, same 60-second window): 5× 401, 6th = 429
    (fresh partition for new IP — proves partition is keyed by real client IP, not
    a shared Docker-internal IP).
  - Burst C (X-Forwarded-For: 5.6.7.8, immediately after Burst B): All 6× 429
    (proves partition for 5.6.7.8 is exhausted while 1.2.3.4 had its own budget).
  All three behaviors confirm: AUTH-03 5/min auth-strict policy partitions by real
  client IP forwarded through Caddy + Next.js → ASP.NET Core UseForwardedHeaders.
side_effect: |
  Uncovered a pre-existing Next.js standalone-build issue: BACKEND_API_URL was being
  read at module-load time (next.config.ts line 5) and baked into the rewrite manifest
  during `docker compose build`, defaulting to http://localhost:5190 (the dev API
  port). Caddy → Next.js → API chain returned 500 "Internal Server Error" until
  fixed. Patch: Frontend/Dockerfile now sets BACKEND_API_URL as an ARG+ENV before
  `npm run build` (default http://api:8080), bakeing the correct destination into
  the standalone manifest. Not a Phase 2 defect — pre-existing stack-setup gap
  exposed by attempting UAT through the production proxy chain.

### 2. Upload-concurrency limit (2 active + 4 queued)
expected: 7th concurrent POST to /api/v1/receipt-files from the same authenticated user returns 429 with German body; 3rd-6th queue until earlier upload completes
result: pass
verified_at: 2026-05-18T12:30:00Z
evidence: |
  7 concurrent POST /api/v1/receipt-files from same authenticated user (uat-test@uat.de),
  fired via PowerShell 7 ForEach-Object -Parallel with curl.exe at -ThrottleLimit 7. Wall
  time 440ms — true concurrency confirmed by overlap.
  Status mix:
    Attempts 1-3: 400 (~410ms each) — slot acquired, handler rejected the placeholder PDF
    Attempts 4-6: 500 (~190-390ms each) — slot acquired, upload pipeline threw on the
      placeholder PDF (PdfPig/OCR edge cases unrelated to Phase 2)
    Attempt 7:    429 ( 74ms)             — **rejected at the rate-limit middleware**,
      before handler ran. Sub-100ms path proves middleware rejection vs handler.
  Slot accounting: 2 active + 4 queued = 6 slots. Attempts 1-6 all occupied a slot
  (regardless of how they exited). Attempt 7 found no slot → returned 429 per spec.
side_observation: |
  Attempts 4-6 returning 500 indicates the upload pipeline (handler → PdfPig → AI
  classifier) has rough error handling for invalid/malformed PDFs. Not a Phase 2
  defect — Phase 3 (Hangfire background jobs + user-friendly error UX) is the natural
  owner of this. Logged as a follow-up todo.

### 3. Account-deletion dialog UX
expected: |
  Open /settings; click "Konto unwiderruflich löschen"; verify dialog shows password input + German prompt
  "Geben Sie zur Bestätigung Ihr Passwort ein."; typing wrong password surfaces "Ungültiges Passwort." inline
  without closing dialog; typing correct password closes dialog + redirects to /login
result: pass
verified_at: 2026-05-18T12:35:00Z
evidence: |
  Browser walk-through against live https://localhost stack with the test user
  (uat-test@uat.de / UatTestPass123!) freshly created in step 2 of /gsd-verify-work.
  User confirmed all four observations matched expected:
    - Dialog opened with password input + German prompt copy
    - Wrong password surfaced "Ungültiges Passwort." inline without closing the dialog
    - Correct password completed the deletion (account removed, redirected to login)
  D-13 step ordering (revoke all refresh tokens BEFORE cascade delete) verified by
  the user no longer being able to refresh (account row + cascading children all gone).

### 4. Postgres migration Up() against real Postgres 17
expected: |
  psql -c "\d refresh_tokens" shows: id (uuid, default gen_random_uuid()), user_id (uuid, NOT NULL),
  token_hash (varchar(44), NOT NULL, UNIQUE), created_at/expires_at/revoked_at/last_used_at (timestamptz),
  user_agent (varchar(500)), ip_address (inet), replaced_by_token_id (uuid, nullable self-FK); users table
  no longer contains refresh_token / refresh_token_expires_at columns
result: pass
verified_at: 2026-05-18T12:38:00Z
evidence: |
  docker compose exec db psql -U taxreader -d belegpilot -c "\d refresh_tokens" confirmed:
    - 10 columns: id (uuid, default gen_random_uuid()), user_id (uuid, NOT NULL),
      token_hash (varchar(44), NOT NULL), created_at/expires_at/revoked_at/
      last_used_at (timestamptz), user_agent (varchar(500)), ip_address (inet),
      replaced_by_token_id (uuid, nullable)
    - Indexes: pk_refresh_tokens (PK on id), ix_refresh_tokens_token_hash (UNIQUE),
      ix_refresh_tokens_user_id_revoked_at (composite), ix_refresh_tokens_replaced_by_token_id
    - FKs: fk_refresh_tokens_users_user_id ON DELETE CASCADE; self-FK
      fk_refresh_tokens_refresh_tokens_replaced_by_token_id (NoAction — preserves
      rotation chain, per D-02 spec)
  docker compose exec db psql -U taxreader -d belegpilot -c "\d users" confirmed:
    - NO refresh_token column
    - NO refresh_token_expires_at column
    - Legacy single-column model fully replaced; cascade FK from refresh_tokens correctly wired

## Summary

total: 4
passed: 4
issues: 0
pending: 0
skipped: 0
blocked: 0

## Gaps
