# Phase 5: Commercial Surface (Payments) - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions captured in 05-CONTEXT.md — this log preserves the discussion.

**Date:** 2026-05-25
**Phase:** 05-commercial-surface-payments
**Mode:** discuss (default interactive)
**Areas discussed:** Token Pack Pricing, Checkout UX Flow, VAT / MwSt Strategy, Billing Page, Dev/Test Environment

---

## Questions & Answers

### Area 1: Token Pack Pricing

| Question | Options Presented | User Answer |
|----------|-------------------|-------------|
| Are the 3 packages (50/200/500 at 4,99/14,99/29,99 €) final? | Yes, locked / Need to adjust | **Yes, locked** |
| How should Stripe Products/Prices be managed? | Manual in Stripe Dashboard / Hardcode Price IDs / Dynamic API lookup | **Manual in Stripe Dashboard** |

**Outcome:** Prices locked at 4,99 / 14,99 / 29,99 €. Manual Stripe Dashboard setup; Price IDs in config.

---

### Area 2: Checkout UX Flow

| Question | Options Presented | User Answer |
|----------|-------------------|-------------|
| What happens when user clicks "Kaufen"? | Redirect to Stripe hosted checkout / Embedded Payment Element | **Redirect to Stripe hosted checkout** |
| After payment, what does user see? | /billing?payment=success / /dashboard with toast / Spinner on TopUpDialog | **/billing?payment=success** |
| Where does Widerrufsrecht gate appear? | Inside TopUpDialog (replace confirm step) / Separate /checkout gate page | **Inside TopUpDialog** |

**Outcome:** Hosted checkout redirect. Success URL = /billing?payment=success. AGB + Widerrufsrecht checkboxes in TopUpDialog before checkout request.

---

### Area 3: VAT / MwSt Strategy

| Question | Options Presented | User Answer |
|----------|-------------------|-------------|
| How should prices be displayed and invoiced? | Bruttopreise (VAT included) / Nettopreise + VAT line / Flat price with footnote | **Bruttopreise (VAT included)** |
| Does TaxReader have a DE USt-IdNr.? | Not yet — Kleinunternehmer (§19 UStG) / Yes — regular taxpayer / Not decided — leave placeholder | **Not yet — Kleinunternehmer (§19 UStG)** |

**Outcome:** Bruttopreise displayed. Kleinunternehmer §19 UStG — no VAT charged. Invoice note: "Gemäß §19 UStG wird keine Umsatzsteuer berechnet." Stripe tax_exempt.

---

### Area 4: Billing Page

| Question | Options Presented | User Answer |
|----------|-------------------|-------------|
| Where does the Billing page live? | Separate /billing route / Section within Settings page | **Separate /billing route** |
| What should the nav item be called? | "Credits & Abrechnung" / "Abonnement & Credits" / "Credits" | **"Credits & Abrechnung"** |
| Negative balance after refund — what happens? | UI shows negative, uploads blocked / Floor at 0 / Account soft-locked | **UI shows negative, new uploads blocked** |

**Outcome:** Separate /billing route. Nav: "Credits & Abrechnung" after Einstellungen. Negative balance shows; uploads blocked until recharged; existing receipts/reports/exports still accessible.

---

### Area 5: Dev / Test Environment (user-initiated topic)

| Question | Options Presented | User Answer |
|----------|-------------------|-------------|
| How should Stripe test mode work locally? | Test keys locally + Stripe CLI / Feature flag disables Stripe / Separate staging env | **Test keys locally + Stripe CLI** |
| Startup guard failure mode? | Throw InvalidOperationException / Log warning but start | **Throw InvalidOperationException** |
| Demo mode toggle? | No — real Stripe test mode is local experience / Yes — Stripe__DemoMode=true toggle | **Yes — Stripe__DemoMode=true toggle** |

**Outcome:** sk_test_ locally, sk_live_ in production. IValidateOptions<StripeOptions> throws on mismatch in Production. Stripe__DemoMode=true skips Stripe and credits directly; shows Demo-Modus banner.

---

## Claude's Discretion Items

The following were not asked — Claude will decide per CLAUDE.md conventions during planning:
- Exact Stripe.net NuGet version
- Whether GrantTokensJob is fire-and-forget or scheduled check
- Exact shadcn components for billing page layout
- `/billing?payment=success` polling implementation detail
- Stripe metadata key names on CheckoutSession

## Deferred Ideas

- Subscription / recurring billing → v2
- Mollie / SEPA / giropay → INT-V2-02
- Volume discounts or enterprise packs → not in scope
- Stripe Tax automatic calculation → when Kleinunternehmer threshold exceeded (no code change needed)
- Audit log for payment grants → Phase 6 (LEG-08)
