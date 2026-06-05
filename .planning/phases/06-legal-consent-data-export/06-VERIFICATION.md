---
phase: 06-legal-consent-data-export
verified: 2026-06-05T00:00:00Z
status: human_needed
score: 9/9 must-haves verified (code-verifiable)
overrides_applied: 0
re_verification:
  previous_status: gaps_found
  previous_score: 7/9
  gaps_closed:
    - "Truth #9 / GAP 1: parsed_receipts.json + parsed_receipts.csv now written into the DSGVO Art. 20 bundle with real vendor/amount/date data"
    - "Truth #9 / CR-01: download endpoint reordered (audit before FileStream open, Invalidate only after Results.File takes ownership) — handle never leaks, one-time token not consumed on delivery-setup failure"
    - "WR-02: ExportUserDataJob wraps body in try/catch and flips token to terminal Expired on failure, then re-throws for Hangfire AutomaticRetry"
    - "WR-04: ExportTokenStore TTL-flip guard changed from (!= Generating) to (!= Expired) so Generating-past-TTL tokens flip to Expired; MarkExpired added"
    - "CR-04: CI hygiene-check guard fails the build on any [bracket] placeholder in (legal) pages — placeholder regression now impossible to deploy"
  gaps_remaining: []
  regressions: []
human_verification:
  - test: "Operator: Replace placeholder contact details in all four (legal) pages (CR-04 — pre-launch blocker, now CI-guarded)"
    expected: "[Name], [Anschrift], [PLZ Ort], [kontakt@taxreader.de] (and any other [bracket] token) replaced with real legal-entity data in impressum/datenschutz/agb/widerruf; mailto link is a valid email. CI hygiene-check currently FAILS by design until this is done."
    why_human: "Operator action — only the operator can supply the real legal-entity name/address/contact. Code-side guard is complete; the fill-in itself is a tracked operator decision, not a code gap."
  - test: "Operator: AVV/DPA signing — Anthropic, Stripe, Sentry, BetterStack (LEG-06)"
    expected: "All four AVVs/DPAs signed/accepted, filed, and checked off in 06-AVV-TRACKING.md; DPA URLs match those in datenschutz/page.tsx"
    why_human: "External operator action requiring each provider's legal portal; cannot be automated. Tracking record (06-AVV-TRACKING.md) exists."
  - test: "Operator: DPMA + EUIPO Marken search for 'TaxReader' classes 9 + 42 (LEG-09)"
    expected: "Search results recorded in 06-MARKEN-SEARCH.md as Clear/Conflicted; decision set to proceed/rename/register; rename decided before launch if conflicted"
    why_human: "External operator action requiring DPMAregister + EUIPO eSearch+; cannot be automated. Tracking record (06-MARKEN-SEARCH.md) exists."
  - test: "Lawyer review of AGB + Datenschutzerklärung (LEG-02/LEG-03 — deferred to Phase 7 QA-07 by design D-02)"
    expected: "06-LEGAL-REVIEW.md rows reach Lawyer-reviewed for all four pages; draft markers removed after sign-off"
    why_human: "External professional engagement; deferred to Phase 7 by design."
  - test: "Legal pages unauthenticated access + footer links + draft markers (UI behavior)"
    expected: "/impressum, /datenschutz, /agb, /widerruf load without redirect for unauthenticated users; amber draft marker visible; footer shows all five links; header reads 'TaxReader'"
    why_human: "Requires running browser against dev server; auth-bypass and visual rendering cannot be verified via grep."
  - test: "Cookie banner TTDSG compliance + Sentry consent gating (LEG-05 — UI behavior)"
    expected: "Banner appears on first visit with equally prominent Alle akzeptieren / Nur notwendige; Fehleranalyse unchecked by default; Sentry init on grant, Sentry.close() on revoke with no page reload; footer Cookie-Einstellungen reopens dialog"
    why_human: "Requires browser + DevTools + Sentry DSN; CR-02/CR-03 edge behaviors require live testing; no frontend test framework this milestone."
  - test: "DSGVO export end-to-end — bundle now includes parsed_receipts, IDOR check, one-time token, failure recovery (LEG-07 — integration)"
    expected: "Settings -> Daten exportieren shows Wird erstellt... then Export bereit; downloaded zip contains receipts, parsed_receipts (vendor/date/amount), items, classifications, token_transactions, audit_log, README; IDOR: second account gets 403; second download attempt 404/expired; a failed/stuck job surfaces as Expired with a re-trigger button"
    why_human: "End-to-end requires docker compose up, two accounts, network inspection, and unzip of the bundle; cannot be fully exercised via unit tests."
deferred:
  - truth: "Lawyer sign-off on AGB + Datenschutzerklärung; draft-marker removal"
    addressed_in: "Phase 7"
    evidence: "Phase 7 QA-07 — final lawyer review; deferred by design decision D-02 (recorded in prior verification and 06-LEGAL-REVIEW.md)"
---

# Phase 6: Legal + Consent + Data Export Verification Report (Re-Verification)

**Phase Goal:** Launch-ready legal posture — all mandated DE pages, TTDSG cookie consent, signed AVVs/DPAs with all sub-processors, DSGVO Art. 20 self-serve data export, audit log for sensitive operations, and Markenrechte clearance.
**Verified:** 2026-06-05
**Status:** human_needed
**Re-verification:** Yes — after gap closure (plans 06-06 LEG-07, 06-07 CR-04). Previous: gaps_found 7/9.

---

## Re-Verification Summary

The two code gaps that drove the prior `gaps_found (7/9)` are both **closed and verified against the actual codebase** (not merely claimed in SUMMARY):

1. **GAP 1 — parsed_receipts missing from DSGVO bundle (was BLOCKER).** `ExportUserDataJob.cs:141-145` now writes `parsed_receipts.json` and `parsed_receipts.csv` from the already-queried `parsedReceipts` projection (vendor, purchase_date, total_amount, currency, parsed_at). README updated (line 231). Test `HandleAsync_ValidUser_ParsedReceiptsCarryRealData` opens the zip entry and asserts it contains the seeded `"Amazon"` vendor and `"29.99"` total — proving real data flows, not an empty entry. The dead-variable HOLLOW finding from the prior Level-4 trace is resolved.

2. **CR-01 — FileStream resource safety (was WARNING).** `ExportEndpoints.cs` download handler now records the audit event (line 116) **before** opening the FileStream (line 125), and calls `tokenStore.Invalidate(token)` (line 129) **only after** the stream is open and ownership transfers to `Results.File`. An exception during audit therefore leaks no handle and consumes no one-time token. IDOR `Results.Forbid()` (line 82) and inherited `RequireAuthorization` are intact; no `AllowAnonymous` introduced.

Two related robustness gaps surfaced during gap-closure were also fixed: **WR-02** (job-failure flips token to terminal Expired, `ExportUserDataJob.cs:182-192`) and **WR-04** (`ExportTokenStore.cs:38` guard now `!= Expired`, plus `MarkExpired`), so a stuck/failed export no longer leaves the UI spinning — it surfaces the existing `Expired` re-trigger branch with zero frontend change.

**CR-04** (placeholder contact data) was correctly NOT a code gap — it is an operator fill-in. Plan 06-07 added a CI guard (`.github/workflows/ci.yml:47-60`) that fails `hygiene-check` on any `[bracket]` token in `(legal)` pages. Verified: the guard's grep finds 16 placeholder occurrences across all four pages today (exit 0), so CI would correctly **fail** until the operator supplies real data. Regression is now impossible to deploy.

**No regressions:** full backend suite 284 passed / 5 skipped / 0 failed (was 280/5; +4 new tests). Legal-page legal substance (TMG, StBerG phrase, Muster-Widerrufsformular, sub-processors) unchanged.

**Status is `human_needed`, not `passed`,** because the phase goal's full achievement still depends on operator/human actions that are correctly out of code scope (placeholder fill-in, AVV signing, Marken search, lawyer review, plus UI/integration UAT). All code-verifiable must-haves are green.

---

## Goal Achievement

### Observable Truths

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | Impressum, Datenschutz, AGB, Widerruf pages exist and render legal substance | VERIFIED | All four page.tsx present; TMG/StBerG-phrase/Muster-Widerrufsformular/Anthropic substance grep-confirmed (no regression) |
| 2 | Every footer links Impressum, Datenschutz, AGB, Widerruf, Cookie-Einstellungen | VERIFIED (regression-checked) | Unchanged since prior VERIFIED (footer.tsx + both layouts) |
| 3 | Datenschutz lists 4 sub-processors with AVV links + Drittland note | VERIFIED (regression-checked) | Unchanged since prior VERIFIED |
| 4 | AGB StBerG-safe + GoBD | VERIFIED (regression-checked) | Unchanged since prior VERIFIED |
| 5 | Every legal page renders draft marker | VERIFIED (regression-checked) | Unchanged since prior VERIFIED |
| 6 | Unauthenticated users reach /agb, /widerruf without redirect | VERIFIED (regression-checked) | auth-provider PUBLIC_PATHS unchanged |
| 7 | Cookie banner equal prominence; Sentry gated on consent; footer revoke | VERIFIED (code) / UNCERTAIN (behavior) | Unchanged code; behavior routed to human UAT (CR-02/CR-03 edge cases) |
| 8 | audit_log table + IAuditLogger append-only + 5 sensitive ops wired | VERIFIED (regression-checked) | Unchanged since prior VERIFIED; 7/7 audit tests still green |
| 9 | DSGVO export bundle complete (incl. parsed_receipts), ownership 403, one-time token, resource-safe, failure-recoverable | VERIFIED (was PARTIAL) | parsed_receipts.json/.csv written w/ real data (ExportUserDataJob.cs:141-145, asserted by ParsedReceiptsCarryRealData); audit-before-stream + Invalidate-after-stream (ExportEndpoints.cs:116/125/129); IDOR Forbid intact; job-failure → Expired (lines 182-192); Generating-past-TTL → Expired (ExportTokenStore.cs:38). 22/22 export tests green |

**Score:** 9/9 code-verifiable truths verified (was 7/9). Truth #7 behavior + Truth #9 end-to-end remain human UAT items.

---

### Deferred Items

| # | Item | Addressed In | Evidence |
|---|------|-------------|----------|
| 1 | Lawyer sign-off on AGB + Datenschutz; draft-marker removal | Phase 7 | QA-07; design decision D-02 (recorded in 06-LEGAL-REVIEW.md) |

---

### Required Artifacts (gap-closure focus)

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `ExportUserDataJob.cs` | parsed_receipts written; failure flips token to Expired | VERIFIED | Lines 141-145 write JSON+CSV; README line 231; try/catch + MarkExpired lines 182-192 |
| `ExportEndpoints.cs` | audit before stream, Invalidate after, IDOR + auth intact | VERIFIED | Lines 116 < 125 < 129; Results.Forbid() line 82; no AllowAnonymous |
| `ExportTokenStore.cs` | Generating-past-TTL flips to Expired; MarkExpired terminal | VERIFIED | Guard `!= Expired` line 38; MarkExpired lines 53-57 |
| `IExportTokenStore.cs` | MarkExpired on contract | VERIFIED | Line 40 |
| `.github/workflows/ci.yml` | hygiene-check guard greps (legal) for [bracket], exit 1 | VERIFIED | Lines 47-60; reuses existing bash/set -e pattern; only hygiene-check job touched |
| `06-LEGAL-REVIEW.md` | Operator-action placeholder note | VERIFIED | "Operator Action: Placeholder Replacement (CR-04)" section line 40 |

---

### Key Link Verification (gap-closure focus)

| From | To | Via | Status | Details |
|------|----|-----|--------|---------|
| ExportUserDataJob.cs | parsed_receipts.json/.csv bundle entries | WriteJsonEntryAsync + WriteCsvEntryAsync on parsedReceipts | VERIFIED | Lines 141-145; data-flow now FLOWING (was HOLLOW) |
| ExportUserDataJob.cs catch | ExportTokenStore.MarkExpired | tokenStore.MarkExpired(exportToken) before re-throw | VERIFIED | Line 190 |
| ExportTokenStore.TryGet | Expired for stuck Generating tokens | guard `Status != ExportStatus.Expired` | VERIFIED | Line 38 (old `!= Generating` removed) |
| ExportEndpoints.cs | audit → stream → invalidate ordering | reordered handler | VERIFIED | RecordAsync L116 < FileStream L125 < Invalidate L129 |
| ci.yml hygiene-check | Frontend/src/app/(legal)/**/page.tsx | grep `\[[^]]+\]`, exit 1 on match | VERIFIED | Guard fires on current 16 placeholders (exit 0 = CI fails) |

---

### Data-Flow Trace (Level 4)

| Artifact | Data Variable | Source | Produces Real Data | Status |
|----------|---------------|--------|-------------------|--------|
| ExportUserDataJob.cs | parsedReceipts | dbContext.Receipts.Where(r => r.ReceiptFile.UserId == userId) | Yes — now serialized to parsed_receipts.json/.csv | FLOWING (was HOLLOW) |
| ExportEndpoints.cs download | zipPath FileStream | Path.Combine(temp, token + ".zip") | Real file; Results.File owns disposal | FLOWING (CR-01 resolved) |

---

### Behavioral Spot-Checks

| Behavior | Command | Result | Status |
|----------|---------|--------|--------|
| Export unit tests (incl. 4 new gap-closure tests) | dotnet test Backend --filter "FullyQualifiedName~Export" | 22/22 passed (was 18) | PASS |
| Full backend suite (regression) | dotnet test Backend | 284 passed, 5 skipped, 0 failed (was 280/5) | PASS |
| parsed_receipts carries real data | ParsedReceiptsCarryRealData asserts "Amazon" + "29.99" in zip entry | present | PASS |
| Job-failure flips token to Expired | HandleAsync_JobFails_MarksTokenExpired (disposes dbContext, asserts Expired) | present + green | PASS |
| Generating-past-TTL flips to Expired | TryGet_GeneratingTokenPastTtl_ReturnsExpired | green | PASS |
| CI guard catches placeholders today | grep -rnE '\[[^]]+\]' "Frontend/src/app/(legal)" | 16 matches, exit 0 (CI would fail) | PASS (guard works) |
| Download handler ordering | RecordAsync < FileStream < Invalidate; Forbid present; no AllowAnonymous | confirmed by read | PASS |

---

### Requirements Coverage

| Requirement | Source Plan | Description | Status | Evidence |
|-------------|------------|-------------|--------|----------|
| LEG-01 | 06-01, 06-07 | Impressum (TMG §5), footer-reachable | VERIFIED (code) / operator fill-in pending | impressum/page.tsx exists; placeholders now CI-guarded |
| LEG-02 | 06-01, 06-07 | Datenschutz (Art.13/22/28, sub-processors, Drittland) | VERIFIED (code) / lawyer review Phase 7 | datenschutz/page.tsx substance confirmed; placeholders CI-guarded |
| LEG-03 | 06-01, 06-07 | AGB (StBerG-safe, GoBD, Widerruf, VSBG) | VERIFIED (code) / lawyer review Phase 7 | Exact StBerG phrase, GoBD, VSBG confirmed |
| LEG-04 | 06-01, 06-07 | Widerrufsbelehrung + Muster-Widerrufsformular | VERIFIED (code) | widerruf/page.tsx confirmed; placeholders CI-guarded |
| LEG-05 | 06-02 | TTDSG cookie banner | VERIFIED (code) / behavior = human UAT | Components confirmed; CR-02/CR-03 edge behavior needs UAT |
| LEG-06 | 06-05 | AVVs/DPAs signed | TRACKED (operator pending) | 06-AVV-TRACKING.md exists; signing is operator action |
| LEG-07 | 06-04, 06-06 | Self-serve data export (Art.20) | VERIFIED (was PARTIAL) | Bundle now complete incl. parsed_receipts; resource-safe download; failure-recoverable. In-app delivery per D-09 |
| LEG-08 | 06-03 | audit_log + 5 sensitive ops | VERIFIED | Entity/enum/interface/impl/migration/DI/5 call-sites/tests green (no regression) |
| LEG-09 | 06-05 | DPMA + EUIPO Marken search | TRACKED (operator pending) | 06-MARKEN-SEARCH.md exists; search is operator action |

All nine LEG IDs accounted for. No orphaned requirements (every LEG-01..09 in REQUIREMENTS.md is claimed by a plan).

**Note on LEG-07 wording:** REQUIREMENTS.md says "download link emailed within 24h"; the implementation uses in-app status-poll + one-time download per accepted design decision D-09 (in-app delivery). This is a documented, accepted deviation, not a gap.

---

### Anti-Patterns Found

| File | Line | Pattern | Severity | Impact |
|------|------|---------|----------|--------|
| ExportUserDataJob.cs | 244-250 | EscapeCsv does not neutralize CSV formula-injection (`=`/`+`/`-`/`@`) on user-controlled vendor/description/reason | WARNING (WR-01 from 06-REVIEW) | Spreadsheet formula execution on opening exported CSV; pre-existing pattern, new vendor field extends exposure. Not goal-blocking; recommend pre-launch fix |
| ExportUserDataJob.cs | 247 | EscapeCsv quotes `\n` but not bare `\r` | WARNING (WR-02 from 06-REVIEW) | Possible row misalignment for strict RFC-4180 readers; not goal-blocking |
| ExportEndpoints.cs | 104-129 | File deleted between File.Exists and FileStream open → duplicate audit row + 500 (TOCTOU) | INFO (IN-01) | Narrow race; consistent with audit-before-delivery decision; deferrable |
| ExportUserDataJob.cs | 182-192 | catch (Exception) also catches OperationCanceledException → logs error + MarkExpired on normal cancel | INFO (IN-02) | Log noise on cooperative cancel; behaviorally harmless (retry re-flips Ready) |
| ci.yml | 55 | Guard regex `\[[^]]+\]` could false-positive on future TSX bracket syntax in legal pages | INFO (IN-03) | Latent; pages are static prose today; narrow regex if dynamic content added |

No BLOCKER anti-patterns remain. The two prior BLOCKERs (parsedReceipts dead variable; and CR-04 unguarded placeholders) are resolved. WR-01/WR-02 CSV-injection are pre-existing-pattern warnings worth a pre-launch follow-up but do not block the phase goal.

---

### Human Verification Required

See `human_verification` frontmatter. Seven items, all genuinely human/operator-scoped:

1. **Operator — replace placeholder contact data (CR-04).** Now CI-guarded; CI fails until done. Only the operator can supply real legal-entity data.
2. **Operator — AVV/DPA signing (LEG-06).** External provider portals.
3. **Operator — DPMA + EUIPO Marken search (LEG-09).** External registers.
4. **Lawyer review of AGB + Datenschutz (Phase 7 QA-07, deferred D-02).**
5. **Legal pages unauthenticated rendering + footer + draft markers (UI).**
6. **Cookie banner TTDSG + Sentry consent gating (UI; CR-02/CR-03 edges).**
7. **DSGVO export end-to-end — now including parsed_receipts, IDOR, one-time token, failure recovery (integration).**

---

## Gaps Summary

**No code gaps remain.** Both prior code gaps are closed and verified directly in the codebase (not via SUMMARY trust):

- DSGVO export bundle is now complete — `parsed_receipts.json/.csv` carry real vendor/date/amount data (test-asserted on actual zip content).
- Download path is resource-safe — audit-before-stream, Invalidate-after-`Results.File`-ownership; handle never leaks; one-time token not consumed on setup failure.
- Stuck/failed exports now recover to a terminal `Expired` state the existing UI surfaces (WR-02 + WR-04).
- CR-04 placeholder regression is blocked by a CI guard that demonstrably fires on the current pages.

Full backend suite green (284/5/0, +4 new tests). No regressions to the previously-verified truths 1-8.

The phase cannot be marked `passed` because remaining must-haves depend on operator/human actions outside code scope (placeholder fill-in, AVV signing, Marken search, lawyer review) plus UI/integration UAT. These are routed to `human_verification`, correctly classified as human/operator-pending rather than code failures — matching the user's stated distinction.

---

*Verified: 2026-06-05*
*Verifier: Claude (gsd-verifier)*
*Mode: Re-verification after gap closure (06-06, 06-07)*
