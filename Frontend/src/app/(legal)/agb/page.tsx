import Link from "next/link";
import { DraftWarning } from "@/components/legal/draft-warning";
import { LEGAL_CONFIG, LEGAL_REVIEWED } from "@/lib/legal-config";

export const metadata = {
  title: "Allgemeine Geschäftsbedingungen – TaxReader",
};

export default function AgbPage() {
  return (
    <div className="mx-auto max-w-2xl space-y-8 px-6 py-10 text-sm leading-relaxed">
      <DraftWarning reviewed={LEGAL_REVIEWED.agb} />

      <h1 className="text-2xl font-semibold tracking-tight">
        Allgemeine Geschäftsbedingungen
      </h1>

      <section>
        <h2 className="mb-2 text-base font-semibold">§ 1 Vertragsgegenstand</h2>
        <p className="text-muted-foreground">
          Vertragsgegenstand ist Strukturierung, keine Steuerberatung. TaxReader
          ist ein Hilfsmittel zur Strukturierung von Belegen und leistet keine
          Steuerberatung im Sinne des StBerG. Die Klassifizierungen und
          Auswertungen des Dienstes sind Vorschläge, die der Nutzer eigenverantwortlich
          überprüfen muss. Eine Haftung für die steuerliche Richtigkeit der
          Ergebnisse wird nicht übernommen. Für verbindliche steuerliche Beurteilungen
          wenden Sie sich bitte an einen zugelassenen Steuerberater oder
          Lohnsteuerhilfeverein.
        </p>
      </section>

      <section>
        <h2 className="mb-2 text-base font-semibold">§ 2 GoBD</h2>
        <p className="text-muted-foreground">
          TaxReader ist kein GoBD-zertifiziertes Aufbewahrungssystem; die
          Aufbewahrungspflicht für Belege und steuerlich relevante Unterlagen
          verbleibt beim Nutzer. Originalbelege und Nachweise sind entsprechend
          den gesetzlichen Aufbewahrungsfristen durch den Nutzer selbst aufzubewahren.
          TaxReader eignet sich zur Vorbereitung der Steuererklärung, ersetzt
          jedoch nicht die gesetzlich vorgeschriebene Aufbewahrung nach GoBD.
        </p>
      </section>

      <section>
        <h2 className="mb-2 text-base font-semibold">§ 3 Widerrufsrecht</h2>
        <p className="text-muted-foreground">
          Verbrauchern steht ein gesetzliches Widerrufsrecht zu. Einzelheiten
          entnehmen Sie bitte unserer{" "}
          <Link href="/widerruf" className="text-primary hover:underline">
            Widerrufsbelehrung
          </Link>
          . Bitte beachten Sie, dass das Widerrufsrecht bei digitalen Inhalten
          erlischt, sobald mit der Ausführung des Vertrags begonnen wurde und
          Sie dem ausdrücklich zugestimmt haben.
        </p>
      </section>

      <section>
        <h2 className="mb-2 text-base font-semibold">§ 4 Erstattung</h2>
        <p className="text-muted-foreground">
          Token-Pakete werden auf Token-Basis abgerechnet. Nicht verbrauchte
          Token verbleiben im Konto und verfallen nicht. Eine Erstattung
          ungenutzter Token-Pakete ist nach Ablauf der Widerrufsfrist
          ausgeschlossen, sofern nicht gesetzliche Vorschriften etwas anderes
          bestimmen. Bei technischen Fehlern, die auf unsere Seite
          zurückzuführen sind und Token-Verbrauch ohne Gegenwert verursacht
          haben, erstatten wir die betroffenen Token auf Anfrage.
        </p>
      </section>

      <section>
        <h2 className="mb-2 text-base font-semibold">§ 5 Support</h2>
        <p className="text-muted-foreground">
          Wir bemühen uns, Anfragen innerhalb von 5 Werktagen zu beantworten.
          Ein verbindlicher Service-Level wird nicht garantiert. Support erfolgt
          ausschließlich per E-Mail an{" "}
          <a
            href={`mailto:${LEGAL_CONFIG.email}`}
            className="text-primary hover:underline"
          >
            {LEGAL_CONFIG.email}
          </a>
          .
        </p>
      </section>

      <section>
        <h2 className="mb-2 text-base font-semibold">§ 6 Streitbeilegung</h2>
        <p className="text-muted-foreground">
          Wir sind nicht verpflichtet und nicht bereit, an
          Streitbeilegungsverfahren vor einer Verbraucherschlichtungsstelle
          teilzunehmen (VSBG). Die Europäische Kommission stellt eine Plattform
          zur Online-Streitbeilegung (OS) bereit:{" "}
          <a
            href="https://ec.europa.eu/consumers/odr/"
            target="_blank"
            rel="noopener noreferrer"
            className="text-primary hover:underline"
          >
            https://ec.europa.eu/consumers/odr/
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
