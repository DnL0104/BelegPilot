# Pitfalls Research

**Domain:** DE B2C tax-receipt SaaS — pitfalls for commercial launch
**Researched:** 2026-05-03
**Confidence:** HIGH for legal/regulatory pitfalls (well-documented); MEDIUM for AI-specific failure modes (newer area)

> **Scope note:** Codebase concerns are catalogued separately in `.planning/codebase/CONCERNS.md` (20 items). This document focuses on launch-specific pitfalls those concerns don't capture: legal/regulatory traps, DSGVO/AI processing, payment dynamics, AI/OCR systematic biases, ops at solo-dev scale.

---

## Critical Pitfalls

### Pitfall 1: Crossing the StBerG line into "tax advice"

**What goes wrong:**
The product strays from "data structuring" into Steuerberatung. A Wettbewerbszentrale or Steuerberaterkammer challenge follows; UWG / StBerG §5 exposure. Real DE precedents (smartsteuer-style cases) have forced product changes mid-flight.

**Why it happens:**
- AI classification reasoning that says "Sie können diese Ausgabe als Werbungskosten in Anlage N geltend machen" — that's an opinion on whether it's claimable, not just a category label.
- Help text or empty states that say "Was kann ich absetzen?" / "Tipps zur Steuererklärung."
- Marketing copy that promises savings ("Holen Sie sich durchschnittlich €1,234 zurück").

**How to avoid:**
- Reasoning fields are descriptive, not prescriptive. AI says "Diese Position passt zu Kategorie 'Werbungskosten Fachliteratur' (Begründung: Buch von Cornelsen-Verlag, fachliche Zuordnung)." NOT "Sie können dies absetzen."
- Disclaimer in footer of every export and prominently at first signup: "TaxReader ist kein Steuerberater. Wir strukturieren Ihre Belege; ob und wie Sie sie geltend machen, klären Sie mit Ihrem Steuerberater oder eigenständig im Rahmen Ihrer Steuererklärung."
- AGB §1 explicit: "Vertragsgegenstand ist die Strukturierung und Klassifikation hochgeladener Belege. Eine steuerliche Beratung im Sinne des StBerG findet nicht statt."
- Marketing copy: zero promises about savings, refunds, or how much money users get back.

**Warning signs:**
- Lawyer flags a help-text wording during ToS review.
- Beta-user feedback says "feels like getting tax advice."
- A competitor's similar product gets an Abmahnung covered in c't / heise.

**Phase to address:** Phase 6 (Legal + consent) — coordinated with lawyer review of AGB and Datenschutz.

---

### Pitfall 2: DSGVO Art. 22 (automated decision-making) without human-loop disclosure

**What goes wrong:**
DSGVO Art. 22 restricts decisions made solely by automation that have "rechtliche oder ähnlich erhebliche Wirkung." TaxReader's AI classification is automated and has financial effect (it shapes what the user transcribes into their tax return). Without explicit user-override + transparency, this is a violation.

**Why it happens:**
- Privacy policy doesn't mention AI in the loop.
- UI hides AI reasoning, only shows the category.
- Auto-confirm-above-threshold runs without making the threshold visible/editable.

**How to avoid:**
- Datenschutzerklärung: explicit section on AI processing, naming Anthropic, listing data sent (extracted line-item text — NOT the original PDF), purpose, retention.
- Every classification surfaces its reasoning in UI ("Warum wurde das so eingeordnet?") — already exists, ensure it stays prominent.
- User can override every classification (already exists).
- Auto-confirm threshold is user-settable; default is conservative (requires manual confirmation). Threshold visible in settings.
- Onboarding mentions "Unsere KI klassifiziert Ihre Belege; Sie behalten die volle Kontrolle und können jede Entscheidung überschreiben."

**Warning signs:**
- A user complaint to the LfDI (Landesbeauftragter für Datenschutz) of any DE Bundesland.
- Lawyer review of Datenschutz flags missing Art. 22 disclosure.

**Phase to address:** Phase 4 (Classification trustworthiness) — surface the audit/reasoning UX. Phase 6 (Legal) — Datenschutz wording.

---

### Pitfall 3: Anthropic AVV (Auftragsverarbeitungsvertrag) not in place at launch

**What goes wrong:**
DSGVO Art. 28 requires a Data Processing Agreement / AVV with every processor handling personal data on your behalf. Anthropic IS a processor (line-item text extracted from receipts is personal data — names, addresses, purchase patterns). Launching paid users without an AVV signed = bare DSGVO violation, fines up to 4% revenue or €20M.

**Why it happens:**
- AVV is treated as paperwork-later instead of a launch blocker.
- Anthropic's standard DPA is sufficient for most cases, but you have to actively request and sign it.

**How to avoid:**
- Day 1 of Phase 6: request Anthropic DPA via support, sign it (or add the signed copy to project records).
- Sub-processor list in Datenschutz includes Anthropic with link to their DPA.
- Same applies to: Stripe (their DPA available in dashboard), Sentry (DPA in dashboard), BetterStack (DPA on request).

**Warning signs:**
- Trying to launch without a signed AVV from any processor.
- Datenschutz page lists Anthropic but no link to DPA / "Vertrag liegt vor."

**Phase to address:** Phase 6 (Legal + consent) — early in the phase. AVV process can take 1-3 weeks; start it on day 1 of Phase 6.

---

### Pitfall 4: Anthropic processes data in the US (Schrems II / TADPF posture)

**What goes wrong:**
Anthropic processes data in the US. Schrems II + the EU-US Data Privacy Framework apply. If Anthropic has not certified under TADPF or doesn't have valid SCCs, transferring DE personal data is a violation regardless of consent.

**Why it happens:**
- Treated as Anthropic's problem ("they're a big company, they handle it").
- Reality: Anthropic has SCCs in their DPA and TADPF certification — but you have to confirm + document it in your Datenschutz.

**How to avoid:**
- Verify Anthropic's TADPF certification status (check `dataprivacyframework.gov`).
- Datenschutz section: "Datenübermittlung in Drittländer" naming Anthropic, the legal basis (SCCs / TADPF), a link to Anthropic's DPF certification page, and a note on what data is sent (extracted line-item text + classification request — NOT receipt PDFs).

**Warning signs:**
- Datenschutz silent on Drittland-Übermittlung.
- Anthropic DPF certification not yet confirmed at launch.

**Phase to address:** Phase 6 (Legal + consent) — same workstream as AVV.

---

### Pitfall 5: Webhook handler that double-grants tokens

**What goes wrong:**
Stripe retries webhooks on non-2xx. If the handler ever takes > 5s OR does the token grant before checking idempotency, users get duplicate tokens.

**Why it happens:**
- Skipping the idempotency table because "Stripe retries are rare."
- Using `event.id` for logging only, not for dedup.

**How to avoid:**
- `payments` table with `(stripe_event_id UNIQUE)` constraint.
- Webhook handler: signature-verify → INSERT into `payments` → on `unique_violation` return 200 immediately → otherwise enqueue grant-tokens job → return 200.
- Token grant inside the job, not the webhook.

**Warning signs:**
- Test scenario: replay the same webhook event twice — should grant once.
- Production support ticket: "I bought 100 tokens but got 200."

**Phase to address:** Phase 5 (Commercial surface) — primary concern of payment integration.

---

### Pitfall 6: Widerrufsrecht waiver missing or hidden

**What goes wrong:**
DE consumer law gives a 14-day Widerrufsrecht for digital services purchased online. For tokens granted immediately, you need an explicit user-acknowledged waiver. Without it, users can demand their money back within 14 days even after spending the tokens.

**Why it happens:**
- Waiver added as small print at the bottom of checkout.
- Not actively confirmed; users assume Widerrufsrecht still applies.
- Stripe Checkout has no built-in DE Widerruf flow — must be added in TaxReader's pre-checkout UX.

**How to avoid:**
- Pre-checkout page (BEFORE Stripe redirect):
  1. Show plan/pack details + price + USt info.
  2. Show Widerrufsbelehrung (full text).
  3. Active checkbox: "Ich verlange ausdrücklich, dass Sie mit der Vertragsausführung beginnen…"
  4. Button to proceed to Stripe is disabled until checkbox is ticked.
- Confirmation email + invoice include Widerrufsbelehrung text.
- AGB + dedicated `/widerruf` page.

**Warning signs:**
- Checkbox is pre-ticked.
- Stripe Checkout fires before the user actively confirms waiver.
- Verbraucherzentrale-style mystery-shopper finds the flow.

**Phase to address:** Phase 5 (Commercial surface) + Phase 6 (Legal).

---

### Pitfall 7: "TaxReader" name conflicts with existing Markenrechte

**What goes wrong:**
"TaxReader" is generic-English; someone may have a DE Markeneintrag under DPMA. Launching without a search exposes to Markenstreit (cease-and-desist, costs up to €5-10k for trademark squatter actions).

**Why it happens:**
- Founder's name fixation.
- Marken search treated as nice-to-have.

**How to avoid:**
- DPMA search at `register.dpma.de` for class 9 (software) and class 42 (SaaS).
- EUIPO search at `tmdn.org`.
- If clear: register the mark proactively (€290 for one class via DPMA; €850 for EUIPO union mark) — optional but cheap insurance.
- If conflict: rename before launch.

**Warning signs:**
- DPMA/EUIPO search returns matches in classes 9 / 42.
- A C&D letter arrives within weeks of launch.

**Phase to address:** Phase 6 (Legal) — Marken search in week 1 of that phase.

---

### Pitfall 8: AI hallucinating amounts or fabricating line items

**What goes wrong:**
Anthropic models occasionally hallucinate plausible-but-wrong values when OCR is degraded. A €123.45 receipt becomes €1234.50 in the report. User trust collapses; refund + chargeback storm.

**Why it happens:**
- LLM doesn't know its own confidence on numeric extraction.
- Garbage-in (poor OCR) becomes confident-output.
- Discount rows + voucher rows confuse line-item models.

**How to avoid:**
- AI is for CLASSIFICATION, not for AMOUNT EXTRACTION. Amounts come from PdfPig / Tesseract directly. (This is already the existing architecture — keep it that way; do not let "improve accuracy" creep into letting AI re-read amounts.)
- Validation: line-item totals must sum to receipt total within €0.50 tolerance; mismatch flags the receipt as `Unverified` and surfaces to user.
- VAT-rate cross-check: 7% / 19% rates per line should sum reasonably to receipt VAT total.
- Confidence threshold below which classification is `Suggested` (not `AutoConfirmed`); user must explicitly confirm.

**Warning signs:**
- A test receipt with €0.99 + €1.99 + €2.99 + total €5.97 produces a report total ≠ €5.97.
- Customer support: "your tool said I spent €X, but my actual expenses were €Y."

**Phase to address:** Phase 4 (Classification trustworthiness) — validation rules + confidence thresholds.

---

### Pitfall 9: German number-format / Umlaut OCR failures

**What goes wrong:**
- "1.234,56 €" (German format) parsed as 1234.56 instead of 1234.56 — wait, this would be wrong; German "1.234,56" means 1234.56 (period thousands separator, comma decimal). If parser uses `decimal.Parse` with US-default it gets 1.234 + 56-as-fraction → 1.234, off by 1000x.
- Umlauts in OCR: "Bürobedarf" comes out as "Bnrobedarf" or "Burobedarf" — vendor recognition fails, classification falls through to Unknown.

**Why it happens:**
- `CultureInfo.InvariantCulture` used for parsing receipt amounts when input is German-formatted.
- Tesseract `deu` language pack not loaded (or path wrong) → English-default OCR mangles umlauts.

**How to avoid:**
- All amount parsing uses `CultureInfo.GetCultureInfo("de-DE")` for German receipts. Optionally detect format from receipt content and choose accordingly.
- Tesseract config explicit: `deu+eng` (existing). Test the deu pack is actually loaded; integration test with a known umlauts-heavy receipt should pass.
- Normalize receipt text post-OCR: NFC Unicode normalization, common confusables fixed (`ﬁ` → `fi`).

**Warning signs:**
- Production receipts where amounts are off by 1000x.
- Vendor "Müller" classified as Unknown but "Mueller" classified correctly.

**Phase to address:** Phase 4 (Classification trustworthiness). Should add an OCR/parser test corpus.

---

### Pitfall 10: Migration runs against live DB without lock-aware planning

**What goes wrong:**
EF Core migrations on a live Postgres can lock tables for minutes. Adding NOT NULL column with default → table rewrite. Adding index without `CONCURRENTLY` → AccessExclusiveLock blocks all reads/writes. Solo-dev "just deploy" turns into a Sunday-night incident.

**Why it happens:**
- Migrations tested against empty DB; on a few-thousand-row prod DB they run for minutes.
- `RUN_MIGRATIONS=true` on container start (already configured) means this happens during deploy.

**How to avoid:**
- For each migration in active development: review the generated SQL.
- NOT NULL on existing column → make it nullable first; backfill in Hangfire job; add NOT NULL in next release.
- New indexes → use `CREATE INDEX CONCURRENTLY`. EF Core 10 has explicit hooks.
- Postpone destructive migrations (DROP COLUMN) — keep columns one release; drop later.
- Maintenance window: at hundreds-of-users scale, a 30-second downtime window during off-hours is acceptable. Set expectation upfront in AGB ("Wartungsfenster: typischerweise sonntags 03:00-04:00 CET, max. 30 Minuten") — defangs complaints.

**Warning signs:**
- Migration that took 1s in dev takes 60s in stage.
- Postgres slow-query log shows lock waits during migration.

**Phase to address:** Cross-phase concern. Add a "migration safety check" step to CI in Phase 1 (compile-time verification of EF migrations + run against a populated test DB).

---

### Pitfall 11: Solo-dev paging burnout

**What goes wrong:**
Sentry / BetterStack pages on every flake. Solo dev gets paged at 3am for an Anthropic 503. Burnout in 3 weeks; product quality drops.

**Why it happens:**
- Default alert thresholds are too aggressive.
- No noise-filtering ("alert if 5 errors in 10 minutes" instead of every error).
- Anthropic transient failures surfacing as alerts.

**How to avoid:**
- Sentry alert rules: NOT every error. Alert when:
  - Error rate > N/min sustained for > 5 min.
  - New error type appears (first occurrence) but with a 1-hour cooldown.
  - Specific high-impact error tags only.
- BetterStack uptime: 2 consecutive failures (not 1) before paging.
- Anthropic 5xx → retry with backoff in Hangfire; alert only if retry budget exceeded.
- Daily digest email of error-rate trends; weekly review for solo dev — pages reserved for "site is meaningfully broken."
- "Quiet hours" 23:00-07:00: only HIGH-severity pages (DB down, all uploads failing).

**Warning signs:**
- More than 1-2 pages per week.
- The dev starts ignoring pages.

**Phase to address:** Phase 1 (Foundation cleanup + CI) — set up Sentry with conservative rules from the start. Tune in Phase 7 once real-traffic baselines exist.

---

### Pitfall 12: Receipt PDFs lingering in `storage/` directory

**What goes wrong:**
Even though the schema removed `StoragePath` (per concern #4), the API container's filesystem may still receive uploads written there during local dev. PII leaks if the container is mounted/inspected.

**Why it happens:**
- Old upload code path that wrote to disk wasn't fully removed.
- `.gitignore` not updated.

**How to avoid:**
- Code search: `grep -rE "storage/|StoragePath|FileStream|File\.WriteAll"` in `Backend/src/`. Verify no path writes receipts to disk (they should live only in memory then go to OCR + DB).
- `.gitignore` add `storage/`, `**/storage/`, `*.binlog`, `build-diag*.txt`.
- `git rm -r --cached` on existing tracked storage dirs.
- Add a CI check: `if grep -q "FileStream" backend/src/TaxReader.Api/storage/" → fail`.

**Warning signs:**
- `storage/2026/04/` exists in working tree.
- A PDF inside that directory has an actual receipt name.

**Phase to address:** Phase 1 (Foundation cleanup) — concern #3 + #4.

---

### Pitfall 13: Refresh-token replay = silent logout

**What goes wrong:**
Without a multi-row refresh-token table (concern #10), a user logged in on phone + laptop has them silently logging each other out every time one refreshes. Worse, a leaked refresh token is undetectable until the legitimate user gets randomly logged out.

**Why it happens:**
- Single-column refresh-token implementation.
- No replay detection.

**How to avoid:**
- Multi-row table (see ARCHITECTURE.md Pattern 4).
- Replay detection: if a refresh comes in for an already-rotated row, revoke ALL the user's tokens immediately and require fresh login.

**Warning signs:**
- User support: "I keep getting logged out."
- Production logs show frequent /auth/refresh denials.

**Phase to address:** Phase 2 (Auth + rate-limit hardening).

---

### Pitfall 14: Stripe live key accidentally in dev

**What goes wrong:**
A dev runs locally with `Stripe__SecretKey=sk_live_...` (env-var leakage from `.env` file). Tests trigger real charges or refunds.

**Why it happens:**
- Single `.env` file shared across environments.
- No environment-aware key validation at startup.

**How to avoid:**
- Separate env-var keys per environment: `STRIPE_SECRET_KEY_TEST` (dev/stage), `STRIPE_SECRET_KEY_LIVE` (prod).
- Code at startup: throw if `Production` and key starts with `sk_test_`; warn loudly if `Development` and key starts with `sk_live_`.
- `.env.example` only references test-mode keys.
- Documented in README: "Never put `sk_live_*` in your local `.env`."

**Warning signs:**
- Stripe dashboard shows charges with the dev hostname as metadata.
- The dev console logs `sk_live_...` somewhere.

**Phase to address:** Phase 5 (Commercial surface) — startup-time guard is part of payment integration.

---

### Pitfall 15: GoBD applicability creep

**What goes wrong:**
GoBD (Grundsätze ordnungsmäßiger Buchführung) governs how accounting records must be kept. If TaxReader is interpreted as "accounting software for receipts," GoBD applies — 10-year retention, audit-trail integrity, etc. Massive scope.

**Why it happens:**
- Marketing copy positions the product as "Buchhaltung."
- ToS implies the user can rely on TaxReader as their accounting system of record.

**How to avoid:**
- AGB explicit: "TaxReader ist kein Buchhaltungssystem im Sinne der GoBD. Originalbelege müssen vom Nutzer selbst gemäß §147 AO aufbewahrt werden. Wir speichern nur extrahierte Daten und Klassifikationen, keine Original-PDFs."
- Marketing avoids "Buchhaltung," "Belegarchiv," "GoBD-konform."
- The architectural choice to NOT store PDFs (existing — column was removed) is exactly right; preserve it.

**Warning signs:**
- Lawyer flags positioning during AGB review.
- A user asks "ist Ihre Lösung GoBD-konform?" — answer: "TaxReader ist keine Buchhaltungslösung; GoBD-Vorgaben gelten für Originalbelege, die Sie selbst aufbewahren müssen."

**Phase to address:** Phase 6 (Legal + consent) — copy review.

---

### Pitfall 16: Status page tells the truth too early

**What goes wrong:**
A 30-second blip during a deploy is visible on the public status page; users see "down" + abandon mid-purchase.

**Why it happens:**
- Sub-3-min check interval + auto-publish to status page.
- No deployment-aware muting.

**How to avoid:**
- BetterStack: 2 consecutive failures before "down."
- Coordinated deploys: schedule maintenance windows in BetterStack to suppress alerts during deploy.
- Critical endpoints only on the public status page (`/health`); internal jobs not.

**Phase to address:** Phase 7 (Test depth + launch QA).

---

### Pitfall 17: Verbraucherzentrale escalation

**What goes wrong:**
A frustrated user contacts Verbraucherzentrale → automated semi-template letter arrives → response deadline + threats of UWG action. All due to small UX/legal slip-ups (Widerruf, AGB, refund handling).

**Why it happens:**
- AGB unclear on refunds.
- Support unresponsive (>48h response).
- Widerrufsrecht confusion.

**How to avoid:**
- Support email response SLA: 48h business days. Auto-reply confirms receipt + expected response time.
- Refund policy explicit in AGB: "Tokens nicht erstattbar nach Verbrauch; nicht-verbrauchte Tokens innerhalb 14 Tagen erstattbar bei aktivem Widerruf vor Verbrauch (sofern Widerrufsrecht nicht ausgeübt verbraucht)."
- A documented disputes process (3 steps: support email → escalation to founder → external dispute resolution per VSBG).
- Track every Verbraucherzentrale-style complaint internally and respond in kind, on time.

**Phase to address:** Phase 6 (Legal + consent) + ongoing ops.

---

## Technical Debt Patterns

| Shortcut | Immediate Benefit | Long-term Cost | When Acceptable |
|----------|-------------------|----------------|-----------------|
| Skip background jobs (sync upload) | Saves 1 week | Users churn at hundreds-of-users scale; refunds; reputation | Never for paid product |
| Skip Sentry / "I'll add monitoring later" | Saves 1 day | First production crash is a black box; recovery 10x harder | Never |
| Hand-roll receipt PDF instead of Stripe Invoicing | "Custom branding" | Opens up VAT-line correctness liability | Never; use Stripe Invoicing |
| Single-row refresh tokens "we'll fix later" | Saves 2 days | Multi-device users complain; security incident risk if leaked | Never for paid product |
| Skip integration tests "in-memory is fine" | Saves test setup time | Production migration fails; cascade-delete bugs | Never for paid product |
| Use ChatGPT-generated AGB | Saves €1000 | One Abmahnung costs €5-10k + product changes | Never for DE commercial |
| Defer Anthropic AVV "until first user complaint" | Saves admin friction | DSGVO violation if a complaint happens — fines + bad press | Never |

---

## Integration Gotchas

| Integration | Common Mistake | Correct Approach |
|-------------|----------------|------------------|
| Stripe | Trusting redirect URL params | Webhook is authoritative; redirect just shows "processing" |
| Stripe | Webhook handler doing heavy work | Sig-verify + idempotent insert + enqueue → 200 |
| Stripe | Not using idempotency keys on retries | Pass `idempotencyKey: customerId-timestamp` for client-side retries |
| Anthropic | Sending receipt PDF | Send extracted text only; never the PDF |
| Anthropic | No AVV signed | Sign their DPA before processing personal data |
| Tesseract | Single Singleton with lock | Pool of engines |
| Tesseract | English-only mode on DE receipts | `deu+eng` languages, NFC normalization |
| EF Core | In-memory provider in tests | Testcontainers.PostgreSql |
| EF Core | Migration adds NOT NULL with default | Make nullable, backfill, NOT NULL in next migration |
| Hangfire | Dashboard exposed without auth | Auth filter wrapping `/hangfire` route |
| Hangfire | Trying to use ICurrentUser in jobs | Pass userId as job argument |
| Sentry | Default config sends PII | `SendDefaultPii = false`; scrub in `BeforeSend` |

---

## Performance Traps

| Trap | Symptoms | Prevention | When It Breaks |
|------|----------|------------|----------------|
| Tesseract Singleton + lock | Image-receipt uploads serialize globally | Engine pool (3-5) + background jobs | ~10 concurrent users uploading images |
| Sync HTTP request through full pipeline | 30+ second requests, browser timeouts | Background-job pipeline | Multi-receipt upload, mobile network |
| EF SaveChanges per item in classify loop | 1000 separate INSERTs | Batch-save outside the loop | ~50 items per batch |
| Hangfire dashboard polling SignalR aggressively | High CPU at low traffic | Limit dashboard idle clients | Solo dev leaves dashboard tab open |
| Anthropic call inside DB transaction | Holds connection 30s; pool exhaustion | Anthropic call OUTSIDE the txn; persist results in a separate save | ~20 concurrent uploads |
| Per-request DbContext lifetime > 30s | Connection-pool starvation | Background-job pattern (DbContext per job, not request) | Already a problem |

---

## Security Mistakes

| Mistake | Risk | Prevention |
|---------|------|------------|
| Stripe webhook secret in `appsettings.json` | Secret leak via git history | Env var only; rotate if leaked |
| JWT_SECRET in `appsettings.Development.json` | Local secret leaks via accidental commit | Env var only; `appsettings.Development.json` references env var |
| Hangfire dashboard public | Enumerate jobs / trigger jobs | Auth filter |
| Stripe API key reused dev/prod | Real charges in dev | Separate keys + startup-time guard |
| User token grant via redirect URL params | Client-fabricated grants | Webhook is authoritative |
| Refresh token in localStorage (browser) | XSS exfiltration | httpOnly cookie or in-memory + refresh-on-401 (existing pattern is OK if cookies are httpOnly) |
| `[ApiController]` errors leaking exception text | Internal info disclosure | Map known exceptions to safe German strings (concern #12) |
| Audit log not protected from user write | User can edit own audit | Audit log writes are server-only; no user-routed endpoint |
| Account-deletion CSRF | Attacker can wipe a session-hijacked user | Re-auth confirmation required (already planned) |

---

## UX Pitfalls

| Pitfall | User Impact | Better Approach |
|---------|-------------|-----------------|
| "Loading..." with no progress for 30s | Users assume crash, refresh, lose state | Background-job + status: "Wird verarbeitet (3/10)" |
| Empty list with no copy | Confusion ("Did upload work?") | "Noch keine Belege hochgeladen — laden Sie Ihren ersten Beleg hoch" + button |
| Toast errors that disappear in 3s | Users miss the error | Persistent error banner for upload/payment failures |
| Auto-confirm at high threshold without indicator | Users don't notice AI made decisions for them | Visible badge: "AI-bestätigt — überprüfen?" |
| Hidden price until checkout | Users feel tricked | Pricing page accessible from landing |
| Endless category dropdown | Users pick wrong category | Searchable + recent / common at top |
| Auto-translate "Eduki" to "Eduki" but a category to English elsewhere | Inconsistent language = looks broken | Strict DE-only audit |
| German formal Sie vs informal du inconsistent | Brand-voice incoherence | Pick one (recommend Sie for tax product), audit everywhere |

---

## "Looks Done But Isn't" Checklist

- [ ] **Working payment**: Verify with a real Stripe test-mode E2E flow that webhook fires, payments table inserts, tokens granted; replay the same event to verify idempotency.
- [ ] **DE legal pages**: A lawyer has reviewed AGB + Datenschutz; AVVs from Stripe / Anthropic / Sentry / BetterStack are signed.
- [ ] **Background-job upload**: Killing the API container mid-upload — does the job survive and complete on restart?
- [ ] **Tesseract pool**: 10 concurrent image-receipt uploads from same user — does the pool block correctly without serializing?
- [ ] **Rate limiting**: Brute-forcing `/auth/login` from one IP is rate-limited but doesn't lock out the legitimate user.
- [ ] **Refresh-token rotation**: Test from two devices simultaneously — both stay logged in across refreshes.
- [ ] **Refresh-token replay**: Simulate a leaked token used after rotation — all user tokens revoked.
- [ ] **CI gates**: A PR with failing tests cannot be merged.
- [ ] **DSGVO data export**: User triggers export → email arrives → JSON contains all their data.
- [ ] **Account deletion**: User triggers delete → all rows cascade → user cannot re-login → audit log records the event.
- [ ] **Cookie banner**: First visit shows banner; "Reject" works; revoke option in footer.
- [ ] **Widerrufsrecht waiver**: Cannot purchase without active waiver checkbox.
- [ ] **Status page**: Live + linked from footer + auto-incident on real outage.
- [ ] **Sentry**: Real error in non-prod surfaces in Sentry; PII not present.
- [ ] **Migration safety**: Run migration against a populated test DB — completes in < 30s.
- [ ] **Hangfire dashboard**: Anonymous request returns 401, not 200.
- [ ] **Stripe live key in dev**: Startup-time guard prevents app from starting in dev with `sk_live_*`.
- [ ] **Anthropic prompt logging**: Verify what Anthropic logs of our prompts; ensure DSGVO disclosure mentions it.
- [ ] **`storage/` cleaned**: No PDFs on disk; `.gitignore` updated; CI check fails if reintroduced.
- [ ] **All user-facing copy in German**: Native-speaker review.

---

## Recovery Strategies

| Pitfall | Recovery Cost | Recovery Steps |
|---------|---------------|----------------|
| Webhook double-grants tokens | LOW | 1. Identify duplicate grant rows. 2. Manually issue compensating debit. 3. Add unique constraint + redeploy. 4. Apologize to user. |
| Stripe live key leaked | MEDIUM | 1. Rotate immediately in Stripe dashboard. 2. Audit for unauthorized charges. 3. Refund affected users. 4. Public incident note. |
| Migration hangs in production | HIGH | 1. Cancel migration if possible. 2. Roll back container. 3. Hand-roll the DDL with `CONCURRENTLY` / batched. 4. Re-deploy. |
| StBerG complaint received | MEDIUM-HIGH | 1. Pause matching marketing copy. 2. Lawyer review of complaint specifics. 3. Targeted product changes. 4. Written response within deadline. |
| DSGVO complaint to LfDI | MEDIUM | 1. Within 30 days (DSGVO requirement) provide all requested info. 2. Internal review of root cause. 3. Update Datenschutz if needed. |
| Anthropic outage | LOW (op) | 1. Hangfire retries with backoff. 2. If sustained > 30 min: status page note "Klassifikation verzögert". 3. Receipts queue normally; user sees "Wird verarbeitet". |
| Tesseract crash on a malformed PDF | LOW | 1. Try-catch around extract; mark file `Failed`. 2. User-friendly error. 3. Don't deduct tokens for extraction failures. |
| Refresh-token replay detected | LOW | 1. Revoke all user tokens (already automatic). 2. Email user about possible compromise. 3. Force password reset on next login. |

---

## Pitfall-to-Phase Mapping

| Pitfall | Prevention Phase | Verification |
|---------|------------------|--------------|
| StBerG line-crossing | Phase 6 (Legal) | Lawyer reviews AGB + product copy; no "Steuertipps" features |
| DSGVO Art. 22 disclosure | Phase 4 (Class. trustworthy) + Phase 6 | Datenschutz section present; AI reasoning visible in UI |
| Anthropic AVV missing | Phase 6 (Legal) | DPA signed copy in records before launch |
| Schrems II / TADPF | Phase 6 (Legal) | Datenschutz Drittland-Übermittlung section |
| Webhook double-grant | Phase 5 (Commercial) | Replay test passes; unique constraint in place |
| Widerrufsrecht waiver hidden | Phase 5 + Phase 6 | E2E test: cannot purchase without active checkbox |
| Markenrechte conflict | Phase 6 (Legal) | DPMA + EUIPO search complete |
| AI hallucinating amounts | Phase 4 (Class. trustworthy) | Sum-validation tests pass with €0.50 tolerance |
| German number/Umlaut OCR | Phase 4 (Class. trustworthy) | Test corpus with German receipts passes |
| Live migration breakage | Phase 1 (Foundation + CI) | CI runs migrations against populated test DB |
| Solo-dev paging burnout | Phase 1 (Sentry) + Phase 7 (tune) | Conservative alert rules; review weekly |
| `storage/` PII leak | Phase 1 (Foundation cleanup) | grep + .gitignore + CI check |
| Refresh-token replay | Phase 2 (Auth hardening) | Replay test triggers full revocation |
| Stripe live in dev | Phase 5 (Commercial) | Startup guard test |
| GoBD scope creep | Phase 6 (Legal) | AGB explicit; marketing copy review |
| Status page over-sensitive | Phase 7 (Launch QA) | 2-failure threshold; deploy maintenance window |
| Verbraucherzentrale escalation | Phase 6 (Legal) + ongoing | SLA in AGB; tracked complaints log |

---

## Sources

- StBerG (Steuerberatungsgesetz) §1, §5
- DSGVO Art. 6, 13, 14, 20, 22, 28
- TADPF (Trans-Atlantic Data Privacy Framework) at `dataprivacyframework.gov`
- BGB §312g + EGBGB Art. 246a (Widerrufsrecht for digital services)
- §14 UStG (invoice content), §19 UStG (Kleinunternehmer)
- §147 AO (Aufbewahrungspflicht for tax-relevant docs)
- GoBD Schreiben des BMF (Bundesministerium der Finanzen)
- Stripe webhook best practices, idempotency docs
- Anthropic Trust Center / DPA terms
- DPMA / EUIPO trademark databases
- TMG §5 (Impressumspflicht)
- TTDSG §25 (Cookie consent)
- Existing project: `.planning/codebase/CONCERNS.md` (20 codebase concerns — extends here with launch-specific risks)

---
*Pitfalls research for: TaxReader hardening milestone (DE commercial launch)*
*Researched: 2026-05-03*
