# Stripe Customer Portal and VAT Configuration (PAY-04, PAY-06)

**Purpose:** Document the steps required to activate the Stripe Customer Portal for billing history and VAT-compliant invoice download (PAY-04), and to record the VAT/Kleinunternehmer configuration switch with `automatic_tax` OFF (PAY-06). PAY-06 completion is blocked on an external §19 UStG determination from the Steuerberater.

---

## PAY-04 — Stripe Customer Portal (Billing History + Invoice Download)

### What is already implemented

Both API endpoints are implemented and deployed with the application:

- `POST /api/v1/payments/portal` — creates a Stripe Customer Portal session URL scoped to the authenticated user's `stripe_customer_id`. The frontend billing page calls this endpoint and redirects the user to the returned URL.
- `GET /api/v1/payments/invoices` — lists Stripe invoices for the authenticated user (for in-app display; the Customer Portal also provides downloadable PDFs).

Source: `Backend/src/TaxReader.Api/Endpoints/PaymentEndpoints.cs`

**No application code changes are required for PAY-04.**

### What requires operator action (dashboard activation)

The Stripe Customer Portal is a Stripe-hosted feature that must be explicitly activated in the Stripe Dashboard **before** `POST /payments/portal` can succeed. If it is not activated, the Stripe API returns a configuration error (see Error Recognition below).

> **Pitfall 6:** Without dashboard activation, `CreatePortalSessionAsync` returns `Stripe.StripeException: Customer portal configuration not found`. The endpoint catches this and returns a German error response — but the portal will remain non-functional until the dashboard is configured.

### Step 1 — Activate the Customer Portal in the Stripe Dashboard (test mode)

1. Log in to the [Stripe Dashboard](https://dashboard.stripe.com/).
2. Ensure you are in **test mode** (toggle in the top-left).
3. Navigate to **Settings → Billing → Customer portal**.
4. Click **Activate portal** (or **Save settings** if already partially configured).
5. Under **Features**, enable **Invoice history** and ensure customers can **download invoices**.
6. Under **Business information → Return URL**, set:
   ```
   {Stripe__AppBaseUrl}/billing
   ```
   Replace `{Stripe__AppBaseUrl}` with the actual value in your `.env` (e.g., `https://taxreader.example.com/billing` for production, `http://localhost:3000/billing` for local dev).
7. Save the configuration.

### Step 2 — Activate the Customer Portal in live mode

Repeat Step 1 with the **live mode** toggle active. Test-mode and live-mode Customer Portal configurations are independent in Stripe.

### Step 3 — Verify the portal is reachable

1. Log in to the application with a user account that has made at least one purchase (so a `stripe_customer_id` is present).
2. Navigate to the billing page.
3. Click the "Rechnungshistorie" / portal link. The app calls `POST /api/v1/payments/portal` and redirects to the Stripe-hosted portal.
4. Confirm the portal loads and a VAT-compliant PDF invoice is downloadable for a completed payment.

### Error recognition

| Symptom | Likely cause |
|---------|-------------|
| `"Weiterleitung zum Kundenportal fehlgeschlagen. Bitte versuchen Sie es erneut."` returned by the API | Customer Portal not yet activated in the Stripe Dashboard, or portal configuration was deleted. Activate via Settings → Billing → Customer portal. |
| `"Kein Stripe-Kundenkonto gefunden. Bitte tätigen Sie zunächst einen Kauf."` returned by the API | The user does not yet have a `stripe_customer_id` — they have not made any purchase. This is expected for new users before their first checkout. |

---

## PAY-06 — VAT / Kleinunternehmer Configuration

### Current configuration (already in code)

The VAT treatment is configured in `Backend/src/TaxReader.Infrastructure/Configuration/StripeOptions.cs` via two defaults:

**`Stripe__KleinunternehmerNote`** (config key; bound from `StripeOptions.KleinunternehmerNote`)

Default value:
```
Gemäß §19 UStG wird keine Umsatzsteuer berechnet.
```

This string is appended as an invoice footer note on every Stripe invoice. It communicates to the buyer that VAT is not charged under the Kleinunternehmer rule.

**`automatic_tax` — stays OFF (no code setting needed)**

The `BuildSessionCreateOptions` in `StripePaymentProvider` does **not** set `automatic_tax` on the Stripe Checkout Session. The Stripe default for an unset `automatic_tax` is `disabled` (OFF). This is the correct behavior under §19 UStG: enabling `automatic_tax` would instruct Stripe to calculate and add VAT, which is illegal for a Kleinunternehmer entity.

> **Do NOT enable `automatic_tax` without first obtaining the §19 UStG determination from the Steuerberater.** Enabling it incorrectly would add VAT charges to invoices when the entity is not permitted to collect VAT.

**No application code changes are required for PAY-06.** The switch is already wired. What remains is an external determination.

### Config change procedure (once the §19 determination arrives)

**Scenario A — Steuerberater confirms §19 UStG applies (Kleinunternehmer status)**

No config changes required. The existing defaults are correct:

- `Stripe__KleinunternehmerNote` remains `"Gemäß §19 UStG wird keine Umsatzsteuer berechnet."` (or update to the exact lawyer-reviewed wording when Phase 3 legal copy is finalized).
- `automatic_tax` remains unset (OFF) in `BuildSessionCreateOptions`.
- Verify: test checkout shows no VAT line on the invoice.

**Scenario B — Steuerberater determines §19 UStG does NOT apply (not Kleinunternehmer)**

This scenario requires revisiting the entire VAT approach with the Steuerberater and a lawyer before making any code or config changes. The implications (VAT registration number, UStId, tax-compliant invoice format under §14 UStG, `automatic_tax` settings) are **out of scope for this phase** and must not be invented here. Treat this as a new external blocker requiring a separate implementation plan.

### External blocker — §19 UStG determination (PAY-06 remains pending)

> **PAY-06 is not complete.** The actual §19 UStG Kleinunternehmer determination is an **external dependency** that must be obtained from the Steuerberater before PAY-06 can be closed.

From `STATE.md` (active todo):

> "Obtain VAT/Kleinunternehmer determination from Steuerberater (blocking Phase 1 PAY-06 completion)"

Until this determination arrives:

- The code defaults to the §19 Kleinunternehmer treatment (note on invoice, no VAT charged).
- This is a conservative, legally safer default: it does not charge VAT that may not be owed.
- `automatic_tax` must remain OFF.
- The Phase 3 Datenschutzerklärung and AGB legal review (both blocking on a lawyer anyway) run in parallel — start the Steuerberater consultation alongside Phase 1.

### Threat notes (from plan threat model)

| Threat | Mitigation |
|--------|-----------|
| T-01-18: Illegal VAT charged under §19 (Tampering) | `automatic_tax` stays OFF by default; config is not changed until the Steuerberater determination is received. |
| T-01-19: Portal returns config-not-found (Availability) | Dashboard activation steps above (PAY-04 Step 1–2) close this gap before launch. |

---

## Related

- `Backend/src/TaxReader.Api/Endpoints/PaymentEndpoints.cs` — `POST /payments/portal`, `GET /payments/invoices`
- `Backend/src/TaxReader.Infrastructure/Configuration/StripeOptions.cs` — `KleinunternehmerNote` property
- `docs/runbook/restore.md` — backup restore procedure (BKP-03)
- `docs/runbook/backup-gdpr-retention.md` — backup retention and GDPR reconciliation (BKP-04)
- CONTEXT.md D-10 — credit pack tier configuration
- REQUIREMENTS.md PAY-04, PAY-06 — requirement traceability
- STATE.md — active todos including Steuerberater determination
