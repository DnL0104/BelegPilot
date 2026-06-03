# Legal Page Review Gate

This document tracks the lawyer-review status of all four mandatory legal pages for TaxReader's commercial DE launch.

## Review Process

The amber "⚠ Entwurf – anwaltliche Prüfung ausstehend" draft marker is shown on each page until that page's row in the table below reaches **Lawyer-reviewed** status. Removing the draft marker from a page requires updating this document's status for that row and removing the `<DraftWarning />` component from the corresponding page file.

Final sign-off is a blocking gate for Phase 7 (QA-07). No legal page may be shown to users without the draft marker until a lawyer has reviewed it and the status is updated here.

## Status Flow

`Drafted → Lawyer-reviewed → Live`

- **Drafted**: Draft copy exists in the codebase with amber draft marker visible. No legal review has been conducted.
- **Lawyer-reviewed**: A qualified German lawyer or Rechtsanwalt has reviewed and approved the page content. The draft marker may be removed after this status is set.
- **Live**: Draft marker removed; page is live in production as reviewed copy.

## Review Tracking

| Page | Status | Lawyer | Notes |
|------|--------|--------|-------|
| Impressum | Drafted | — | Placeholders [Name], [Anschrift], [PLZ Ort], [kontakt@taxreader.de] must be filled with real operator data before review. §19 UStG Kleinunternehmer note confirmed, no USt-IdNr. |
| Datenschutzerklärung | Drafted | — | Sub-processor table (Anthropic, Stripe, Sentry, BetterStack) and Drittland/TADPF section must reflect AVV signing status from 06-AVV-TRACKING.md before review. |
| AGB | Drafted | — | **Flag for lawyer:** §5 support SLA uses "5 Werktagen" as a conservative placeholder for a solo developer. Confirm whether this SLA wording is appropriate or should be softened further (e.g., "sobald möglich"). StBerG-safe positioning and GoBD non-applicability wording also require legal review. |
| Widerrufsbelehrung | Drafted | — | §356 Abs. 4 BGB waiver text reproduced verbatim from statutory template. Muster-Widerrufsformular is the standard BGB Anlage 2 template. Placeholder operator address [Name, Anschrift] must be filled before review. |

## Removal Checklist (per page)

Before changing status to **Lawyer-reviewed**:

- [ ] Operator data (name, address, contact email) filled in for all placeholders
- [ ] AVVs/DPAs signed for all four sub-processors (see 06-AVV-TRACKING.md)
- [ ] Lawyer has reviewed the specific page
- [ ] Lawyer sign-off documented here (Lawyer column, date, name/firm)
- [ ] `<DraftWarning />` component removed from the page file
- [ ] Build passes after draft marker removal
- [ ] Phase 7 QA-07 sign-off complete before setting status to **Live**
