import Link from "next/link";

export const metadata = {
  title: "Impressum – BelegPilot",
};

export default function ImpressumPage() {
  return (
    <div className="mx-auto max-w-2xl space-y-8 px-6 py-10 text-sm leading-relaxed">

      <h1 className="text-2xl font-bold tracking-tight">Impressum</h1>

      <section>
        <h2 className="mb-2 text-base font-semibold">
          Angaben gemäß § 5 TMG
        </h2>
        {/* ⚠️  PLATZHALTER – vor dem Launch mit echten Daten ersetzen */}
        <p className="whitespace-pre-line text-muted-foreground">
          [VOLLSTÄNDIGER NAME]{"\n"}
          [STRAßE UND HAUSNUMMER]{"\n"}
          [PLZ ORT]
        </p>
      </section>

      <section>
        <h2 className="mb-2 text-base font-semibold">Kontakt</h2>
        <p className="text-muted-foreground">
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
        <h2 className="mb-2 text-base font-semibold">Umsatzsteuer-ID</h2>
        <p className="text-muted-foreground">
          Umsatzsteuer-Identifikationsnummer gemäß § 27a UStG:{" "}
          [UST-ID oder „nicht vorhanden"]
        </p>
      </section>

      <section>
        <h2 className="mb-2 text-base font-semibold">
          Verantwortlich für den Inhalt nach § 55 Abs. 2 RStV
        </h2>
        <p className="text-muted-foreground">
          [VOLLSTÄNDIGER NAME]
          <br />
          [ANSCHRIFT]
        </p>
      </section>

      <section>
        <h2 className="mb-2 text-base font-semibold">Haftungsausschluss</h2>
        <p className="text-muted-foreground">
          BelegPilot ist ein Werkzeug zur Unterstützung bei der Verwaltung von
          Belegen und Ausgaben. Die von BelegPilot vorgenommenen
          Klassifizierungen und Auswertungen stellen{" "}
          <strong className="text-foreground">keine Steuerberatung</strong> dar
          und ersetzen nicht die Beratung durch einen zugelassenen
          Steuerberater. Für die steuerliche Richtigkeit der Angaben übernehmen
          wir keine Haftung.
        </p>
      </section>

      <section>
        <h2 className="mb-2 text-base font-semibold">Streitschlichtung</h2>
        <p className="text-muted-foreground">
          Die Europäische Kommission stellt eine Plattform zur
          Online-Streitbeilegung (OS) bereit:{" "}
          <a
            href="https://ec.europa.eu/consumers/odr/"
            target="_blank"
            rel="noopener noreferrer"
            className="text-primary hover:underline"
          >
            https://ec.europa.eu/consumers/odr/
          </a>
          . Wir sind nicht verpflichtet und nicht bereit, an
          Streitbeilegungsverfahren vor einer Verbraucherschlichtungsstelle
          teilzunehmen.
        </p>
      </section>

      <div className="border-t pt-6 text-xs text-muted-foreground">
        <Link href="/datenschutz" className="hover:underline">
          Datenschutzerklärung
        </Link>
      </div>

    </div>
  );
}
