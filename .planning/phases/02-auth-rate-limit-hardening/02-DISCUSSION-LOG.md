# Phase 2: Auth + Rate-Limit Hardening - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions captured in CONTEXT.md — this log preserves the conversation.

**Date:** 2026-05-12
**Phase:** 02-auth-rate-limit-hardening
**Mode:** discuss (default, single-question turns)
**Areas discussed:** Refresh-token storage & replay scope; Rate-limit policy specifics; Account-deletion re-auth UX; Migration & user-session impact

---

## Area Selection

**Q:** Which areas do you want to discuss for Phase 2 (Auth + Rate-Limit Hardening)?

Options presented:
- Refresh-token storage & replay scope
- Rate-limit policy specifics
- Account-deletion re-auth UX
- Migration & user-session impact

**User selected:** All four areas

---

## Area A — Refresh-token storage & replay scope

### Q-A1: Hash algorithm for refresh-token storage

Options presented:
1. **HMAC-SHA256 with server pepper (Recommended)** — new `RefreshToken__HashKey` env var (256-bit); HMAC the random 64-byte token before storage; DB-only leak insufficient for forgery; rotating the pepper invalidates all sessions.
2. Plain SHA-256 — simpler; token has ~512 bits of entropy so rainbow-tables don't apply; weaker against DB-only leak.
3. BCrypt (work factor 10) — overkill; ~100ms BCrypt.Verify on every refresh; no real benefit since tokens are not user-chosen.

**User chose:** Option 1 (HMAC-SHA256 with server pepper)
→ Recorded as **D-01**

### Q-A2: `refresh_tokens` table per-token metadata

Options presented:
1. **AUTH-01 baseline + ip_address (Recommended)** — full schema including `ip_address`, `user_agent`, `replaced_by_token_id`; enables future "Active sessions" UI; DSGVO disclosed via LEG-02.
2. AUTH-01 baseline only, no ip_address — slightly lower PII surface; user_agent alone is borderline-PII.
3. Minimal (no user_agent either) — cleanest DSGVO posture; forensics fall back to Serilog logs; "Active sessions" UI later harder.

**User chose:** Option 1 (AUTH-01 baseline + ip_address)
→ Recorded as **D-02**

### Q-A3: User-facing surface for replay-detection-triggered "log out everywhere"

Options presented:
1. **Silent revoke + generic re-login (Recommended)** — 401 on next refresh; frontend interceptor redirects to /login like any expiry; no special message; doesn't leak detection-fired signal.
2. Silent revoke + German flash on /login — specific error code → flash ("Aus Sicherheitsgründen wurden Sie auf allen Geräten abgemeldet"); informs legitimate user but leaks detection signal to attacker.
3. Silent + email notification — requires email-sending dependency not in stack; defers the surface to a later phase.

**User chose:** Option 1 (Silent revoke + generic re-login)
→ Recorded as **D-04** (D-03 = spec-locked "revoke ALL user's tokens" per AUTH-01 SC #2)

---

## Area B — Rate-limit policy specifics

### Q-B1: `/auth/refresh` partition strategy (no JWT identity available)

Options presented:
1. **Per source IP, 30 req/min (Recommended)** — stateless; doesn't query DB; two NAT'd users share a bucket (acceptable at 100–500 user scale).
2. Per refresh-token hash — precise per-session; CPU-cheap HMAC per request; attacker can blow up partition cache with random tokens.
3. Per refresh-token hash, fall back to per-IP for malformed/missing — best precision but most complex; custom partition factory needed.

**User chose:** Option 1 (Per source IP)
→ Recorded as **D-05**

### Q-B2: Real-client-IP resolution behind Caddy

Options presented:
1. **UseForwardedHeaders + trust the Docker subnet (Recommended)** — `UseForwardedHeaders` early in pipeline; `KnownNetworks.Add(172.16.0.0/12)` to trust Docker bridge ranges only; Caddy already sets `X-Forwarded-For`.
2. Trust ANY proxy (KnownProxies empty + ForwardLimit null) — faster wire but spoofable if API port ever exposed; not recommended.
3. Custom `X-Real-IP` Caddy header + manual parse — bypasses forwarded-headers machinery; more moving parts; no real upside.

**User chose:** Option 1 (UseForwardedHeaders + KnownNetworks)
→ Recorded as **D-06**

### Q-B3: `/receipt-files` upload over-limit behavior

Options presented:
1. **Queue with short wait, then 429 (Recommended)** — concurrency=2 + QueueLimit=4 + OldestFirst + ~30s wait; double-clicked uploads queue rather than fail; abuse stays bounded; Phase 3 (PIPE-02) retires this.
2. Reject immediately with 429 — concurrency=2 + QueueLimit=0; matches AUTH-03 literal reading; double-click on slow connection gives undeserved 429.
3. Per-FILE concurrency-2 inside the handler via SemaphoreSlim — skips AddRateLimiter; misses Phase 2 hardening surface (no 429, no Retry-After).

**User chose:** Option 1 (Queue + short wait + 429)
→ Recorded as **D-07**

---

## Area C — Account-deletion re-auth UX

### Q-C1: Re-auth UX shape

Options presented:
1. **Replace typed-phrase with password input (Recommended)** — single input; clean UX; exactly satisfies AUTH-02 SC #4.
2. Keep typed-phrase AND add password (two-step) — defense-in-depth against accidental click-and-paste; harder to test.
3. Password-only via separate /auth/reauthenticate endpoint — architectural overhead for single-use; useful only if other sensitive-op endpoints will reuse later.

**User chose:** Option 1 (Replace typed-phrase with password input)
→ Recorded as **D-10, D-11**

### Q-C2: Wrong-password handling on DELETE /auth/account

Options presented:
1. **401 with inline German error in dialog (Recommended)** — `{ error: "Ungültiges Passwort." }`; dialog stays open; endpoint joins AUTH-03's brute-force-resistant set (5/min/IP same as /auth/login).
2. 401 + redirect to /login — heavier UX (two logins in 30 seconds); typo more likely than stolen-access-token scenario.
3. Generic 400 to obscure success/failure — leaks-prevention; counter-intuitive UX for legitimate user.

**User chose:** Option 1 (401 + inline German)
→ Recorded as **D-12, D-13** (pre-deletion order of operations: verify → revoke refresh tokens → CASCADE delete)

---

## Area D — Migration & user-session impact

### Q-D1: Migration approach for `users.refresh_token` → `refresh_tokens` table

Options presented:
1. **Drop columns in same migration — force re-login (Recommended)** — one EF migration creates table + drops legacy columns; pre-launch milestone so only dev's own session is the victim; no dual-write code.
2. Keep columns for one release, dual-write — existing sessions stay valid; legacy plaintext column persists during dual-write window.
3. Keep columns, mark deprecated, no dual-write — every existing user gets bounced anyway (token not migrated); worst of both worlds.

**User chose:** Option 1 (Drop columns in same migration)
→ Recorded as **D-15**

### Q-D2: Expired-token cleanup approach for Phase 2

Options presented:
1. **Defer to Phase 3 (Recommended)** — PIPE-01 already lists "recurring cleanup jobs registered"; 4–6 weeks of growth at 100–500 users with 30-day TTL is trivial for Postgres.
2. Opportunistic cleanup inside RefreshTokenService — fire-and-forget DELETE per refresh, scoped to user_id; cost: extra DELETE per refresh; dead tokens persist for inactive users.
3. BackgroundService in API process — `IHostedService` every 6h; works but duplicates Phase 3 effort.

**User chose:** Option 1 (Defer to Phase 3)
→ Recorded as **D-16**

---

## Claude's Discretion (recorded in CONTEXT.md `<decisions>` section)

- Exact `RefreshTokenService` API surface (`IssueAsync`, `ValidateAndRotateAsync`, `RevokeAllForUserAsync`)
- `OnRejected` callback implementation details (Stream-write inside limiter middleware)
- ProblemDetails extension fields beyond `title`/`detail`/`status`
- BCrypt work factor (stay at default 10 unless security review flags)
- Middleware-attached vs per-endpoint `.RequireRateLimiting("policy-name")` (likely per-endpoint for clarity)
- Frontend axios `deleteAccount` body-on-DELETE serialization details
- Caddy `KnownNetworks` exact subnet list (`172.16.0.0/12` covers Docker default bridges)
- Whether to log a structured Information event on every successful refresh rotation (probably yes for Sentry baselining)

---

## Deferred Ideas (carried to `<deferred>` in CONTEXT.md)

- Active-sessions UI (`GET /auth/sessions`, "log out everywhere")
- Email notification on replay detection
- Audit-log entries for account deletion + refresh-token revocation (Phase 6 LEG-08)
- Pepper rotation procedure documentation
- BCrypt work-factor tuning beyond library default 10
- `/webhooks/stripe` rate limit (Phase 5)
- W3C `traceparent` browser → backend trace propagation
- Refresh-token pepper stored in secret manager
- Per-route concurrency limit on `POST /receipts/{id}/reclassify`
- Password-reuse detection (haveibeenpwned)
- OAuth / social login

---

*Discussion log preserved for audit: 2026-05-12*
