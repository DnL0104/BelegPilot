---
phase: 01-foundation-cleanup-ci
plan: 03
subsystem: observability
tags: [sentry, observability, pii-scrubbing, eu-residency, dotnet, nextjs, gdpr]

requires:
  - phase: 01-foundation-cleanup-ci/01
    provides: Microsoft.AspNetCore.Mvc.Testing test rig + clean baseline (100/100); Anthropic startup canary as the precedent for typed-options visibility
  - phase: 01-foundation-cleanup-ci/04
    provides: Serilog enricher pipeline (FromLogContext + WithEnvironmentName) + 103/103 baseline; the new Sentry events flow through the same logging surface (EnvironmentName lands on every Sentry-routed event by transitivity)
provides:
  - Backend Sentry capture via Sentry.AspNetCore 6.4.1 (init is the FIRST builder registration, Pitfall 1)
  - PII scrubber (SentryScrubbing.Scrub) enforcing all six D-14 rules with active Extra-key allow-list (D-14 #6 defence in depth)
  - Empty-DSN no-op behaviour (Phase 1 default — operator fills SENTRY_DSN_BACKEND when ready)
  - Frontend @sentry/nextjs 10.52.0 scaffolded via Next.js 16 file convention (instrumentation-client.ts at root); Sentry.init gated on NEXT_PUBLIC_SENTRY_ENABLED === "true" — Phase 1 keeps it OFF (D-16, awaits Phase 6 TTDSG cookie banner)
  - Conditional withSentryConfig wrap in next.config.ts (Pitfall 6) — production builds work without SENTRY_ORG/SENTRY_PROJECT in Phase 1 CI
  - 10 new unit tests (`SentryScrubbingTests`) guarding each D-14 rule + a kitchen-sink integration test
affects: [01-02, 02-*, 03-*, 06-*, 07-*]

tech-stack:
  added:
    - Sentry 6.4.1 (TaxReader.Infrastructure direct ref; Sentry.Extensions.Logging 6.4.1 flows transitively)
    - Sentry.AspNetCore 6.4.1 (TaxReader.Api direct ref)
    - "@sentry/nextjs" 10.52.0 (Frontend; resolved latest stable at install time)
    - "Microsoft.AspNetCore.App" FrameworkReference on TaxReader.Infrastructure (lets the scrubber use Microsoft.AspNetCore.WebUtilities.QueryHelpers without depending on Sentry.AspNetCore in the lower layer)
  patterns:
    - "BeforeSend scrubber with allow-list-based default-deny posture (request body, query keys, headers, Extra keys, user identifiers) — the active enforcement for D-14 #6 means future Sentry.SetExtra('vendor', ...) call sites cannot leak receipt content"
    - "Conditional withSentryConfig wrap in next.config.ts — Sentry build-plugin runs only when the runtime flag is on, so absent/incomplete Sentry env vars don't break Phase 1 CI"
    - "Sentry init as the FIRST WebHost registration after CreateBuilder (Pitfall 1) — DI-time exceptions reach Sentry"

key-files:
  created:
    - Backend/src/TaxReader.Infrastructure/Observability/SentryScrubbing.cs
    - Backend/tests/TaxReader.UnitTests/Observability/SentryScrubbingTests.cs
    - Frontend/instrumentation-client.ts
    - Frontend/instrumentation.ts
    - Frontend/sentry.server.config.ts
    - Frontend/sentry.edge.config.ts
    - .planning/phases/01-foundation-cleanup-ci/01-03-SUMMARY.md
  modified:
    - Backend/Directory.Packages.props
    - Backend/src/TaxReader.Api/TaxReader.Api.csproj
    - Backend/src/TaxReader.Infrastructure/TaxReader.Infrastructure.csproj
    - Backend/src/TaxReader.Api/Program.cs
    - Backend/src/TaxReader.Api/appsettings.json
    - docker-compose.yml
    - .env.example
    - Frontend/package.json
    - Frontend/package-lock.json
    - Frontend/next.config.ts

key-decisions:
  - "HashUserId is `public` (not `internal`) on SentryScrubbing — preferred surgical option (a) from the plan over `[InternalsVisibleTo(\"TaxReader.UnitTests\")]` (option b). Pure helper, no behavioural risk to elevating visibility."
  - "Scrubber lives at Backend/src/TaxReader.Infrastructure/Observability/SentryScrubbing.cs (not Api/Observability) — matches the architectural rule \"Infrastructure implements external concerns\" and the OcrTextNormalizer.cs analog. Cost: TaxReader.Infrastructure now references both Sentry (PackageReference) and Microsoft.AspNetCore.App (FrameworkReference, for QueryHelpers). The latter is the standard way for a class library to use ASP.NET Core types."
  - "@sentry/nextjs ^10.52.0 (latest stable at install) instead of plan's `^10.51.0` — pinned to current stable per RESEARCH.md guidance; major-version pinning policy honoured."
  - "Frontend Sentry intentionally OFF in Phase 1 (D-16). Bundle-weight cost of ~50KB accepted for optionality; runtime PII transmission is zero with the gate off."
  - "withSentryConfig is conditionally applied (Pitfall 6) — when NEXT_PUBLIC_SENTRY_ENABLED ≠ \"true\", the export is the bare nextConfig. This means Phase 1 CI does not need SENTRY_ORG/SENTRY_PROJECT to be set for `npm run build` to succeed."

patterns-established:
  - "Default-deny PII scrubber: allow-list everything that may travel to a third-party SaaS (queries, headers, Extras), strip the rest before send. The hash-then-drop pattern for User.Id (`Other[\"id_hash\"]` 16-char SHA-256 prefix) preserves cross-event correlation without re-identification."
  - "Sentry config canary (deferred): the same IOptions<T>-resolved-at-startup canary pattern from 01-01's Anthropic config could be applied to log the Sentry environment + DSN-set status at boot — not done in this plan because the empty-DSN smoke check already proves no-op, and the Sentry SDK itself logs an init-suppressed message when DSN is empty."

requirements-completed: [OBS-01]

duration: 11min
completed: 2026-05-10
---

# Phase 1 Plan 03: Sentry SDK + PII Scrubbing Summary

**Wired backend Sentry.AspNetCore 6.4.1 with `UseSentry` as the FIRST builder registration (Pitfall 1) and a six-rule D-14 PII scrubber that actively wipes Extra keys not in a small allow-list; scaffolded frontend @sentry/nextjs 10.52.0 using the Next.js 16 file convention (`instrumentation-client.ts`, NOT the deprecated `sentry.client.config.ts`) and gated the init off until Phase 6's TTDSG cookie banner lands; conditional `withSentryConfig` keeps the production build clean without Sentry env vars in Phase 1; 10 new tests pass; full backend suite 113/113 green.**

## Performance

- **Duration:** 11 min
- **Started:** 2026-05-10T10:51:47Z
- **Completed:** 2026-05-10T11:02:33Z
- **Tasks:** 2
- **Files touched:** 17 (7 new + 10 modified)
- **Test delta:** 103 → 113 backend tests, all green

## Accomplishments

- **Backend Sentry init (Pitfall 1):** `builder.WebHost.UseSentry(...)` is the FIRST registration after `WebApplication.CreateBuilder(args);` so DI-time exceptions reach Sentry. Configured `SetBeforeSend` (not the deprecated `BeforeSend` property) wired to `SentryScrubbing.Scrub`. Defence in depth: `MaxRequestBodySize = RequestSize.None`, `SendDefaultPii = false`. DSN binds automatically from `Sentry__Dsn` env var or the `"Sentry": { "Dsn": "" }` block in appsettings.json. Empty DSN = no-op (Phase 1 default), confirmed by smoke test (`SENTRY_DSN_BACKEND= dotnet run` starts cleanly with no Sentry exception or warning).

- **PII scrubber (D-14):** `Backend/src/TaxReader.Infrastructure/Observability/SentryScrubbing.cs` enforces all six rules:
  1. `Request.Data` → null (request body)
  2. `Request.QueryString` → allow-list `{page, pageSize, year, format}` via `QueryHelpers.ParseQuery`
  3. `Request.Headers` → allow-list `{User-Agent}` (case-insensitive)
  4. `Request.Url` UUID segments → `:id` via `[GeneratedRegex]` with `RegexOptions.IgnoreCase`
  5. `User.Email/Username/IpAddress/Id` → null; `User.Other["id_hash"]` set to 16-char SHA-256 hex prefix (cross-event correlation without re-identification)
  6. `Extra` → keys not in `{receipt_id, processing_run_id, request_id, job_id, phase}` are wiped (active defence-in-depth — future `Sentry.SetExtra("vendor", ...)` cannot leak receipt content)

- **Backend test coverage:** `SentryScrubbingTests.cs` provides 7 facts + 4 theory cases = 10 distinct test cases, one per D-14 rule + a kitchen-sink integration that asserts every rule fires on a single multi-field event. Hash determinism asserted via `result.User.Other["id_hash"].Should().Be(SentryScrubbing.HashUserId("user-id-1234"))`.

- **Backend config plumbing:** `appsettings.json` gets a top-level `"Sentry": { "Dsn": "" }` block (between Serilog and Tesseract). `docker-compose.yml` `api.environment` gets `Sentry__Dsn: ${SENTRY_DSN_BACKEND:-}` and `Sentry__Environment: production`; `web.environment` gets `NEXT_PUBLIC_SENTRY_ENABLED: ${NEXT_PUBLIC_SENTRY_ENABLED:-false}` and `NEXT_PUBLIC_SENTRY_DSN: ${NEXT_PUBLIC_SENTRY_DSN:-}`. `.env.example` appends a documented `# ── Sentry ──` block.

- **Frontend Sentry scaffolding (Next.js 16 convention):**
  - `Frontend/instrumentation-client.ts` (NOT the deprecated `sentry.client.config.ts`) — browser init gated on `NEXT_PUBLIC_SENTRY_ENABLED === "true"`. Exports `onRouterTransitionStart = Sentry.captureRouterTransitionStart` (required by Next.js 16 per `node_modules/next/dist/docs/01-app/03-api-reference/03-file-conventions/instrumentation-client.md`). Includes a client-side `beforeSend` mirroring backend scrubber rules: drop `request.data`, allow-list query string + headers, mask UUID URL segments, drop `user.email/username/ip_address/id` outright (no async hashing on client).
  - `Frontend/instrumentation.ts` — Next.js 16 server runtime hook; `register()` switches on `process.env.NEXT_RUNTIME` to dynamically import `./sentry.server.config` (nodejs) or `./sentry.edge.config` (edge); exports `onRequestError = Sentry.captureRequestError`.
  - `Frontend/sentry.server.config.ts` + `Frontend/sentry.edge.config.ts` — both gated on private (non-`NEXT_PUBLIC_`) DSN env vars per Pitfall 7. Each ends with `export {}` to ensure ES-module shape for the dynamic import.

- **Frontend conditional Sentry build wrap (Pitfall 6):** `next.config.ts` imports `withSentryConfig` and conditionally wraps the export only when `NEXT_PUBLIC_SENTRY_ENABLED === "true"`. With the flag unset, the bare `nextConfig` is exported — production builds succeed without `SENTRY_ORG/SENTRY_PROJECT/authToken`. Existing `nextConfig` shape (`output: "standalone"`, `allowedDevOrigins`, `/api/v1/*` rewrite block) preserved verbatim per surgical-changes rule.

- **Test deltas:** 103 → 113 backend tests (10 new in `SentryScrubbingTests`, all pass). Frontend `npm run build` succeeds in 2.7s (12/12 pages generated, TypeScript passes); `npx eslint` on the 5 Sentry-related files reports zero lint errors.

## Task Commits

1. **Task 1 — Backend Sentry init + PII scrubber + 10 tests + config plumbing** — `fc538af` (feat)
2. **Task 2 — Frontend Sentry SDK files (Next.js 16 convention) + conditional withSentryConfig** — `4896ea0` (feat)

_Note: Per-task `tdd="true"` markers were honoured at task granularity. Task 1's tests were authored from the plan template before the scrubber implementation was wired (RED was implicit — the test file imports `TaxReader.Infrastructure.Observability.SentryScrubbing` which didn't compile until the source file landed in the same edit). Atomic commit per execute-plan.md protocol when no plan-level `type: tdd` gate is set; same pattern as 01-01 and 01-04. Task 2 has no test artifact (Frontend has no test framework configured per CLAUDE.md) — verification is `npm run lint` + `npm run build` smoke checks._

## Resolved Versions

- **Sentry (.NET):** 6.4.1 (resolved direct + transitive `Sentry.Extensions.Logging` 6.4.1 — verified via `dotnet list Backend/src/TaxReader.Api package --include-transitive`)
- **Sentry.AspNetCore:** 6.4.1
- **@sentry/nextjs:** 10.52.0 (latest stable at install time; plan permitted `^10.51.0`, npm resolved 10.52.0 within the caret range)

## DSN-set state for first deploy

Both DSN environment variables (`SENTRY_DSN_BACKEND`, `NEXT_PUBLIC_SENTRY_DSN`) are EMPTY by design in Phase 1:
- Backend: SDK is a no-op when DSN empty (verified via smoke test — `SENTRY_DSN_BACKEND= dotnet run` boots cleanly, no Sentry exception or warning)
- Frontend: `NEXT_PUBLIC_SENTRY_ENABLED` defaults to `false` in `docker-compose.yml`, so `Sentry.init` is gated off regardless of DSN
- The operator fills both via `.env` (template documented in `.env.example`) when ready to flip on capture

## Conditional withSentryConfig outcome

The conditional `withSentryConfig` worked on first `npm run build` — no Pitfall 6 fallback (env-var-always-set workaround) was needed. With `NEXT_PUBLIC_SENTRY_ENABLED` unset at build time, the export is the bare `nextConfig` and the Sentry build plugin never runs, so missing `SENTRY_ORG/SENTRY_PROJECT` does not error.

## HashUserId visibility decision

`SentryScrubbing.HashUserId` is **public** (option (a) from the plan), not `internal` with `[InternalsVisibleTo("TaxReader.UnitTests")]`. Rationale: the helper is a pure deterministic function with zero behavioural risk to widening visibility; the surgical-changes rule prefers fewer assembly-attribute additions; the test asserts hash determinism by re-using the production helper directly (`result!.User.Other!["id_hash"].Should().Be(SentryScrubbing.HashUserId("user-id-1234"))`).

## Sentry account / project setup status

**Manual operator step (deferred — out of plan execution scope).** The plan's `user_setup` block documents:

- Create Sentry organisation on the EU region (`sentry.eu.io`) — Developer Free tier covers 5k errors/month per D-13
- Create two projects: `taxreader-api` (.NET) and `taxreader-web` (Next.js)
- Set the two D-15 alert rules: (a) new-error-type 1h cooldown, (b) sustained rate ≥ 10 events/min for ≥ 5 min
- Disable the default `Send a notification for new issues` rule (D-15 instruction)
- Verify only Email is configured as a delivery channel (no Slack/PagerDuty/Discord)
- Record both DSNs and add them to `.env`

The code is ready to receive a DSN — once the operator runs the above and populates `.env`, capture begins immediately for the backend; frontend stays dormant until Phase 6's cookie-banner work flips `NEXT_PUBLIC_SENTRY_ENABLED=true`.

## Files Created

- `Backend/src/TaxReader.Infrastructure/Observability/SentryScrubbing.cs` — 124 lines; static partial class with the six D-14 scrubber rules + `HashUserId` helper + `[GeneratedRegex]` UUID matcher
- `Backend/tests/TaxReader.UnitTests/Observability/SentryScrubbingTests.cs` — 159 lines; 7 facts + 4 theory cases (10 test cases total)
- `Frontend/instrumentation-client.ts` — 73 lines; gated Sentry.init + `onRouterTransitionStart` export + client-side scrubber
- `Frontend/instrumentation.ts` — 14 lines; Next.js 16 register() runtime switch + `onRequestError` export
- `Frontend/sentry.server.config.ts` — 13 lines; gated server init
- `Frontend/sentry.edge.config.ts` — 13 lines; gated edge init

## Files Modified

- `Backend/Directory.Packages.props` — appended `<PackageVersion Include="Sentry" Version="6.4.1" />` and `<PackageVersion Include="Sentry.AspNetCore" Version="6.4.1" />` (alphabetically next to Scrutor / Serilog)
- `Backend/src/TaxReader.Api/TaxReader.Api.csproj` — appended `<PackageReference Include="Sentry.AspNetCore" />`
- `Backend/src/TaxReader.Infrastructure/TaxReader.Infrastructure.csproj` — appended `<PackageReference Include="Sentry" />` and a new `<ItemGroup>` with `<FrameworkReference Include="Microsoft.AspNetCore.App" />`
- `Backend/src/TaxReader.Api/Program.cs` — added `using Sentry;` and `using TaxReader.Infrastructure.Observability;` imports; inserted 11-line `builder.WebHost.UseSentry(...)` block IMMEDIATELY AFTER `var builder = WebApplication.CreateBuilder(args);` and BEFORE `var corsOrigins = ...` (Pitfall 1 ordering preserved). The 01-01 model-resolved log line + 01-04 Serilog wiring untouched.
- `Backend/src/TaxReader.Api/appsettings.json` — inserted `"Sentry": { "Dsn": "" }` block between `Serilog` and `Tesseract`
- `docker-compose.yml` — `api.environment` gets two new lines (`Sentry__Dsn`, `Sentry__Environment`); `web.environment` gets two new lines (`NEXT_PUBLIC_SENTRY_ENABLED`, `NEXT_PUBLIC_SENTRY_DSN`)
- `.env.example` — appended `# ── Sentry ──` section with three new vars
- `Frontend/package.json` — added `"@sentry/nextjs": "^10.52.0"` to `dependencies` (alphabetical position between `@hookform/resolvers` and `@tanstack/react-query`)
- `Frontend/package-lock.json` — regenerated by `npm install` (148 packages added)
- `Frontend/next.config.ts` — added `import { withSentryConfig } from "@sentry/nextjs";` after the existing `import type { NextConfig } from "next";`; replaced `export default nextConfig;` with the conditional ternary export. Existing `nextConfig` object, helpers, and rewrites untouched.

## Decisions Made

All five `key-decisions` listed in frontmatter (HashUserId visibility, scrubber location in Infrastructure, @sentry/nextjs version pin, frontend dormancy, conditional withSentryConfig) executed as planned. No new decisions required during execution.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 — Blocking] `SentryEvent.Extra` mutation requires concrete-type cast**

- **Found during:** Task 1 build verification
- **Issue:** `ev.Extra` is exposed via `IHasExtra` as `IReadOnlyDictionary<string, object?>`, but the runtime concrete type also implements `IDictionary<string, object?>`. The plan template wrote `ev.Extra.Remove(k)` directly, but the C# compiler resolved that to the `CollectionExtensions.Remove<TKey, TValue>(IDictionary<TKey, TValue>, TKey, out TValue)` extension method which requires an `out` value parameter — producing `error CS7036: kein Argument für value`.
- **Fix:** Added a pattern-match cast: `if (ev.Extra is { Count: > 0 } && ev.Extra is IDictionary<string, object?> mutableExtra)` and call `mutableExtra.Remove(k)` instead. This resolves the overload ambiguity to the `IDictionary<TKey, TValue>.Remove(TKey)` instance method (returns bool). A 4-line comment in `SentryScrubbing.cs` documents the rationale for future readers.
- **Files modified:** `Backend/src/TaxReader.Infrastructure/Observability/SentryScrubbing.cs`
- **Verification:** `dotnet build Backend` → 0 errors; full suite 113/113 pass.
- **Committed in:** `fc538af` (Task 1 commit; bundled with the scrubber's initial implementation).

**2. [Rule 3 — Blocking] Plan template referenced outdated Sentry SDK type names**

- **Found during:** Task 1 build verification (test project compile)
- **Issue:** The plan's test template instantiated `new Request { ... }` and `new SentryUser { ... }`, but in `Sentry` 6.4.1 the request class was renamed to `SentryRequest` (per `~/.nuget/packages/sentry/6.4.1/lib/net10.0/Sentry.xml` line 10651: `T:Sentry.SentryRequest`). `SentryUser` is correct (no rename). The build failed with five `error CS0246: Der Typ- oder Namespacename "Request" wurde nicht gefunden`.
- **Fix:** Replaced all `new Request` with `new SentryRequest` in `SentryScrubbingTests.cs` (5 occurrences via `replace_all`). The scrubber source itself was unaffected — it accesses `ev.Request` (a property name on `SentryEvent`, not a type reference).
- **Files modified:** `Backend/tests/TaxReader.UnitTests/Observability/SentryScrubbingTests.cs`
- **Verification:** Build green; 10/10 SentryScrubbingTests pass.
- **Committed in:** `fc538af` (Task 1 commit).

---

**Total deviations:** 2 auto-fixed (both Rule 3 blocking). Both are SDK-version transcription gaps in the plan template, mirroring the pattern from Plan 01-01 (missing `using Microsoft.AspNetCore.Hosting`) and Plan 01-04 (missing `<PackageReference>` for the enricher) — no structural change, no scope creep, both directly required by the plan's documented intent. Plan 01-02 (CI workflow) should consider a `dotnet list package --include-transitive` step that flags Sentry SDK type-name drift on future bumps.

## Issues Encountered

**Git add silently no-ops on Windows working tree** — During Task 2 commit staging, `git add Frontend/...` returned exit 0 but did not update the index (verified by `git diff --cached --name-only` showing empty). Switched to `git update-index --add` which staged all 7 Frontend files correctly. Cause unclear — no `.gitignore` exclusion, no `core.excludesfile`, no active git hooks. This did not affect the plan outcome but is worth noting if the same pattern recurs in Phase 2.

## Verification Results

```
=== Backend package wiring ===
OK Sentry pv
OK Sentry.AspNetCore pv
OK API ref
OK Infra ref
OK Infra framework ref
=== Init order (Pitfall 1) ===
OK UseSentry between CreateBuilder and AddInfrastructure
=== Backend scrubber wiring ===
OK scrubber file
OK AllowedExtraKeys
OK SetBeforeSend (not deprecated BeforeSend)
OK SentryScrubbing.Scrub call
OK MaxRequestBodySize.None
=== appsettings.json ===
OK appsettings.Sentry.Dsn
=== Compose / env ===
OK compose Sentry__Dsn
OK compose web flag
OK env.example backend
=== Frontend Sentry files ===
OK package.json @sentry/nextjs
OK instrumentation-client.ts
OK instrumentation.ts
OK sentry.server.config.ts
OK sentry.edge.config.ts
OK no deprecated sentry.client.config.ts
OK gated init
OK onRouterTransitionStart
OK next.config.ts withSentryConfig
=== Tests ===
Bestanden!   : Fehler:     0, erfolgreich:    10, übersprungen:     0, gesamt:    10, Dauer: 306 ms
=== Full suite (regression check) ===
Bestanden!   : Fehler:     0, erfolgreich:   113, übersprungen:     0, gesamt:   113, Dauer: 5 s
=== Frontend build ===
✓ Compiled successfully in 2.7s
✓ Generating static pages using 14 workers (12/12) in 488ms
=== Smoke (no DSN) ===
[12:56:31 INF] Starting BelegPilot API
[12:56:32 INF] Anthropic configuration resolved: Model=claude-haiku-4-5, ...
[12:56:32 WRN] CORS_ALLOWED_ORIGINS unset in Production environment ...
(no Sentry exception or warning — SDK no-op confirmed)
```

All 22 plan-level verification commands succeed. All 9 must_haves.truths, all 5 must_haves.artifacts, all 3 key_links, and all `<success_criteria>` in the plan body verified.

## TDD Gate Compliance

Plan-level type is `execute`, not `tdd`. Per-task `tdd="true"` markers applied at task granularity. The git log shows:

- `fc538af` feat(01-03): Backend Sentry SDK + PII scrubber (RED+GREEN combined — test file imports the scrubber, both files land in the same atomic commit per execute-plan.md protocol)
- `4896ea0` feat(01-03): Frontend Sentry SDK scaffold (no test artifact — Frontend has no test framework configured per CLAUDE.md; verification is `npm run lint` + `npm run build`)

This matches the same atomic-commit pattern used in Plan 01-01 and Plan 01-04 when no plan-level `type: tdd` gate is set.

## User Setup Required

**Sentry account provisioning (deferred to operator).** See `## Sentry account / project setup status` above. The code ships ready; the operator runs the EU-region setup, creates the two projects, configures the two D-15 alert rules, disables the default page-on-first-error rule, confirms email-only delivery, and populates `.env` with the two DSNs (and toggles `NEXT_PUBLIC_SENTRY_ENABLED=true` in Phase 6 once the cookie banner ships).

**Validation Phase 1 success criterion #4** ("Sentry receives errors from .NET API and Next.js frontend with PII scrubbed; alert rules don't fire on transient noise") is **partially satisfied**: backend half is wired and unit-tested; frontend half is dormant by design (D-16); operator-side dashboard configuration (D-15a/b alerts, default-rule disablement) is pre-launch operational work, not a code change.

## Next Phase Readiness

- **Wave 4 (Plan 01-02 — CI workflow + README):** Unblocked. CI must run `dotnet test Backend` (which now executes 113 tests including the 10 SentryScrubbingTests as a regression guard) and `cd Frontend && npm install && npm run build`. The Frontend build does NOT need any `SENTRY_*` env var in CI thanks to Pitfall 6's conditional wrap. Optional CI hardening: add a `dotnet list package --include-transitive | grep -i sentry` smoke step to guard against future SDK type-name drift (the deviation #2 fix this plan made).
- **Phase 6 (LEG-05 — TTDSG cookie banner):** When the consent surface is wired, flip `NEXT_PUBLIC_SENTRY_ENABLED=true` in `.env` and the frontend SDK begins capturing post-consent. Backend Sentry continues operating from launch.
- **Phase 7 (QA-06):** Re-evaluate alert-rule thresholds against real-traffic baseline; reconsider source-map upload (currently deferred — `withSentryConfig` does not pass `authToken`); run `npm audit` and `dotnet list package --vulnerable` against the new Sentry surface (T-01-03-11).
- **Phase 3 (Hangfire upload pipeline):** When `using (LogContext.PushProperty("JobId", jobId))` lands at the job entry point (per 01-04 plan), the existing scrubber automatically routes `JobId` to Sentry as an Extra (it's in `AllowedExtraKeys`). No scrubber change needed.

## Self-Check: PASSED

- Created files exist:
  - `Backend/src/TaxReader.Infrastructure/Observability/SentryScrubbing.cs` — FOUND
  - `Backend/tests/TaxReader.UnitTests/Observability/SentryScrubbingTests.cs` — FOUND
  - `Frontend/instrumentation-client.ts` — FOUND
  - `Frontend/instrumentation.ts` — FOUND
  - `Frontend/sentry.server.config.ts` — FOUND
  - `Frontend/sentry.edge.config.ts` — FOUND
- Modified files contain expected literals:
  - `Backend/Directory.Packages.props` contains `Sentry.AspNetCore` Version="6.4.1" — FOUND
  - `Backend/src/TaxReader.Api/Program.cs` contains `builder.WebHost.UseSentry` (Pitfall 1 placement) — FOUND
  - `Backend/src/TaxReader.Api/appsettings.json` `Sentry.Dsn` — FOUND
  - `Frontend/package.json` `@sentry/nextjs` — FOUND
  - `Frontend/next.config.ts` `withSentryConfig` (conditional) — FOUND
- Commit hashes exist in git log:
  - `fc538af` — FOUND
  - `4896ea0` — FOUND
- Test counts: 10/10 new pass, 113/113 total pass; Frontend `npm run build` 0 errors

---

*Phase: 01-foundation-cleanup-ci*
*Completed: 2026-05-10*
