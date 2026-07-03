"use client";

import { useState } from "react";
import Link from "next/link";
import { toast } from "sonner";
import { Coins, Loader2, Sparkles } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Checkbox } from "@/components/ui/checkbox";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { useTokenBalance } from "@/hooks/use-tokens";
import { useCreateCheckoutSession } from "@/hooks/use-billing";

interface TopUpDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
}

// WR-03: credit tiers mirror the backend allow-list in PaymentEndpoints.cs (validCredits).
// The backend is the authority and rejects any credits value not listed there — keep both
// in sync when adding/removing a tier.
const PACKAGES = [
  { credits: 100, price: "4,99 €", popular: false },
  { credits: 500, price: "19,99 €", popular: true },
  { credits: 1500, price: "49,99 €", popular: false },
];

export function TopUpDialog({ open, onOpenChange }: TopUpDialogProps) {
  const { data: balance } = useTokenBalance();
  const checkout = useCreateCheckoutSession();
  const [selected, setSelected] = useState<number>(500);
  const [agbChecked, setAgbChecked] = useState(false);
  const [widerrufsrechtChecked, setWiderrufsrechtChecked] = useState(false);

  const handleOpenChange = (v: boolean) => {
    if (!v) {
      // Reset checkboxes on close — D-05: never pre-ticked on re-open
      setAgbChecked(false);
      setWiderrufsrechtChecked(false);
    }
    onOpenChange(v);
  };

  const handleKaufen = async () => {
    try {
      const data = await checkout.mutateAsync({
        credits: selected,
        waiverAccepted: widerrufsrechtChecked,
        agbAccepted: agbChecked,
      });
      // D-14: DemoMode — redirect to billing with demo flag so billing page can show banner
      if (data.isDemoMode) {
        window.location.href = "/billing?payment=success&demo=true";
      } else {
        window.location.href = data.checkoutUrl;
      }
    } catch {
      toast.error("Bezahlung konnte nicht gestartet werden. Bitte versuchen Sie es erneut.");
    }
  };

  const selectedPkg = PACKAGES.find((p) => p.credits === selected);

  return (
    <Dialog open={open} onOpenChange={handleOpenChange}>
      <DialogContent className="sm:max-w-md">
        <DialogHeader>
          <DialogTitle className="flex items-center gap-2">
            <Sparkles className="h-5 w-5 text-primary" />
            AI Credits aufladen
          </DialogTitle>
          <DialogDescription>
            Jede automatische KI-Klassifizierung verbraucht 1 Credit. Regel-basierte
            Klassifizierungen sind kostenlos.
          </DialogDescription>
        </DialogHeader>

        <div className="rounded-md border bg-muted/30 p-3 flex items-center justify-between">
          <span className="text-sm text-muted-foreground">Aktueller Stand</span>
          <span className="font-semibold flex items-center gap-1.5">
            <Coins className="h-4 w-4" />
            {balance?.balance ?? 0} Credits
          </span>
        </div>

        <div className="grid gap-2 py-2">
          {PACKAGES.map((pkg) => (
            <button
              key={pkg.credits}
              type="button"
              onClick={() => setSelected(pkg.credits)}
              className={`flex items-center justify-between rounded-md border p-3 text-left transition ${
                selected === pkg.credits
                  ? "border-primary bg-primary/5 ring-1 ring-primary"
                  : "hover:bg-muted/50"
              }`}
            >
              <div className="flex items-center gap-3">
                <Coins className="h-5 w-5 text-primary" />
                <div>
                  <div className="font-medium flex items-center gap-2">
                    {pkg.credits} Credits
                    {pkg.popular && (
                      <span className="text-xs font-normal rounded-full bg-primary/10 text-primary px-2 py-0.5">
                        Beliebt
                      </span>
                    )}
                  </div>
                  <div className="text-xs text-muted-foreground">
                    ~{pkg.credits} Artikel klassifizieren
                  </div>
                </div>
              </div>
              <div className="font-semibold">{pkg.price}</div>
            </button>
          ))}
        </div>

        {/* Legal gate — D-05: both checkboxes required, neither pre-ticked */}
        <div className="space-y-3 py-2">
          <p className="text-xs text-muted-foreground">
            Mit dem Kauf bestätigen Sie, dass Sie auf Ihr Widerrufsrecht für
            digitale Inhalte verzichten, da der Inhalt sofort bereitgestellt
            wird (§356 Abs. 4 BGB).
          </p>
          <div className="flex items-start gap-3">
            <Checkbox
              id="agb-checkbox"
              checked={agbChecked}
              onCheckedChange={(v) => setAgbChecked(!!v)}
            />
            <label
              htmlFor="agb-checkbox"
              className="text-sm leading-normal cursor-pointer"
            >
              Ich akzeptiere die{" "}
              <Link href="/agb" className="underline underline-offset-2">
                Allgemeinen Geschäftsbedingungen
              </Link>
              .
            </label>
          </div>
          <div className="flex items-start gap-3">
            <Checkbox
              id="widerrufsrecht-checkbox"
              checked={widerrufsrechtChecked}
              onCheckedChange={(v) => setWiderrufsrechtChecked(!!v)}
            />
            <label
              htmlFor="widerrufsrecht-checkbox"
              className="text-sm leading-normal cursor-pointer"
            >
              Ich verlange ausdrücklich, dass mit der Ausführung des Vertrags
              sofort begonnen wird. Mir ist bekannt, dass ich hierdurch mein
              Widerrufsrecht verliere.
            </label>
          </div>
        </div>

        <DialogFooter>
          <Button
            variant="outline"
            onClick={() => handleOpenChange(false)}
            disabled={checkout.isPending}
          >
            Abbrechen
          </Button>
          <Button
            onClick={handleKaufen}
            disabled={!agbChecked || !widerrufsrechtChecked || checkout.isPending}
          >
            {checkout.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
            {checkout.isPending
              ? "Weiterleitung..."
              : `${selected} Credits kaufen – ${selectedPkg?.price}`}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
