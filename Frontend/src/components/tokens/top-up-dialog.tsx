"use client";

import { useState } from "react";
import { toast } from "sonner";
import { Coins, Loader2, Sparkles } from "lucide-react";
import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { useTokenBalance, usePurchaseTokens } from "@/hooks/use-tokens";

interface TopUpDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
}

const PACKAGES = [
  { credits: 50, price: "4,99 €", popular: false },
  { credits: 200, price: "14,99 €", popular: true },
  { credits: 500, price: "29,99 €", popular: false },
];

export function TopUpDialog({ open, onOpenChange }: TopUpDialogProps) {
  const { data: balance } = useTokenBalance();
  const purchase = usePurchaseTokens();
  const [selected, setSelected] = useState<number>(200);

  const handlePurchase = async () => {
    try {
      await purchase.mutateAsync(selected);
      toast.success(`${selected} Credits aufgeladen`);
      onOpenChange(false);
    } catch {
      toast.error("Aufladen fehlgeschlagen");
    }
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
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

        <p className="text-xs text-muted-foreground">
          Hinweis: Im Demo-Modus erfolgt keine echte Zahlung. Credits werden direkt gutgeschrieben.
        </p>

        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)}>
            Abbrechen
          </Button>
          <Button onClick={handlePurchase} disabled={purchase.isPending}>
            {purchase.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
            {selected} Credits kaufen
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
