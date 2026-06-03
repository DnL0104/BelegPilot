---
phase: 06-legal-consent-data-export
verified: 2026-06-03T08:00:00Z
status: gaps_found
score: 7/9 must-haves verified
overrides_applied: 0
gaps:
  - truth: "Bundle includes receipts, items, classifications, token_transactions, the user's own audit_log, and README.txt"
    status: partial
    reason: "parsedReceipts (Receipt entity: vendor, purchase_date, total_amount, currency, parsed_at) is queried at ExportUserDataJob.cs:50 but never written to any zip entry. The bundle contains ReceiptFile metadata only — the most user-meaningful parsed receipt data (who sold what, when, for how much) is silently absent. Users are entitled to this under DSGVO Art. 20."
    artifacts:
      - path: "Backend/src/TaxReader.Application/Jobs/ExportUserDataJob.cs"
        issue: "parsedReceipts variable assigned at line 50 (dbContext.Receipts.Where(...)) but referenced nowhere in the archive-writing block (lines 133-163). No parsed_receipts.json or parsed_receipts.csv entry exists in the bundle."
    missing:
      - "Add WriteJsonEntryAsync(archive, 'parsed_receipts.json', parsedReceipts, ...) and WriteCsvEntryAsync(archive, 'parsed_receipts.csv', ...) inside the ZipArchive block"
      - "Update README.txt to list parsed_receipts.json/.csv"
      - "Update ExportUserDataJobTests to assert 'parsed_receipts.json' is present in the zip"
  - truth: "Download endpoint validates ownership (403 on mismatched user) and is auth-required"
    status: partial
    reason: "Ownership check (403) and RequireAuthorization are both present and correct. However ExportEndpoints.cs has a FileStream resource-management bug (CR-01): the FileStream opened before Invalidate/audit calls is never wrapped in using or try/finally. If auditLogger.RecordAsync throws (e.g. DB timeout), the file handle leaks and the one-time token is consumed without the user receiving the file. The 403 ownership logic itself is correct; the partial flag is for the incomplete resource safety that can silently consume a user's one-time download."
    artifacts:
      - path: "Backend/src/TaxReader.Api/Endpoints/ExportEndpoints.cs"
        issue: "CR-01 from code review: FileStream opened at ~line 115 but not in a using block; Invalidate called at line 118 before response is confirmed delivered; auditLogger.RecordAsync (line 120) can throw after stream is open, leaking the handle"
    missing:
      - "Move audit logging before FileStream open OR wrap FileStream in using/try-finally"
      - "Consider deferring Invalidate to after response is confirmed sent (or accept best-effort and move audit before stream open)"
human_verification:
  - test: "Legal pages unauthenticated access + footer links + draft markers"
    expected: "All four pages (/impressum, /datenschutz, /agb, /widerruf) load without redirect for unauthenticated users; amber draft marker visible at top of each; footer shows all five links (Impressum, Datenschutzerklärung, AGB, Widerrufsbelehrung, Cookie-Einstellungen); header reads 'TaxReader'"
    why_human: "Requires running browser against dev server; cannot verify auth-bypass behavior or visual rendering via grep"
  - test: "Cookie banner TTDSG compliance — equal prominence, no pre-ticked checkboxes"
    expected: "On first visit (cleared localStorage), banner appears at bottom with Alle akzeptieren and Nur notwendige at equal visual size; Einstellungen opens dialog where Fehleranalyse is unchecked by default and Notwendig is disabled; footer Cookie-Einstellungen reopens dialog"
    why_human: "Requires browser and DevTools to verify button sizing parity and localStorage behavior; no frontend test framework exists this milestone"
  - test: "Sentry consent gating — init on grant, close on revoke, no page reload"
    expected: "With NEXT_PUBLIC_SENTRY_ENABLED=true: clicking Alle akzeptieren initializes Sentry (visible in DevTools network or Sentry.isInitialized() in console); revoking from Cookie-Einstellungen calls Sentry.close() without any page reload; localStorage key taxreader-consent reflects choice"
    why_human: "Requires browser + DevTools + environment variable; consent race condition (CR-02) and instrumentation-client double-init (CR-03) require live testing to confirm behavior"
  - test: "DSGVO export end-to-end — bundle contents, IDOR check, one-time token"
    expected: "Settings -> Daten exportieren shows Wird erstellt... then Export bereit; downloaded zip contains receipts, items, classifications, token_transactions, audit_log, README (noting parsed_receipts MISSING per gap above); IDOR: second account cannot download first account's token (403); second download attempt returns 404/expired"
    why_human: "Requires docker compose up, two test accounts, network tab inspection of download endpoint; export bundle content verification needs file open"
  - test: "Placeholder contact details must be replaced before any public deployment"
    expected: "[Name], [Anschrift], [PLZ Ort], [kontakt@taxreader.de] tokens replaced with real operator data in all four legal pages before launch; mailto link must be a valid email address"
    why_human: "Operator action — only the operator can supply real contact data; requires a decision and code change. CR-04 from code review classifies this as a blocker for commercial launch under TMG §5"
  - test: "AVV/DPA signing — Anthropic, Stripe, Sentry, BetterStack"
    expected: "All four AVVs/DPAs signed/accepted, filed, and checked off in 06-AVV-TRACKING.md; DPA URLs in 06-AVV-TRACKING.md match those in datenschutz/page.tsx"
    why_human: "External operator action requiring access to each provider's legal portal; cannot be automated"
  - test: "DPMA + EUIPO Marken search for 'TaxReader' classes 9 + 42"
    expected: "Search results recorded in 06-MARKEN-SEARCH.md as Clear or Conflicted; Decision set to proceed/rename/register; if Conflicted, rename decision made before launch"
    why_human: "External operator action requiring access to DPMAregister and EUIPO eSearch+; cannot be automated"
  - test: "Lawyer review of AGB + Datenschutzerklärung"
    expected: "06-LEGAL-REVIEW.md rows updated to Lawyer-reviewed for all four pages; draft markers removed; real operator name/address/contact filled in"
    why_human: "External professional engagement; deferred to Phase 7 QA-07 by design (D-02)"
deferred: []
---

# Phase 6: Legal + Consent + Data Export Verification Report

**Phase Goal:** Launch-ready legal posture — all mandated DE pages, TTDSG cookie consent, signed AVVs/DPAs with all sub-processors, DSGVO Art. 20 self-serve data export, audit log for sensitive operations, and Markenrechte clearance.
**Verified:** 2026-06-03T08:00:00Z
**Status:** gaps_found
**Re-verification:** No — initial verification

---

## Goal Achievement

### Observable Truths

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | Impressum, Datenschutzerklärung, AGB, Widerrufsbelehrung pages exist and render | VERIFIED | All four page.tsx files exist in Frontend/src/app/(legal)/; all contain § 5 TMG / Art. 22 / AGB / Widerruf copy confirmed via grep |
| 2 | Every page footer links Impressum, Datenschutz, AGB, Widerruf, and Cookie-Einstellungen | VERIFIED | footer.tsx confirmed: href="/impressum", /datenschutz, /agb, /widerruf; CookieSettingsLink present; Footer imported in both (legal) and (authenticated) layouts |
| 3 | Datenschutz lists Anthropic, Stripe, Sentry, BetterStack as sub-processors with AVV links + Drittland note | VERIFIED | grep confirmed: Anthropic, Stripe, Sentry, BetterStack in table; anthropic.com/legal/dpa link; Schrems II + TADPF + Data Privacy Framework wording present; Art. 22 disclosed |
| 4 | AGB states StBerG-safe positioning and GoBD non-applicability | VERIFIED | "Vertragsgegenstand ist Strukturierung, keine Steuerberatung" exact phrase at agb/page.tsx:27; GoBD, StBerG, VSBG all confirmed; /widerruf link present |
| 5 | Every legal page renders the '⚠ Entwurf – anwaltliche Prüfung ausstehend' marker | VERIFIED | grep confirmed on all four page files at line 10 each |
| 6 | Unauthenticated users can reach /agb and /widerruf without redirect | VERIFIED | auth-provider.tsx line 29: PUBLIC_PATHS includes "/agb" and "/widerruf" |
| 7 | Cookie banner appears on first visit with equally prominent 'Alle akzeptieren' and 'Nur notwendige'; Sentry gated on consent; footer revoke wires to reopenSettings | VERIFIED (code) / UNCERTAIN (behavior) | cookie-banner.tsx: both buttons rendered, role="region", aria-label present; consent-provider.tsx: taxreader-consent, acceptAll, acceptNecessary, reopenSettings, Sentry.isInitialized(), Sentry.close(2000); instrumentation-client.ts: hasSentryConsent() + compound guard; cookie-settings-link.tsx calls reopenSettings; ConsentProvider wraps AuthProvider in layout.tsx; CookieBanner in both layouts. CR-02 (Sentry rapid-toggle race) and CR-03 (unconditional onRouterTransitionStart export) found by code review — behavior requires human UAT to confirm |
| 8 | audit_log table exists, IAuditLogger writes append-only rows, 5 sensitive ops are wired | VERIFIED | AuditLogEntry.cs (Dictionary<string,object?> Metadata, no User nav); AuditAction.cs (all 7 values); AuditLogger.cs (AuditLogEntries.Add + SaveChangesAsync); AuditLogEntryConfiguration.cs (ToTable("audit_log"), jsonb, no HasOne/OnDelete Cascade); Migration 20260603045456_AddAuditLog.cs (no FK); DI AddScoped<IAuditLogger,AuditLogger>. All 5 call sites: DeleteAccountHandler (AccountDeleted + email_hash), GrantTokensJob (TokensGranted), RevokeTokensJob (TokensRevoked), RefreshTokenService (RefreshTokenReplayDetected), SaveClassificationRuleHandler (ClassificationRuleCreated). No AuditLogEntries.Remove/RemoveRange found. Tests: 7/7 AuditLog tests pass; 280/285 total suite passes |
| 9 | DSGVO export bundle (receipts, items, classifications, token_transactions, audit_log, README) is downloadable from settings; ownership validates 403; one-time token | PARTIAL | ExportUserDataJob.cs writes: receipts.json/csv (ReceiptFile metadata), items.json/csv, classifications.json/csv, token_transactions.json/csv, audit_log.json/csv, README.txt. MISSING: parsedReceipts (Receipt entity: vendor, purchase_date, total_amount) queried but never written to zip — confirmed dead variable at line 50. ExportEndpoints.cs: ownership check (rec.UserId != currentUser.UserId → 403), Invalidate after download (one-time), MapExportEndpoints in Program.cs, no AllowAnonymous. Frontend: requestDataExport, getExportStatus, use-data-export.ts with refetchInterval, settings page with all state labels. FileStream resource bug (CR-01) present |

**Score:** 7/9 truths verified (8 with UNCERTAIN counted as warning, 1 PARTIAL = gap)

---

### Required Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `Frontend/src/app/(legal)/agb/page.tsx` | AGB with StBerG + GoBD + Widerrufsrecht + VSBG | VERIFIED | Contains exact phrase "Vertragsgegenstand ist Strukturierung, keine Steuerberatung", GoBD, StBerG, VSBG, /widerruf link |
| `Frontend/src/app/(legal)/widerruf/page.tsx` | Widerrufsbelehrung + Muster-Widerrufsformular | VERIFIED | Contains Muster-Widerrufsformular and §356 BGB waiver text ("mein Widerrufsrecht verliere" at lines 66-67) |
| `Frontend/src/components/layout/footer.tsx` | Site-wide footer with five legal/consent links | VERIFIED | All four href links + CookieSettingsLink; 22+ lines; Server Component (no "use client") |
| `Frontend/src/providers/consent-provider.tsx` | localStorage-backed consent context + Sentry init/close | VERIFIED | Contains taxreader-consent, acceptAll, acceptNecessary, reopenSettings, Sentry.close(2000), Sentry.isInitialized() guard |
| `Frontend/src/components/consent/cookie-banner.tsx` | TTDSG banner with equal-prominence buttons | VERIFIED | Contains Alle akzeptieren, Nur notwendige, role="region", aria-label="Cookie-Einstellungen", returns null when decided=true |
| `Frontend/src/components/consent/consent-settings-dialog.tsx` | Granular consent controls | VERIFIED | Fehleranalyse, htmlFor="fehleranalyse-checkbox", disabled Notwendig, Einstellungen speichern |
| `Backend/src/TaxReader.Domain/Entities/AuditLogEntry.cs` | Audit log entity (no User nav property) | VERIFIED | Dictionary<string,object?> Metadata; no "public User User" |
| `Backend/src/TaxReader.Domain/Enums/AuditAction.cs` | Auditable action enum | VERIFIED | All 7 values including AccountDeleted, DataExportRequested, DataExportDownloaded |
| `Backend/src/TaxReader.Application/Interfaces/IAuditLogger.cs` | RecordAsync contract | VERIFIED | Interface exists with correct signature |
| `Backend/src/TaxReader.Infrastructure/Services/AuditLogger.cs` | AuditLogger impl (DbSet.Add + SaveChangesAsync) | VERIFIED | AuditLogEntries.Add + SaveChangesAsync confirmed |
| `Backend/src/TaxReader.Infrastructure/Migrations/AddAuditLog.cs` | EF migration creating audit_log (no FK) | VERIFIED | File 20260603045456_AddAuditLog.cs: audit_log table, jsonb column, no ForeignKey for actor/subject |
| `Backend/src/TaxReader.Application/Jobs/ExportUserDataJob.cs` | Hangfire job assembling the zip bundle | PARTIAL | ZipArchive, LogContext.PushProperty, AutomaticRetry, AuditLogEntries present. parsedReceipts queried but NOT written to bundle (IN-01 confirmed) |
| `Backend/src/TaxReader.Application/Jobs/ExportCleanupJob.cs` | Recurring 24h purge of /tmp/exports | VERIFIED | AddHours(-24), File.Delete, DisableConcurrentExecution confirmed; registered as "export-cleanup" in RecurringJobsBootstrap |
| `Backend/src/TaxReader.Api/Endpoints/ExportEndpoints.cs` | /export/request, /export/status, /export/download | VERIFIED (with caveat) | All three routes present; no AllowAnonymous; ownership check (403); Invalidate; audit logging. CR-01 FileStream resource safety bug present |
| `Backend/src/TaxReader.Infrastructure/Services/ExportTokenStore.cs` | Singleton in-memory token store | VERIFIED | ConcurrentDictionary, AddSingleton<IExportTokenStore,ExportTokenStore> |
| `.planning/phases/06-legal-consent-data-export/06-AVV-TRACKING.md` | AVV/DPA sign-off checklist | VERIFIED | All four sub-processors, anthropic.com/legal/dpa, stripe.com/de/legal/dpa, sentry.io/legal/dpa, betterstack.com/privacy, Drittland + Schrems/TADPF, Datenschutz coupling note |
| `.planning/phases/06-legal-consent-data-export/06-MARKEN-SEARCH.md` | DPMA/EUIPO search record classes 9+42 | VERIFIED | DPMA, EUIPO, Nizza, classes 9 and 42, Clear, Conflicted, rename as decision options |
| `.planning/phases/06-legal-consent-data-export/06-LEGAL-REVIEW.md` | Lawyer-review gate checklist | VERIFIED | Lawyer-reviewed, Drafted, Live status flow; all four pages; 5 Werktagen flagged for lawyer |

---

### Key Link Verification

| From | To | Via | Status | Details |
|------|----|-----|--------|---------|
| (legal)/layout.tsx | footer.tsx | import + mount after `<main>` | VERIFIED | import { Footer } at line 3; `<Footer />` at line 24 |
| (authenticated)/layout.tsx | footer.tsx | import + mount | VERIFIED | import { Footer } at line 6; `<Footer />` at line 34 |
| footer.tsx | /agb /widerruf /impressum /datenschutz | next/link hrefs | VERIFIED | All four href values confirmed |
| auth-provider.tsx | PUBLIC_PATHS | array includes /agb and /widerruf | VERIFIED | Line 29 confirmed |
| instrumentation-client.ts | taxreader-consent localStorage | hasSentryConsent() reads localStorage | VERIFIED | hasSentryConsent at line 11; compound guard at line 23 |
| consent-provider.tsx | Sentry.init / Sentry.close | grant/revoke handlers | VERIFIED | Sentry.isInitialized() guard + Sentry.close(2000) confirmed |
| cookie-settings-link.tsx | useConsent().reopenSettings() | footer client component | VERIFIED | useConsent import; reopenSettings() in onClick at line 6 |
| ExportEndpoints.cs | IExportTokenStore + ICurrentUser | ownership validation: rec.UserId != currentUser.UserId → 403 | VERIFIED | Lines confirmed; no AllowAnonymous |
| ExportUserDataJob.cs | dbContext.AuditLogEntries (user's own rows) | SubjectUserId == userId filter | VERIFIED | Line 107: .Where(a => a.SubjectUserId == userId) |
| settings/page.tsx | /api/v1/export/request + /export/status | use-data-export hook (TanStack Query polling) | VERIFIED | requestDataExport, getExportStatus in api-client.ts; refetchInterval in use-data-export.ts; settings page wires all states |
| ExportUserDataJob.cs | parsedReceipts → zip bundle | WriteJsonEntryAsync for parsed_receipts.json | FAILED | parsedReceipts queried but never passed to WriteJsonEntryAsync or WriteCsvEntryAsync — dead variable |
| DeleteAccountHandler.cs | IAuditLogger.RecordAsync | primary-constructor injection + call before Users.Remove | VERIFIED | auditLogger.RecordAsync(AuditAction.AccountDeleted) at line 43; email_hash (not raw email) used |
| AuditLogger.cs | dbContext.AuditLogEntries | DbSet.Add + SaveChangesAsync | VERIFIED | Lines 16 + 25 confirmed |
| DependencyInjection.cs | IAuditLogger -> AuditLogger | AddScoped registration | VERIFIED | Line 133 confirmed |

---

### Data-Flow Trace (Level 4)

| Artifact | Data Variable | Source | Produces Real Data | Status |
|----------|---------------|--------|-------------------|--------|
| settings/page.tsx export section | exportToken, status | requestDataExport() → POST /export/request; getExportStatus() → GET /export/status | Yes — flows through api-client.ts to backend endpoints to ExportTokenStore | FLOWING |
| ExportUserDataJob.cs | receipts, items, classifications, tokenTransactions, auditEntries | dbContext.ReceiptFiles/Receipts/ReceiptItems/ItemClassifications/TokenTransactions/AuditLogEntries.Where(userId) | Yes — real EF queries | FLOWING |
| ExportUserDataJob.cs | parsedReceipts | dbContext.Receipts.Where(r => r.ReceiptFile.UserId == userId) | Queried but NOT written to zip | HOLLOW — data fetched, never serialized |
| ExportEndpoints.cs download | zipPath FileStream | Path.Combine(tempPath, token + ".zip") | Real file — written by ExportUserDataJob | FLOWING (with CR-01 resource safety caveat) |

---

### Behavioral Spot-Checks

| Behavior | Command | Result | Status |
|----------|---------|--------|--------|
| AuditLog unit tests (7 tests) | dotnet test Backend --filter "FullyQualifiedName~AuditLog" | 7/7 passed | PASS |
| Export unit tests (18 tests) | dotnet test Backend --filter "FullyQualifiedName~Export" | 18/18 passed | PASS |
| Full backend test suite | dotnet test Backend | 280 passed, 5 skipped, 0 failed (285 total) | PASS |
| ExportUserDataJob produces parsedReceipts in bundle | Read ExportUserDataJob.cs + grep for parsedReceipts write | parsedReceipts variable never passed to WriteJsonEntryAsync | FAIL |
| Frontend build | cd Frontend && npm run build | Not run (dependency on local npm) | SKIP — would require local environment |
| audit_log has no Remove/Delete paths | grep AuditLogEntries.Remove in Backend/src | No results | PASS |
| migration has no actor/subject ForeignKey | grep ForeignKey in 20260603045456_AddAuditLog.cs | No results | PASS |

---

### Requirements Coverage

| Requirement | Source Plan | Description | Status | Evidence |
|-------------|------------|-------------|--------|----------|
| LEG-01 | 06-01 | Impressum (TMG §5), reachable from footer | VERIFIED | impressum/page.tsx exists with § 5 TMG, § 19 UStG, ODR link; footer links confirmed |
| LEG-02 | 06-01 | Datenschutzerklärung (DSGVO Art.13/22/28, sub-processors, Drittland) | VERIFIED | datenschutz/page.tsx: Art. 22, 4 sub-processors, Anthropic DPA link, Schrems II/TADPF |
| LEG-03 | 06-01 | AGB (StBerG-safe, GoBD, Widerrufsrecht, VSBG) | VERIFIED | agb/page.tsx: exact StBerG phrase, GoBD, VSBG, /widerruf link. Lawyer review pending (Phase 7) |
| LEG-04 | 06-01 | Widerrufsbelehrung + Muster-Widerrufsformular | VERIFIED | widerruf/page.tsx: Muster-Widerrufsformular, §356 BGB waiver text |
| LEG-05 | 06-02 | TTDSG cookie banner — equal prominence, no pre-ticked, footer revoke, Sentry gated | VERIFIED (code) | All components confirmed; behavior requires human UAT; CR-02/CR-03 warnings noted |
| LEG-06 | 06-05 | AVVs/DPAs signed and on file | TRACKED (operator pending) | 06-AVV-TRACKING.md exists with all 4 sub-processors + DPA URLs + operator action steps; signing is operator action |
| LEG-07 | 06-04 | Self-serve data export (DSGVO Art.20) | PARTIAL | ExportUserDataJob implemented; parsedReceipts (vendor/date/amount) MISSING from bundle; download endpoint wired; frontend trigger present. In-app delivery accepted per D-09 |
| LEG-08 | 06-03 | audit_log table + 5 sensitive ops | VERIFIED | Entity, enum, interface, impl, migration, DI, all 5 call-sites, tests green |
| LEG-09 | 06-05 | DPMA + EUIPO Marken search for "TaxReader" | TRACKED (operator pending) | 06-MARKEN-SEARCH.md exists with DPMA/EUIPO rows for classes 9+42; search is operator action |

---

### Anti-Patterns Found

| File | Line | Pattern | Severity | Impact |
|------|------|---------|----------|--------|
| Backend/src/TaxReader.Application/Jobs/ExportUserDataJob.cs | 50 | `parsedReceipts` variable queried but never referenced in archive write block | BLOCKER | DSGVO Art.20 completeness — parsed receipt data (vendor, date, total) absent from export bundle |
| Backend/src/TaxReader.Api/Endpoints/ExportEndpoints.cs | ~115 | FileStream opened without using/try-finally; Invalidate called before response confirmed | WARNING | File handle leak on exception; one-time token consumed without user receiving file |
| Backend/src/TaxReader.Infrastructure/Services/ExportTokenStore.cs | 37 | `Status != Generating` guard prevents Expired flip on stuck Generating tokens | WARNING | User permanently sees spinner if job fails after all retries; no recovery path |
| Frontend/src/providers/consent-provider.tsx | ~57 | fire-and-forget void Sentry.close(2000); rapid toggle can cause double-init | WARNING | Overlapping Sentry SDK instances on rapid consent toggle; TTDSG correctness at edge |
| Frontend/instrumentation-client.ts | 38 | `onRouterTransitionStart = Sentry.captureRouterTransitionStart` unconditional | WARNING | Executed regardless of consent; relies on undocumented SDK no-op guarantee |
| Frontend/src/app/(legal)/*.page.tsx | multiple | Placeholder tokens [Name], [Anschrift], [kontakt@taxreader.de] in live production paths | BLOCKER (CR-04) | TMG §5 violation (missing Impressum contact) + DSGVO Art.13 violation; mailto link broken; must be replaced before any public deployment |

---

### Human Verification Required

**Note:** All five plans ended with blocking HUMAN-UAT gates by design. The items below combine the UAT requirements from all plans plus additional items surfaced by code review.

#### 1. Legal Pages — Unauthenticated Rendering + Footer

**Test:** Start dev server (`cd Frontend && npm run dev`). As unauthenticated user, visit /impressum, /datenschutz, /agb, /widerruf.
**Expected:** All four pages load (no /login redirect); amber "⚠ Entwurf – anwaltliche Prüfung ausstehend" marker visible at top; footer shows Impressum, Datenschutzerklärung, AGB, Widerrufsbelehrung, Cookie-Einstellungen links; header reads "TaxReader".
**Why human:** Route auth bypass and visual rendering require browser; cannot verify via grep.

#### 2. Cookie Banner — TTDSG Equal Prominence + No Pre-tick

**Test:** Clear localStorage, load app. Click "Einstellungen" without accepting.
**Expected:** Banner appears at bottom with Alle akzeptieren and Nur notwendige rendered at equal visual height/padding; Fehleranalyse unchecked by default; Notwendig checked and non-interactive; footer Cookie-Einstellungen reopens panel.
**Why human:** Button sizing equality and checkbox state require browser; CR-02 Sentry rapid-toggle race requires DevTools/console interaction; no frontend test framework this milestone.

#### 3. Sentry Consent Gating — Init on Grant, Close on Revoke

**Test:** Set NEXT_PUBLIC_SENTRY_ENABLED=true; click Alle akzeptieren; check Sentry.isInitialized() in console; then revoke via Cookie-Einstellungen.
**Expected:** Sentry initializes only on accept; Sentry.close() runs on revoke; no page reload occurs; localStorage taxreader-consent reflects choice.
**Why human:** Requires live environment with Sentry DSN configured; CR-03 (unconditional onRouterTransitionStart) requires SDK version check; CR-02 double-init race requires rapid toggling test.

#### 4. DSGVO Export — Bundle Contents, IDOR, One-Time Token

**Test:** docker compose up; log in; trigger export from settings; download bundle; check contents; IDOR test with second account; re-download.
**Expected:** Bundle contains receipts, items, classifications, token_transactions, audit_log, README. NOTE: parsed_receipts.json/csv (vendor, purchase_date, total_amount) will be MISSING due to the gap identified — this is an observable failure. IDOR: second account gets 403. Second download: 404/expired.
**Why human:** End-to-end requires running stack; IDOR requires two accounts; file inspection requires unzip.

#### 5. Operator: Replace Placeholder Contact Details (CR-04 — Pre-launch Blocker)

**Test:** Replace all [Name], [Anschrift], [PLZ Ort], [kontakt@taxreader.de] tokens in all four legal pages with real operator data.
**Expected:** No [bracket] tokens remain in any (legal) page; mailto link is a valid RFC 2822 email address; CI check added to prevent regression.
**Why human:** Only the operator knows the real legal entity name/address/contact; requires a code change and operator data decision.

#### 6. Operator: AVV/DPA Signing (LEG-06)

**Test:** For each sub-processor in 06-AVV-TRACKING.md — sign/accept the DPA at the listed URL; file signed copy; mark "Signed" + "Link in Datenschutz" = ✓.
**Expected:** All four checkboxes in 06-AVV-TRACKING.md marked complete before commercial launch.
**Why human:** External operator action requiring provider portals; cannot be automated.

#### 7. Operator: DPMA + EUIPO Marken Search (LEG-09)

**Test:** Run DPMAregister and EUIPO eSearch+ for "TaxReader" in Nizza classes 9 + 42; record results + decision in 06-MARKEN-SEARCH.md.
**Expected:** Result recorded as Clear/Conflicted/Already registered; Decision set; if Conflicted, rename decision made before any public marketing.
**Why human:** Requires manual access to trademark registers; results determine whether product name can be used commercially.

#### 8. Lawyer Review of AGB + Datenschutzerklärung (LEG-03, LEG-02 — Phase 7 QA-07)

**Test:** Qualified German lawyer reviews all four legal pages; updates 06-LEGAL-REVIEW.md status to Lawyer-reviewed; draft markers removed; real operator data filled in.
**Expected:** All four rows reach Lawyer-reviewed in 06-LEGAL-REVIEW.md.
**Why human:** External professional engagement; deferred to Phase 7 by design (D-02).

---

## Gaps Summary

Two gaps block full phase goal achievement:

**Gap 1 — DSGVO Export Bundle Incompleteness (parsedReceipts missing)**

`ExportUserDataJob` queries `dbContext.Receipts` (the `Receipt` entity containing vendor, purchase_date, total_amount, currency, parsed_at) into `parsedReceipts` at line 50, but this variable is never passed to `WriteJsonEntryAsync` or `WriteCsvEntryAsync`. The zip bundle contains `receipts.json/csv` (ReceiptFile metadata only — filename, upload date, status, file size) but is missing the parsed receipt data that gives DSGVO Art. 20 exports their value to the user. This is a silent omission: the variable is allocated, the DB is queried, but the data is discarded. Tests pass because the test asserts on zip entry names (receipts.json exists) without checking whether the more meaningful parsed-receipts data is also present. Fix: add `WriteJsonEntryAsync(archive, "parsed_receipts.json", parsedReceipts, ...)` + matching CSV entry inside the archive block.

**Gap 2 — Placeholder Contact Details in Live Legal Pages (CR-04)**

All four legal pages contain `[Name]`, `[Anschrift]`, `[PLZ Ort]`, and `[kontakt@taxreader.de]` as literal displayed strings. The Impressum is a legally-required public contact disclosure under TMG §5; an Impressum with placeholder brackets instead of real contact data is a regulatory violation. The broken `mailto:[kontakt@taxreader.de]` link will not open a mail client. This must be resolved before any public deployment. It is separate from lawyer review (Phase 7) — it requires the operator to supply their real legal entity details. The code-review classified this CR-04 BLOCKER.

**Items correctly deferred to Phase 7:**
- Final lawyer sign-off on AGB + Datenschutzerklärung (QA-07)
- BetterStack uptime monitors + status-page footer link (OBS-03, QA-06)
- Draft marker removal from legal pages (after Lawyer-reviewed status)

**Items correctly classified as operator-pending (not code gaps):**
- AVV/DPA signing for Anthropic, Stripe, Sentry, BetterStack (LEG-06)
- DPMA + EUIPO Marken search and decision (LEG-09)

**Additional code-quality concerns (not blocking goal but need attention before launch):**
- CR-01: FileStream resource leak in ExportEndpoints download path
- CR-02: Sentry double-init race on rapid consent toggle in ConsentProvider
- CR-03: Unconditional onRouterTransitionStart export in instrumentation-client.ts
- WR-02: ExportUserDataJob has no error handler — stuck Generating tokens after job failure
- WR-04: ExportTokenStore expiry-flip logic excludes Generating status — permanent spinner on job failure

---

*Verified: 2026-06-03T08:00:00Z*
*Verifier: Claude (gsd-verifier)*
