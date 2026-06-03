# AVV/DPA Sign-Off Tracking

**Tracks:** DSGVO Art. 28 Auftragsverarbeitungsverträge (AVV) / Data Processing Agreements (DPA) for all sub-processors used by TaxReader.

**Purpose:** Tracks AVV/DPA sign-off for all DSGVO Art. 28 sub-processors. Signing is an operator action. Each row's DPA URL MUST match the sub-processor link in the Datenschutzerklärung (`Frontend/src/app/(legal)/datenschutz/page.tsx`) — coupling enforced by the plan's acceptance criteria. Mark "Signed" and "Link in Datenschutz" only after verifying the URLs are identical.

---

## Sub-Processor Sign-Off Status

| Sub-processor | Purpose | DPA/AVV URL | Signed | Link in Datenschutz |
|---|---|---|---|---|
| Anthropic | KI-Klassifizierung | https://www.anthropic.com/legal/dpa | — | — |
| Stripe | Zahlungsabwicklung | https://stripe.com/de/legal/dpa | — | — |
| Sentry | Fehleranalyse | https://sentry.io/legal/dpa/ | — | — |
| BetterStack | Uptime-Monitoring | https://betterstack.com/privacy | — | — |

**"Signed" column values:** `—` (pending) / `✓ YYYY-MM-DD` (signed on date)
**"Link in Datenschutz" column values:** `—` (not verified) / `✓` (URL confirmed identical to Datenschutz sub-processor table)

---

## Drittland-Übermittlung (USA)

**Anthropic (USA):** Artikelbeschreibungen zur KI-Klassifizierung werden in die USA übermittelt. Die Übermittlung erfolgt auf Grundlage des EU-U.S. Data Privacy Framework (TADPF), an dem Anthropic teilnimmt (Schrems II-Nachfolgerahmen, Angemessenheitsentscheidung der Europäischen Kommission vom Juli 2023), sowie ergänzend auf Basis von Standardvertragsklauseln gemäß Art. 46 DSGVO.

**Stripe (USA/Irland):** Zahlungsverarbeitung mit US-Mutterkonzern. Ebenfalls TADPF-zertifiziert; SCCs als ergänzende Schutzmaßnahme.

**Sentry / BetterStack:** EU-Verarbeitung bevorzugt (Functional Software EU-Region / BetterStack EU). Kein Drittlandtransfer für die primäre Datenverarbeitung erwartet, jedoch abhängig von Konfiguration — beim AVV-Abschluss prüfen und ggf. EU-Region erzwingen.

---

## Operator Actions (HUMAN-UAT — Blocking before commercial launch)

These steps must be completed by the operator before commercial launch. They cannot be automated.

**Step 1 — Accept/sign each DPA:**
1. Anthropic DPA: Go to https://www.anthropic.com/legal/dpa — accept the Data Processing Addendum or sign a custom DPA. File a copy (PDF / email confirmation).
2. Stripe DPA: Go to https://stripe.com/de/legal/dpa — Stripe's DPA is incorporated by reference into the Stripe Services Agreement. Confirm your account's DPA is active. Download/file the confirmation.
3. Sentry DPA: Go to https://sentry.io/legal/dpa/ — sign the DPA in the Sentry organization settings ("Legal" section). Download the signed PDF.
4. BetterStack: Review https://betterstack.com/privacy — check whether a separate DPA/AVV is available for paid accounts. If yes, sign and file; if BetterStack's privacy policy constitutes the Art. 28 basis, document this explicitly.

**Step 2 — File the signed copies:**
- Store signed DPAs in a secure location (e.g., encrypted drive or legal folder) accessible to the operator.
- Note the signing date for each row above.

**Step 3 — Verify URL coupling and update this table:**
- For each sub-processor, confirm the DPA URL in this file EXACTLY matches the link rendered in `Frontend/src/app/(legal)/datenschutz/page.tsx` (the Datenschutzerklärung sub-processor table).
- Mark "Signed" = `✓ YYYY-MM-DD` (with the actual date).
- Mark "Link in Datenschutz" = `✓` once URL parity is confirmed.

**Step 4 — Drittland check:**
- Confirm Anthropic and Stripe are listed in the EU-U.S. Data Privacy Framework participant list (https://www.privacyshield.gov or the DPF participant search at https://www.dataprivacyframework.gov/s/participant-search).
- Document findings in a comment on this file or in the legal folder.

---

## Sign-Off Gate

- [ ] Anthropic DPA signed and filed
- [ ] Stripe DPA signed/confirmed active and filed
- [ ] Sentry DPA signed and filed
- [ ] BetterStack DPA/privacy basis confirmed and filed
- [ ] All DPA URLs verified identical to Datenschutz sub-processor table
- [ ] Drittland DPF participant status confirmed for Anthropic + Stripe
- [ ] 06-AVV-TRACKING.md "Signed" column fully marked ✓ before launch

**Status:** Pending operator action — not cleared for commercial launch until all items above are checked.

---

*Created by plan executor: 06-05 (avv-marken-tracking)*
*Requirement: LEG-06*
*Coupling: Datenschutz sub-processor table URLs in `Frontend/src/app/(legal)/datenschutz/page.tsx`*
