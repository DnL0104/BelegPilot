# Phase 1: Foundation Cleanup + CI - Research

**Researched:** 2026-05-04
**Domain:** Hygiene + CI + Sentry observability + Serilog correlation
**Confidence:** HIGH — every load-bearing claim verified against NuGet, npm, official docs, or in-repo code

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

**Anthropic model alignment (FND-02)**
- **D-01:** `claude-haiku-4-5` becomes the single documented production default.
- **D-02:** Lock-in mechanism = single source of truth + startup-log. Code default in `AnthropicOptions.cs` is canonical (`"claude-haiku-4-5"`); `docker-compose.yml:38` and `.env.example:19` are updated to match. `CLAUDE.md` documents the choice. On startup, the API logs the resolved `Anthropic__Model` value (info-level).
- **D-03:** No startup-time hard guard or allow-list; no required-config strip.

**Hygiene cleanup (FND-01, FND-03)**
- **D-04:** Delete `Backend/src/TaxReader.Api/storage/2026/04/`. Files are untracked → just delete from disk; no `git rm` needed.
- **D-05:** Extend `.gitignore` with `Backend/src/TaxReader.Api/storage/`, `build-diag*.txt`, `*.binlog`.
- **D-06:** Add a CI `hygiene-check` job that fails build if `storage/`, `Backend/storage/`, `Backend/src/TaxReader.Api/storage/`, `build-diag*.txt`, or `*.binlog` files appear in the tree.
- **D-07:** CORS production fail-mode for FND-03: when `CORS_ALLOWED_ORIGINS` is unset AND env is **not** Development, register a deny-all CORS policy (no `WithOrigins` call). Log a warning at startup. Drop the `localhost:3000` fallback from the non-Dev branch.

**CI workflow design (FND-04)**
- **D-08:** Single `.github/workflows/ci.yml` with parallel jobs: `hygiene-check`, `backend-build-test`, `frontend-lint-build`. Triggers: PRs to `main` + pushes to `main`. Concurrency group with `cancel-in-progress: true` for PRs.
- **D-09:** Backend test scope = existing `TaxReader.UnitTests` only. No Postgres in CI. Phase 7 adds integration tests.
- **D-10:** Branch protection on `main`: PR required, required status checks = three job names, no required reviewers, no signed-commit/linear-history requirement.
- **D-11:** No CI secrets needed in Phase 1.

**README (FND-05)**
- **D-12:** Top-level `README.md`, English. Tagline → prerequisites (.NET 10 SDK, Node 22+, Docker, Tesseract for non-container dev) → quick start (`cp .env.example .env`, edit, `docker compose up --build`, browse to `https://localhost`) → links to `CLAUDE.md` + `.planning/codebase/`. No screenshots.

**Sentry integration (OBS-01)**
- **D-13:** Sentry Developer Free tier on EU region. 5k errors / 10k perf units per month is sufficient.
- **D-14:** PII scrubbing posture = default-deny + small allow-list. In `BeforeSend` / `BeforeSendTransaction`:
  - Strip request bodies entirely
  - Strip query strings except allow-list (`page`, `pageSize`, `year`, `format`)
  - Strip HTTP headers except `User-Agent`
  - Mask URL path segments matching a UUID pattern to `:id`
  - Strip user email; keep a hash of user ID as `user.id_hash`
  - Strip raw receipt content, item descriptions, vendor names, classification reasoning text
- **D-15:** Alert routing = email-only to solo-dev. Two starting rules: (a) new-error-type with 1h cooldown, (b) sustained-rate ≥ 10 events/min for ≥ 5 min. **No** page-on-first-error. No Slack/PagerDuty.
- **D-16:** Frontend Sentry stays **disabled in production** until Phase 6 wires the TTDSG consent banner. `Sentry.init` only runs when `process.env.NEXT_PUBLIC_SENTRY_ENABLED === "true"`. We leave unset in `docker-compose.yml`'s `web` service for Phase 1. Backend Sentry runs unconditionally.

**Correlation IDs in long-running handlers (OBS-02)**
- **D-17:** Backend-internal correlation only. Add `Enrichers.FromLogContext()` and `Enrichers.WithEnvironmentName()` to Serilog config via `appsettings.json`. `UseSerilogRequestLogging` already attaches `RequestId`. No frontend changes; no W3C `traceparent`; no custom header.
- **D-18:** Inside `UploadReceiptFilesHandler.HandleAsync`, wrap per-file processing block with `using (LogContext.PushProperty("ReceiptFileId", receiptFileId))`. Phase 3 adds `JobId` later; explicitly NOT pre-wired now.
- **D-19:** Correlation ID surface = Serilog only. No Sentry tag wiring. No `X-Request-Id` HTTP response header.

### Claude's Discretion
- Exact Serilog console output template (default to readable plain-text dev + structured JSON in production).
- Exact Sentry SDK package selection (.NET: `Sentry.AspNetCore`; frontend: `@sentry/nextjs`) and minor-version pinning.
- Hygiene-check shell snippet implementation.
- Whether `setup-dotnet` cache-key suffix includes `Directory.Packages.props` hash (research recommends YES — see Implementation Patterns).
- Where in `CLAUDE.md` to document the Anthropic model choice.

### Deferred Ideas (OUT OF SCOPE)
- Stripe / payment-provider env vars + multi-environment safety (Phase 5)
- W3C `traceparent` browser → backend trace propagation (Phase 6/7)
- Sentry Slack / PagerDuty integration
- OpenTelemetry / distributed tracing
- Dev-machine pre-commit hooks
- macOS/Linux `start.sh` / `stop.sh`
- Container/database rebrand from `belegpilot-*` to `taxreader-*`
- Sentry release tagging + source maps (Phase 7)
- Sentry Performance / tracing / session replay
- Backend Sentry test endpoint (`/sentry-debug`)
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| FND-01 | Remove `storage/` + `build-diag.txt`; `.gitignore` hygiene; verify no code path writes receipts to disk | Verified in-repo: migration `20260420055623_RemoveStoragePath` already dropped `storage_path` column; `UploadReceiptFilesHandler` uses `MemoryStream` only; no `WriteAllBytes`/`FileStream`/`Directory.Create` calls remain in `Backend/src` |
| FND-02 | Reconcile Anthropic model default; document chosen default | `AnthropicOptions.cs:10` has `"claude-haiku-4-5"`; `docker-compose.yml:38` has `claude-sonnet-4-5`; `.env.example:19` has `claude-sonnet-4-5`; all three need to converge |
| FND-03 | Lock CORS production fail-mode (deny-all) | `Program.cs:88-112` has the policy block with the broken non-Dev fallback (`localhost:3000`); replace with no-`WithOrigins` policy + warning log |
| FND-04 | GitHub Actions CI workflow — merge-blocking checks | `actions/setup-dotnet@v4` + `actions/setup-node@v4`; both support built-in caching with `cache-dependency-path` |
| FND-05 | Top-level `README.md` | Backend has its own `Backend/README.md`; Frontend has `Frontend/README.md`; repo root has none |
| OBS-01 | Sentry installed for .NET API + Next.js frontend with EU residency, PII scrubbing, conservative alerts | `Sentry.AspNetCore` 6.4.1 + `@sentry/nextjs` 10.51.0 are current as of 2026-05; both support EU DSN routing |
| OBS-02 | Serilog enrichers + `LogContext.PushProperty` in long-running handlers | `Enrich.FromLogContext()` is built into Serilog; `Enrich.WithEnvironmentName()` requires `Serilog.Enrichers.Environment` package; both wired through `appsettings.json` since `Program.cs:32-33` calls `ReadFrom.Configuration` |
</phase_requirements>

## Summary

Phase 1 is a small-surface foundation hardening pass with two non-trivial integration points (Sentry + Serilog correlation) and a handful of cleanup tasks. CONTEXT.md has already settled architecture and approach across 19 locked decisions; this research's job is to verify the exact package names, versions, init shapes, and pitfalls the planner needs.

Key verified findings:
1. **`@sentry/nextjs` 10.51.0** explicitly declares Next.js 16 in its peerDependencies (`^16.0.0-0`) and Next.js 16 has switched from `sentry.client.config.ts` to **`instrumentation-client.ts`** (introduced in Next 15.3 per official Next.js docs). The planner must NOT use the old `sentry.client.config.ts` filename.
2. **`Sentry.AspNetCore` 6.4.1** is the current stable line; supports .NET 10. The init shape uses `builder.WebHost.UseSentry(o => { ... })`. The correct C# delegate name is **`SetBeforeSend`** (not `BeforeSend`), with signature `Func<SentryEvent, SentryHint, SentryEvent?>`.
3. **`Enrich.FromLogContext()` is built into core Serilog** — no separate `Serilog.Enrichers.CorrelationId` package needed for D-18's `LogContext.PushProperty`. Only `Serilog.Enrichers.Environment` is a new dependency.
4. **No code path writes receipts to disk** — verified via grep + migration history. The `storage/` PDFs are pure dev-time leftovers; deletion is safe.

**Primary recommendation:** Plan four discrete plans matching ROADMAP.md's plan list (01-01 hygiene, 01-02 CI+README, 01-03 Sentry, 01-04 Serilog). Add Sentry packages to `Directory.Packages.props` (NuGet CPM) and `Frontend/package.json`. Use the exact init shapes documented below — they have been verified against current docs as of 2026-05-04.

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Hygiene file removal + `.gitignore` | Repo root (filesystem) | — | Pure VCS hygiene; not application code |
| CI workflow | GitHub Actions (CI/CD tier) | — | External to runtime; gates merges to `main` |
| README content | Repo root (docs tier) | — | Developer-facing documentation; not shipped to users |
| Anthropic model default | Backend Infrastructure (Configuration) | docker-compose env, `.env.example`, `CLAUDE.md` | `AnthropicOptions.cs` is the canonical write site per D-02; compose + env are downstream consumers |
| CORS deny-all production policy | Backend API (`Program.cs`) | — | Cross-cutting middleware concern; lives in API host bootstrap |
| Sentry .NET capture | Backend API + Infrastructure | — | `UseSentry` on `WebHost` (API tier); `BeforeSend` may consume `IConfiguration` and `IHttpContextAccessor` |
| Sentry Next.js capture | Frontend (Browser + Next.js Server + Edge) | — | `@sentry/nextjs` produces three runtime configs; client gated on `NEXT_PUBLIC_SENTRY_ENABLED` per D-16 |
| Serilog enrichers + LogContext correlation | Backend Application (handler) + API (config) | — | Enricher registration is API-tier (Program.cs reads `appsettings.json`); `LogContext.PushProperty` lives in the handler at the per-file scope per D-18 |

## Standard Stack

### Core (NuGet — add to `Backend/Directory.Packages.props`)
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| `Sentry.AspNetCore` | 6.4.1 | Sentry capture for ASP.NET Core 10 | Latest stable; explicitly supports .NET 10 [VERIFIED: NuGet flatcontainer index for `sentry.aspnetcore` returned 6.4.1 as latest stable on 2026-05-04] [CITED: https://docs.sentry.io/platforms/dotnet/guides/aspnetcore/ — "Install-Package Sentry.AspNetCore -Version 6.4.1"] |
| `Serilog.Enrichers.Environment` | 3.0.1 | `WithEnvironmentName()` enricher (D-17) | Maintained by serilog org; the canonical environment-property enricher [VERIFIED: NuGet flatcontainer for `serilog.enrichers.environment` shows 3.0.1 as latest stable] [CITED: https://github.com/serilog/serilog-enrichers-environment — confirms `WithEnvironmentName()` reads `ASPNETCORE_ENVIRONMENT` / `DOTNET_ENVIRONMENT`] |

> **Note:** `Serilog.Enrichers.CorrelationId` (REQUIREMENTS.md OBS-02) is NOT needed. The third-party `ekmsystems/serilog-enrichers-correlation-id` package is unmaintained and overlaps with what `UseSerilogRequestLogging` + `LogContext.PushProperty` already provide. CONTEXT.md D-17 explicitly chose `FromLogContext()` + `WithEnvironmentName()`, which is the correct minimal set. [VERIFIED: https://github.com/serilog/serilog/wiki/Enrichment confirms `FromLogContext` is built-in to core Serilog]

### Core (npm — add to `Frontend/package.json`)
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| `@sentry/nextjs` | ^10.51.0 | Sentry capture for Next.js 16 + React 19 | Single package covers client + server + edge; peerDependencies declare `next ^16.0.0-0` so it explicitly supports Next.js 16.2.2 [VERIFIED: `npm view @sentry/nextjs version` returned `10.51.0`; `peerDependencies` returned `{ next: '^13.2.0 || ^14.0 || ^15.0.0-rc.0 || ^16.0.0-0' }`] |

### Supporting (GitHub Actions — pin in `.github/workflows/ci.yml`)
| Action | Version | Purpose | When to Use |
|--------|---------|---------|-------------|
| `actions/checkout` | `@v4` | Clone repo into runner | Every job |
| `actions/setup-dotnet` | `@v4` | Install .NET 10 SDK + NuGet cache | `backend-build-test` job |
| `actions/setup-node` | `@v4` | Install Node 22 + npm cache | `frontend-lint-build` job |

[CITED: https://github.com/actions/setup-dotnet — confirms `cache: true` + `cache-dependency-path` parameters; matrix supports `'10.0.x'`]

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| `Sentry.AspNetCore` | `Sentry.Serilog` (sink) | `Sentry.AspNetCore` already wires `ILogger<T>` capture via the SDK's logging integration AND captures unhandled HTTP exceptions; adding `Sentry.Serilog` on top would double-capture every log line. Avoid. |
| `@sentry/nextjs` | `@sentry/react` + `@sentry/node` separately | The combined package handles webpack/turbopack instrumentation and source maps automatically; splitting would re-implement what `withSentryConfig` already does. Don't split. |
| Wizard `npx @sentry/wizard@latest -i nextjs` | Manual setup | The wizard auto-creates `instrumentation-client.ts`, `sentry.server.config.ts`, `sentry.edge.config.ts`, modifies `next.config.ts` to add `withSentryConfig`, and adds env vars. **Recommended for monorepo with `Frontend/` subdirectory: cd into `Frontend/` first, then run wizard there** — it operates on the current working directory. After wizard, post-edit the client init to wrap in `if (process.env.NEXT_PUBLIC_SENTRY_ENABLED === "true")` per D-16. |

**Installation (Backend — central package management):**
```xml
<!-- Add to Backend/Directory.Packages.props ItemGroup -->
<PackageVersion Include="Sentry.AspNetCore" Version="6.4.1" />
<PackageVersion Include="Serilog.Enrichers.Environment" Version="3.0.1" />
```
```xml
<!-- Add to Backend/src/TaxReader.Api/TaxReader.Api.csproj -->
<PackageReference Include="Sentry.AspNetCore" />
<PackageReference Include="Serilog.Enrichers.Environment" />
```

**Installation (Frontend):**
```bash
cd Frontend
npm install --save @sentry/nextjs@^10.51.0
```

**Version verification (run during execution to confirm currency):**
```bash
# Backend
curl -s "https://api.nuget.org/v3-flatcontainer/sentry.aspnetcore/index.json" | python -c "import json,sys; print([v for v in json.load(sys.stdin)['versions'] if not any(x in v for x in ['preview','alpha','beta','rc','sync','maxpath','segv'])][-1])"
curl -s "https://api.nuget.org/v3-flatcontainer/serilog.enrichers.environment/index.json" | python -c "import json,sys; print([v for v in json.load(sys.stdin)['versions'] if not any(x in v for x in ['preview','alpha','beta','rc','dev'])][-1])"

# Frontend
npm view @sentry/nextjs version
npm view @sentry/nextjs peerDependencies
```

## Implementation Patterns

### System Architecture Diagram

```
┌──────────────────────────┐         ┌──────────────────────────┐
│  PR / push to main       │         │  Browser                 │
│  (GitHub event)          │         │  (Next.js client)        │
└────────────┬─────────────┘         └─────────────┬────────────┘
             │                                     │ (Phase 6: gated on
             ▼                                     │  NEXT_PUBLIC_SENTRY_ENABLED;
┌──────────────────────────┐                       │  Phase 1: disabled)
│ GitHub Actions ci.yml    │                       │
│  ├─ hygiene-check        │                       ▼
│  ├─ backend-build-test   │         ┌──────────────────────────┐
│  └─ frontend-lint-build  │         │ Caddy (TLS edge)         │
└──────────────────────────┘         └─────────────┬────────────┘
                                                   │
        ┌──────────────────────────────────────────┴───────────┐
        │                                                      │
        ▼                                                      ▼
┌──────────────────────┐                       ┌──────────────────────┐
│ web (Next.js 16)     │                       │ api (.NET 10)        │
│  - instrumentation-  │                       │  - UseSentry()       │
│    client.ts (off)   │                       │  - SetBeforeSend     │
│  - instrumentation.  │                       │    (PII scrub D-14)  │
│    ts                │                       │  - Serilog enrichers │
│  - sentry.server.    │                       │    (FromLogContext + │
│    config.ts         │                       │     EnvironmentName) │
│  - sentry.edge.      │                       │  - LogContext.Push   │
│    config.ts         │                       │    (ReceiptFileId)   │
└──────────────────────┘                       └────────┬─────────────┘
                                                        │
                                                        ▼
                                       ┌──────────────────────────┐
                                       │ sentry.eu.io             │
                                       │ (EU region — DSGVO)      │
                                       └──────────────────────────┘
```

### Recommended Project Structure (additions only)

```
TaxReader/
├── .github/
│   └── workflows/
│       └── ci.yml                  # NEW (FND-04, D-08)
├── README.md                       # NEW (FND-05, D-12)
├── Backend/
│   ├── Directory.Packages.props    # MODIFIED — add 2 PackageVersion entries
│   └── src/TaxReader.Api/
│       ├── Program.cs              # MODIFIED — UseSentry, deny-all CORS, model log
│       ├── TaxReader.Api.csproj    # MODIFIED — add 2 PackageReference entries
│       ├── appsettings.json        # MODIFIED — add Serilog.Enrich array, add Sentry section
│       └── appsettings.Development.json
└── Frontend/
    ├── instrumentation-client.ts   # NEW (Sentry client init, D-16 gated)
    ├── instrumentation.ts          # NEW (Sentry server registration)
    ├── sentry.server.config.ts     # NEW (Sentry server init)
    ├── sentry.edge.config.ts       # NEW (Sentry edge init)
    ├── next.config.ts              # MODIFIED — wrap in withSentryConfig
    └── package.json                # MODIFIED — add @sentry/nextjs
```

### Pattern 1: Backend Sentry Init in Program.cs

**What:** Register Sentry on the WebHost between `WebApplication.CreateBuilder` and `builder.Services.AddInfrastructure`.

**Where:** `Backend/src/TaxReader.Api/Program.cs`, immediately after line 26 (`var builder = WebApplication.CreateBuilder(args);`).

**Code:**
```csharp
// Source: https://docs.sentry.io/platforms/dotnet/guides/aspnetcore/
//        + Options reference: https://docs.sentry.io/platforms/dotnet/guides/aspnetcore/configuration/options/
builder.WebHost.UseSentry(options =>
{
    // Dsn binds automatically from configuration via Sentry section
    // (Sentry__Dsn env var or "Sentry":{"Dsn":"..."} in appsettings.json).
    options.Environment = builder.Environment.EnvironmentName;

    // Default-deny PII (D-14). The default is already false but make it explicit.
    options.SendDefaultPii = false;
    options.MaxRequestBodySize = Sentry.Extensibility.RequestSize.None;

    options.SetBeforeSend((sentryEvent, hint) =>
    {
        return SentryScrubbing.Scrub(sentryEvent);
    });
});
```

**BeforeSend extraction site (new file `Backend/src/TaxReader.Api/Observability/SentryScrubbing.cs`):**
```csharp
// Source: https://docs.sentry.io/platforms/dotnet/guides/aspnetcore/data-management/sensitive-data/
namespace TaxReader.Api.Observability;

internal static class SentryScrubbing
{
    private static readonly HashSet<string> AllowedQueryKeys =
        new(StringComparer.OrdinalIgnoreCase) { "page", "pageSize", "year", "format" };

    private static readonly HashSet<string> AllowedHeaders =
        new(StringComparer.OrdinalIgnoreCase) { "User-Agent" };

    private static readonly System.Text.RegularExpressions.Regex UuidSegment =
        new(@"\b[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}\b",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

    public static SentryEvent? Scrub(SentryEvent ev)
    {
        // Strip request body entirely (D-14 #1)
        if (ev.Request is not null)
        {
            ev.Request.Data = null;

            // Strip query strings except allow-list (D-14 #2)
            // Sentry stores QueryString as a single string; reparse, filter, rebuild.
            if (!string.IsNullOrEmpty(ev.Request.QueryString))
            {
                var query = Microsoft.AspNetCore.WebUtilities.QueryHelpers
                    .ParseQuery(ev.Request.QueryString);
                var filtered = query
                    .Where(kvp => AllowedQueryKeys.Contains(kvp.Key))
                    .Select(kvp => $"{kvp.Key}={Uri.EscapeDataString(kvp.Value!)}");
                ev.Request.QueryString = string.Join("&", filtered);
            }

            // Strip headers except allow-list (D-14 #3)
            var headers = ev.Request.Headers;
            if (headers is not null)
            {
                var keysToRemove = headers.Keys.Where(k => !AllowedHeaders.Contains(k)).ToList();
                foreach (var k in keysToRemove) headers.Remove(k);
            }

            // Mask UUID path segments to :id (D-14 #4)
            if (!string.IsNullOrEmpty(ev.Request.Url))
            {
                ev.Request.Url = UuidSegment.Replace(ev.Request.Url, ":id");
            }
        }

        // Strip user email; keep hash of user.Id (D-14 #5)
        if (ev.User is not null)
        {
            ev.User.Email = null;
            ev.User.Username = null;
            ev.User.IpAddress = null;

            if (!string.IsNullOrEmpty(ev.User.Id))
            {
                ev.User.Other ??= new Dictionary<string, string>();
                ev.User.Other["id_hash"] = HashUserId(ev.User.Id);
                ev.User.Id = null;
            }
        }

        return ev;
    }

    private static string HashUserId(string userId)
    {
        var bytes = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(userId));
        return Convert.ToHexString(bytes)[..16].ToLowerInvariant();
    }
}
```

**Why this shape:**
- `SetBeforeSend` (not `BeforeSend`) is the correct method on `SentryAspNetCoreOptions` in 6.x. [CITED: https://docs.sentry.io/platforms/dotnet/guides/aspnetcore/]
- Delegating to a static class keeps `Program.cs` thin and lets us unit-test the scrubber in isolation.
- `MaxRequestBodySize = RequestSize.None` is the configuration-level body block; `BeforeSend` is the runtime defence-in-depth.
- D-14 #6 ("strip raw receipt content / vendor names / classification reasoning text from any captured event") is automatic if we never put those into `Sentry.SetExtra` / `Sentry.SetTag` / breadcrumbs. The handler doesn't add them today; the planner should add a "do not pass user content to Sentry breadcrumbs" comment in the upload handler so future contributors don't regress.

### Pattern 2: Sentry DSN configuration (Backend)

**Where:** `Backend/src/TaxReader.Api/appsettings.json`
```json
{
  "Sentry": {
    "Dsn": ""
  }
}
```

**Where:** `docker-compose.yml` `api.environment` block
```yaml
Sentry__Dsn: ${SENTRY_DSN_BACKEND:-}
Sentry__Environment: production
```

**Where:** `.env.example`
```
# ── Sentry ────────────────────────────────────────────────────────────────────
# Backend DSN — EU region (sentry.eu.io). Optional — leave blank to disable.
SENTRY_DSN_BACKEND=
# Frontend Sentry — disabled in Phase 1 until TTDSG cookie banner lands (Phase 6).
NEXT_PUBLIC_SENTRY_ENABLED=false
NEXT_PUBLIC_SENTRY_DSN=
```

**Why empty DSN works:** When `Sentry__Dsn` is unset/empty, the SDK enters a no-op state and does NOT throw. This lets us merge the integration without an active project, and turn it on by setting the env var. [CITED: https://docs.sentry.io/platforms/dotnet/guides/aspnetcore/configuration/options/ — "Without [DSN] set, the SDK will just not send any events."]

### Pattern 3: Frontend Sentry init — Next.js 16 file convention

**CRITICAL — do NOT use `sentry.client.config.ts`.** Next.js 15.3 introduced `instrumentation-client.ts` as the new client-init convention; Next.js 16 requires it. Sentry v8+ docs were updated to match. [CITED: https://nextjs.org/docs/app/api-reference/file-conventions/instrumentation-client — version history table shows "v15.3: instrumentation-client introduced"; the `Frontend/AGENTS.md` warning that "this is NOT the Next.js you know" specifically applies here]

**File:** `Frontend/instrumentation-client.ts` (NEW — repo-root of `Frontend/`)
```typescript
// Source: https://docs.sentry.io/platforms/javascript/guides/nextjs/manual-setup/
//        + https://nextjs.org/docs/app/api-reference/file-conventions/instrumentation-client
import * as Sentry from "@sentry/nextjs";

// D-16: Frontend Sentry stays disabled in production until Phase 6 wires
// the TTDSG cookie banner. We init only when the env flag is explicitly "true".
if (process.env.NEXT_PUBLIC_SENTRY_ENABLED === "true") {
  Sentry.init({
    dsn: process.env.NEXT_PUBLIC_SENTRY_DSN,
    environment: process.env.NEXT_PUBLIC_SENTRY_ENV ?? "production",
    sendDefaultPii: false,
    tracesSampleRate: 0,         // No perf tier in Phase 1
    replaysOnErrorSampleRate: 0, // No session replay
    replaysSessionSampleRate: 0,
    beforeSend(event, hint) {
      return scrubEvent(event);
    },
  });
}

// Required by Next.js 16: capture router transitions for breadcrumbs.
export const onRouterTransitionStart = Sentry.captureRouterTransitionStart;

const ALLOWED_QUERY_KEYS = new Set(["page", "pageSize", "year", "format"]);
const ALLOWED_HEADERS = new Set(["user-agent"]);
const UUID_RE =
  /\b[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}\b/gi;

function scrubEvent(event: Sentry.ErrorEvent): Sentry.ErrorEvent | null {
  if (event.request) {
    delete event.request.data;

    if (typeof event.request.query_string === "string") {
      event.request.query_string = filterQueryString(event.request.query_string);
    }

    if (event.request.headers) {
      const keep: Record<string, string> = {};
      for (const [k, v] of Object.entries(event.request.headers)) {
        if (ALLOWED_HEADERS.has(k.toLowerCase())) keep[k] = v as string;
      }
      event.request.headers = keep;
    }

    if (typeof event.request.url === "string") {
      event.request.url = event.request.url.replace(UUID_RE, ":id");
    }
  }

  if (event.user) {
    delete event.user.email;
    delete event.user.username;
    delete event.user.ip_address;
    if (typeof event.user.id === "string") {
      // SubtleCrypto is async — we store a marker; backend events carry the real
      // hash. For client-side, just drop the id.
      delete event.user.id;
    }
  }

  return event;
}

function filterQueryString(qs: string): string {
  const params = new URLSearchParams(qs);
  const filtered = new URLSearchParams();
  for (const [k, v] of params.entries()) {
    if (ALLOWED_QUERY_KEYS.has(k)) filtered.append(k, v);
  }
  return filtered.toString();
}
```

**File:** `Frontend/instrumentation.ts` (NEW)
```typescript
// Source: https://docs.sentry.io/platforms/javascript/guides/nextjs/manual-setup/
import * as Sentry from "@sentry/nextjs";

export async function register() {
  if (process.env.NEXT_RUNTIME === "nodejs") {
    await import("./sentry.server.config");
  }
  if (process.env.NEXT_RUNTIME === "edge") {
    await import("./sentry.edge.config");
  }
}

export const onRequestError = Sentry.captureRequestError;
```

**File:** `Frontend/sentry.server.config.ts` (NEW)
```typescript
import * as Sentry from "@sentry/nextjs";

if (process.env.SENTRY_DSN_FRONTEND_SERVER) {
  Sentry.init({
    dsn: process.env.SENTRY_DSN_FRONTEND_SERVER,
    environment: process.env.SENTRY_ENV ?? "production",
    sendDefaultPii: false,
    tracesSampleRate: 0,
    beforeSend(event) {
      // Server-side events from Next.js — apply same scrubbing as the client.
      // Reuse the scrubber by extracting it; for Phase 1 the simplest path is
      // to inline the same filter logic. (The scrubber from instrumentation-
      // client.ts cannot be imported here directly because that file is the
      // browser entry point; planner: extract scrubber to shared module.)
      return event;
    },
  });
}
```

**File:** `Frontend/sentry.edge.config.ts` (NEW)
```typescript
import * as Sentry from "@sentry/nextjs";

if (process.env.SENTRY_DSN_FRONTEND_EDGE) {
  Sentry.init({
    dsn: process.env.SENTRY_DSN_FRONTEND_EDGE,
    environment: process.env.SENTRY_ENV ?? "production",
    sendDefaultPii: false,
    tracesSampleRate: 0,
  });
}
```

**File modification:** `Frontend/next.config.ts`
```typescript
// Existing imports + helpers above unchanged …
import { withSentryConfig } from "@sentry/nextjs";

const nextConfig: NextConfig = {
  // … existing config unchanged …
};

// D-16: wrap is always present; SDK becomes a no-op when DSN is unset.
export default withSentryConfig(nextConfig, {
  silent: true,
  org: process.env.SENTRY_ORG,
  project: process.env.SENTRY_PROJECT,
  // Source-map upload deferred per CONTEXT.md <deferred> — do NOT pass authToken.
});
```

**Notes for planner:**
- Sentry's setup wizard (`npx @sentry/wizard@latest -i nextjs`) will scaffold all four files automatically. Recommended approach: `cd Frontend && npx @sentry/wizard@latest -i nextjs`, then post-edit `instrumentation-client.ts` to wrap `Sentry.init` in the `NEXT_PUBLIC_SENTRY_ENABLED` flag check (D-16). Manual scaffolding is also fine if the wizard pulls in unwanted optional features.
- For a server-side scrubber that mirrors the client scrubber, extract the function into `Frontend/src/lib/sentry-scrubber.ts` and import from both `instrumentation-client.ts` and `sentry.server.config.ts`. Keep this minor — Phase 1 client init is gated off so server-side capture is the only live frontend path; it WILL fire if `SENTRY_DSN_FRONTEND_SERVER` is set.

### Pattern 4: CORS deny-all production fail-mode (D-07)

**Replace `Backend/src/TaxReader.Api/Program.cs:88-112` block** with:

```csharp
// CORS — D-07: production fail-mode is deny-all when CORS_ALLOWED_ORIGINS unset.
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        if (corsOrigins is { Length: > 0 })
        {
            policy.WithOrigins(corsOrigins)
                  .AllowAnyHeader()
                  .AllowAnyMethod();
            return;
        }

        if (builder.Environment.IsDevelopment())
        {
            policy.WithOrigins("http://localhost:3000")
                  .AllowAnyHeader()
                  .AllowAnyMethod();
            return;
        }

        // D-07: non-Development with no origins → deny all.
        // We still register an empty default policy so app.UseCors() doesn't
        // error on a missing default; calling .WithOrigins() with zero origins
        // means no preflight responses pass.
        Log.Warning(
            "CORS_ALLOWED_ORIGINS unset in {Environment} environment — denying all cross-origin requests. " +
            "Browsers reaching the API via the same-origin Caddy proxy are unaffected.",
            builder.Environment.EnvironmentName);
    });
});
```

**Why "register but no origins":** `policy.WithOrigins()` with zero arguments is rejected at runtime, but constructing the `CorsPolicyBuilder` and not calling `.WithOrigins()` at all yields a policy where `Origins` is empty — middleware checks against the empty list and rejects every cross-origin request. [VERIFIED: this matches `Microsoft.AspNetCore.Cors.Infrastructure.CorsPolicy.Origins` semantics — empty list → no allowed origins. Same-origin requests bypass CORS entirely (browser doesn't add `Origin` header), so Caddy → API requests at `https://localhost` are unaffected, matching CONTEXT.md note that "browsers in production speak to Caddy (same-origin) so this is mostly inert."]

### Pattern 5: Anthropic model alignment (D-01, D-02)

**Action 1:** `AnthropicOptions.cs:10` — already correct (`"claude-haiku-4-5"`). No change needed.

**Action 2:** Update `docker-compose.yml:38`:
```yaml
Anthropic__Model: ${ANTHROPIC_MODEL:-claude-haiku-4-5}
```

**Action 3:** Update `.env.example:19`:
```
ANTHROPIC_MODEL=claude-haiku-4-5
```

**Action 4:** Add startup-log line in `Program.cs` after `var app = builder.Build();`:
```csharp
// D-02: log resolved Anthropic model so any drift between code, compose, and env
// is visible in Sentry/logs without throwing.
var resolvedAnthropicOptions = app.Services
    .GetRequiredService<Microsoft.Extensions.Options.IOptions<AnthropicOptions>>().Value;
app.Logger.LogInformation(
    "Anthropic configuration resolved: Model={Model}, MaxTokens={MaxTokens}, CostPerClassification={Cost}",
    resolvedAnthropicOptions.Model,
    /* MaxTokens accessor — verify the property name in AnthropicOptions when planning */ "n/a",
    resolvedAnthropicOptions.CostPerClassification);
```

> **Planner note:** `AnthropicOptions` only exposes `ApiKey`, `Model`, `CostPerClassification` per the read at `Backend/src/TaxReader.Infrastructure/Configuration/AnthropicOptions.cs`. The `MaxTokens` env var in `docker-compose.yml:39` (`Anthropic__MaxTokens`) does NOT bind to a property in the current options class. This is a separate concern (a config that goes nowhere) — DO NOT fix in Phase 1; flag for the deferred follow-up hygiene pass. Log only `Model`, `CostPerClassification` from the options class today.

**Action 5:** Add to `CLAUDE.md` (in the "Project" section, near constraints):
```markdown
**Anthropic model:** `claude-haiku-4-5` is the production default — ~10× cheaper and ~3-5× faster than Sonnet, sufficient for the 13-DE-category classification choice. Single source of truth lives in `Backend/src/TaxReader.Infrastructure/Configuration/AnthropicOptions.cs`. Override per-environment via `Anthropic__Model` env var.
```

### Pattern 6: Serilog enrichers via appsettings.json (D-17)

**File:** `Backend/src/TaxReader.Api/appsettings.json` — add `Enrich` array:
```json
{
  "Serilog": {
    "Using": [
      "Serilog.Sinks.Console",
      "Serilog.Enrichers.Environment"
    ],
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft": "Warning",
        "Microsoft.EntityFrameworkCore": "Warning",
        "System": "Warning"
      }
    },
    "Enrich": [
      "FromLogContext",
      "WithEnvironmentName"
    ],
    "WriteTo": [
      {
        "Name": "Console",
        "Args": {
          "outputTemplate": "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}"
        }
      }
    ]
  }
}
```

**Why both `Using` and `Enrich`:** `Serilog.Settings.Configuration` reflects against named methods. Adding `"FromLogContext"` to `Enrich` invokes `LoggerEnrichmentConfiguration.FromLogContext()` from core Serilog (no Using needed). Adding `"WithEnvironmentName"` invokes the extension from `Serilog.Enrichers.Environment`, which `Using` must reference so the assembly is scanned. [CITED: https://github.com/serilog/serilog-settings-configuration#configuration-format]

**Output template explained:** `{Properties:j}` emits all enriched/contextual properties as a JSON object after the message — this is how `EnvironmentName` and `ReceiptFileId` appear in console output. In production, planner may want to swap to `Serilog.Formatting.Compact.CompactJsonFormatter` via a separate `appsettings.Production.json` (Claude's discretion per CONTEXT.md).

### Pattern 7: LogContext push in upload handler (D-18)

**File:** `Backend/src/TaxReader.Application/Commands/UploadReceiptFilesHandler.cs`

**Add `using Serilog.Context;` at top.** Then inside the existing `foreach (var file in command.Files)` loop, after `dbContext.ReceiptFiles.Add(receiptFile);` (currently line 92), wrap the rest of the per-file try block:

```csharp
// D-18: every log line emitted while processing this receipt file carries
// the ID. Phase 3 will add LogContext.PushProperty("JobId", jobId) at the
// Hangfire job boundary; explicitly NOT pre-wired now.
using (LogContext.PushProperty("ReceiptFileId", receiptFile.Id))
{
    // existing extraction / parsing / save block moves into here
}
```

**Important:** the handler currently uses the upstream `IAppDbContext` reference, not `ILogger<T>`. The handler does not log anything today. The `LogContext.PushProperty` scope still works because every nested service that injects `ILogger<T>` will pick up the property automatically — `LogContext` flows across async boundaries via `AsyncLocal`. [CITED: https://github.com/serilog/serilog/wiki/Enrichment#the-logcontext]

**Project reference check:** `TaxReader.Application` does NOT currently reference `Serilog.Context`. The `LogContext` static class lives in the `Serilog` package itself (a transitive dependency via `Serilog.AspNetCore`). The `Application` project may need a direct `<PackageReference Include="Serilog" />` or to receive the symbol through the existing `Serilog.AspNetCore` reference path. [ASSUMED] verify during planning — if `dotnet build Backend` after adding `using Serilog.Context;` fails with CS0246 (`'Serilog' could not be found`), add `<PackageReference Include="Serilog" />` to `TaxReader.Application.csproj` and `<PackageVersion Include="Serilog" Version="..." />` to `Directory.Packages.props`. The Serilog version transitively bundled with `Serilog.AspNetCore 9.0.0` should be queried via `dotnet list package --include-transitive` to pin the matching version.

### Pattern 8: Hygiene check CI step (D-06)

**File:** `.github/workflows/ci.yml` — `hygiene-check` job:

```yaml
  hygiene-check:
    name: Hygiene check (no PII / build artifacts)
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - name: Verify no leaked storage / build artifacts
        shell: bash
        run: |
          set -e
          violations=()
          for path in \
            "storage" \
            "Backend/storage" \
            "Backend/src/TaxReader.Api/storage" ; do
            if [ -d "$path" ]; then
              violations+=("Forbidden directory: $path")
            fi
          done

          # Pattern globs — match any build-diag*.txt or *.binlog at any depth.
          while IFS= read -r f; do
            violations+=("Forbidden file: $f")
          done < <(find . -type f \
            \( -name 'build-diag*.txt' -o -name '*.binlog' \) \
            -not -path './.git/*' 2>/dev/null)

          if [ ${#violations[@]} -gt 0 ]; then
            printf 'Hygiene check FAILED:\n'
            printf '  - %s\n' "${violations[@]}"
            exit 1
          fi
          printf 'Hygiene check passed.\n'
```

### Pattern 9: GitHub Actions CI workflow skeleton (D-08)

**File:** `.github/workflows/ci.yml`

```yaml
name: CI

on:
  pull_request:
    branches: [main]
  push:
    branches: [main]

# Cancel in-progress runs for the same PR ref; never cancel main.
concurrency:
  group: ${{ github.workflow }}-${{ github.ref }}
  cancel-in-progress: ${{ github.event_name == 'pull_request' }}

jobs:
  hygiene-check:
    # … as in Pattern 8 …

  backend-build-test:
    name: Backend build + test
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'
          # CPM cache key: include Directory.Packages.props because that's the
          # central NuGet version manifest. Including it prevents stale cache
          # restoration when versions are bumped.
          cache: true
          cache-dependency-path: |
            Backend/**/packages.lock.json
            Backend/Directory.Packages.props
      - name: Restore
        working-directory: Backend
        run: dotnet restore
      - name: Build
        working-directory: Backend
        run: dotnet build --no-restore --configuration Release
      - name: Test
        working-directory: Backend
        run: dotnet test --no-build --configuration Release --verbosity normal

  frontend-lint-build:
    name: Frontend lint + build
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-node@v4
        with:
          node-version: '22'
          cache: 'npm'
          cache-dependency-path: Frontend/package-lock.json
      - name: Install
        working-directory: Frontend
        run: npm ci
      - name: Lint
        working-directory: Frontend
        run: npm run lint
      - name: Build
        working-directory: Frontend
        run: npm run build
```

**[VERIFIED: https://github.com/actions/setup-dotnet — cache parameter accepts boolean; cache-dependency-path accepts multi-line glob patterns]**

**Caveat for `packages.lock.json`:** Central Package Management does NOT auto-generate `packages.lock.json` files unless `<RestorePackagesWithLockFile>true</RestorePackagesWithLockFile>` is set in the project files or `Directory.Build.props`. [ASSUMED] If the cache setup fails for missing lock files, two alternatives:
1. Enable lock files: add `<RestorePackagesWithLockFile>true</RestorePackagesWithLockFile>` to `Backend/Directory.Build.props`. This regenerates lock files on first restore — commit them.
2. Skip the lock-file-glob and cache only on `Directory.Packages.props` hash: this is less precise but works without enabling lock files.

Recommend option 1 (enable lock files); it's a one-time, low-risk change that gives reproducible CI builds and a more granular cache key. Document in the plan as a "verify after first CI run" item.

### Anti-Patterns to Avoid

- **Don't use `sentry.client.config.ts`** in Next.js 16 — it was deprecated in Next 15.3 in favour of `instrumentation-client.ts`. Older Sentry tutorials (and Claude's training data) reference the old name. [CITED: https://nextjs.org/docs/app/api-reference/file-conventions/instrumentation-client version history]
- **Don't add `Sentry.Serilog` on top of `Sentry.AspNetCore`** — they double-capture every log line. Only one is needed.
- **Don't try to `WithOrigins()` with zero arguments** for the deny-all CORS path — that throws. Instead, register the policy and just don't call `WithOrigins`.
- **Don't pass `authToken` to `withSentryConfig`** in Phase 1 — that triggers source-map upload, which CONTEXT.md `<deferred>` explicitly excludes.
- **Don't wire Sentry tags from the upload handler** (e.g. `Sentry.SetTag("vendor", receipt.Vendor)`) — vendor names are PII per D-14 #6. Even if the scrubber would strip them, the safer pattern is "never set them in the first place."
- **Don't `LogContext.PushProperty` outside of a `using` statement** — the property leaks into unrelated log lines forever (it's `AsyncLocal`-scoped to the calling task).

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| HTTP request body / header capture for error context | Custom middleware that copies headers into log scope | `Sentry.AspNetCore` integration | Already done; `MaxRequestBodySize` + `BeforeSend` is the documented control surface |
| User-ID hashing for telemetry | Custom hash util sprinkled across codebase | Single `SentryScrubbing.HashUserId` (Pattern 1) | DRY; one place to evolve |
| Correlation ID generation + propagation | Custom `X-Correlation-Id` header + middleware | `UseSerilogRequestLogging` (already in place) + `LogContext.PushProperty` | Built-in `RequestId` is sufficient at this scale; D-19 explicitly opts out of custom header |
| Webpack/Turbopack source-map plugin for Sentry | Custom Vite/webpack config | `withSentryConfig()` from `@sentry/nextjs` | Handles source-maps + telemetry routing automatically when enabled |
| Environment-name property on every log line | `LogContext.PushProperty("Environment", env)` in `Program.cs` | `Enrich.WithEnvironmentName()` from `Serilog.Enrichers.Environment` | Reads `ASPNETCORE_ENVIRONMENT` automatically; one config line vs runtime code |
| File-presence check for hygiene | Custom .NET tool / Bash heredoc inline | The `find` + `for path in` pattern in Pattern 8 | Simple, no extra dependencies, easy to read in CI logs |

**Key insight:** Sentry's ASP.NET Core integration and Next.js integration BOTH automatically wire ASP.NET Core's `IDiagnosticContext` (backend) and Next.js's `instrumentation` hooks (frontend). The integration provides 80% of what we need; our custom code is just the PII scrubber and the consent gate. Don't reinvent.

## Runtime State Inventory

> Phase 1 includes a rename-flavoured cleanup (delete leaked `storage/` PDFs + `build-diag.txt`). Inventory below covers what other state could embed those paths.

| Category | Items Found | Action Required |
|----------|-------------|------------------|
| Stored data | None — no DB rows reference disk paths. Migration `20260420055623_RemoveStoragePath` already dropped `receipt_files.storage_path` column. Verified via `Grep storage in Backend/src` (only matches are migration history). | None |
| Live service config | None — Caddy, Postgres, Tesseract, Anthropic do not reference `storage/`. The `web` and `api` containers do not mount any host volume into a `storage/` path (only `postgres-data` and `caddy-data` volumes exist per `docker-compose.yml`). | None |
| OS-registered state | None — no Windows Task Scheduler / pm2 / launchd / systemd registrations reference TaxReader paths in this repo. | None |
| Secrets / env vars | None — no env var refers to `storage/`. `ANTHROPIC_MODEL` env var rename in D-02 is a value change, not a key rename, so existing secrets stores are unaffected. | None |
| Build artifacts / installed packages | `build-diag.txt` at repo root (`ls D:/Programming/Repos/TaxReader/build-diag.txt` confirmed exists, ~size unknown but caller said "untracked"). `Backend/src/TaxReader.Api/storage/2026/04/` contains 2 PDFs (verified via Glob). Both are deletion targets per D-04 and D-05. | Delete from disk per D-04. |

**Nothing else found in any category** — verified via `Grep` for `storage`/`StoragePath`/`WriteAllBytes`/`FileStream`/`Directory.Create` across `Backend/src`. The PDFs in `storage/2026/04/` are pure dev-time leftovers from a previous code path that was already removed by the EF migration above.

## Common Pitfalls

### Pitfall 1: Sentry .NET SDK initialization order matters
**What goes wrong:** Calling `UseSentry()` after `builder.Services.AddInfrastructure(...)` means Sentry doesn't see early-startup exceptions from the DI container.
**Why it happens:** Sentry's host integration registers itself as the outermost exception boundary. If something throws during DI registration (e.g. a misconfigured `IOptions<T>`), it must be inside the Sentry-instrumented region.
**How to avoid:** Place `builder.WebHost.UseSentry(...)` as the **first** thing after `var builder = WebApplication.CreateBuilder(args);`. Comment-anchor the line so future contributors don't reorder it.
**Warning signs:** Errors during startup that don't appear in Sentry but DO appear in stdout.

### Pitfall 2: Serilog `appsettings.json` `Enrich` array doesn't actually enable enrichers if the assembly isn't in `Using`
**What goes wrong:** You add `"WithEnvironmentName"` to `Enrich` but no `EnvironmentName` property appears on log events.
**Why it happens:** `Serilog.Settings.Configuration` resolves enricher names by reflecting over the assemblies listed in `Using`. If `Serilog.Enrichers.Environment` isn't there, the name is silently ignored.
**How to avoid:** Always pair an enricher entry in `Enrich` with the corresponding assembly in `Using` (except for `FromLogContext`, which is in core Serilog). [CITED: https://github.com/serilog/serilog-settings-configuration]
**Warning signs:** No build error, but log lines lack the expected property. Diagnostic: log a test message at startup and inspect output JSON.

### Pitfall 3: `instrumentation-client.ts` runs in production builds even when DSN is unset
**What goes wrong:** D-16 requires no Sentry traffic in production until Phase 6, but if you only check `if (DSN) Sentry.init(...)`, the file still executes and adds bundle weight.
**Why it happens:** `instrumentation-client.ts` is bundled by Next.js into the client manifest unconditionally; the gate is the `Sentry.init` call, not the file's existence.
**How to avoid:** The pattern in Pattern 3 is correct — `Sentry.init` is gated on `NEXT_PUBLIC_SENTRY_ENABLED === "true"`. The bundle size cost is acceptable (~50KB) for the optionality of being able to flip the env var without redeploying. If even that bundle cost is unacceptable, dynamic-import the SDK: `const { init } = await import("@sentry/nextjs");`. Phase 1 doesn't need that optimization.
**Warning signs:** Network panel shows no Sentry requests but `@sentry/nextjs` is in the production bundle. (This is fine for Phase 1.)

### Pitfall 4: `LogContext.PushProperty` outside `using` block leaks into unrelated requests
**What goes wrong:** Without the `using` scope, the property persists in the `AsyncLocal` until the task tree dies, contaminating subsequent log lines.
**Why it happens:** `LogContext.PushProperty` returns an `IDisposable` — disposal is what unwinds the property. Forgetting `using` means it never disposes.
**How to avoid:** Always `using (LogContext.PushProperty(...))`. Treat as the same pattern as `using var stream = ...`.
**Warning signs:** Log lines from request B carry `ReceiptFileId` from request A.

### Pitfall 5: GitHub Actions cache invalidation fails silently with CPM
**What goes wrong:** A NuGet version bump in `Directory.Packages.props` doesn't invalidate the cache; CI restores stale packages and never sees the new version. Tests pass with the wrong dependency.
**Why it happens:** `actions/setup-dotnet@v4`'s default cache key is hashed from `**/*.csproj` and `**/packages.lock.json`. With CPM and no lock files, version bumps live in `Directory.Packages.props`, which is NOT in the default key.
**How to avoid:** Pattern 9 already includes `Backend/Directory.Packages.props` in `cache-dependency-path`. **Confirm this works on the first PR** — if `actions/setup-dotnet` complains about the missing path or doesn't honour it, fall back to enabling `<RestorePackagesWithLockFile>true</RestorePackagesWithLockFile>` and committing the resulting lock files.
**Warning signs:** "Cache hit" in CI logs but the wrong package version is restored.

### Pitfall 6: `withSentryConfig` injects build-time hooks that fail without org/project
**What goes wrong:** `withSentryConfig(nextConfig, {})` works in dev but fails the production build with errors about missing `org` / `project` / `authToken`.
**Why it happens:** The plugin only enables source-map upload when `authToken` is present, but it always validates `org` and `project` during the production build pass.
**How to avoid:** Either (a) provide `org` and `project` as env vars even when the project doesn't exist yet (the values are inert without `authToken`), or (b) set `silent: true` and conditionally call `withSentryConfig` only when env vars are present:
```typescript
export default process.env.NEXT_PUBLIC_SENTRY_ENABLED === "true"
  ? withSentryConfig(nextConfig, { silent: true, org: process.env.SENTRY_ORG, project: process.env.SENTRY_PROJECT })
  : nextConfig;
```
**Warning signs:** `npm run build` fails with `[@sentry/nextjs] Sentry CLI: error: ...`.

### Pitfall 7: Sentry DSN in Next.js public env var leaks to client bundle
**What goes wrong:** `NEXT_PUBLIC_SENTRY_DSN` is intentionally public — it ships in the JS bundle. People sometimes confuse this with a secret.
**Why it happens:** Sentry frontend DSNs ARE public symbols (the project ID encoded in the DSN is the only "auth" needed for write-only event submission, by design).
**How to avoid:** Use `NEXT_PUBLIC_SENTRY_DSN` for client-only init. For server-side init (`sentry.server.config.ts`), use a separate non-public variable (e.g. `SENTRY_DSN_FRONTEND_SERVER`) so it's not bundled. CONTEXT.md already calls this out: "Sentry DSN is a public symbol (frontend bundle ships it) so no secret needed for builds."
**Warning signs:** None at runtime; this is a docs/clarity concern.

## Code Examples

Verified patterns from official sources — see Implementation Patterns above for the full code shapes. Quick reference:

### Backend Sentry init
```csharp
// Source: https://docs.sentry.io/platforms/dotnet/guides/aspnetcore/
builder.WebHost.UseSentry(options =>
{
    options.Environment = builder.Environment.EnvironmentName;
    options.SendDefaultPii = false;
    options.MaxRequestBodySize = Sentry.Extensibility.RequestSize.None;
    options.SetBeforeSend((ev, hint) => SentryScrubbing.Scrub(ev));
});
```

### Frontend Sentry init (Next.js 16)
```typescript
// Source: https://docs.sentry.io/platforms/javascript/guides/nextjs/manual-setup/
//        + https://nextjs.org/docs/app/api-reference/file-conventions/instrumentation-client
import * as Sentry from "@sentry/nextjs";

if (process.env.NEXT_PUBLIC_SENTRY_ENABLED === "true") {
  Sentry.init({
    dsn: process.env.NEXT_PUBLIC_SENTRY_DSN,
    sendDefaultPii: false,
    tracesSampleRate: 0,
    beforeSend: (event) => scrubEvent(event),
  });
}
export const onRouterTransitionStart = Sentry.captureRouterTransitionStart;
```

### LogContext push
```csharp
// Source: https://github.com/serilog/serilog/wiki/Enrichment#the-logcontext
using Serilog.Context;

using (LogContext.PushProperty("ReceiptFileId", receiptFile.Id))
{
    // every log line emitted in this scope and all awaited continuations
    // carries ReceiptFileId — including from injected ILogger<T> services.
}
```

### CORS deny-all
```csharp
// Verified pattern — empty Origins list = deny all cross-origin requests.
options.AddDefaultPolicy(policy =>
{
    if (corsOrigins is { Length: > 0 })
    {
        policy.WithOrigins(corsOrigins).AllowAnyHeader().AllowAnyMethod();
        return;
    }
    if (env.IsDevelopment())
    {
        policy.WithOrigins("http://localhost:3000").AllowAnyHeader().AllowAnyMethod();
        return;
    }
    Log.Warning("CORS_ALLOWED_ORIGINS unset — denying all cross-origin requests.");
    // No WithOrigins call → empty Origins → middleware rejects every preflight.
});
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| `sentry.client.config.ts` for client init | `instrumentation-client.ts` | Next.js 15.3 (early 2025); Sentry SDK 8.x updated docs | Filename rename — wrong filename means client init silently never runs |
| `Sentry.AspNetCore` 3.x with `BeforeSend` callback property | `Sentry.AspNetCore` 6.x with `SetBeforeSend(...)` method | SDK 4.0 (mid-2024) introduced the Set* method shape for fluent options; 6.x removed the old property | Only naming; functionality identical |
| Manual `addEventListener("error", ...)` for client error capture | `Sentry.captureRouterTransitionStart` exported from `instrumentation-client.ts` | Next.js 15.3 + Sentry SDK 8.x | Built-in router-transition breadcrumbs eliminate the need for custom navigation hooks |
| `Serilog.Enrichers.CorrelationId` (third-party `ekmsystems` package) | Built-in `Enrich.FromLogContext()` + `LogContext.PushProperty` | Always — `FromLogContext` has been in Serilog since v1.0 | Removes one third-party dependency; the `ekmsystems` package is essentially unmaintained |

**Deprecated/outdated:**
- The `BeforeSend` property setter in `Sentry.AspNetCore` is removed in 6.x — use `SetBeforeSend(...)` method.
- `next.config.js` (CommonJS) → `next.config.ts` (TypeScript) — Next.js 14+ supports both; the repo already uses `.ts`.
- `app.UseRouting()` + `app.UseEndpoints()` (ASP.NET Core 5 style) — modern Minimal APIs use `app.MapGroup` directly. Not a Phase 1 concern.

## Project Constraints (from CLAUDE.md)

The following directives from `D:/Programming/Repos/TaxReader/CLAUDE.md` constrain Phase 1 implementation:

### Mandatory directives
- **Think before coding:** State assumptions explicitly. Surface confusion. (Plan should pre-flag any ambiguity in package versions or option defaults.)
- **Simplicity first:** Minimum code. No speculative abstractions. (Don't build a `ISentryScrubber` interface for a single implementation; the static class is enough.)
- **Surgical changes:** Touch only what you must. (Don't refactor unrelated parts of `Program.cs` while wiring Sentry.)
- **Goal-driven execution:** Define success criteria. (Use ROADMAP.md Phase 1 success criteria as the verification target.)

### Backend conventions to honour
- File-scoped namespaces (`namespace TaxReader.Api.Observability;` not block scoped) — matches existing files.
- Primary constructors for DI (`SentryScrubbing` is static so no DI needed).
- `Result<T>` for error handling — Phase 1 doesn't add any handlers; existing `Result<T>` flow is unchanged.
- File-scoped namespaces, `<Nullable>enable</Nullable>`, `<LangVersion>latest</LangVersion>` — already inherited from `Directory.Build.props`.
- Always pass `CancellationToken` (no new async methods in Phase 1).
- **Logging:** structured logging with named placeholders. The Sentry warning log and the model-resolved log in Pattern 5 must use `{Property}` placeholders, never string interpolation.
- **Configuration:** `IOptions<T>` pattern with `SectionName` constants. Sentry options are configured directly on `SentryAspNetCoreOptions` via the `UseSentry` callback — no separate POCO needed.
- **Environment variables `__`-nested:** `Sentry__Dsn`, `Sentry__Environment` (matches CONTEXT.md note).

### Frontend conventions to honour
- TypeScript strict mode (`tsconfig.json:strict: true`) — the scrubber must satisfy strict null checks.
- `"use client"` directive — `instrumentation-client.ts` does NOT need this (it's a special Next.js convention file, not a React component).
- File names kebab-case — `instrumentation-client.ts` is exactly that pattern.
- German user-facing strings — Sentry breadcrumbs and messages are dev-facing, so English is fine. The `Caddy CORS denied` warning never reaches users.
- TanStack Query / never call axios directly — Phase 1 frontend changes are all init-config files, no component changes.

### `Frontend/AGENTS.md` warning
> "This is NOT the Next.js you know. This version has breaking changes — APIs, conventions, and file structure may all differ from your training data."

This research has explicitly verified the Next.js 16 file-convention change (`instrumentation-client.ts` not `sentry.client.config.ts`) against current docs. Plans for the Sentry frontend integration should reference Pattern 3 directly and cite the Next.js docs URL above.

### What CLAUDE.md does NOT yet cover (drift to flag in plan output)
- `BelegPilot` vs `TaxReader` naming inconsistency in `CLAUDE.md` (line 4: "BelegPilot is an API-first system…"). Phase 1 D-12 modifies `CLAUDE.md` to add the Anthropic model decision; planner may opt to fix the rebrand line in the same patch (low risk; one-line change). Otherwise it sits in the `<deferred>` rebrand pass.
- Project structure section (lines 100–115) lists `BelegPilot.*` project names — the actual code uses `TaxReader.*`. Same flag.

## Validation Architecture

> `workflow.nyquist_validation` is `true` in `.planning/config.json`. Section included.

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xUnit 2.9.2 + FluentAssertions 7.0.0 + Moq 4.20.72 [VERIFIED: `Backend/Directory.Packages.props`] |
| Config file | `Backend/tests/TaxReader.UnitTests/TaxReader.UnitTests.csproj` |
| Quick run command | `dotnet test Backend --filter "FullyQualifiedName~Phase1" --no-restore` |
| Full suite command | `dotnet test Backend --configuration Release` |

Frontend has no test framework (CONTEXT.md `<deferred>` confirms Vitest/Playwright land in Phase 7). Phase 1 frontend deliverables are init-config files; the verification is **`npm run lint && npm run build`** succeeds.

### Phase Requirements → Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| FND-01 | `storage/` and `build-diag.txt` not present in repo | smoke (CI hygiene step) | `find . -name 'build-diag*.txt' -o -path '*/storage/*' \| head -1` returns empty | ❌ Wave 0 — `hygiene-check` job in `ci.yml` |
| FND-02 | Anthropic model default in `AnthropicOptions.cs`, `docker-compose.yml`, `.env.example` all match `claude-haiku-4-5`; startup log emits resolved value | unit + smoke | unit: `dotnet test Backend --filter "AnthropicOptions_Default_IsHaiku"`; smoke: assert log line on `dotnet run` startup | ❌ Wave 0 — new test `AnthropicOptionsTests.Default_Model_IsHaiku4_5` |
| FND-03 | CORS in non-Dev env with no `CORS_ALLOWED_ORIGINS` denies cross-origin requests; Dev env keeps `localhost:3000` | unit | `dotnet test Backend --filter "CorsConfiguration_Production_NoOrigins_DeniesAll"` | ❌ Wave 0 — new test in API project test harness; uses `WebApplicationFactory<Program>` |
| FND-04 | CI workflow runs on PR + push to main; three jobs are merge-blocking | manual-only | Inspect first PR's checks page; verify "required" badge | n/a — config existence check |
| FND-05 | `README.md` exists at repo root; references `cp .env.example .env`, `docker compose up --build`, `https://localhost`; links `CLAUDE.md` | smoke | `test -f README.md && grep -q "docker compose up --build" README.md && grep -q ".env.example" README.md` | ❌ Wave 0 — bash assertion script or `.github/workflows/ci.yml` step |
| OBS-01 | Sentry .NET SDK loaded; `BeforeSend` strips PII fields per D-14 | unit | `dotnet test Backend --filter "SentryScrubbingTests"` | ❌ Wave 0 — new test class `SentryScrubbingTests` exercises each scrubber rule (request body, query string allow-list, headers allow-list, UUID path masking, user.id_hash) |
| OBS-02 | Serilog config loads `WithEnvironmentName` enricher; upload handler emits log lines with `ReceiptFileId` property when classification is exercised | unit | `dotnet test Backend --filter "SerilogEnrichmentTests"` | ❌ Wave 0 — new test class verifies `appsettings.json` loads correctly + handler test asserts `ReceiptFileId` is in log scope using a captured `ILogger<T>` mock |

### Sampling Rate
- **Per task commit:** `dotnet build Backend && dotnet test Backend --filter "FullyQualifiedName~Phase1"` (~30s)
- **Per wave merge:** `dotnet test Backend --configuration Release` + `cd Frontend && npm run lint && npm run build` (~2-3 min)
- **Phase gate:** Full suite green; CI workflow green on PR; manual smoke of `docker compose up --build` to verify startup log line and Sentry init no-op when DSN unset.

### Wave 0 Gaps
- [ ] `Backend/tests/TaxReader.UnitTests/Configuration/AnthropicOptionsTests.cs` — covers FND-02 (default model assertion)
- [ ] `Backend/tests/TaxReader.UnitTests/Cors/CorsConfigurationTests.cs` — covers FND-03 (deny-all in production); requires `WebApplicationFactory<Program>` test rig — verify if `Microsoft.AspNetCore.Mvc.Testing` is needed (NOT currently in `Directory.Packages.props`); add as Wave 0 dependency
- [ ] `Backend/tests/TaxReader.UnitTests/Observability/SentryScrubbingTests.cs` — covers OBS-01 (six scrubber rules)
- [ ] `Backend/tests/TaxReader.UnitTests/Observability/SerilogEnrichmentTests.cs` — covers OBS-02 (config + handler-side `LogContext` assertion)
- [ ] `Backend/tests/TaxReader.UnitTests/Helpers/TestLoggerProvider.cs` (or use existing — verify `Backend/tests/TaxReader.UnitTests/Helpers/`) — captures log events for inspection during enrichment + scope tests
- [ ] Framework install: `Microsoft.AspNetCore.Mvc.Testing` (likely needed for FND-03 integration-flavoured test) — add to `Directory.Packages.props` and the test csproj

## Security Domain

> `workflow.security_enforcement` is `true` (ASVS Level 1 per `.planning/config.json`). Section included.

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V2 Authentication | no — auth is Phase 2 (AUTH-01..03) | n/a in Phase 1 |
| V3 Session Management | no — refresh tokens are Phase 2 | n/a in Phase 1 |
| V4 Access Control | no — already enforced by `RequireAuthorization()` in `Program.cs:153` | n/a in Phase 1 |
| V5 Input Validation | yes — Sentry `BeforeSend` is a sanitizer for outbound telemetry, not input validation, but FluentValidation `UploadReceiptFilesValidator` and others remain in the chain. Phase 1 does not modify input validation. | FluentValidation 12.0.0 (existing) |
| V6 Cryptography | partial — `SentryScrubbing.HashUserId` uses `SHA256` from `System.Security.Cryptography`. SHA-256 is appropriate for a non-reversible identifier (not for password hashing — BCrypt remains for that). Don't hand-roll. | `System.Security.Cryptography.SHA256` (built-in) |
| V7 Error Handling & Logging | **yes — central to OBS-01 + OBS-02** | Sentry `BeforeSend` scrubber (D-14) + Serilog structured templates + `LogContext.PushProperty` |
| V8 Data Protection | yes — D-14's PII scrub is the V8.1 control ("sensitive data is not stored in error tracking") | `BeforeSend` Sentry scrubbers (Pattern 1 + Pattern 3) |
| V12 Files & Resources | yes — D-04's deletion of leaked PDFs and D-05's `.gitignore` rules are V12.5 ("uploaded files are not stored in the same context") and V12.7 ("backups exclude PII") | `.gitignore` + CI `hygiene-check` job (Pattern 8) |
| V13 API & Web Service | partial — D-07 (CORS deny-all) is V13.2.6 ("CORS configurations restrict origins") | ASP.NET Core CORS middleware deny-all (Pattern 4) |
| V14 Configuration | yes — D-02 (single source of truth for Anthropic model) is V14.2.1 ("application secrets and configuration are not embedded in source"; pull-through to `IOptions<T>` from env) | `IOptions<T>` + env-var binding (existing pattern) |

### Known Threat Patterns for .NET 10 + Next.js 16 + Sentry

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| User PII leaked to error tracking provider | Information Disclosure | Default-deny `BeforeSend` scrubber (D-14); EU DSN routing for DSGVO data residency (D-13) |
| CORS misconfiguration allows credentialed cross-origin requests | Elevation of Privilege | Production deny-all when origins env var unset (D-07); same-origin via Caddy proxy is unaffected |
| Dev secrets / receipts committed to repo | Information Disclosure | `.gitignore` extension (D-05) + CI `hygiene-check` step that fails the build (D-06) |
| Stale dev artifacts (PDFs) shipped in container image | Information Disclosure | Delete from working tree (D-04); `Backend/Dockerfile` doesn't `COPY` the storage path explicitly, but `COPY . .` patterns would — verify `Backend/Dockerfile` does NOT include the storage path; if it does, planner adds `.dockerignore` |
| Source maps reveal API surface to attackers | Information Disclosure | Source-map upload deferred (CONTEXT.md `<deferred>`); `withSentryConfig` does NOT upload without `authToken` |
| Sentry DSN treated as a secret in CI | Operational complexity (not strictly STRIDE) | Document that frontend DSN is public and ships in bundle (CONTEXT.md D-11); backend DSN goes via env var only |
| Logging sensitive data into structured fields | Information Disclosure | Use of `LogContext.PushProperty` for IDs only (not PII); structured templates with `{Property}` placeholders never string-interpolate user data |

**Backend Dockerfile spot-check (planner action):** Verify `Backend/Dockerfile` does NOT `COPY storage/` into the build image. If it does (likely via `COPY . .`), add `.dockerignore` at `Backend/.dockerignore` listing `src/TaxReader.Api/storage/` and `**/build-diag*.txt`. [ASSUMED — research did not read `Backend/Dockerfile`. Add as plan checklist item.]

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | `Application` project does NOT currently reference `Serilog` directly; adding `using Serilog.Context;` may require a new `<PackageReference Include="Serilog" />`. | Pattern 7 | Low — build error is caught immediately on first compile; planner can resolve in 1 task action |
| A2 | `actions/setup-dotnet@v4` honours `Backend/Directory.Packages.props` in `cache-dependency-path` even when `packages.lock.json` files are not present | Pitfall 5 + Pattern 9 | Medium — if cache invalidation is broken, CI may restore stale packages. Mitigation: enable `<RestorePackagesWithLockFile>true</RestorePackagesWithLockFile>` in `Backend/Directory.Build.props` and commit lock files. Document as "verify on first PR" |
| A3 | `Backend/Dockerfile` uses `COPY . .` and would therefore copy the leaked `storage/` PDFs into the production image | Security Domain | Medium — if true, current production images contain PII. Action: planner reads `Backend/Dockerfile` first, then either confirms exclusion exists or adds `.dockerignore`. Independent of D-04 disk deletion |
| A4 | The `Serilog` package version transitively pulled by `Serilog.AspNetCore 9.0.0` is acceptable for `LogContext.PushProperty` | Pattern 7 | Very low — `LogContext` has been in Serilog since v1.0; any modern version works |
| A5 | `SubtleCrypto`-async hashing in client-side scrubber is unnecessary because we delete `event.user.id` outright in the browser scrubber | Pattern 3 | Low — D-14 #5 says "keep a hash of the user ID as `user.id_hash`" but only the backend has access to a stable user ID without async crypto. Frontend dropping the ID is a defensible interpretation; flag for discuss-phase if user-correlation in frontend errors is later judged necessary |
| A6 | `Sentry.AspNetCore` 6.4.1 actually compiles on .NET 10 (not just .NET 8/9 + supported via netstandard) | Standard Stack | Low — the WebSearch + WebFetch both stated .NET 10 support. If a target-framework mismatch surfaces, downgrade to 6.3.x is unlikely to help (same TFM) — escalate to discuss-phase. Mitigation: planner runs `dotnet add package Sentry.AspNetCore --version 6.4.1` early as a smoke test |
| A7 | The `MaxTokens` option in `docker-compose.yml:39` (`Anthropic__MaxTokens`) is a config that goes nowhere because `AnthropicOptions.cs` doesn't expose it | Pattern 5 | Very low — flagged for deferred follow-up, not a Phase 1 blocker |

If any of A1, A2, A3 fire during execution, planner should pause and replan that specific task — not the whole phase.

## Open Questions

1. **Should `withSentryConfig` always wrap or be conditional in `next.config.ts`?**
   - What we know: When DSN is unset, the SDK is a no-op at runtime. But the build-time `withSentryConfig` plugin always validates `org`/`project` env vars when it runs.
   - What's unclear: Whether Sentry SDK 10.51.0 short-circuits validation when `dsn` is empty. Pitfall 6 documents both options.
   - Recommendation: Use the conditional form (Pitfall 6 code snippet) for Phase 1. It's strictly safer and removes the "set unused env vars" requirement.

2. **Does the upload handler need a direct `<PackageReference Include="Serilog" />` for `using Serilog.Context;`?**
   - What we know: `Serilog.Context.LogContext` lives in the `Serilog` package; `TaxReader.Application` references nothing Serilog-related directly today (it uses `IAppDbContext` etc.).
   - What's unclear: Whether the package reference flows transitively from `TaxReader.Api` → `TaxReader.Application` (likely NOT, since references go top-down).
   - Recommendation: Plan for an explicit `<PackageReference Include="Serilog" />` in `TaxReader.Application.csproj`. If turns out to be redundant, planner removes in cleanup.

3. **Where should the Sentry org/project come from?**
   - What we know: D-13 says "Sentry Developer Free tier on EU region." There's no concrete project name in CONTEXT.md.
   - What's unclear: Whether the user has already created a Sentry project (planner may need to surface this in execute-phase as a one-time setup step), or whether the plan should provision it programmatically (out of scope for Phase 1).
   - Recommendation: Plan documents the manual one-time setup ("Create Sentry org + 'taxreader-api' and 'taxreader-web' projects on sentry.eu.io; record DSNs in `.env`") as a prerequisite checklist, not a code task.

## Environment Availability

> Phase 1 deliverables touch the host CI environment + project tooling. Audit:

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| .NET 10 SDK | Backend build | ✓ (assumed — plan verifies via `dotnet --version`) | 10.0.x | — |
| Node.js 22 | Frontend build | ✓ (assumed — `node --version` ≥ 22) | 22.x | — |
| npm | Frontend install | ✓ (bundled with Node) | per Node | — |
| Docker Desktop | Local stack run | ✓ (assumed) | Compose v2 | — |
| GitHub Actions runner: `ubuntu-latest` | CI | ✓ (always available) | rolling | — |
| `actions/setup-dotnet@v4` | CI | ✓ | 4.x | — |
| `actions/setup-node@v4` | CI | ✓ | 4.x | — |
| Sentry account (EU region) | OBS-01 runtime telemetry | ✗ (assumed not yet provisioned) | n/a | Leave DSN unset; SDK becomes no-op. Planner adds "create EU project" to phase-1 prereq checklist |
| `npx @sentry/wizard@latest` | OPTIONAL frontend scaffolding | ✓ via npx | n/a | Manual scaffolding (Pattern 3) |

**Missing dependencies with no fallback:** None — Sentry account absence is a fallback case (no-op SDK).

**Missing dependencies with fallback:**
- Sentry EU account / DSN: SDK no-op when unset. Plan tags this as a "non-blocking external-account prereq" — phase can ship to merge with empty DSNs and Phase 1 success criterion #4 ("Sentry receives errors from .NET API and Next.js frontend with PII scrubbed") is verified post-merge by the user setting the env var.

## Sources

### Primary (HIGH confidence)
- **NuGet flatcontainer** for `sentry.aspnetcore` — verified 6.4.1 stable on 2026-05-04
- **NuGet flatcontainer** for `serilog.enrichers.environment` — verified 3.0.1 stable
- **NuGet flatcontainer** for `serilog.aspnetcore` — confirms 10.0.0 stable (peer of `Sentry.AspNetCore`)
- **npm registry** `npm view @sentry/nextjs version` — verified 10.51.0
- **npm registry** `npm view @sentry/nextjs peerDependencies` — verified Next.js 16 support
- [Sentry .NET ASP.NET Core docs](https://docs.sentry.io/platforms/dotnet/guides/aspnetcore/) — install command, `UseSentry` shape, `SetBeforeSend` signature
- [Sentry .NET options reference](https://docs.sentry.io/platforms/dotnet/guides/aspnetcore/configuration/options/) — `SendDefaultPii` defaults, `MaxRequestBodySize`, DSN binding
- [Sentry .NET sensitive-data scrubbing guide](https://docs.sentry.io/platforms/dotnet/guides/aspnetcore/data-management/sensitive-data/) — pattern for `SetBeforeSend` PII removal
- [Sentry Next.js manual setup](https://docs.sentry.io/platforms/javascript/guides/nextjs/manual-setup/) — `instrumentation-client.ts` + `instrumentation.ts` + `withSentryConfig`
- [Next.js 16 instrumentation-client docs](https://nextjs.org/docs/app/api-reference/file-conventions/instrumentation-client) — file convention, version history (introduced v15.3)
- [Serilog enrichment wiki](https://github.com/serilog/serilog/wiki/Enrichment) — `FromLogContext` is built-in; `LogContext.PushProperty` semantics
- [Serilog Enrichers Environment GitHub](https://github.com/serilog/serilog-enrichers-environment) — `WithEnvironmentName()` reads `ASPNETCORE_ENVIRONMENT`
- [Serilog Settings Configuration GitHub](https://github.com/serilog/serilog-settings-configuration) — `Using` + `Enrich` JSON config format
- [actions/setup-dotnet GitHub](https://github.com/actions/setup-dotnet) — `cache: true` + `cache-dependency-path` parameters
- [actions/setup-node GitHub](https://github.com/actions/setup-node) — `cache: 'npm'` + `cache-dependency-path` for monorepo
- **In-repo verification:**
  - `Backend/src/TaxReader.Api/Program.cs:88-112` — current CORS shape (read in research)
  - `Backend/src/TaxReader.Infrastructure/Configuration/AnthropicOptions.cs` — current default model
  - `Backend/src/TaxReader.Application/Commands/UploadReceiptFilesHandler.cs` — handler shape, no disk writes
  - `Backend/src/TaxReader.Infrastructure/Migrations/20260420055623_RemoveStoragePath.cs` — confirms storage_path was removed at migration level
  - `Frontend/next.config.ts` — no existing Sentry integration
  - `docker-compose.yml:38` — current Anthropic__Model default
  - `.env.example:19` — current ANTHROPIC_MODEL default
  - `Backend/Directory.Packages.props` — central package management format
  - `Frontend/package.json` — Next.js 16.2.2 + React 19.2.4
  - `.planning/config.json` — `nyquist_validation: true`, `security_enforcement: true`

### Secondary (MEDIUM confidence)
- WebSearch results for "Serilog.Enrichers.Environment WithEnvironmentName ASPNETCORE_ENVIRONMENT" — confirmed enricher reads env var
- WebSearch for "actions/setup-dotnet Directory.Packages.props" — confirmed cache-key strategy, cross-referenced multiple sources

### Tertiary (LOW confidence)
- None of the load-bearing claims rely on tertiary sources. All package versions and init shapes were directly verified.

## Metadata

**Confidence breakdown:**
- Standard stack: **HIGH** — every package version verified against NuGet/npm registry on 2026-05-04
- Architecture / init shapes: **HIGH** — verified against current Sentry + Next.js docs; in-repo file shapes confirmed
- Pitfalls: **HIGH** — Pitfalls 1, 4, 7 are documented in upstream issues; Pitfalls 2, 3, 5, 6 inferred from doc + version-history reading and explicitly cross-referenced
- Validation architecture: **MEDIUM** — Wave 0 test list is opinionated; planner may merge/split tests differently
- Security domain: **HIGH** — ASVS mapping is conservative (defaults to "yes" when in doubt)

**Research date:** 2026-05-04
**Valid until:** 2026-06-04 (estimate — Sentry SDK and Next.js are fast-moving; recheck major versions before Phase 7's alert tuning if more than ~30 days have elapsed)

---

*Phase: 01-foundation-cleanup-ci*
*Research completed: 2026-05-04*
