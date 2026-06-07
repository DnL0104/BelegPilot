# Human UAT — Manual-Only Verification Items

**Phase:** 07-test-depth-launch-qa
**Purpose:** These items cannot be automated in CI. Each requires human judgment, a physical
device, or an external counterparty. Work through all rows before setting the go/no-go decision.

---

## Manual-Only Verifications

| Behavior | Requirement | Blocking? | Instructions |
|----------|-------------|-----------|--------------|
| **Native-speaker DE polish review** — all user-facing copy reviewed by a German native speaker for `Sie`-form consistency, natural phrasing, and absence of Denglisch. | QA-04 / D-07 | **No** (non-blocking per D-06) | Arrange a one-time review pass at launch. Reviewer reads every page: Dashboard, Upload, Receipts list, Receipt detail, Reports, all four legal pages, cookie banner, error toasts. Note findings. Reviewer sign-off: `Name: ___ Date: ___` |
| **Mobile phone-camera photo-receipt upload** — upload a photographed (not scanned) receipt from a real phone at `sm` (640 px) and `md` (768 px) viewports; confirm the upload, extraction, classification, and confirm flow complete end-to-end. | QA-05 | **No** (non-blocking per D-06; automated Playwright viewport smoke covers layout at sm/md — camera is manual) | On a real Android or iOS device, navigate to the upload page. Use the camera to photograph a German receipt. Upload the photo (JPG/WEBP). Verify: (1) upload succeeds; (2) text extraction runs (OCR path — Tesseract); (3) at least one item is classified; (4) classification confirm works. Note device model + OS version. Result: `Pass / Fail — Device: ___` |
| **Lawyer sign-off on AGB + Datenschutzerklärung** — a qualified German Rechtsanwalt reviews and approves all four legal pages; draft markers removed after approval. | QA-07 / LEG-02/LEG-03 | **YES (D-05 hard blocker)** | 1. Fill all `[bracketed]` placeholders in the four legal pages (Impressum, Datenschutz, AGB, Widerruf) with real operator data — CI guard must be green first. 2. Send all four pages to the lawyer for review. 3. Incorporate lawyer feedback. 4. Update `06-LEGAL-REVIEW.md` status to **Lawyer-reviewed** for each page. 5. Remove `<DraftWarning />` from each page file. 6. Build must pass. Sign-off: `Lawyer: ___ Date: ___` |
| **AVV/DPA signing — all four sub-processors** (Anthropic, Stripe, Sentry, BetterStack) — DSGVO Art. 28 Auftragsverarbeitungsverträge signed with every sub-processor. | QA-07 / LEG-06 / D-05 | **YES (D-05 hard blocker)** | Follow the operator instructions in `06-AVV-TRACKING.md`: (1) Accept/sign each DPA at the listed URL; (2) file a copy; (3) verify each DPA URL matches the sub-processor link in `datenschutz/page.tsx`; (4) mark "Signed" column `✓ YYYY-MM-DD` and "Link in Datenschutz" column `✓` for all four rows. AVVs: Anthropic (`anthropic.com/legal/dpa`), Stripe (`stripe.com/de/legal/dpa`), Sentry (`sentry.io/legal/dpa/`), BetterStack (`betterstack.com/privacy`). |
| **Legal placeholder replacement + CI guard green** — all `[Name]`, `[Anschrift]`, `[PLZ Ort]`, `[kontakt@taxreader.de]` tokens replaced with real legal-entity data; `hygiene-check` CI job passes. | CR-04 / D-05 | **YES (D-05 hard blocker — prerequisite for lawyer review)** | Replace every `[bracketed]` token in `Frontend/src/app/(legal)/impressum/page.tsx`, `datenschutz/page.tsx`, `agb/page.tsx`, `widerruf/page.tsx`. Verify locally: `grep -rn '\[' Frontend/src/app/\(legal\)/` returns no output. Push to `main` and confirm the `hygiene-check` CI job is green. |
| **BetterStack keyword monitors live and reporting Up** — both `/health` and `/api/v1/health` have active keyword monitors asserting `"healthy"`. | OBS-03 / QA-06 / D-08 | **Yes (ops gate — required before go-live)** | Follow `07-OPS-SETUP.md` Section 1. Confirm BetterStack dashboard shows both monitors as **Up** with keyword check configured. |
| **Sentry quiet-hours alert rule configured** — 23:00-07:00 Europe/Berlin, HIGH-severity pages only, channel email + push. | QA-06 / D-08 | **Yes (ops gate — required before go-live)** | Follow `07-OPS-SETUP.md` Section 2. Confirm Sentry → Alerts → Alert rules shows the rule active with the correct time window. |

---

## Status Summary

| # | Item | Blocking? | Status | Sign-Off |
|---|------|-----------|--------|---------|
| 1 | Native-speaker DE review | No (D-06) | PENDING | |
| 2 | Phone-camera upload (QA-05) | No | PENDING | |
| 3 | Lawyer sign-off — AGB + Datenschutz | YES (D-05) | PENDING | |
| 4 | AVV/DPA signing — all four | YES (D-05) | PENDING | |
| 5 | Legal placeholders filled + CI guard green | YES (D-05 prereq) | PENDING | |
| 6 | BetterStack keyword monitors live | Yes (ops gate) | PENDING | |
| 7 | Sentry quiet-hours rule | Yes (ops gate) | PENDING | |

---

## How to close this document

1. Work through each row above top to bottom.
2. Update the Status Summary when each item is complete.
3. Once all D-05 (blocking) rows show **DONE**, update `07-GO-NO-GO.md` D-05 section.
4. Non-blocking rows may remain PENDING at launch but must be tracked.

---

_Authored: Phase 7 Plan 07 (07-07)_
_Requirements: QA-04 / QA-05 / QA-06 / QA-07 / OBS-03_
_Last updated: 2026-06-07_
