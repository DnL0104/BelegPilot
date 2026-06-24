import Link from "next/link";
import { DraftWarning } from "@/components/legal/draft-warning";
import { LEGAL_CONFIG, LEGAL_REVIEWED } from "@/lib/legal-config";

export const metadata = {
  title: "Impressum – TaxReader",
};

export default function ImpressumPage() {
  return (
    <div className="mx-auto max-w-2xl space-y-8 px-6 py-10 text-sm leading-relaxed">
      <DraftWarning reviewed={LEGAL_REVIEWED.impressum} />

      <h1 className="text-2xl font-semibold tracking-tight">Impressum</h1>

      <section>
        <h2 className="mb-2 text-base font-semibold">Angaben gemäß § 5 TMG</h2>
        <p className="whitespace-pre-line text-muted-foreground">
          {LEGAL_CONFIG.name}{"\n"}
          {LEGAL_CONFIG.address}{"\n"}
          {LEGAL_CONFIG.city}
        </p>
      </section>

      <section>
        <h2 className="mb-2 text-base font-semibold">Kontakt</h2>
        <p className="text-muted-foreground">
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
