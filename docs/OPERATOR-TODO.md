# Operator-TODO — offene Aufgaben vor dem Go-Live

> Stand: 2026-06-24. Diese Liste sind **Aufgaben für dich (Operator)**, kein Entwicklungsaufwand.
> Der Code der Phasen 1–3 ist fertig und verifiziert (Build clean, 342 Unit- + 8 Integrationstests grün,
> LEGAL-06 GDPR-Löschung end-to-end gegen echtes PostgreSQL bewiesen).
> Was hier steht, sind externe/manuelle Schritte (Stripe-Konfiguration, Anwalt, Steuerberater, Anthropic).

---

## 🔴 A — Zahlungen scharf schalten (blockt Phase 1 / PAY-Go-Live)

- [ ] **Stripe-Preise anlegen** (PAY-01, Plan 01-04): 3 Prices in **Test- und Live-Mode** erstellen:
  - 100 Credits → 4,99 €
  - 500 Credits → 19,99 €
  - 1500 Credits → 49,99 €
  - Price-IDs in `Stripe__PricePacks` eintragen. **Die Platzhalter-Price-IDs aus den user-secrets entfernen**
    und durch die echten Test-IDs ersetzen.
  - Siehe `01-04-SUMMARY.md`.
- [ ] **Test-Kauf durchführen**: mit Testkarte `4242 4242 4242 4242` einen Kauf abschließen und prüfen,
  dass die Credits gutgeschrieben werden. Außerdem bestätigen, dass ein **nicht bestätigter Checkout
  (Widerrufsverzicht/AGB nicht akzeptiert) abgelehnt** wird (PAY-05).
- [ ] **Stripe Customer Portal aktivieren** (PAY-04, Plan 01-05): Settings → Billing → Customer Portal,
  Return-URL `{Stripe__AppBaseUrl}/billing`, in **Test- und Live-Mode**. Danach prüfen, dass die
  Rechnungs-Historie lädt. Siehe `docs/runbook/stripe-portal-and-vat.md`.
- [ ] **VAT / Kleinunternehmer §19 UStG klären** (PAY-06): Bestimmung beim **Steuerberater** einholen,
  bevor der Live-Stripe-Checkout final konfiguriert wird (Umsatzsteuer-Ausweis ja/nein).

---

## 🔴 B — Rechtliches für DE-Launch (blockt Phase 3 / Legal-Go-Live)

- [ ] **Anwaltliche Prüfung beauftragen** (längste externe Vorlaufzeit — zuerst anstoßen):
  Impressum, Datenschutzerklärung, AGB, Widerrufsrecht, GoBD-Anwendbarkeit. Dabei auch den
  **§257 HGB / §147 AO Aufbewahrungsumfang** für anonymisierte Payment-Datensätze bestätigen lassen.
- [ ] **Nach Anwalts-Freigabe:** pro Seite das Flag `LEGAL_REVIEWED.<seite>` in
  `Frontend/src/lib/legal-config.ts` auf `true` setzen und im Browser prüfen, dass der amber
  „Entwurf — anwaltliche Prüfung ausstehend"-Banner verschwindet.
- [ ] **Ladungsfähige Geschäftsadresse besorgen** und die `LEGAL_CONFIG`-Platzhalter füllen
  (`[Name]`, `[Anschrift]`, `[PLZ Ort]`, `[kontakt@taxreader.de]`):
  - Echte **ladungsfähige Anschrift** (kein Postfach), idealerweise über einen Impressumsservice.
  - **Konsistent** halten über Impressum, Datenschutz, Stripe-Rechnung (`Stripe__BusinessAddress`,
    §14 UStG) und Gewerbeanmeldung.
  - Rechtlicher Name des Einzelunternehmens nötig (Auftritt als „Velrion"). Adresse vom Anwalt bestätigen lassen.
- [ ] **Anthropic AVV/DPA** (LEGAL-04): Konto auf **bezahltem Plan** bestätigen, AVV/DPA herunterladen
  und unterzeichnen/ablegen — erst dann darf die Datenschutzerklärung Anthropic als Auftragsverarbeiter
  wahrheitsgemäß nennen.

---

## 🟡 C — Klassifizierungs-Qualität (Pre-Launch-Gate, blockt Phase 4 NICHT)

- [ ] **Golden-Dataset kuratieren** (CLS-07, Plan 02-04): ≥50 anonymisierte deutsche Belege nach
  `eval/golden-dataset/labels.json` (PII → `ANONYM`, ≥5 Beispiele pro Zielkategorie). Dann
  `dotnet test Backend/tests/TaxReader.EvalTests --filter "Category=Eval"` mit gesetztem
  `Anthropic__ApiKey` laufen lassen und `eval/baseline/<datum>.md` committen.
  Jede abgedeckte Kategorie muss **≥80 % Genauigkeit** erreichen. (Harness ist fertig — reine Datenaufgabe.)
- [ ] **Structured-Outputs prüfen**: vor Aktivierung von `output_format` bestätigen, dass
  `claude-haiku-4-5` Structured Outputs unterstützt.

---

## 🟢 D — Optionales Entwickler-Backlog (kein Operator-Task, niedrige Priorität)

Aus dem Skills-Audit der Phasen 1–3 bewusst zurückgestellt — kann später ein Entwickler erledigen:

- [ ] **WR-S02**: `StripeWebhookHandler` macht nach dem atomaren Raw-SQL-Upsert ein zweites
  `SaveChangesAsync`. Sauberer wäre `ExecuteUpdateAsync(... StripeCustomerId == null)`. Zurückgestellt
  wegen Test-Kompatibilitätsrisiko (Pfad nur via Mock erreichbar); realer Impact theoretisch.
- [ ] **IN-S01**: Exakter -90-Tage-Grenzfall für `AuditLogRetentionJob` ist nicht deterministisch
  testbar (Job nutzt internes `DateTime.UtcNow`). Nur sinnvoll mit injizierbarer Clock.
- [ ] **P1-CR-01-Regressionstest**: Der Fix (NULL statt `""` für fehlende PaymentIntentId) ist
  eingebaut, hat aber keinen *dedizierten* Integrationstest, der die NULL-Persistenz prüft
  (die bestehenden Webhook-Idempotenz-Tests laufen grün → keine Regression).

---

## Erledigt — keine Aktion nötig (zur Info)

- ✅ Phase 1 Code (Observability, Backups/DR, Payments-Hardening)
- ✅ Phase 2 Code (Failed-State, Chunking/Retry, Confidence-Tiers, Eval-Harness)
- ✅ Phase 3 Code + Verification 7/7 (LEGAL-06 GDPR-Löschung gegen echtes PostgreSQL bewiesen)
- ✅ Skills-Audit Phasen 1–3 → 16 Fixes (inkl. Refund-Token-Revoke-Bug P1-CR-01)
- ✅ Agent-Skills-Verdrahtung (dotnet + UI Skills für GSD-Agents)

**Nächster Entwicklungsschritt:** Phase 4 (Full UI Redesign) planen — `/gsd-plan-phase 4`.
