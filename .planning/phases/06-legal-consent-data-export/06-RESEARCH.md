# Phase 6: Legal + Consent + Data Export + AVVs - Research

**Researched:** 2026-06-02
**Domain:** DSGVO compliance, TTDSG cookie consent, EF Core append-only audit log, Hangfire async export, Next.js 16 instrumentation-client
**Confidence:** HIGH (all primary claims verified against live codebase)

---

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

**Legal Content (LEG-01, LEG-02, LEG-03, LEG-04):**
- D-01: Full German draft copy for all four pages; Kleinunternehmer §19 → no USt-IdNr.; sub-processors = Anthropic, Stripe, Sentry, BetterStack; every page renders "⚠ Entwurf – anwaltliche Prüfung ausstehend" marker.
- D-02: Lawyer-review gate tracked via `06-LEGAL-REVIEW.md` + blocking HUMAN-UAT item. Phase 7 does final sign-off.
- D-03: `/agb` and `/widerruf` inside existing `(legal)` route group, sharing `(legal)/layout.tsx`. Placeholders replaced with D-01 drafts.
- D-04: New site-wide Footer component (none exists) linking Impressum, Datenschutzerklärung, AGB, Widerrufsbelehrung, "Cookie-Einstellungen" from every page.

**Cookie Consent (LEG-05):**
- D-05: Custom lightweight `ConsentProvider` React context + `localStorage`; no CMP library.
- D-06: Two categories only — "Notwendig" (always on) and "Fehleranalyse" (Sentry; opt-in, not pre-ticked). Equal-prominence buttons "Alle akzeptieren" / "Nur notwendige". "Einstellungen" opens granular control.
- D-07: `NEXT_PUBLIC_SENTRY_ENABLED` is the deploy-level kill-switch. `Sentry.init` runs only when env-enabled AND runtime consent granted. `Sentry.close()` fires on revoke. No page reload.
- D-08: Consent revoke reachable from footer "Cookie-Einstellungen" link reopening the settings panel.

**Data Export (LEG-07):**
- D-09: Async Hangfire `ExportUserDataJob` + in-app download. No email/SMTP. Delivery is in-app status "Bereit – Herunterladen". Deliberate deviation from literal LEG-07 "emailed within 24h". Planner and verifier treat in-app delivery as acceptance-satisfying.
- D-10: Bundle stored transiently (`/tmp/exports/{token}.zip`); expiring, ownership-validated one-time token; purged after 24h by Hangfire cleanup job.
- D-11: Bundle = JSON + CSV, zipped: receipts, items, classifications, token_transactions, user's own `audit_log` entries, `README.txt`. Excludes password hash and internal noise.
- D-12: Existing `(authenticated)/settings/page.tsx` gains "Meine Daten exportieren" trigger with status states.

**Audit Log (LEG-08):**
- D-13: Explicit `IAuditLogger` interface (Application) + `AuditLogger` impl (Infrastructure). Five call sites: `DeleteAccountHandler`, `GrantTokensJob`, `RevokeTokensJob`, `RefreshTokenService` (replay), `SaveClassificationRuleHandler` (CLASS-05).
- D-14: Schema: `id uuid PK, action text/enum, actor_user_id uuid?, subject_user_id uuid?, metadata jsonb, created_at timestamptz`. Append-only. Retained indefinitely. Actor survives user deletion (nullable).
- D-15: DSGVO Art. 15 via the LEG-07 export bundle (`audit_log.json/.csv`). No separate audit-log endpoint.

**AVVs/DPAs + Marken:**
- D-16: AVV/DPA sign-off tracked via `06-AVV-TRACKING.md`; Datenschutzerklärung links each sub-processor's DPA + Drittland note. Signing is an operator HUMAN-UAT task.
- D-17: DPMA + EUIPO Marken search for "TaxReader" (Nizza classes 9+42) documented in `06-MARKEN-SEARCH.md`. Operator performs register lookups.

### Claude's Discretion
- Exact shadcn components for banner and export panel (follow base-nova patterns).
- Exact German microcopy wording within the agreed page/section structure.
- Zip/compression approach (`System.IO.Compression`).
- `AuditAction` enum value naming.
- Whether the consent settings panel is a dialog or a footer-anchored route.

### Deferred Ideas (OUT OF SCOPE)
- Email/SMTP infrastructure — not built.
- Third "Statistik"/analytics consent category — not added.
- Dedicated user-facing audit-log view/endpoint — Phase 7+ candidate.
- Final pre-launch lawyer sign-off — Phase 7 (QA-07).
- BetterStack uptime monitors + footer status-page link — Phase 7.
- Automatic Markenregister API integration — manual operator task.
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| LEG-01 | Impressum page (TMG §5) with name, address, contact email, USt-ID n/a (§19), ODR link; reachable from every page footer | D-04 Footer component + `(legal)/impressum/page.tsx` replacement; `auth-provider.tsx` PUBLIC_PATHS must include `/impressum` (already present) |
| LEG-02 | Datenschutzerklärung: DSGVO Art. 13/22/28, sub-processor list, Drittland-Übermittlung for Anthropic | `(legal)/datenschutz/page.tsx` replacement with full draft; links to sub-processor AVVs per D-16 |
| LEG-03 | AGB: StBerG-safe positioning, GoBD non-applicability, Widerrufsrecht clause, refund policy, VSBG signpost; lawyer-reviewed | New `(legal)/agb/page.tsx`; 06-LEGAL-REVIEW.md gate |
| LEG-04 | `/widerruf` page with full Widerrufsbelehrung text + Muster-Widerrufsformular | New `(legal)/widerruf/page.tsx`; Phase-5 `/widerruf` link now resolves |
| LEG-05 | TTDSG-compliant cookie banner — equal prominence, no pre-ticked, Sentry gated on consent | `ConsentProvider` + `cookie-banner.tsx`; `instrumentation-client.ts` consent gate; `Sentry.close()` on revoke |
| LEG-06 | AVVs/DPAs signed for Anthropic, Stripe, Sentry, BetterStack | 06-AVV-TRACKING.md operator checklist + Datenschutzerklärung links |
| LEG-07 | DSGVO Art. 20 self-serve data export: async job, JSON+CSV bundle, in-app delivery (D-09 deviation from literal "emailed") | `ExportUserDataJob` + download endpoint + settings page trigger + 24h purge job |
| LEG-08 | `audit_log` table + AuditLogger at five call sites; DSGVO Art. 15 via export | `AuditLogEntry` entity, `IAuditLogger` interface, `AuditLogger` impl, EF migration, 5 call-site additions |
| LEG-09 | DPMA + EUIPO Marken search for "TaxReader" classes 9+42 | 06-MARKEN-SEARCH.md operator checklist; if conflicted, rename forced before launch |
</phase_requirements>

---

## Summary

Phase 6 delivers the legal posture required for commercial DE launch. It touches five distinct technical domains: (1) replacing legal page placeholders with full German draft content; (2) a custom TTDSG consent banner with runtime Sentry gate; (3) an append-only audit log backed by a new `audit_log` EF entity; (4) a Hangfire async export job producing a zip bundle with transient storage; and (5) operator-tracked documents for AVV sign-off and Marken search.

The codebase is highly aligned with what this phase needs. Hangfire is already fully wired (PostgreSQL storage, recurring-job pattern, `IBackgroundJobClient`). The `(legal)` route group and both placeholder legal pages exist. The `instrumentation-client.ts` already gates `Sentry.init` on `NEXT_PUBLIC_SENTRY_ENABLED` — the consent gate slots in as an additional runtime condition within that existing guard. `Sentry.close()` is verified exported from `@sentry/nextjs` 10.52 (confirmed via `node -e`). The `IEntityTypeConfiguration<T>` + snake_case + `ApplyConfigurationsFromAssembly` pattern is well-established for the `AuditLogEntry` configuration.

The two integration-point landmines are: (a) EF Core InMemory provider does not support `jsonb` column type — tests using `Dictionary<string, object>` metadata must serialize to `string` for in-memory, but use `HasColumnType("jsonb")` in the real configuration; and (b) `instrumentation-client.ts` runs before React hydration, so `localStorage` is readable but the `ConsentProvider` React context is not available — the consent gate in `instrumentation-client.ts` must read `localStorage` directly, not via the React context.

**Primary recommendation:** Build in the order: (1) audit log entity + migration + IAuditLogger + 5 call sites; (2) export job + download endpoint + settings UI; (3) consent banner + Sentry gate; (4) legal pages + footer. Each step is independently testable.

---

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Audit log writes | API/Backend (Application handlers + jobs) | — | Business-meaning events, not generic EF interception |
| Audit log storage | Database (`audit_log` table, jsonb metadata) | — | Append-only rows; retained indefinitely |
| Export bundle generation | API/Backend (Hangfire job) | — | Reads DB, writes to /tmp; server-side only |
| Export file delivery | API/Backend (download endpoint) | — | Ownership validation + one-time token; stream from /tmp |
| Export status polling | Frontend (TanStack Query) | API/Backend (status field) | Client polls for "Bereit"; server holds state |
| Cookie consent state | Browser/Client (localStorage) | — | No server-side consent storage needed |
| Sentry init/close | Browser/Client (instrumentation-client.ts) | — | Runs before hydration; reads localStorage directly |
| Consent UI | Frontend Server (React context + banner component) | — | ConsentProvider wraps authenticated layout |
| Legal page content | Frontend Server (Server Components, static) | — | No interactivity needed; (legal) route group |
| Site-wide footer | Frontend Server (Server Component) | — | Static links; mounted in both root and authenticated layouts |
| AVV sign-off tracking | Operator (manual, 06-AVV-TRACKING.md) | — | Cannot be automated; external service sign-off |
| Marken search | Operator (manual, 06-MARKEN-SEARCH.md) | — | Cannot query DPMA/EUIPO programmatically |

---

## Standard Stack

### Core (all already in Directory.Packages.props — no new packages required)

| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| `System.IO.Compression` | BCL (.NET 10) | Zip archive for export bundle | No external dep; `ZipArchive` + `CreateEntry` is the idiomatic .NET pattern |
| `Npgsql.EntityFrameworkCore.PostgreSQL` | 10.0.1 | jsonb column for `audit_log.metadata` | `HasColumnType("jsonb")` supported; Npgsql handles `Dictionary<string,object?>` ↔ jsonb natively via `HasColumnType` |
| `Hangfire.Core` | 1.8.23 | `ExportUserDataJob` fire-and-forget + 24h purge recurring job | Already registered; `IBackgroundJobClient` + `RecurringJobManager.AddOrUpdate` pattern established |
| `@sentry/nextjs` | 10.52.0 | `Sentry.init`/`Sentry.close` runtime consent gate | Already installed; `close()` confirmed exported [VERIFIED: node -e require] |

### No New Packages Needed

No NuGet or npm packages are required for this phase. All functionality is achievable with BCL (`System.IO.Compression`, `System.Text.Json`) and the already-installed stack.

**Version verification:** All packages confirmed from `Backend/Directory.Packages.props` and `Frontend/package.json`. [VERIFIED: codebase]

---

## Architecture Patterns

### System Architecture Diagram

```
User (Browser)
  │
  ├─[1. Legal pages]─────────────► (legal) route group → Server Components
  │                                  impressum/ datenschutz/ agb/ widerruf/
  │                                  (legal)/layout.tsx → static HTML
  │
  ├─[2. Cookie consent]──────────► instrumentation-client.ts (before hydration)
  │                                  reads localStorage["taxreader-consent"]
  │                                  ↓ if "Fehleranalyse" granted
  │                                  Sentry.init(...)
  │                                  ConsentProvider (React context, post-hydration)
  │                                  ↓ on revoke
  │                                  Sentry.close() → localStorage update
  │
  ├─[3. Export trigger]──────────► POST /api/v1/export/request
  │                                  ↓ enqueue ExportUserDataJob (Hangfire)
  │                                  returns { exportId }
  │                                  ↓ GET /api/v1/export/status
  │                                  { status: "Generating" | "Ready" | "Expired" }
  │                                  ↓ GET /api/v1/export/download?token={oneTimeToken}
  │                                  streams /tmp/exports/{token}.zip
  │
  └─[4. Audit log]───────────────► IAuditLogger.RecordAsync(action, actor, subject, metadata)
                                     ↓ (call sites: DeleteAccountHandler, GrantTokensJob,
                                     │  RevokeTokensJob, RefreshTokenService, SaveClassificationRuleHandler)
                                     AuditLogger (Infrastructure)
                                     ↓
                                     INSERT INTO audit_log (...) — append-only
                                     ↓ (included in export bundle by ExportUserDataJob)
```

### Recommended Project Structure (additions only)

```
Backend/src/
├── TaxReader.Domain/
│   ├── Entities/AuditLogEntry.cs       # NEW — plain POCO, no nav props
│   └── Enums/AuditAction.cs            # NEW — enum of auditable actions
├── TaxReader.Application/
│   ├── Interfaces/IAuditLogger.cs      # NEW — RecordAsync(...)
│   ├── Jobs/ExportUserDataJob.cs       # NEW — Hangfire fire-and-forget
│   └── Jobs/ExportCleanupJob.cs        # NEW — daily purge of /tmp/exports
├── TaxReader.Infrastructure/
│   ├── Data/AppDbContext.cs            # MODIFIED — add AuditLogEntries DbSet
│   ├── Data/Configurations/
│   │   └── AuditLogEntryConfiguration.cs  # NEW — jsonb + no-cascade + index
│   ├── Migrations/
│   │   └── YYYYMMDD_AddAuditLog.cs    # NEW EF migration
│   └── Services/AuditLogger.cs         # NEW — implements IAuditLogger
├── TaxReader.Api/
│   ├── Endpoints/ExportEndpoints.cs    # NEW — /export/request, /status, /download
│   └── Hangfire/RecurringJobsBootstrap.cs  # MODIFIED — add export-cleanup job

Frontend/src/
├── app/
│   ├── (legal)/
│   │   ├── agb/page.tsx                # NEW
│   │   └── widerruf/page.tsx           # NEW
│   │   ├── datenschutz/page.tsx        # REPLACED with full draft
│   │   ├── impressum/page.tsx          # REPLACED with full draft
│   │   └── layout.tsx                  # MODIFIED — add footer
│   ├── (authenticated)/
│   │   ├── layout.tsx                  # MODIFIED — mount ConsentBanner
│   │   └── settings/page.tsx           # MODIFIED — add export trigger
│   └── layout.tsx                      # MODIFIED — mount ConsentProvider
├── components/
│   ├── layout/footer.tsx               # NEW — site-wide footer
│   └── consent/
│       ├── cookie-banner.tsx           # NEW — TTDSG banner
│       └── consent-settings-dialog.tsx # NEW — granular controls
├── providers/
│   └── consent-provider.tsx            # NEW — localStorage-backed context
└── lib/api-client.ts                   # MODIFIED — export trigger + status
instrumentation-client.ts               # MODIFIED — runtime consent gate
```

### Pattern 1: IAuditLogger Interface + AuditLogger Implementation

**What:** Application-layer interface; Infrastructure writes directly to the EF DbSet.
**When to use:** Any sensitive operation that must be auditable per DSGVO Art. 15/17 or LEG-08.

```csharp
// Application/Interfaces/IAuditLogger.cs
// Source: CONTEXT.md D-13, codebase patterns IClassificationService / ITokenService

namespace TaxReader.Application.Interfaces;

public interface IAuditLogger
{
    Task RecordAsync(
        AuditAction action,
        Guid? actorUserId,
        Guid? subjectUserId,
        Dictionary<string, object?> metadata,
        CancellationToken cancellationToken = default);
}
```

```csharp
// Infrastructure/Services/AuditLogger.cs
// Primary constructor DI, IAppDbContext direct usage (no repository pattern)

public class AuditLogger(IAppDbContext dbContext) : IAuditLogger
{
    public async Task RecordAsync(
        AuditAction action,
        Guid? actorUserId,
        Guid? subjectUserId,
        Dictionary<string, object?> metadata,
        CancellationToken cancellationToken = default)
    {
        dbContext.AuditLogEntries.Add(new AuditLogEntry
        {
            Id = Guid.NewGuid(),
            Action = action.ToString(),
            ActorUserId = actorUserId,
            SubjectUserId = subjectUserId,
            Metadata = metadata,
            CreatedAt = DateTime.UtcNow
        });
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
```

### Pattern 2: AuditLogEntry Entity + Configuration (append-only enforcement)

**What:** No Update or Delete path in any handler. EF configuration has no cascade pointing at `audit_log`. The actor FK is nullable so records survive user deletion.

```csharp
// Domain/Entities/AuditLogEntry.cs
// Source: CONTEXT.md D-14

namespace TaxReader.Domain.Entities;

public class AuditLogEntry
{
    public Guid Id { get; set; }
    public string Action { get; set; } = string.Empty;   // stored as string; enum on read
    public Guid? ActorUserId { get; set; }                // nullable: survives user deletion
    public Guid? SubjectUserId { get; set; }
    public Dictionary<string, object?> Metadata { get; set; } = [];
    public DateTime CreatedAt { get; set; }
}
```

```csharp
// Infrastructure/Data/Configurations/AuditLogEntryConfiguration.cs
// Source: existing TokenTransactionConfiguration.cs pattern [VERIFIED: codebase]

public class AuditLogEntryConfiguration : IEntityTypeConfiguration<AuditLogEntry>
{
    public void Configure(EntityTypeBuilder<AuditLogEntry> builder)
    {
        builder.ToTable("audit_log");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasDefaultValueSql("gen_random_uuid()");

        builder.Property(e => e.Action).IsRequired().HasMaxLength(100);
        builder.Property(e => e.CreatedAt).IsRequired();

        // jsonb metadata: Npgsql maps Dictionary<string, object?> ↔ PostgreSQL jsonb
        // when HasColumnType("jsonb") is specified.
        builder.Property(e => e.Metadata)
            .HasColumnType("jsonb")
            .IsRequired();

        // FK to users — RESTRICT (not CASCADE): actor row stays when user deleted.
        // actor_user_id is nullable for the same reason.
        builder.HasIndex(e => e.SubjectUserId);
        builder.HasIndex(e => e.CreatedAt);
        // No navigation property to User — avoids accidental EF cascade on user delete.
    }
}
```

**Append-only enforcement approach:** There is no `Update` or `Delete` code path on `audit_log` in any handler or service. The EF DbSet is exposed as `DbSet<AuditLogEntry>` (add-only by convention). The `IAppDbContext` interface adds `DbSet<AuditLogEntry> AuditLogEntries { get; }`. No `ExecuteDeleteAsync` or `Remove` calls anywhere. Periodic retention is handled by an operator policy decision (out of scope this phase); the schema allows it via a future migration.

### Pattern 3: ExportUserDataJob (Hangfire fire-and-forget)

**What:** Hangfire job that reads all user data from DB, writes JSON+CSV files into a `ZipArchive` in `/tmp/exports/{token}.zip`, stores the token in a small ephemeral record or in-memory, then marks export ready.

**Key constraints:**
- Hangfire job classes cannot inject `ICurrentUser` (no HttpContext). User ID passed as job argument.
- `[AutomaticRetry(Attempts = 3)]` (safe to retry — token is regenerated if job re-runs; old tmp file overwritten).
- The export token is a cryptographically random `Guid.NewGuid().ToString("N")` — not the user ID.

```csharp
// Application/Jobs/ExportUserDataJob.cs
// Source: existing GrantTokensJob.cs pattern [VERIFIED: codebase]

public class ExportUserDataJob(IAppDbContext dbContext, ILogger<ExportUserDataJob> logger)
{
    [AutomaticRetry(Attempts = 3, DelaysInSeconds = new[] { 30, 60, 120 })]
    public async Task HandleAsync(Guid userId, string exportToken, CancellationToken cancellationToken)
    {
        using var _ = LogContext.PushProperty("JobId", $"Export_{userId}");

        // 1. Query all user data (receipts, items, classifications, token_transactions, audit_log)
        // 2. Serialize to JSON + CSV (System.Text.Json + manual CSV)
        // 3. Create ZipArchive at Path.Combine(Path.GetTempPath(), "taxreader-exports", exportToken + ".zip")
        // 4. Add JSON entries, CSV entries, README.txt
        // 5. Mark export ready — store (userId, exportToken, expiresAt=UtcNow+24h) in a table
        //    OR in a static ConcurrentDictionary<string, ExportRecord> (lightweight; survives single container restart risk)
        // Decision: use a lightweight in-memory ConcurrentDictionary backed by ExportTokenStore (Singleton)
        //   Rationale: no new DB table needed; container restart is low-risk (export just re-runs);
        //   consistent with FND-01 "no persistent storage" spirit. See Pitfall 4 for the tradeoff.
    }
}
```

**Export cleanup job:**
```csharp
// Application/Jobs/ExportCleanupJob.cs
// Registered in RecurringJobsBootstrap as daily at 02:00 UTC

public class ExportCleanupJob(ILogger<ExportCleanupJob> logger)
{
    public Task HandleAsync(CancellationToken cancellationToken)
    {
        var exportsDir = Path.Combine(Path.GetTempPath(), "taxreader-exports");
        if (!Directory.Exists(exportsDir)) return Task.CompletedTask;

        var cutoff = DateTime.UtcNow.AddHours(-24);
        foreach (var file in Directory.GetFiles(exportsDir, "*.zip"))
        {
            if (File.GetCreationTimeUtc(file) < cutoff)
                File.Delete(file);
        }
        return Task.CompletedTask;
    }
}
```

### Pattern 4: One-Time Download Endpoint

**What:** `GET /api/v1/export/download?token={exportToken}` — validates ownership (token → userId mapping), streams the zip, then invalidates the token. Anonymous endpoint NOT acceptable — must be behind `RequireAuthorization()` and validate the token belongs to the requesting user.

```csharp
// The endpoint extracts ICurrentUser.UserId, looks up ExportTokenStore to confirm
// the token belongs to that user and is not expired, then streams the file.
// Ownership validation: ExportTokenStore.TryGet(token) → (userId, expiresAt)
// Compare ExportTokenStore.userId == currentUser.UserId → 403 if mismatch.
// After streaming: ExportTokenStore.Invalidate(token) + optionally delete the file.
```

### Pattern 5: Sentry Consent Gate in instrumentation-client.ts

**What:** `instrumentation-client.ts` runs after HTML load, before React hydration — `window` and `localStorage` are available but the React `ConsentProvider` context is not. The gate reads `localStorage` directly.

```typescript
// Frontend/instrumentation-client.ts (MODIFIED from current)
// Source: current file [VERIFIED: codebase] + Next.js docs [VERIFIED: node_modules docs]

import * as Sentry from "@sentry/nextjs";
import { scrubEvent } from "@/lib/sentry-scrubber";

const SENTRY_CONSENT_KEY = "taxreader-consent";

function hasSentryConsent(): boolean {
  try {
    const raw = localStorage.getItem(SENTRY_CONSENT_KEY);
    if (!raw) return false;
    const parsed = JSON.parse(raw) as { fehleranalyse?: boolean };
    return parsed.fehleranalyse === true;
  } catch {
    return false;
  }
}

if (process.env.NEXT_PUBLIC_SENTRY_ENABLED === "true" && hasSentryConsent()) {
  Sentry.init({
    dsn: process.env.NEXT_PUBLIC_SENTRY_DSN,
    environment: process.env.NEXT_PUBLIC_SENTRY_ENV ?? "production",
    sendDefaultPii: false,
    tracesSampleRate: 0,
    replaysOnErrorSampleRate: 0,
    replaysSessionSampleRate: 0,
    beforeSend(event) {
      return scrubEvent(event);
    },
  });
}

export const onRouterTransitionStart = Sentry.captureRouterTransitionStart;
```

**Revoke path in ConsentProvider:** When user revokes consent, `ConsentProvider` calls `Sentry.close()` — confirmed exported from `@sentry/nextjs` 10.52 [VERIFIED: node -e]. No page reload required. `Sentry.close()` flushes pending events and disables further capture.

**Grant path:** When user grants consent at runtime (banner "Alle akzeptieren"), `ConsentProvider` calls `Sentry.init(...)` with the same config as `instrumentation-client.ts`. `Sentry.isInitialized()` guards against double-init.

```typescript
// In consent-provider.tsx (simplified)
import * as Sentry from "@sentry/nextjs";

function grantSentry() {
  if (process.env.NEXT_PUBLIC_SENTRY_ENABLED === "true" && !Sentry.isInitialized()) {
    Sentry.init({ /* same config */ });
  }
}

function revokeSentry() {
  if (Sentry.isInitialized()) {
    Sentry.close(2000); // 2s timeout for flush
  }
}
```

### Pattern 6: ConsentProvider React Context

**What:** Lightweight context backed by `localStorage`. Key `"taxreader-consent"` stores `{ notwendig: true, fehleranalyse: boolean }`. `notwendig` is always `true` (no toggle).

```typescript
// Frontend/src/providers/consent-provider.tsx
// Modeled on auth-provider.tsx pattern [VERIFIED: codebase]

"use client";

const CONSENT_KEY = "taxreader-consent";

interface ConsentState {
  notwendig: true;          // always true
  fehleranalyse: boolean;
  decided: boolean;          // false = banner not yet shown
}

interface ConsentContextValue {
  consent: ConsentState;
  acceptAll: () => void;
  acceptNecessary: () => void;
  updateConsent: (state: Partial<ConsentState>) => void;
  reopenSettings: () => boolean; // signals the banner to show settings panel
  settingsPanelOpen: boolean;
}
```

**Banner visibility:** `decided === false` → show banner on first visit. `decided === true` → hide banner. Footer "Cookie-Einstellungen" link sets `settingsPanelOpen = true` to reopen the settings dialog.

### Pattern 7: Legal Pages + Footer

**Existing layout structure:** [VERIFIED: codebase]
- `(legal)/layout.tsx` — minimal header (BelegPilot logo + theme toggle), `<main>{children}</main>`. **No footer currently**.
- `(authenticated)/layout.tsx` — `SidebarProvider` + `AppSidebar` + `SidebarInset`. **No footer currently**.
- Root `layout.tsx` — providers only.

**Footer mounting strategy:**
- `(legal)/layout.tsx` MODIFIED: add `<Footer />` after `<main>`.
- `(authenticated)/layout.tsx` MODIFIED: add `<Footer />` inside `SidebarInset` below `{children}`.
- Root layout is providers-only — do NOT add footer there (authenticated layout already has it for app pages).

**PUBLIC_PATHS update required:** `auth-provider.tsx` [VERIFIED: codebase line 8] has `PUBLIC_PATHS = ["/login", "/register", "/impressum", "/datenschutz"]`. Must add `/agb` and `/widerruf` so unauthenticated users can access the new legal pages.

**Draft marker pattern:**
```tsx
// Consistent across all four legal pages
function DraftWarning() {
  return (
    <div className="rounded border border-yellow-400 bg-yellow-50 dark:bg-yellow-500/10 px-4 py-2 text-sm text-yellow-800 dark:text-yellow-200">
      ⚠ Entwurf – anwaltliche Prüfung ausstehend
    </div>
  );
}
```

**BelegPilot → TaxReader branding note:** The `(legal)/layout.tsx` currently renders "BelegPilot" as the logo text [VERIFIED: codebase]. This phase must update it to "TaxReader" as part of replacing the legal page content.

### Anti-Patterns to Avoid

- **EF SaveChanges interceptor for audit logging:** Chosen against (D-13) — an interceptor would attach to every `SaveChangesAsync` and require filtering by entity type, losing business-meaning context. The explicit `IAuditLogger.RecordAsync` call at each of the five call sites is the correct pattern.
- **Cascade delete on `audit_log.actor_user_id`:** Do NOT configure `OnDelete(DeleteBehavior.Cascade)` for the actor FK. The audit record must survive user deletion. Use `OnDelete(DeleteBehavior.SetNull)` or no FK constraint at all (nullable column without navigation property).
- **Page reload on consent change:** D-07 explicitly forbids a page reload. `Sentry.close()` handles the revoke path; `Sentry.init()` guarded by `Sentry.isInitialized()` handles the grant path in the same browser session.
- **Pre-checking the "Fehleranalyse" checkbox in the banner:** TTDSG violation. The checkbox must default to unchecked.
- **Storing export token in a separate DB table:** Unnecessary complexity. An in-memory `ExportTokenStore` Singleton is sufficient for a single-container deployment. The risk (lost on restart) is explicitly acceptable per D-10's "transient storage" wording.
- **Using `ICurrentUser` inside Hangfire jobs:** `ICurrentUser` reads from `HttpContext` which is null in Hangfire workers. Pass `userId` as a job argument (confirmed established pattern: `GrantTokensJob.HandleAsync(Guid userId, ...)` [VERIFIED: codebase]).

---

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Zip archive creation | Custom byte concatenation | `System.IO.Compression.ZipArchive` (.NET BCL) | Handles entry headers, compression, streaming; built into .NET 10 |
| JSON serialization in export | Manual string building | `System.Text.Json.JsonSerializer` | Already used throughout; handles nested types |
| CRON scheduling for 24h purge | Custom timer | `RecurringJob.AddOrUpdate` (Hangfire) | Already registered in `RecurringJobsBootstrap.cs` [VERIFIED: codebase] |
| Consent state persistence | Custom DB table or cookie | `localStorage` with JSON serialization | Zero backend dep; survives page refresh; standard TTDSG pattern |
| Token generation for export | Sequential counter | `Guid.NewGuid().ToString("N")` | Unpredictable, no collision, zero deps |
| German legal copy structure | Research from scratch | Established DE TMG §5 / DSGVO templates with project-specific facts | The structural requirements (Impressum fields, Art. 13 structure) are legally well-defined |

---

## Verified Call Sites (D-13 audit log wiring)

All five call sites confirmed in codebase [VERIFIED: grep + file read]:

| Handler / Service | File | Audit event to add | Existing signature |
|---|---|---|---|
| `DeleteAccountHandler.HandleAsync` | `Application/Commands/DeleteAccountHandler.cs` | `AccountDeleted` | `(DeleteAccountRequest request, CancellationToken ct)` — inject `IAuditLogger` via primary constructor |
| `GrantTokensJob.HandleAsync` | `Application/Jobs/GrantTokensJob.cs` | `TokensGranted` | `(Guid userId, int credits, CancellationToken ct)` — inject `IAuditLogger` via primary constructor |
| `RevokeTokensJob.HandleAsync` | `Application/Jobs/RevokeTokensJob.cs` | `TokensRevoked` | `(Guid userId, int credits, CancellationToken ct)` — inject `IAuditLogger` via primary constructor |
| `RefreshTokenService.ValidateAndRotateAsync` | `Infrastructure/Services/RefreshTokenService.cs` | `RefreshTokenReplayDetected` | Replay path at `if (existing.RevokedAt is not null)` block; `IAuditLogger` injected via primary constructor (Infrastructure service) |
| `SaveClassificationRuleHandler.HandleAsync` | `Application/Commands/SaveClassificationRuleHandler.cs` | `ClassificationRuleCreated` | `(SaveClassificationRuleCommand command, CancellationToken ct)` — inject `IAuditLogger` via primary constructor |

**Important note on `RefreshTokenService`:** This is in the Infrastructure layer. `IAuditLogger` is defined in Application — Infrastructure can reference Application (confirmed by architecture: "Infrastructure implements Application interfaces"). So injecting `IAuditLogger` into `RefreshTokenService` is layering-compliant.

**Recommended `AuditAction` enum values:**
```csharp
public enum AuditAction
{
    AccountDeleted,
    TokensGranted,
    TokensRevoked,
    RefreshTokenReplayDetected,
    ClassificationRuleCreated,
    DataExportRequested,   // for the export trigger call site
    DataExportDownloaded,  // optional — on download
}
```

---

## Common Pitfalls

### Pitfall 1: jsonb Column in InMemory Provider (Tests)
**What goes wrong:** EF Core InMemory provider ignores `HasColumnType("jsonb")` but also does not serialize `Dictionary<string, object?>` — it stores the reference as-is. Tests that `Add` an `AuditLogEntry` and then re-query it will get the same dictionary back by reference. But if a test serializes and deserializes, the types inside the dictionary change (e.g. `int` becomes `JsonElement`).
**Why it happens:** InMemory provider has no concept of column types; `HasColumnType` is a no-op there.
**How to avoid:** In `AuditLogger` tests, assert on the stored `Metadata` dictionary by key presence and string equality, not by casting to specific types. For production, `HasColumnType("jsonb")` is mandatory so Npgsql handles the PostgreSQL jsonb wire format.
**Warning signs:** `InvalidCastException` in tests when reading metadata values back from InMemory DB.

### Pitfall 2: EF Cascade on Nullable FK (actor_user_id)
**What goes wrong:** If `actor_user_id` has an FK to `users` with `OnDelete(DeleteBehavior.Cascade)`, deleting a user deletes all audit log rows where they are the actor — which defeats the audit record retention requirement.
**Why it happens:** EF defaults FK relationships to cascade delete when the property is non-nullable. With nullable FKs and no navigation property, EF may or may not configure a FK depending on convention.
**How to avoid:** Explicitly: do NOT configure a HasOne/WithMany for `actor_user_id`. If a FK constraint is desired in the DB for index purposes, use `OnDelete(DeleteBehavior.SetNull)`. Safest for audit log: no FK constraint at all, just an index on the column for query performance.

### Pitfall 3: Double-Init of Sentry (consent banner grant path)
**What goes wrong:** `instrumentation-client.ts` runs before React hydration. If the user already has consent granted from a previous session, `Sentry.init` is called there. Later, when `ConsentProvider` mounts and calls `grantSentry()`, it calls `Sentry.init` again — Sentry SDK logs a warning about double-init and the second call is a no-op but may reset configuration.
**Why it happens:** Two code paths both call `Sentry.init` without coordination.
**How to avoid:** Guard with `Sentry.isInitialized()` before calling `Sentry.init` in the ConsentProvider's grant path. `Sentry.isInitialized()` is confirmed exported from `@sentry/nextjs` 10.52 [VERIFIED].

### Pitfall 4: Export Token Lost on Container Restart
**What goes wrong:** `ExportTokenStore` is a Singleton in-memory dictionary. If the API container restarts between a user requesting an export and downloading it, the token mapping is lost. The zip file in `/tmp/exports/` is also gone (tmpfs reset).
**Why it happens:** Single Docker Compose deployment; no shared cache.
**How to avoid:** This is an explicit accepted tradeoff per D-10 ("transient storage"). The UX consequence is the user sees "Export fehlgeschlagen" and can re-request. Document this in the settings page UI with "Der Link ist 24 Stunden gültig. Falls der Export nicht mehr verfügbar ist, bitte erneut anfordern." The export job is idempotent — a new request generates a fresh token and file.
**Alternative not taken:** Storing the token + expiry in a `user_exports` DB table would survive restarts. Explicitly deferred to keep scope minimal.

### Pitfall 5: `instrumentation-client.ts` and localStorage SSR Conflict
**What goes wrong:** `instrumentation-client.ts` runs client-side only (after HTML load, before hydration per Next.js 16 docs [VERIFIED: node_modules/next/dist/docs]). It is safe to access `localStorage` there. However, any utility function imported into `instrumentation-client.ts` that references `window` or `document` at module-level (not inside a function) will throw during SSR if the same module is also imported from a Server Component.
**Why it happens:** `instrumentation-client.ts` is client-only but the imported modules may be evaluated server-side if they are not `"use client"` guarded.
**How to avoid:** The `hasSentryConsent()` function accesses `localStorage` inside the function body (not at module level). Wrap in `try/catch` for safety. Keep `instrumentation-client.ts` imports minimal — only `@sentry/nextjs` and `@/lib/sentry-scrubber` (which is already the pattern [VERIFIED: codebase]).

### Pitfall 6: `/agb` and `/widerruf` not in PUBLIC_PATHS
**What goes wrong:** Unauthenticated users clicking footer links to `/agb` or `/widerruf` get redirected to `/login` because `auth-provider.tsx:8` only has `PUBLIC_PATHS = ["/login", "/register", "/impressum", "/datenschutz"]` [VERIFIED: codebase].
**Why it happens:** Phase 5 created `/agb` and `/widerruf` as placeholders — they existed but PUBLIC_PATHS was never updated.
**How to avoid:** Add `/agb` and `/widerruf` to `PUBLIC_PATHS` in `auth-provider.tsx` as part of this phase.

### Pitfall 7: `Sentry.close()` is Async (returns Promise)
**What goes wrong:** `Sentry.close()` returns `Promise<boolean>`. If called without `await`, the flush may be incomplete before the component re-renders.
**Why it happens:** `close()` flushes pending events before disabling the SDK.
**How to avoid:** `await Sentry.close(2000)` (2-second timeout) in the ConsentProvider's revoke handler. Since this is called in a non-async event handler context, use `.then(() => {...})` or wrap in an async IIFE.

### Pitfall 8: BelegPilot Branding in (legal)/layout.tsx
**What goes wrong:** The existing `(legal)/layout.tsx` [VERIFIED: codebase] renders `<span>BelegPilot</span>` as the logo text. Shipping the new legal pages with wrong branding undermines the legal document validity.
**Why it happens:** Phase 1 created the (legal) layout with the original product name.
**How to avoid:** Update `(legal)/layout.tsx` logo text to "TaxReader" as part of this phase.

---

## Code Examples

### Adding AuditLogEntry to IAppDbContext and AppDbContext

```csharp
// IAppDbContext.cs — add one line [VERIFIED: current interface shape]
DbSet<AuditLogEntry> AuditLogEntries { get; }

// AppDbContext.cs — add one line [VERIFIED: current class shape]
public DbSet<AuditLogEntry> AuditLogEntries => Set<AuditLogEntry>();
```

### Wiring IAuditLogger into DeleteAccountHandler

```csharp
// Current [VERIFIED: codebase]
public class DeleteAccountHandler(
    IAppDbContext dbContext,
    ICurrentUser currentUser,
    IRefreshTokenService refreshTokenService)

// After Phase 6 addition:
public class DeleteAccountHandler(
    IAppDbContext dbContext,
    ICurrentUser currentUser,
    IRefreshTokenService refreshTokenService,
    IAuditLogger auditLogger)
{
    public async Task<Result<bool>> HandleAsync(
        DeleteAccountRequest request,
        CancellationToken cancellationToken = default)
    {
        // ... existing validation + revoke ...

        await auditLogger.RecordAsync(
            AuditAction.AccountDeleted,
            actorUserId: userId,
            subjectUserId: userId,
            metadata: new Dictionary<string, object?> { ["email_hash"] = HashEmail(user.Email) },
            cancellationToken);

        dbContext.Users.Remove(user);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result<bool>.Success(true);
    }
}
```

### RecurringJobsBootstrap — add ExportCleanupJob

```csharp
// Backend/src/TaxReader.Api/Hangfire/RecurringJobsBootstrap.cs
// Add to existing Register() method [VERIFIED: current shape]

manager.AddOrUpdate<ExportCleanupJob>(
    recurringJobId: "export-cleanup",
    methodCall: job => job.HandleAsync(CancellationToken.None),
    cronExpression: "0 2 * * *",           // daily at 02:00 UTC
    options: new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });
```

### System.IO.Compression ZipArchive in ExportUserDataJob

```csharp
// No external package needed — System.IO.Compression is BCL [ASSUMED: .NET 10 BCL]
var exportsDir = Path.Combine(Path.GetTempPath(), "taxreader-exports");
Directory.CreateDirectory(exportsDir);
var zipPath = Path.Combine(exportsDir, exportToken + ".zip");

using var zipStream = new FileStream(zipPath, FileMode.Create);
using var archive = new ZipArchive(zipStream, ZipArchiveMode.Create, leaveOpen: false);

// JSON entry example
var receiptsEntry = archive.CreateEntry("receipts.json", CompressionLevel.Optimal);
using var receiptsWriter = new StreamWriter(receiptsEntry.Open());
await JsonSerializer.SerializeAsync(receiptsWriter.BaseStream, receipts, cancellationToken: cancellationToken);

// CSV entry example (manual — no external CSV lib needed for this simplicity level)
var itemsEntry = archive.CreateEntry("items.csv", CompressionLevel.Optimal);
using var csvWriter = new StreamWriter(itemsEntry.Open());
await csvWriter.WriteLineAsync("id,receipt_id,description,amount,category");
foreach (var item in items)
    await csvWriter.WriteLineAsync($"{item.Id},{item.ReceiptId},\"{item.Description}\",{item.TotalPrice},{item.Category}");

// README.txt
var readmeEntry = archive.CreateEntry("README.txt");
using var readmeWriter = new StreamWriter(readmeEntry.Open());
await readmeWriter.WriteAsync("""
    TaxReader — Datenschutz-Export gemäß DSGVO Art. 20
    Erstellt: {DateTime.UtcNow:dd.MM.yyyy HH:mm} UTC

    Inhalt:
    - receipts.json / receipts.csv: Ihre Belege
    - items.json / items.csv: Einzelpositionen
    - classifications.json / classifications.csv: Klassifizierungen
    - token_transactions.json / token_transactions.csv: Token-Transaktionen
    - audit_log.json / audit_log.csv: Protokoll sensitiver Vorgänge
    """);
```

---

## Runtime State Inventory

This phase is not a rename/refactor. No runtime state migration required. Skipped.

---

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|---|---|---|---|
| `sentry.client.config.ts` | `instrumentation-client.ts` | Next.js 15.3 | The old file convention is deprecated; existing codebase already uses the new file [VERIFIED] |
| Manual CSV with external library | `System.Text.Json` + manual CSV | N/A | BCL sufficient for this use case; QuestPDF already handles PDF export separately |
| CMP (Consent Management Platform) | Custom `ConsentProvider` + localStorage | N/A | Only one non-essential category; CMP would be overengineering |

**Deprecated/outdated:**
- `sentry.client.config.ts`: The old Next.js Sentry convention — not used here; `instrumentation-client.ts` is the correct file for Next.js 16 [VERIFIED: Next.js docs in node_modules].
- The existing `(legal)/layout.tsx` logo text "BelegPilot": must be updated to "TaxReader".

---

## Open Questions

1. **Export token persistence strategy (in-memory vs DB table)**
   - What we know: D-10 says "transient"; in-memory ConcurrentDictionary is simplest.
   - What's unclear: How often do container restarts actually happen in practice? Is the "re-request" UX acceptable to the operator?
   - Recommendation: Proceed with in-memory `ExportTokenStore` Singleton. If Phase 7 UAT surfaces complaints, add a lightweight `user_exports` DB table as a follow-up.

2. **AGB content scope for StBerG-safe positioning**
   - What we know: D-01 locks the structure. The executor generates draft copy.
   - What's unclear: The exact SLA commitment in "support response SLA" (LEG-03) — unspecified in CONTEXT or REQUIREMENTS.
   - Recommendation: Use a conservative "Wir bemühen uns, Anfragen innerhalb von 5 Werktagen zu beantworten." (5 business days is manageable for a solo dev) and flag in 06-LEGAL-REVIEW.md for lawyer to adjust.

3. **Footer position in authenticated layout**
   - What we know: The authenticated layout uses `SidebarInset` which has `overflow-hidden` [VERIFIED: codebase line 34 `className="overflow-hidden"`].
   - What's unclear: Whether a footer inside `SidebarInset` will be visible or clipped by the overflow hidden.
   - Recommendation: Place the Footer outside `SidebarInset` but still within `SidebarProvider`; or use `sticky bottom-0` within the scrollable content area. The planner should prototype this layout at plan time.

---

## Environment Availability

Step 2.6: SKIPPED — this phase is purely code/config changes. No external dependencies beyond what is already installed and running (Hangfire, PostgreSQL, Sentry SDK). No new services, CLIs, or runtimes are required.

---

## Validation Architecture

`nyquist_validation: true` in `.planning/config.json` [VERIFIED: codebase].

### Test Framework

| Property | Value |
|----------|-------|
| Framework | xUnit 2.9.2 + FluentAssertions 7.0.0 + Moq 4.20.72 |
| Config file | `Backend/tests/TaxReader.UnitTests/TaxReader.UnitTests.csproj` |
| Quick run command | `dotnet test Backend/tests/TaxReader.UnitTests -x` |
| Full suite command | `dotnet test Backend` |

### Phase Requirements → Test Map

| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| LEG-01 | Impressum page reachable, contains required TMG §5 fields | build-visible (page renders) | `npm run build` in Frontend | ❌ Wave 0 — new file |
| LEG-02 | Datenschutz contains Art. 13/22/28 + sub-processor list | build-visible (page renders) | `npm run build` in Frontend | existing file, replaced |
| LEG-03 | AGB page renders with StBerG disclaimer | build-visible | `npm run build` in Frontend | ❌ Wave 0 — new file |
| LEG-04 | /widerruf page renders with §356 statutory text | build-visible | `npm run build` in Frontend | ❌ Wave 0 — new file |
| LEG-05 | ConsentProvider context — acceptAll sets fehleranalyse=true in localStorage | unit (manual) | (no automated frontend test framework) | ❌ no frontend test infra |
| LEG-05 | Cookie banner "Alle akzeptieren" / "Nur notwendige" equal prominence | manual UAT | visual check | — |
| LEG-05 | Sentry.close() called on consent revoke | structural grep | `grep -r "Sentry.close" Frontend/` | ❌ Wave 0 |
| LEG-07 | ExportUserDataJob writes zip to /tmp/exports/{token}.zip | unit | `dotnet test Backend/tests/TaxReader.UnitTests -x --filter "ExportUserData"` | ❌ Wave 0 |
| LEG-07 | Download endpoint validates ownership (403 on mismatched userId) | unit | `dotnet test Backend/tests/TaxReader.UnitTests -x --filter "ExportDownload"` | ❌ Wave 0 |
| LEG-07 | ExportCleanupJob deletes files older than 24h | unit | `dotnet test Backend/tests/TaxReader.UnitTests -x --filter "ExportCleanup"` | ❌ Wave 0 |
| LEG-08 | AuditLogger.RecordAsync writes row to audit_log DbSet | unit | `dotnet test Backend/tests/TaxReader.UnitTests -x --filter "AuditLogger"` | ❌ Wave 0 |
| LEG-08 | DeleteAccountHandler calls IAuditLogger before Remove | unit | `dotnet test Backend/tests/TaxReader.UnitTests -x --filter "DeleteAccount"` | existing test modified |
| LEG-08 | GrantTokensJob calls IAuditLogger | unit | `dotnet test Backend/tests/TaxReader.UnitTests -x --filter "GrantTokens"` | existing test modified |
| LEG-08 | RevokeTokensJob calls IAuditLogger | unit | `dotnet test Backend/tests/TaxReader.UnitTests -x --filter "RevokeTokens"` | existing test modified |
| LEG-08 | RefreshTokenService replay path calls IAuditLogger | unit | `dotnet test Backend/tests/TaxReader.UnitTests -x --filter "RefreshToken"` | existing test modified |
| LEG-08 | SaveClassificationRuleHandler calls IAuditLogger | unit | `dotnet test Backend/tests/TaxReader.UnitTests -x --filter "SaveClassificationRule"` | existing test modified |
| LEG-09 | 06-MARKEN-SEARCH.md created | structural | file exists check | ❌ Wave 0 |
| LEG-06 | 06-AVV-TRACKING.md created | structural | file exists check | ❌ Wave 0 |

### Sampling Rate

- **Per task commit:** `dotnet test Backend/tests/TaxReader.UnitTests -x` (unit tests only, < 30s)
- **Per wave merge:** `dotnet test Backend && cd Frontend && npm run build`
- **Phase gate:** Full suite green before `/gsd-verify-work`

### Wave 0 Gaps

Backend (new test files needed):
- [ ] `Backend/tests/TaxReader.UnitTests/Application/AuditLoggerTests.cs` — covers LEG-08 (RecordAsync writes row)
- [ ] `Backend/tests/TaxReader.UnitTests/Jobs/ExportUserDataJobTests.cs` — covers LEG-07 (zip created, content included)
- [ ] `Backend/tests/TaxReader.UnitTests/Jobs/ExportCleanupJobTests.cs` — covers LEG-07 (24h purge)
- [ ] `Backend/tests/TaxReader.UnitTests/Application/ExportDownloadEndpointTests.cs` — covers LEG-07 (ownership validation)

Backend (existing test files to be modified — add IAuditLogger mock assertions):
- `Backend/tests/TaxReader.UnitTests/Application/Commands/DeleteAccountHandlerTests.cs` (exists in Commands/ folder)
- `Backend/tests/TaxReader.UnitTests/Jobs/GrantTokensJobTests.cs` [VERIFIED: exists]
- `Backend/tests/TaxReader.UnitTests/Jobs/RevokeTokensJobTests.cs` [VERIFIED: exists]
- `Backend/tests/TaxReader.UnitTests/Services/RefreshTokenServiceTests.cs`

Frontend test infrastructure note: No frontend test framework is configured [VERIFIED: `package.json` has no vitest/jest]. LEG-05 and LEG-07 frontend behaviors are validated via manual UAT in `06-HUMAN-UAT.md` and build smoke (`npm run build`). This is unchanged from prior phases.

---

## Security Domain

`security_enforcement: true`, `security_asvs_level: 1` in `.planning/config.json` [VERIFIED].

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---|---|---|
| V2 Authentication | no (no new auth mechanisms) | existing JWT bearer |
| V3 Session Management | no (no session changes) | — |
| V4 Access Control | yes — export download endpoint | ownership validation via `ICurrentUser.UserId == ExportTokenStore.userId`; 403 on mismatch |
| V5 Input Validation | minimal — export request has no user-supplied content | FluentValidation if any parameters added |
| V6 Cryptography | no — export token is Guid.NewGuid() (random, opaque) | `Guid.NewGuid()` uses OS CSPRNG; not hand-rolled |
| V9 Data Protection | yes — audit log contains PII (email hash in AccountDeleted) | Only store hashes, not raw PII, in audit log metadata |
| V13 API | yes — download endpoint | one-time token invalidation on download; expiry check |

### Known Threat Patterns for this Stack

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| Export link enumeration (guessing other users' download tokens) | Information Disclosure | Token = `Guid.NewGuid().ToString("N")` (128-bit random) + ownership validation by `ICurrentUser.UserId`; 403 on mismatch |
| Audit log tampering | Tampering | Append-only by code convention; no Update/Delete path; no soft-delete flag |
| IDOR on export download | Information Disclosure | `ExportTokenStore.TryGet(token)` returns `(userId, expiresAt)`; compare userId to `ICurrentUser.UserId` before streaming |
| Cookie consent pre-checked | Privacy violation (TTDSG) | "Fehleranalyse" checkbox defaults to unchecked; server receives no consent assumption |
| PII in audit log metadata | Data minimisation (DSGVO Art. 5(1)(c)) | Store only `email_hash` (SHA-256 of email), not raw email, in AccountDeleted audit entry metadata |

---

## Operator-Tracked Artifacts (created by executor, completed by operator)

These three documents are created as checklists during execution. The actual actions (lawyer review, AVV signing, Marken search) are operator tasks tracked as HUMAN-UAT items.

### 06-LEGAL-REVIEW.md columns
| Page | Status | Lawyer | Notes |
|------|--------|--------|-------|
| Impressum | Drafted | — | — |
| Datenschutzerklärung | Drafted | — | — |
| AGB | Drafted | — | — |
| Widerrufserklärung | Drafted | — | — |

Status values: `Drafted → Lawyer-reviewed → Live` (removing the draft marker is gated on "Lawyer-reviewed").

### 06-AVV-TRACKING.md rows
| Sub-processor | Purpose | DPA/AVV URL | Signed | Link in Datenschutz |
|---|---|---|---|---|
| Anthropic | AI classification | https://www.anthropic.com/legal/dpa | — | — |
| Stripe | Payment processing | https://stripe.com/de/legal/dpa | — | — |
| Sentry | Error monitoring | https://sentry.io/legal/dpa/ | — | — |
| BetterStack | Uptime monitoring | https://betterstack.com/privacy | — | — |

### 06-MARKEN-SEARCH.md structure
Document: Nizza classes searched (9: software, 42: SaaS/IT services), search date, result (Clear / Conflicted / Already registered by us), evidence screenshots, decision (proceed / rename / register).

---

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | `System.IO.Compression` `ZipArchive` is available in .NET 10 BCL without a NuGet package | Standard Stack / Code Examples | Low — it has been BCL since .NET Core 1.0; would require adding `System.IO.Compression` NuGet if wrong |
| A2 | `ExportTokenStore` in-memory Singleton is sufficient for single-container deployment (token not persisted across restart) | Pattern 3 | Medium — if the container restarts between export request and download, user must re-request; operator must find this acceptable |
| A3 | `SidebarInset`'s `overflow-hidden` class does not permanently clip a sticky footer | Common Pitfalls #3 / Open Questions | Low-Medium — layout may need adjustment; prototype at plan time |

All other claims in this document were verified against the live codebase via file reads, bash commands, or node -e invocations.

---

## Sources

### Primary (HIGH confidence — verified against codebase or installed packages)
- `Backend/src/TaxReader.Application/Commands/DeleteAccountHandler.cs` — confirmed signature and structure
- `Backend/src/TaxReader.Application/Jobs/GrantTokensJob.cs` — confirmed Hangfire pattern, signature, no ICurrentUser
- `Backend/src/TaxReader.Application/Jobs/RevokeTokensJob.cs` — confirmed Hangfire pattern
- `Backend/src/TaxReader.Infrastructure/Services/RefreshTokenService.cs` — confirmed replay detection location
- `Backend/src/TaxReader.Application/Commands/SaveClassificationRuleHandler.cs` — confirmed CLASS-05 handler signature
- `Backend/src/TaxReader.Api/Hangfire/RecurringJobsBootstrap.cs` — confirmed `RecurringJob.AddOrUpdate` pattern
- `Backend/src/TaxReader.Infrastructure/DependencyInjection.cs` — confirmed Hangfire registration, parser order
- `Backend/src/TaxReader.Infrastructure/Data/Configurations/TokenTransactionConfiguration.cs` — IEntityTypeConfiguration pattern
- `Backend/src/TaxReader.Infrastructure/Data/AppDbContext.cs` — confirmed DbSet pattern
- `Backend/src/TaxReader.Application/Interfaces/IAppDbContext.cs` — confirmed interface shape
- `Backend/Directory.Packages.props` — confirmed all package versions (Hangfire 1.8.23, Npgsql 10.0.1, etc.)
- `Frontend/instrumentation-client.ts` — confirmed current gating pattern
- `Frontend/src/app/(legal)/layout.tsx` — confirmed existing structure, BelegPilot branding
- `Frontend/src/providers/auth-provider.tsx` — confirmed PUBLIC_PATHS missing `/agb` and `/widerruf`
- `Frontend/src/components/layout/` — confirmed no footer.tsx exists
- `node_modules/@sentry/nextjs` — confirmed `Sentry.close` exported (version 10.52.0, node -e test)
- `node_modules/next/dist/docs/01-app/03-api-reference/03-file-conventions/instrumentation-client.md` — confirmed execution timing (after HTML load, before hydration), `localStorage` safe
- `.planning/config.json` — confirmed `nyquist_validation: true`, `security_enforcement: true`

### Secondary (MEDIUM confidence — planning documents)
- `06-CONTEXT.md` — all 17 decisions D-01 through D-17
- `03-CONTEXT.md` — Hangfire recurring-job pattern, IBackgroundJobClient usage, WorkerCount alignment
- `05-CONTEXT.md` — Widerruf §356 text, Kleinunternehmer §19, /agb + /widerruf placeholders in Phase 5

---

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — all packages verified against live files
- Architecture: HIGH — all integration points verified against actual code
- Call sites (D-13): HIGH — all five files read and shapes confirmed
- Sentry consent gate: HIGH — `Sentry.close` verified exported; instrumentation-client timing verified from Next.js docs
- Pitfalls: HIGH — all except footer layout (A3 assumption) verified against codebase
- Legal content structure: MEDIUM — TMG §5 / DSGVO Art. 13 structure is well-established DE law; executor authors the actual copy

**Research date:** 2026-06-02
**Valid until:** 2026-07-02 (stable stack — no fast-moving dependencies)
