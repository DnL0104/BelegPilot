import Link from "next/link";
import { DraftWarning } from "@/components/legal/draft-warning";
import { LEGAL_CONFIG, LEGAL_REVIEWED } from "@/lib/legal-config";

export const metadata = {
  title: "Widerrufsbelehrung | BelegPilot",
};

export default function WiderrufPage() {
  return (
    <div className="mx-auto max-w-2xl space-y-8 px-6 py-10 text-sm leading-relaxed">
      <DraftWarning reviewed={LEGAL_REVIEWED.widerruf} />

      <h1 className="text-2xl font-semibold tracking-tight">
        Widerrufsbelehrung
      </h1>

      <section>
        <h2 className="mb-2 text-base font-semibold">Widerrufsrecht</h2>
        <p className="text-muted-foreground">
          Sie haben das Recht, binnen vierzehn Tagen ohne Angabe von Gründen
          diesen Vertrag zu widerrufen. Die Widerrufsfrist beträgt vierzehn
          Tage ab dem Tag des Vertragsabschlusses.
        </p>
        <p className="mt-3 text-muted-foreground">
          Um Ihr Widerrufsrecht auszuüben, müssen Sie uns ({LEGAL_CONFIG.name},{" "}
          {LEGAL_CONFIG.address}, {LEGAL_CONFIG.city}, E-Mail:{" "}
          {LEGAL_CONFIG.email}) mittels einer eindeutigen Erklärung (z. B. ein
          mit der Post versandter Brief oder eine E-Mail) über Ihren Entschluss,
          diesen Vertrag zu widerrufen, informieren. Sie können dafür das
          nachstehende Muster-Widerrufsformular verwenden, das jedoch nicht
          vorgeschrieben ist.
        </p>
        <p className="mt-3 text-muted-foreground">
          Zur Wahrung der Widerrufsfrist reicht es aus, dass Sie die Mitteilung
          über die Ausübung des Widerrufsrechts vor Ablauf der Widerrufsfrist
          absenden.
        </p>
      </section>

      <section>
        <h2 className="mb-2 text-base font-semibold">Widerrufsfolgen</h2>
        <p className="text-muted-foreground">
          Wenn Sie diesen Vertrag widerrufen, haben wir Ihnen alle Zahlungen,
          die wir von Ihnen erhalten haben, unverzüglich und spätestens binnen
          vierzehn Tagen ab dem Tag zurückzuzahlen, an dem die Mitteilung über
          Ihren Widerruf dieses Vertrags bei uns eingegangen ist. Für diese
          Rückzahlung verwenden wir dasselbe Zahlungsmittel, das Sie bei der
          ursprünglichen Transaktion eingesetzt haben, es sei denn, mit Ihnen
          wurde ausdrücklich etwas anderes vereinbart; in keinem Fall werden
          Ihnen wegen dieser Rückzahlung Entgelte berechnet.
        </p>
      </section>

      <section>
        <h2 className="mb-2 text-base font-semibold">
          Erlöschen des Widerrufsrechts bei digitalen Inhalten (§ 356 Abs. 4 BGB)
        </h2>
        <p className="text-muted-foreground">
          Ich verlange ausdrücklich, dass mit der Ausführung des Vertrags sofort
          begonnen wird. Mir ist bekannt, dass ich hierdurch mein Widerrufsrecht
          verliere.
        </p>
      </section>

      <section>
        <h2 className="mb-2 text-base font-semibold">Muster-Widerrufsformular</h2>
        <p className="mb-3 text-muted-foreground">
          (Wenn Sie den Vertrag widerrufen wollen, dann füllen Sie bitte dieses
          Formular aus und senden Sie es zurück.)
        </p>
        <div className="rounded border border-border bg-muted/30 p-4 text-muted-foreground">
          <p>
            An: {LEGAL_CONFIG.name}, {LEGAL_CONFIG.address},{" "}
            {LEGAL_CONFIG.city}, E-Mail: {LEGAL_CONFIG.email}
          </p>
          <p className="mt-3">
            Hiermit widerrufe(n) ich/wir (*) den von mir/uns (*) abgeschlossenen
            Vertrag über den Kauf der folgenden Waren (*)/die Erbringung der
            folgenden Dienstleistung (*)
          </p>
          <p className="mt-2">Bestellt am (*)/erhalten am (*)</p>
          <p className="mt-2">Name des/der Verbraucher(s)</p>
          <p className="mt-2">Anschrift des/der Verbraucher(s)</p>
          <p className="mt-2">
            Unterschrift des/der Verbraucher(s) (nur bei Mitteilung auf Papier)
          </p>
          <p className="mt-2">Datum</p>
          <p className="mt-3 text-xs">(*) Unzutreffendes streichen.</p>
        </div>
      </section>

      <div className="border-t pt-6 text-xs text-muted-foreground">
        <span>Weitere Informationen in unserer </span>
        <Link href="/agb" className="hover:underline">
          AGB
        </Link>{" "}
        und im{" "}
        <Link href="/impressum" className="hover:underline">
          Impressum
        </Link>
        .
      </div>
    </div>
  );
}
