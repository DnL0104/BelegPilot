import Link from "next/link";

export const metadata = {
  title: "Datenschutzerklärung – BelegPilot",
};

export default function DatenschutzPage() {
  return (
    <div className="mx-auto max-w-2xl space-y-8 px-6 py-10 text-sm leading-relaxed">

      <div>
        <h1 className="text-2xl font-bold tracking-tight">
          Datenschutzerklärung
        </h1>
        <p className="mt-1 text-xs text-muted-foreground">Stand: April 2025</p>
      </div>

      <section>
        <h2 className="mb-3 text-base font-semibold">1. Verantwortlicher</h2>
        {/* ⚠️  PLATZHALTER – vor dem Launch mit echten Daten ersetzen */}
        <p className="text-muted-foreground">
          Verantwortlich im Sinne der DSGVO ist:
          <br />
          <br />
          [VOLLSTÄNDIGER NAME]
          <br />
          [STRAßE UND HAUSNUMMER]
          <br />
          [PLZ ORT]
          <br />
          E-Mail:{" "}
          <a
            href="mailto:[E-MAIL-ADRESSE]"
            className="text-primary hover:underline"
          >
            [E-MAIL-ADRESSE]
          </a>
        </p>
      </section>

      <section>
        <h2 className="mb-3 text-base font-semibold">
          2. Welche Daten werden erhoben?
        </h2>
        <div className="space-y-4 text-muted-foreground">
          <div>
            <h3 className="mb-1 font-medium text-foreground">
              Bei der Registrierung
            </h3>
            <p>
              Zur Nutzung von BelegPilot erfassen wir Name, E-Mail-Adresse sowie
              ein Passwort (gespeichert als sicherer Hash, niemals im Klartext).
            </p>
          </div>
          <div>
            <h3 className="mb-1 font-medium text-foreground">
              Bei der Belegverarbeitung
            </h3>
            <p>
              Du kannst PDF-Belege hochladen. Wir extrahieren daraus den Text
              (Händlername, Kaufdatum, Beträge, Artikelbeschreibungen) und
              speichern diese strukturierten Daten in deinem Konto. Die
              ursprüngliche PDF-Datei wird unmittelbar nach der Textextraktion
              automatisch und unwiderruflich gelöscht — sie verbleibt nicht auf
              unseren Servern.
            </p>
          </div>
          <div>
            <h3 className="mb-1 font-medium text-foreground">Nutzungsdaten</h3>
            <p>
              Wir speichern keine Serverprotokolle mit IP-Adressen oder
              ähnlichen Verbindungsdaten über die für den Betrieb technisch
              notwendige Dauer hinaus.
            </p>
          </div>
        </div>
      </section>

      <section>
        <h2 className="mb-3 text-base font-semibold">
          3. KI-gestützte Klassifizierung (Anthropic)
        </h2>
        <p className="text-muted-foreground">
          Zur automatischen steuerlichen Kategorisierung werden
          Artikelbeschreibungen und Belegdaten (kein PDF, kein Passwort, keine
          E-Mail-Adresse) an die API von{" "}
          <strong className="text-foreground">Anthropic PBC</strong> (San
          Francisco, USA) übermittelt. Anthropic verarbeitet diese Daten
          ausschließlich zur Erbringung des API-Dienstes gemäß seiner eigenen
          Datenschutzrichtlinie (
          <a
            href="https://www.anthropic.com/privacy"
            target="_blank"
            rel="noopener noreferrer"
            className="text-primary hover:underline"
          >
            anthropic.com/privacy
          </a>
          ). Die Übermittlung erfolgt auf Grundlage von Art. 6 Abs. 1 lit. b
          DSGVO sowie Standardvertragsklauseln nach Art. 46 DSGVO für die
          Drittlandsübermittlung in die USA.
        </p>
      </section>

      <section>
        <h2 className="mb-3 text-base font-semibold">4. Rechtsgrundlagen</h2>
        <ul className="list-disc space-y-1 pl-5 text-muted-foreground">
          <li>
            <strong className="text-foreground">
              Art. 6 Abs. 1 lit. b DSGVO
            </strong>{" "}
            – Vertragserfüllung: Verarbeitung der Kontodaten und Belegdaten zur
            Bereitstellung des Dienstes.
          </li>
          <li>
            <strong className="text-foreground">
              Art. 6 Abs. 1 lit. f DSGVO
            </strong>{" "}
            – Berechtigtes Interesse: Sicherstellung des technischen Betriebs
            und Missbrauchsschutz.
          </li>
        </ul>
      </section>

      <section>
        <h2 className="mb-3 text-base font-semibold">5. Speicherdauer</h2>
        <p className="text-muted-foreground">
          Deine Daten werden gespeichert, solange dein Konto aktiv ist. Nach
          einer Kontolöschung werden alle zugehörigen Daten (Konto, Belege,
          Klassifizierungen) vollständig entfernt. Du kannst die Löschung
          deines Kontos jederzeit über die Einstellungen veranlassen.
        </p>
      </section>

      <section>
        <h2 className="mb-3 text-base font-semibold">
          6. Cookies und lokale Speicherung
        </h2>
        <p className="text-muted-foreground">
          BelegPilot verwendet ausschließlich technisch notwendige
          Authentifizierungs-Token (JWT), die für den Login erforderlich sind.
          Es werden{" "}
          <strong className="text-foreground">
            keine Tracking- oder Analyse-Cookies
          </strong>{" "}
          eingesetzt. Ein Cookie-Consent-Banner ist daher nicht erforderlich.
        </p>
      </section>

      <section>
        <h2 className="mb-3 text-base font-semibold">
          7. Datenweitergabe an Dritte
        </h2>
        <p className="text-muted-foreground">
          Deine Daten werden nicht an Werbetreibende, Analysedienste oder
          andere Dritte weitergegeben. Ausnahme: die unter Punkt 3 beschriebene
          Übermittlung an Anthropic zum Zweck der KI-Klassifizierung.
        </p>
      </section>

      <section>
        <h2 className="mb-3 text-base font-semibold">8. Deine Rechte</h2>
        <p className="mb-3 text-muted-foreground">
          Du hast gemäß DSGVO folgende Rechte bezüglich deiner
          personenbezogenen Daten:
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
            <strong className="text-foreground">Portabilität</strong> (Art. 20
            DSGVO)
          </li>
        </ul>
        <p className="mt-3 text-muted-foreground">
          Zur Ausübung dieser Rechte:{" "}
          <a
            href="mailto:[E-MAIL-ADRESSE]"
            className="text-primary hover:underline"
          >
            [E-MAIL-ADRESSE]
          </a>
        </p>
      </section>

      <section>
        <h2 className="mb-3 text-base font-semibold">
          9. Beschwerderecht bei der Aufsichtsbehörde
        </h2>
        <p className="text-muted-foreground">
          Du hast das Recht, dich bei einer Datenschutz-Aufsichtsbehörde zu
          beschweren. Eine Liste der deutschen Behörden findest du auf der
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

      <section>
        <h2 className="mb-3 text-base font-semibold">
          10. Kein Steuerberater
        </h2>
        <p className="text-muted-foreground">
          BelegPilot ist ein Softwarewerkzeug und stellt keine Steuerberatung
          dar. Die automatisch vorgenommenen Klassifizierungen sind Vorschläge
          und ersetzen nicht die individuelle Beratung durch einen zugelassenen
          Steuerberater oder Lohnsteuerhilfeverein.
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
