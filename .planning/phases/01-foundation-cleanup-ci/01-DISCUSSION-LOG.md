# Phase 1: Foundation Cleanup + CI - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions captured in `01-CONTEXT.md` — this log preserves the conversation that produced them.

**Date:** 2026-05-04
**Phase:** 01-foundation-cleanup-ci
**Mode:** discuss (interactive, default)
**Areas selected:** Anthropic model alignment, CI workflow design, Sentry tier + PII scrubbing + alerts, Correlation ID strategy

---

## Area 1: Anthropic model alignment

### Q1.1 — Which Anthropic model becomes the single documented production default?

**Options presented:**
| Option | Tradeoff |
|---|---|
| Haiku 4.5 (Recommended) | ~10× cheaper, ~3-5× faster than Sonnet; sufficient for 13-category classification per existing `AnthropicOptions.cs` comment; keeps token-economy margin generous |
| Sonnet 4.5 | More accurate; protects Core Value at higher cost; pass-through token economy absorbs cost |
| Opus 4.7 | Top-tier accuracy at ~5× Sonnet cost; overkill for receipt classification |
| Per-environment split | Haiku in dev/test, Sonnet in prod; reintroduces the very drift this requirement is meant to fix |

**User selected:** Haiku 4.5 (Recommended) → **D-01**

### Q1.2 — How do we lock and document the Haiku 4.5 default to prevent the mismatch from coming back?

**Options presented:**
| Option | Tradeoff |
|---|---|
| Single source + startup-log (Recommended) | Code default is source of truth; compose + .env.example align; CLAUDE.md documents; startup logs resolved model — lightweight, easy to reason about |
| Startup-time hard guard | Throw on startup if model not in allow-list; catches typos but adds friction when testing new models |
| Strip the code default entirely | Make `Anthropic__Model` required in config; throw at startup if missing — slightly stricter |

**User selected:** Single source + startup-log (Recommended) → **D-02, D-03**

---

## Area 2: CI workflow design

### Q2.1 — How should the GitHub Actions CI workflow be structured?

**Options presented:**
| Option | Tradeoff |
|---|---|
| Single workflow, parallel jobs (Recommended) | One `ci.yml` with parallel jobs (`backend-build-test`, `frontend-lint-build`); built-in caching; clear status checks for branch protection |
| Split per-stack workflows | Separate `backend-ci.yml` + `frontend-ci.yml` with `paths:` filters; faster individual runs but less consistent PR signal |
| Matrix across .NET / Node versions | .NET 10 × Node 22 LTS only; useful when stack starts moving; small overhead now |

**User selected:** Single workflow, parallel jobs (Recommended) → **D-08**

### Q2.2 — What backend test scope runs in CI today, given Phase 7 will add Postgres integration tests?

**Options presented:**
| Option | Tradeoff |
|---|---|
| Only existing unit tests (Recommended) | Run `dotnet test Backend` against `TaxReader.UnitTests` (in-memory EF); Phase 7 adds Testcontainers later in a separate job |
| Add Postgres service now | `services: postgres:17-alpine` in CI without integration tests yet; pre-wires Phase 7 at ~30s per-run cost |
| Skip backend tests entirely until Phase 7 | Only `dotnet build`; faster but loses regression signal; contradicts FND-04 explicit `dotnet test` mention |

**User selected:** Only existing unit tests (Recommended) → **D-09**

### Q2.3 — How strict should branch protection on `main` be?

**Options presented:**
| Option | Tradeoff |
|---|---|
| All CI checks required, solo-friendly (Recommended) | Required status checks; no required reviewers (solo dev); no signed commits; PRs only |
| Strict: required reviewer + signed commits + linear history | Better posture; slower for solo work (self-review trick) |
| Loose: required checks only, allow direct push | Defeats FND-04 — direct push to main bypasses CI |

**User selected:** All CI checks required, solo-friendly (Recommended) → **D-10**

### Q2.4 — How do we enforce the 'no `storage/` or `build-diag.txt` in working tree' guarantee (Success Criterion #2)?

**Options presented:**
| Option | Tradeoff |
|---|---|
| Dedicated CI hygiene step (Recommended) | `hygiene-check` job fails build if forbidden paths exist; belt-and-suspenders alongside `.gitignore`; satisfies "CI fails if reintroduced" |
| Rely on .gitignore only | Trust contributors not to `git add -f`; lighter setup, weaker guarantee |
| Pre-commit hook (husky/lefthook) | Local enforcement; bypassable with `--no-verify`; adds per-machine setup |

**User selected:** Dedicated CI hygiene step (Recommended) → **D-06**

---

## Area 3: Sentry tier + PII scrubbing + alerts

### Q3.1 — Which Sentry plan/tier do we provision?

**Options presented:**
| Option | Tradeoff |
|---|---|
| Developer Free — EU region (Recommended) | 5k errors / 10k perf units; sufficient for 100–500 user target; no cost; one-click upgrade |
| Team Plan — EU region (~$26/mo) | 50k errors / 100k perf units; dashboards; premature pre-revenue |
| Self-hosted Sentry on the same Docker stack | Avoids vendor cost; full sovereignty; ~5 extra services balloon ops surface |

**User selected:** Developer Free — EU region (Recommended) → **D-13**

### Q3.2 — How aggressive should PII scrubbing be in `BeforeSend` / `BeforeSendTransaction`?

**Options presented:**
| Option | Tradeoff |
|---|---|
| Default-deny + small allow-list (Recommended) | Strip request bodies, query strings (except `page`/`pageSize`/`year`/`format`), HTTP headers (except `User-Agent`); mask UUID path segments; strip emails; keep user ID hash; strip raw receipt content; strongest GDPR posture |
| Targeted scrubbing of known-sensitive fields | Allow most context through; explicitly scrub known fields; easier to debug; relies on never missing a sensitive field |
| Sentry's built-in data scrubbers only | Simplest; insufficient for receipt content + email under DSGVO |

**User selected:** Default-deny + small allow-list (Recommended) → **D-14**

### Q3.3 — Where do Sentry alerts route, and how loud are they (Phase 1 starting baseline — QA-06 in Phase 7 will tune against real traffic)?

**Options presented:**
| Option | Tradeoff |
|---|---|
| Email only — quiet baseline (Recommended) | Two rules: (a) new-error type with 1h cooldown, (b) sustained-rate ≥10 events/min for ≥5 min; no page-on-first-error; matches "paging-style" expectation without noise |
| Email + Slack channel | Add #alerts via Slack integration; permanent timeline; one extra dep |
| Email + PagerDuty | True paging with on-call escalation; overkill at pre-revenue scale |

**User selected:** Email only — quiet baseline (Recommended) → **D-15**

### Q3.4 — When does Sentry get gated by user consent? Phase 6 will add the TTDSG cookie banner, but Phase 1 ships Sentry first.

**Options presented:**
| Option | Tradeoff |
|---|---|
| Frontend Sentry off until consent lands (Recommended) | Backend Sentry runs unconditionally (server-internal); frontend `Sentry.init` disabled until Phase 6; no DSGVO risk before launch |
| Frontend Sentry on, gated by a temporary 'pre-launch' flag | Init behind hardcoded `enabled = false` until Phase 6 wires real flag; tiny premature-flip risk |
| Frontend Sentry on, scoped to staff-only IPs | Whitelisted dev IPs/sessions; useful for catching frontend errors but no production users yet |

**User selected:** Frontend Sentry off until consent lands (Recommended) → **D-16**

---

## Area 4: Correlation ID strategy

### Q4.1 — How far should correlation flow — backend-internal only, or end-to-end across browser → backend (and later → background job)?

**Options presented:**
| Option | Tradeoff |
|---|---|
| Backend-internal correlation (Recommended) | Serilog enrichers + `LogContext.PushProperty("ReceiptFileId", id)` in long-running handler; ASP.NET Core `RequestId` already attached by `UseSerilogRequestLogging`; matches OBS-02 wording |
| End-to-end W3C trace context (traceparent) | Frontend axios mints `traceparent`; backend reads via `Activity.Current`; same trace ID flows to Sentry tags; best diagnostic story but value delayed (frontend Sentry is off until Phase 6) |
| Custom `X-Correlation-Id` header from frontend | Frontend axios mints UUID; backend reads/generates; simpler than W3C; redundant if Phase 7 later adds OpenTelemetry |

**User selected:** Backend-internal correlation (Recommended) → **D-17**

### Q4.2 — What identifiers get pushed into the `LogContext` for long-running handlers?

**Options presented:**
| Option | Tradeoff |
|---|---|
| ReceiptFileId now, JobId in Phase 3 (Recommended) | OBS-02 requires `ReceiptFileId` correlation in `UploadReceiptFilesHandler` today; Phase 3's `ProcessReceiptFileJob` adds `JobId` later; no speculative code |
| Both fields wired now (JobId optional) | Push both now; `JobId` empty until Phase 3 populates; pre-wires pattern; slight scope creep |
| Add UserId too | `UserId` valuable for cross-request debugging; expands PII surface; conflicts with default-deny Sentry posture; would need to be a hash |

**User selected:** ReceiptFileId now, JobId in Phase 3 (Recommended) → **D-18**

### Q4.3 — Where does the correlation ID surface beyond the log line?

**Options presented:**
| Option | Tradeoff |
|---|---|
| Serilog only (Recommended) | `{ReceiptFileId}` and `{RequestId}` in every log line within handler scope; satisfies OBS-02 + Success Criterion #5; no HTTP header changes, no Sentry tag wiring |
| Serilog + Sentry tags | Push `ReceiptFileId` as Sentry tag; easy filter in Sentry UI; small win for backend-only Sentry |
| Serilog + Sentry tags + HTTP `X-Request-Id` response header | Echo to browser; precise user-reported failure IDs; overkill for solo-dev support workflow |

**User selected:** Serilog only (Recommended) → **D-19**

---

## Claude's Discretion (no question asked)

Items where Claude was given latitude or where the recommended option was clear enough not to require user confirmation:

- **README language and depth** — Defaulted to English brief docs (matches code/comments) per FND-05 wording. Captured as **D-12**.
- **Storage cleanup specifics** — `Backend/src/TaxReader.Api/storage/` deletion + `.gitignore` extension covering the actually-affected paths. Captured as **D-04, D-05**.
- **CORS production fail-mode** — Deny-all policy + warn-log when `CORS_ALLOWED_ORIGINS` is unset in non-Development. Captured as **D-07**.
- **CI secret handling** — No CI secrets needed in Phase 1; deferred to Phase 7 when integration tests need them. Captured as **D-11**.
- **Container/DB rebrand from `belegpilot-*`** — Out of scope for Phase 1; deferred (cosmetic + DB rename has migration cost).

---

## Deferred ideas raised during discussion

- W3C `traceparent` browser → backend tracing — possible Phase 6/7 follow-up
- Sentry Slack / PagerDuty integration — reconsider after Phase 7 alert tuning
- OpenTelemetry / distributed tracing — out of scope at current scale
- macOS/Linux `start.sh` / `stop.sh` — backlog (CONCERNS.md #19)
- Container rebrand `belegpilot-*` → `taxreader-*` — backlog
- Sentry release tagging + source maps — Phase 7 polish
- Sentry Performance / session replay — out of scope (cost + PII surface)

---

*Discussion log captured: 2026-05-04*
