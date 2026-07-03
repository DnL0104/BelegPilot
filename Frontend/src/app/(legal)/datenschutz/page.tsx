import Link from "next/link";
import { DraftWarning } from "@/components/legal/draft-warning";
import { LEGAL_CONFIG, LEGAL_REVIEWED } from "@/lib/legal-config";

export const metadata = {
  title: "Datenschutzerklärung | BelegPilot",
};

export default function DatenschutzPage() {
  return (
    <div className="mx-auto max-w-2xl space-y-8 px-6 py-10 text-sm leading-relaxed">
      <DraftWarning reviewed={LEGAL_REVIEWED.datenschutz} />

      <h1 className="text-2xl font-semibold tracking-tight">
        Datenschutzerklärung
      </h1>

      <section>
        <h2 className="mb-2 text-base font-semibold">1. Verantwortlicher</h2>
        <p className="text-muted-foreground">
          Verantwortlich im Sinne der DSGVO ist:
          <br />
          <br />
          {LEGAL_CONFIG.name}
          <br />
          {LEGAL_CONFIG.address}
          <br />
          {LEGAL_CONFIG.city}
          <br />
          E-Mail:{" "}
          <a
            href={`mailto:${LEGAL_CONFIG.email}`}
            className="text-primary hover:underline"
          >
            {LEGAL_CONFIG.email}
          </a>
        </p>
      </section>

      <section>
        <h2 className="mb-2 text-base font-semibold">
          2. Zwecke und Rechtsgrundlagen der Verarbeitung (Art. 13 DSGVO)
        </h2>
        <div className="space-y-3 text-muted-foreground">
          <p>
            TaxReader verarbeitet personenbezogene Daten ausschließlich für die
            folgenden Zwecke:
          </p>
          <ul className="list-disc space-y-1 pl-5">
            <li>
              <strong className="text-foreground">Kontoverwaltung</strong> –
              Name, E-Mail-Adresse und Passwort (gespeichert als sicherer Hash,
              niemals im Klartext) zur Bereitstellung des Dienstes.
              Rechtsgrundlage: Art. 6 Abs. 1 lit. b DSGVO (Vertragserfüllung).
            </li>
            <li>
              <strong className="text-foreground">Belegverarbeitung</strong> –
              Aus hochgeladenen Belegen (PDF/Bild) werden Text (Händlername,
              Kaufdatum, Beträge, Artikelbeschreibungen) und strukturierte Daten
              extrahiert. Die ursprüngliche Datei wird nach der Textextraktion
              automatisch und unwiderruflich gelöscht. Rechtsgrundlage:
              Art. 6 Abs. 1 lit. b DSGVO.
            </li>
            <li>
              <strong className="text-foreground">KI-Klassifizierung</strong> –
              Artikelbeschreibungen werden zur automatischen steuerlichen
              Kategorisierung an Anthropic übermittelt (kein PDF, kein Passwort,
              keine E-Mail-Adresse). Rechtsgrundlage: Art. 6 Abs. 1 lit. b
              DSGVO.
            </li>
            <li>
              <strong className="text-foreground">Zahlungsabwicklung</strong> –
              Zahlungsdaten werden durch Stripe verarbeitet. TaxReader speichert
              keine vollständigen Zahlungsdaten. Rechtsgrundlage: Art. 6
              Abs. 1 lit. b DSGVO.
            </li>
            <li>
              <strong className="text-foreground">
                Betrieb und Sicherheit
              </strong>{" "}
              – Technische Protokolldaten zur Sicherstellung des Betriebs und
              zum Missbrauchsschutz. Rechtsgrundlage: Art. 6 Abs. 1 lit. f
              DSGVO (berechtigtes Interesse).
            </li>
            <li>
              <strong className="text-foreground">
                Fehleranalyse (optional, mit Einwilligung)
              </strong>{" "}
              – Anonyme Fehlerberichte über Sentry nur bei erteilter
              Einwilligung. Rechtsgrundlage: Art. 6 Abs. 1 lit. a DSGVO.
            </li>
          </ul>
        </div>
      </section>

      <section>
        <h2 className="mb-2 text-base font-semibold">
          3. Automatisierte Klassifizierung (Art. 22 DSGVO)
        </h2>
        <p className="text-muted-foreground">
          TaxReader nutzt KI-gestützte Klassifizierung zur Kategorisierung von
          Belegpositionen. Diese Verarbeitung erfolgt automatisiert, enthält
          jedoch{" "}
          <strong className="text-foreground">keine ausschließlich automatisierte
          Entscheidungsfindung</strong> im Sinne des Art. 22 DSGVO: Alle
          Klassifizierungsvorschläge sind für Sie als Nutzer sichtbar,
          überprüfbar und jederzeit manuell korrigierbar. Die finale
          steuerliche Einordnung obliegt stets Ihnen oder Ihrem Steuerberater.
        </p>
      </section>

      <section>
        <h2 className="mb-2 text-base font-semibold">
          4. Auftragsverarbeiter (Art. 28 DSGVO)
        </h2>
        <p className="mb-3 text-muted-foreground">
          TaxReader setzt folgende Auftragsverarbeiter ein, mit denen
          Auftragsverarbeitungsverträge (AVV) gemäß Art. 28 DSGVO bestehen
          oder angebahnt werden:
        </p>
        <div className="overflow-x-auto">
          <table className="w-full text-sm border-collapse">
            <thead>
              <tr className="border-b border-border text-left">
                <th className="pb-2 pr-4 font-semibold text-foreground">Dienst</th>
                <th className="pb-2 pr-4 font-semibold text-foreground">Zweck</th>
                <th className="pb-2 pr-4 font-semibold text-foreground">Sitz</th>
                <th className="pb-2 font-semibold text-foreground">AVV / DPA</th>
              </tr>
            </thead>
            <tbody className="text-muted-foreground">
              <tr className="border-b border-border">
                <td className="py-2 pr-4">Anthropic</td>
                <td className="py-2 pr-4">KI-Klassifizierung</td>
                <td className="py-2 pr-4">USA</td>
                <td className="py-2">
                  <a
                    href="https://www.anthropic.com/legal/dpa"
                    target="_blank"
                    rel="noopener noreferrer"
                    className="text-primary hover:underline"
                  >
                    DPA
                  </a>
                </td>
              </tr>
              <tr className="border-b border-border">
                <td className="py-2 pr-4">Stripe</td>
                <td className="py-2 pr-4">Zahlungsabwicklung</td>
                <td className="py-2 pr-4">Irland / USA</td>
                <td className="py-2">
                  <a
                    href="https://stripe.com/de/legal/dpa"
                    target="_blank"
                    rel="noopener noreferrer"
                    className="text-primary hover:underline"
                  >
                    DPA
                  </a>
                </td>
              </tr>
              <tr className="border-b border-border">
                <td className="py-2 pr-4">Sentry</td>
                <td className="py-2 pr-4">Fehleranalyse</td>
                <td className="py-2 pr-4">EU (Functional Software, USA)</td>
                <td className="py-2">
                  <a
                    href="https://sentry.io/legal/dpa/"
                    target="_blank"
                    rel="noopener noreferrer"
                    className="text-primary hover:underline"
                  >
                    DPA
                  </a>
                </td>
              </tr>
              <tr className="border-b border-border">
                <td className="py-2 pr-4">BetterStack</td>
                <td className="py-2 pr-4">Uptime-Monitoring</td>
                <td className="py-2 pr-4">EU</td>
                <td className="py-2">
                  <a
                    href="https://betterstack.com/privacy"
                    target="_blank"
                    rel="noopener noreferrer"
                    className="text-primary hover:underline"
                  >
                    Datenschutz
                  </a>
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      </section>

      <section>
        <h2 className="mb-2 text-base font-semibold">
          5. Drittland-Übermittlung (USA)
        </h2>
        <p className="text-muted-foreground">
          Anthropic (KI-Klassifizierung) und Stripe (Zahlungsabwicklung)
          verarbeiten Daten in den USA. Die Übermittlung in dieses Drittland
          erfolgt auf Grundlage des EU-U.S. Data Privacy Framework (TADPF),
          an dem Anthropic und Stripe teilnehmen, sowie ergänzend auf Basis
          von Standardvertragsklauseln gemäß Art. 46 DSGVO. Die
          Angemessenheitsentscheidung der Europäischen Kommission vom Juli 2023
          (Schrems II-Nachfolgerahmen) bildet die Grundlage für den
          Datentransfer. Weitere Informationen finden Sie in der{" "}
          <a
            href="https://www.anthropic.com/legal/dpa"
            target="_blank"
            rel="noopener noreferrer"
            className="text-primary hover:underline"
          >
            DPA von Anthropic
          </a>
          .
        </p>
      </section>

      <section>
        <h2 className="mb-2 text-base font-semibold">6. Speicherdauer</h2>
        <p className="text-muted-foreground">
          Ihre Daten werden gespeichert, solange Ihr Konto aktiv ist. Nach
          Kontolöschung werden alle zugehörigen Daten (Konto, Belege,
          Klassifizierungen) vollständig und unwiderruflich gelöscht. Die
          Kontolöschung ist jederzeit über die Einstellungen möglich.
          Transaktionsdaten können für steuerliche Aufbewahrungsfristen
          anonymisiert vorgehalten werden.
        </p>
      </section>

      <section>
        <h2 className="mb-2 text-base font-semibold">7. Ihre Rechte</h2>
        <p className="mb-3 text-muted-foreground">
          Sie haben gemäß DSGVO folgende Rechte:
        </p>
        <ul className="list-disc space-y-1 pl-5 text-muted-foreground">
          <li>
            <strong className="text-foreground">Auskunft</strong> (Art. 15
            DSGVO)
          </li>
          <li>
            <strong className="text-foreground">Berichtigung</strong> (Art. 16
            DSGVO)
          </li>
          <li>
            <strong className="text-foreground">Löschung</strong> (Art. 17
            DSGVO)
          </li>
          <li>
            <strong className="text-foreground">Einschränkung</strong> (Art. 18
            DSGVO)
          </li>
          <li>
            <strong className="text-foreground">Widerspruch</strong> (Art. 21
            DSGVO)
          </li>
          <li>
            <strong className="text-foreground">Datenportabilität</strong>{" "}
            (Art. 20 DSGVO) — Sie können Ihre Daten jederzeit über die{" "}
            <Link href="/settings" className="text-primary hover:underline">
              Einstellungen
            </Link>{" "}
            exportieren.
          </li>
        </ul>
        <p className="mt-3 text-muted-foreground">
          Zur Ausübung Ihrer Rechte:{" "}
          <a
            href={`mailto:${LEGAL_CONFIG.email}`}
            className="text-primary hover:underline"
          >
            {LEGAL_CONFIG.email}
          </a>
        </p>
      </section>

      <section>
        <h2 className="mb-2 text-base font-semibold">
          8. Beschwerderecht
        </h2>
        <p className="text-muted-foreground">
          Sie haben das Recht, sich bei einer Datenschutz-Aufsichtsbehörde zu
          beschweren. Eine Liste der deutschen Behörden finden Sie auf der
          Website des{" "}
          <a
            href="https://www.bfdi.bund.de/DE/Infothek/Anschriften_Links/anschriften_links-node.html"
            target="_blank"
            rel="noopener noreferrer"
            className="text-primary hover:underline"
          >
            Bundesbeauftragten für den Datenschutz
          </a>
          .
        </p>
      </section>

      <div className="border-t pt-6 text-xs text-muted-foreground">
        <Link href="/impressum" className="hover:underline">
          Impressum
        </Link>
      </div>
    </div>
  );
}
