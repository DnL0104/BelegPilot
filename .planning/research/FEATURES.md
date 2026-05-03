# Feature Research

**Domain:** DE B2C tax-receipt SaaS — feature surface for commercial-launch hardening
**Researched:** 2026-05-03
**Confidence:** HIGH for legal-pages requirements (regulation-driven); MEDIUM-HIGH for UX patterns (industry norms)

> **Scope note:** This document focuses on what's MISSING to make the existing TaxReader credible as a paid commercial product in Germany. Features already implemented (upload, OCR, AI classification, manual override, PDF/CSV export, JWT auth, account deletion, token economy) are NOT re-listed. See `.planning/PROJECT.md` Validated section.

---

## Feature Landscape

### Table Stakes — Users Expect These

Missing these means the product looks unfinished or untrustworthy.

| Feature | Why Expected | Complexity | Notes |
|---------|--------------|------------|-------|
| **Working token purchase that charges money** | Currently a stub (concern #7); without it there's no commercial product | HIGH | Stripe Checkout + webhook → token grant |
| **DE-compliant invoice (Rechnung) per purchase** | §14 UStG; users with employer reimbursement need it; Steuerberater expects it | MEDIUM | Stripe Invoicing handles most; need to ensure all required fields (vendor, USt-ID, invoice number, etc.) |
| **Impressum** | TMG §5; failing to display = Wettbewerbszentrale Abmahnung within weeks of launch | LOW | Static page; must include name, address, contact, USt-ID if applicable |
| **Datenschutzerklärung** | DSGVO Art. 13 + 14; required to explain Anthropic processing, cookies, payment data | MEDIUM | Recommend lawyer review (~€500-1500); cannot copy-paste a generic template |
| **AGB (Allgemeine Geschäftsbedingungen)** | BGB §305 ff; required for trust + Widerrufsrecht clarity | MEDIUM | Lawyer review essential; must be presented before purchase, with active opt-in |
| **Widerrufsbelehrung** | BGB §312g + EGBGB Art. 246a §1; 14-day right of withdrawal for digital services unless waived | LOW | Standard pattern: present before purchase, user actively waives Widerrufsrecht for immediate token grant |
| **Cookie-/Consent banner (TTDSG-compliant)** | TTDSG §25; non-essential cookies require active consent; reject = first-class option | MEDIUM | Use a small library (e.g. Klaro, CookieConsent v3) — don't hand-roll |
| **DE-localized UI for everything user-visible** | Audience is DE-only; English copy = "diese Software ist nicht für mich" | MEDIUM | i18n via `next-intl` or hand-roll string keys; the existing PDF export is already DE-localized |
| **EUR-formatted numbers (1.234,56 €)** | Wrong format = looks like a US-imported tool, low trust | LOW | `Intl.NumberFormat('de-DE', { style: 'currency', currency: 'EUR' })` — verify across all numeric displays |
| **Self-serve invoice download** | Users want to expense token purchases; lookup-then-email-support is friction | LOW | Stripe Customer Portal handles for free, or list + download from settings |
| **Self-serve token-balance + history** | Users need to know what they've spent and on what | LOW | Already exists technically; surface as Account → Tokens page |
| **Password change** | Standard expectation; security-conscious users will look for it | LOW | Existing auth covers; just needs a UI form |
| **Self-serve data export (DSGVO Art. 20)** | Right to data portability — must be available, not just on request | MEDIUM | JSON/CSV bundle of receipts + classifications + ledger; trigger from settings; email a download link |
| **Account deletion confirmation flow** | Already implemented but needs friction (re-auth prompt + irreversibility warning) | LOW | Add password-confirmation modal before `/auth/account` DELETE call |
| **Useful error messages on upload failure** | Currently leaks internal exception text (concern #12); paying users want German "Verarbeitung fehlgeschlagen — versuchen Sie es erneut oder kontaktieren Sie support@" | LOW | Map known error types to German strings; log technical details server-side |
| **Background-processed uploads with status visibility** | 35-second blocked HTTP requests are unacceptable for paid users | HIGH | Tied to architecture — see ARCHITECTURE.md background-job pattern |
| **Empty / loading / error states for every screen** | Polish gap; "white-screen-of-thinking" is a paid-product red flag | MEDIUM | Audit every page: receipts list, dashboard, reports, upload, settings |
| **Mobile-responsive layout** | DE users do tax stuff on phones; current Tailwind base helps but needs audit | LOW-MEDIUM | shadcn / Tailwind already responsive-friendly; QA pass at sm/md breakpoints |
| **Support contact (email + reasonable response promise)** | Verbraucherzentrale reads ToS for "where do I complain"; missing = trust collapse | LOW | Email address (not contact form), response promise in AGB, monitored inbox |
| **Status page (linked from footer)** | When something is broken users want to confirm it's not just them | LOW | Free with BetterStack Uptime |

### Differentiators — Competitive Advantage

These are where this product can win against the obvious competitors (smartsteuer, WISO, Taxfix, lohnsteuer.de Klein-Tools).

| Feature | Value Proposition | Complexity | Notes |
|---------|-------------------|------------|-------|
| **AI classification with auditable reasoning** | "Why did the AI pick this category?" answer is a real product moat | already built | Surface the reasoning prominently — it's currently buried |
| **Bulk re-classify by rule** | When user corrects "all Eduki orders → SpecialistLiterature," persist as a rule for future receipts | HIGH | Wire up the existing `ClassificationRule` entity (concern #16); explicit "create rule from this" UX beats opaque auto-promotion |
| **Plain-language category descriptions matched to Anlagen** | Most users don't know which category maps to which form line; we tell them | LOW | One-line per-category description plus "geht in Anlage N, Zeile 41" hint |
| **PDF export specifically formatted for handing to Steuerberater** | Most tools dump CSVs; an advisor-ready PDF (logo-less, neutral, machine-readable section + summary) is a real win | MEDIUM | QuestPDF already in stack; design the layout |
| **Receipt-by-receipt audit trail** | "Show me every classification I overrode this year" — for advisor + for own confidence | LOW-MEDIUM | Existing `ItemClassification` history is data-rich; just needs a UI |
| **Year-over-year trend by category** | Helps users see whether they're documenting more or less than last year — drives retention | MEDIUM | Pure read-side query; existing aggregation extends naturally |
| **No ads, no tracking, EU-hosted-everything** | Marketing posture for privacy-conscious DE buyers | LOW (pos.) | Be explicit on landing page: "Keine Werbung, kein Tracking, deutsche Server" |

### Anti-Features — Deliberately Not Built

These will be requested. Document the reasoning to push back.

| Feature | Why Requested | Why Problematic | Alternative |
|---------|---------------|-----------------|-------------|
| **"What can I claim?" recommendations** | Users want a tax tool to tell them | Crosses StBerG line into Steuerberatung; opens regulatory risk | Plain-language category descriptions ("hier wird typischerweise X erfasst") with explicit "dies ist keine Steuerberatung" disclaimer |
| **Auto-categorize VAT (Umsatzsteuer) for self-employed** | Self-employed users will ask | Vorsteuer rules are non-trivial and getting them wrong is a real liability | Out of scope for v1; show gross/net but don't infer Vorsteuer-Anteil |
| **ELSTER auto-submission** | Saves transcription | Requires ERiC certification + audit; ~6 month separate product | Out of scope; PDF/CSV summary is the bridge |
| **Bank account import / DATEV sync** | "I want it to be automatic" | Adds GDPR/financial-data scope, FinTS/HBCI complexity | Already excluded in PROJECT.md |
| **Mobile native apps** | "I need an app" | Solo dev cannot maintain three platforms in 3 months | Mobile web is responsive; PWA add-to-home-screen later |
| **Tax-advice chat / "ask the AI" feature** | Engagement-y | StBerG land-mine even if disclaimed | Anti-feature — explicitly excluded |
| **Multi-user accounts / shared workspaces** | "My partner and I file jointly" | Scope explosion (permissions, audit, shared billing) | Couples use one account — documented in onboarding copy |
| **Real-time collaboration** | Borrowed from other SaaS | Single-user product, no value | Anti-feature — never |
| **Public sharing of reports** | Borrowed from analytics SaaS | PII risk | Anti-feature — never |
| **AI-generated tax tips emails** | "Engagement" | StBerG + DSGVO marketing-consent risk | Anti-feature; transactional emails only |
| **Free unlimited tier** | "Lower the bar" | Anthropic costs scale with usage; abuse risk | 10-token welcome credit (existing) is the trial |

---

## Tax-Category Coverage for "Anyone DE"

The existing teacher-focused categories (`ConsumablesAndOfficeSupplies`, `SpecialistLiterature`, `Unknown`) are insufficient. Below is a realistic minimum for "credible across DE private taxpayers." Each maps to an Anlage line.

### Werbungskosten (Anlage N — most teachers, employees)

| Category | Anlage N line | Examples |
|----------|---------------|----------|
| Arbeitsmittel (≤ €952 brutto, sofort absetzbar 2024+) | Z. 42-43 | Laptop, software, books, office supplies |
| Fachliteratur | Z. 42 | Specialist books, journals, online courses |
| Büromaterial | Z. 42 | Pens, paper, printer ink |
| Reisekosten | Z. 49-57 | Travel for work (excl. commute) |
| Fortbildungskosten | Z. 44-48 | Continuing education, certifications |
| Bewerbungskosten | Z. 46 | Job search expenses |
| Telekommunikation (anteilig) | Z. 42 | Phone/internet share for work use |
| Häusliches Arbeitszimmer | Z. 43 | Home office (limited rules apply) |

### Sonderausgaben (Anlage Sonderausgaben)

| Category | Examples |
|----------|----------|
| Vorsorgeaufwendungen (Krankenversicherung etc.) | Health insurance, pension contributions — typically pre-filled by employer, but receipts may be private supplements |
| Spenden / Mitgliedsbeiträge | Donations, eligible memberships (excludes politische Parteien which has separate handling) |
| Kirchensteuer | Church tax (usually pre-filled) |
| Schulgeld | Private-school tuition share |

### Außergewöhnliche Belastungen (Anlage Außergewöhnliche Belastungen)

| Category | Examples |
|----------|----------|
| Krankheitskosten (private Anteil) | Co-pays, prescriptions, dental, glasses, medical aids |
| Pflegekosten | Care expenses for self or dependent |
| Unterhaltsleistungen | Maintenance to dependents (separate form Anlage U) |

### Haushaltsnahe Dienstleistungen + Handwerkerleistungen (Anlage Haushaltsnahe Aufwendungen)

| Category | Examples |
|----------|----------|
| Haushaltsnahe Dienstleistungen | Cleaning, gardening (must be Rechnung + bank transfer; cash out) |
| Handwerkerleistungen | Craftsperson services in own home (also Rechnung + transfer) |
| Kinderbetreuungskosten (Anlage Kind) | Daycare, after-school care |

### Anlage S / Anlage EÜR (Self-employed — explicitly NOT in v1 scope)

Out of scope for v1 per PROJECT.md (focus on Werbungskosten / Sonderausgaben / agB / Haushaltsnahe). Self-employed scope = future milestone.

### Recommended v1 Category Set

Minimum credible set for "Anyone DE" launch:

```
WerbungskostenArbeitsmittel
WerbungskostenFachliteratur
WerbungskostenBueromaterial
WerbungskostenReisekosten
WerbungskostenFortbildung
WerbungskostenTelekommunikation
SonderausgabenSpenden
SonderausgabenVorsorgeaufwendungen
AussergewoehnlicheBelastungenKrankheit
HaushaltsnaheDienstleistung
Handwerkerleistung
Privat (nicht steuerlich relevant)
Unbekannt (User must classify)
```

13 categories — broad enough to cover most receipts, narrow enough that AI + rules can pick reliably. Existing two enum values become subcategories under WerbungskostenArbeitsmittel and WerbungskostenFachliteratur respectively.

---

## DE Legal Pages — Specific Requirements

### Impressum (TMG §5)

Required information (no negotiation):
- Name and address (no PO box for sole proprietors)
- Contact (email; phone optional but recommended for trust)
- USt-ID if applicable
- Trade register / Handelsregister info if GmbH/UG
- Responsible-person line for journalistic content (irrelevant here)
- Online dispute resolution link: `https://ec.europa.eu/consumers/odr/`

Footer link "Impressum" must be reachable from every page in ≤ 2 clicks.

### Datenschutzerklärung (DSGVO Art. 13)

Must cover:
- Identity of controller (matches Impressum)
- Purpose of processing
- Legal basis (Art. 6(1) — typically (b) contract performance for paid users, (a) consent for analytics)
- Categories of data
- Recipients / sub-processors — **explicitly list Anthropic + Stripe + Sentry + BetterStack + any analytics**
- Storage duration
- User rights (access, rectification, erasure, portability, withdrawal of consent, complaint to supervisory authority)
- Whether data is required for the contract
- Existence of automated decision-making (Art. 22) — TaxReader's AI classification IS automated decision-making; must disclose plus offer human override (which exists ✓)
- Cookie / consent banner state and how to revoke

Lawyer review: non-negotiable. Templates as a starting point are fine; copy-pasting verbatim is not.

### AGB

Required content patterns:
- Vertragsgegenstand (what is sold — token packs)
- Preise inkl. USt (or Kleinunternehmer note)
- Bezahlung (payment methods)
- Vertragslaufzeit / Kündigung (no subscription auto-renew without 1-month notice; if pay-as-you-go, "no subscription, tokens don't expire")
- Widerrufsrecht (if waiver applies for immediate digital delivery)
- Haftungsausschluss for AI classification accuracy (must be carefully worded — broad disclaimers void under §307 BGB)
- Streitbeilegung (alternative dispute resolution — required signpost even if not signed up)

### Widerrufsbelehrung

Standard pattern for digital service with immediate execution:
1. User shown widerrufsbelehrung text before purchase
2. Active checkbox: "Ich verlange ausdrücklich, dass Sie mit der Vertragsausführung beginnen, und bestätige meine Kenntnis darüber, dass ich mein Widerrufsrecht durch Beginn der Ausführung verliere."
3. Without this checkbox active = no purchase, full €0 charge
4. With it active = tokens granted immediately, Widerrufsrecht waived

### TTDSG-compliant cookie banner

- Two equally prominent options: "Alle akzeptieren" + "Nur notwendige" (or "Ablehnen")
- No pre-ticked checkboxes
- Settings reachable to revoke
- Essential (auth session) cookies don't need consent; Sentry/Sentry-PII/analytics do

---

## Feature Dependencies

```
Working payment integration
    └──requires──> Refresh-token table (race-condition safety on grant)
    └──requires──> Background-job pipeline (webhook handler decouples from HTTP)
    └──enables──> Self-serve invoice download
    └──enables──> Stripe Customer Portal embed

Background-job pipeline
    └──requires──> Hangfire installed + dashboard auth-gated
    └──enables──> Tesseract pool (off-request OCR)
    └──enables──> Useful upload status (poll job state)
    └──enables──> Cancellation UX

Rule + AI hybrid classification
    └──requires──> Existing ClassificationRule entity wired up
    └──enables──> Bulk re-classify by rule
    └──enables──> Plain-language category descriptions (rules document expected matches)

Self-serve data export
    └──requires──> Background-job pipeline (export is async)

GDPR Datenschutzerklärung
    └──requires──> Sub-processor list final (Anthropic, Stripe, Sentry, BetterStack)
    └──requires──> Anthropic AVV signed
```

---

## MVP Definition

### Launch With (v1) — Hardening Milestone

Must be in by tax season. Each item maps to PROJECT.md Active requirements.

- [ ] Working Stripe checkout + webhook → token grant
- [ ] DE-compliant invoice on every purchase (Stripe Invoicing)
- [ ] All 4 DE legal pages (Impressum, Datenschutz, AGB, Widerrufsbelehrung)
- [ ] TTDSG-compliant cookie banner
- [ ] Background-job upload pipeline (202 Accepted + polling)
- [ ] Tesseract pool (3-5 engine instances)
- [ ] Rate limiting on `/auth/*` and `/receipt-files`
- [ ] Refresh-token table (replacing single column)
- [ ] CI/CD with build + test + lint gates
- [ ] Frontend test suite (Vitest unit + Playwright happy path)
- [ ] PostgreSQL integration tests (Testcontainers)
- [ ] Sentry error tracking + paging
- [ ] BetterStack uptime monitoring + status page
- [ ] DE category set expansion (Werbungskosten, Sonderausgaben, agB, Haushaltsnah, Handwerker)
- [ ] Rule + AI hybrid classification (basic — wire ClassificationRule)
- [ ] Self-serve data export (DSGVO Art. 20)
- [ ] Account deletion re-auth confirmation
- [ ] User-friendly upload error messages (no exception leakage)
- [ ] Useful empty/loading/error states across all pages
- [ ] CORS production policy lock-down
- [ ] Anthropic model alignment between code + compose
- [ ] Removed leaked `storage/` + `build-diag.txt`; added to `.gitignore`
- [ ] German localization audit (every user-facing string)
- [ ] Status page link in footer + support email

### Add After Validation (v1.x)

Wait for first 50 paying users to surface what's missing.

- [ ] Bulk re-classify by rule (UI surfacing of existing rule engine)
- [ ] Year-over-year trends in dashboard
- [ ] Reclassification history per user (audit trail UI)
- [ ] Plain-language category descriptions with Anlage-line hints
- [ ] Mobile-responsive QA pass + PWA manifest

### Future Consideration (v2+)

- [ ] Self-employed mode (Anlage S / EÜR / Vorsteuer logic)
- [ ] DATEV / Lexware export format
- [ ] ELSTER ERiC integration (separate certified product)
- [ ] Mollie alongside Stripe for DE-only payment methods
- [ ] Auto-promotion of N-corrections-into-rule

---

## Feature Prioritization Matrix

| Feature | User Value | Implementation Cost | Priority |
|---------|------------|---------------------|----------|
| Working payment | HIGH | HIGH | P1 |
| DE legal pages | HIGH (legal mandate) | MEDIUM | P1 |
| Background-job uploads | HIGH | HIGH | P1 |
| Sentry + paging | MEDIUM (ops) | LOW | P1 |
| CI/CD | MEDIUM (dev) | MEDIUM | P1 |
| DE category expansion | HIGH | MEDIUM | P1 |
| Rule + AI hybrid (basic) | HIGH (Core Value) | MEDIUM | P1 |
| Self-serve data export | HIGH (legal mandate) | LOW | P1 |
| Frontend tests | MEDIUM (dev) | MEDIUM | P1 |
| Refresh-token table | MEDIUM (sec) | LOW-MEDIUM | P1 |
| Rate limiting | MEDIUM (sec) | LOW | P1 |
| Empty/loading/error states | MEDIUM (polish) | MEDIUM | P1 |
| German localization audit | HIGH | LOW-MEDIUM | P1 |
| Status page | LOW (trust) | LOW | P1 |
| Year-over-year trends | MEDIUM | LOW | P2 |
| Bulk re-classify UI | HIGH | MEDIUM | P2 |
| Plain-language category hints | MEDIUM | LOW | P2 |
| Self-employed mode | LOW (v1 audience) | HIGH | P3 |
| DATEV export | MEDIUM (advisor users) | MEDIUM | P3 |
| ELSTER ERiC | HIGH | VERY HIGH | P3 (separate product) |

---

## Competitor Reference Points

| Feature | smartsteuer | WISO | Taxfix | Our Approach |
|---------|-------------|------|--------|--------------|
| Receipt OCR + AI categorization | Limited (forms-driven) | Limited (forms-driven) | Photo-based, smart classification | Our core — should be best-in-class |
| Pricing model | One-time per Steuerjahr | One-time per Steuerjahr | Pay-on-submit | Token packs (pay-per-use, low commitment) |
| Onboarding | Form-by-form wizard | Form-by-form wizard | Conversational | Just-upload-receipts (lowest friction) |
| Tax advice / chat | Yes (separate Berater service) | Limited | Helper interactions | None — Helfer not Berater |
| ELSTER submission | Yes (ERiC-certified) | Yes (ERiC-certified) | Yes | None — manual transcription bridge |
| Privacy posture | Standard DE SaaS | Standard DE SaaS | Standard | Differentiator: no ads, no tracking, EU-only |

We are not competing with smartsteuer/WISO on full-form-filling; we are the receipt-aggregation layer that complements them. Position as such in copy.

---

## Sources

- TMG §5 (Telemediengesetz) — Impressum requirements
- DSGVO Art. 13 + 14 + 22 — Information obligations + automated decision-making
- BGB §305-310 — AGB requirements
- BGB §312g + EGBGB Art. 246a — Widerrufsrecht for digital services
- TTDSG §25 — Cookie consent
- §14 UStG — Invoice content requirements
- §19 UStG — Kleinunternehmer status
- Existing PROJECT.md, codebase ARCHITECTURE.md, CONCERNS.md
- Industry observation of smartsteuer / WISO Steuer / Taxfix product surfaces

---
*Feature research for: TaxReader hardening milestone (DE commercial launch)*
*Researched: 2026-05-03*
