import Link from "next/link";

export const metadata = {
  title: "Impressum – TaxReader",
};

function DraftWarning() {
  return (
    <div className="rounded border border-amber-400 bg-amber-50 dark:bg-amber-500/10 px-4 py-2 text-sm text-amber-800 dark:text-amber-200">
      ⚠ Entwurf – anwaltliche Prüfung ausstehend
    </div>
  );
}

export default function ImpressumPage() {
  return (
    <div className="mx-auto max-w-2xl space-y-8 px-6 py-10 text-sm leading-relaxed">
      <DraftWarning />

      <h1 className="text-2xl font-semibold tracking-tight">Impressum</h1>

      <section>
        <h2 className="mb-2 text-base font-semibold">Angaben gemäß § 5 TMG</h2>
        <p className="whitespace-pre-line text-muted-foreground">
          [Name]{"\n"}
          [Anschrift]{"\n"}
          [PLZ Ort]
        </p>
      </section>

      <section>
        <h2 className="mb-2 text-base font-semibold">Kontakt</h2>
        <p className="text-muted-foreground">
          E-Mail:{" "}
          <a
            href="mailto:[kontakt@taxreader.de]"
            className="text-primary hover:underline"
          >
            [kontakt@taxreader.de]
          </a>
        </p>
      </section>

      <section>
        <h2 className="mb-2 text-base font-semibold">Umsatzsteuer</h2>
        <p className="text-muted-foreground">
          Gemäß § 19 UStG wird keine Umsatzsteuer berechnet; es wird keine
          Umsatzsteuer-Identifikationsnummer (USt-IdNr.) geführt.
        </p>
      </section>

      <section>
        <h2 className="mb-2 text-base font-semibold">
          Verbraucherstreitbeilegung / Online-Streitbeilegung
        </h2>
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

      <section>
        <h2 className="mb-2 text-base font-semibold">Haftungsausschluss</h2>
        <p className="text-muted-foreground">
          TaxReader ist ein Hilfsmittel zur Strukturierung von Belegen und
          leistet keine Steuerberatung im Sinne des StBerG. Die von TaxReader
          vorgenommenen Klassifizierungen sind Vorschläge und ersetzen nicht die
          Beratung durch einen zugelassenen Steuerberater.
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
