"use client";

import { useState, useEffect } from "react";
import { toast } from "sonner";
import { Loader2, Check, Sparkles, AlertTriangle } from "lucide-react";
import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { useConfirmClassification } from "@/hooks/use-receipt-items";
import type { ReceiptItem, Category } from "@/types/api";
import { categoryLabel, formatCurrency } from "@/lib/format";

interface ClassifyDialogProps {
  item: ReceiptItem | null;
  open: boolean;
  onOpenChange: (open: boolean) => void;
}

const categories: Category[] = [
  "ConsumablesAndOfficeSupplies",
  "SpecialistLiterature",
  "TeachingMaterials",
  "DigitalToolsAndSoftware",
  "OfficeEquipment",
  "TravelAndCommuting",
  "ProfessionalDevelopment",
];

export function ClassifyDialog({
  item,
  open,
  onOpenChange,
}: ClassifyDialogProps) {
  const [category, setCategory] = useState<string>("");
  const mutation = useConfirmClassification();

  const suggestedCategory = item?.latestClassification?.category;
  const isSuggested = item?.latestClassification?.status === "Suggested";
  const isConfirmed = item?.latestClassification?.status === "Confirmed";
  const isUnknown =
    !suggestedCategory || suggestedCategory === "Unknown";
  const failureReason = item?.latestClassification?.reason ?? "";

  useEffect(() => {
    if (open && suggestedCategory && suggestedCategory !== "Unknown") {
      setCategory(suggestedCategory);
    } else if (open) {
      setCategory("");
    }
  }, [open, suggestedCategory]);

  const handleConfirm = async () => {
    if (!item || !category) return;

    try {
      await mutation.mutateAsync({ itemId: item.id, category });
      toast.success("Klassifizierung bestätigt");
      onOpenChange(false);
      setCategory("");
    } catch {
      toast.error("Klassifizierung fehlgeschlagen");
    }
  };

  const handleQuickConfirm = async () => {
    if (!item || !suggestedCategory || suggestedCategory === "Unknown") return;

    try {
      await mutation.mutateAsync({ itemId: item.id, category: suggestedCategory });
      toast.success("Vorschlag bestätigt");
      onOpenChange(false);
    } catch {
      toast.error("Bestätigung fehlgeschlagen");
    }
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-lg">
        <DialogHeader>
          <DialogTitle>Artikel klassifizieren</DialogTitle>
          <DialogDescription className="line-clamp-2">
            {item?.description}
          </DialogDescription>
        </DialogHeader>

        <div className="space-y-4 py-2">
          <div className="flex items-center justify-between rounded-xl border border-border p-4 bg-muted/30">
            <span className="text-sm text-muted-foreground">Betrag</span>
            <span className="text-lg font-semibold">{item ? formatCurrency(item.totalPrice) : "—"}</span>
          </div>

          {isSuggested && suggestedCategory && suggestedCategory !== "Unknown" && (
            <div className="rounded-xl border border-emerald-200 bg-emerald-50 p-4 dark:border-emerald-900 dark:bg-emerald-950/30">
              <div className="flex items-center gap-2 mb-3">
                <Sparkles className="h-4 w-4 text-emerald-600 dark:text-emerald-400" />
                <span className="text-sm font-semibold text-emerald-800 dark:text-emerald-300">
                  Automatischer Vorschlag
                </span>
              </div>
              <div className="flex items-center justify-between gap-4">
                <div>
                  <p className="font-semibold">{categoryLabel(suggestedCategory)}</p>
                  {item?.latestClassification?.reason && (
                    <p className="text-xs text-muted-foreground mt-1">
                      {item.latestClassification.reason}
                    </p>
                  )}
                </div>
                <Button
                  size="sm"
                  onClick={handleQuickConfirm}
                  disabled={mutation.isPending}
                  className="shrink-0"
                >
                  {mutation.isPending ? (
                    <Loader2 className="mr-1.5 h-3.5 w-3.5 animate-spin" />
                  ) : (
                    <Check className="mr-1.5 h-3.5 w-3.5" />
                  )}
                  Bestätigen
                </Button>
              </div>
            </div>
          )}

          {isSuggested && isUnknown && failureReason && (
            <div className="rounded-xl border border-amber-200 bg-amber-50 p-4 dark:border-amber-900 dark:bg-amber-950/30">
              <div className="flex items-start gap-2">
                <AlertTriangle className="h-4 w-4 shrink-0 text-amber-600 dark:text-amber-400 mt-0.5" />
                <div className="space-y-1">
                  <p className="text-sm font-semibold text-amber-800 dark:text-amber-300">
                    Kein automatischer Vorschlag
                  </p>
                  <p className="text-xs text-amber-700 dark:text-amber-400 break-words">
                    {failureReason}
                  </p>
                </div>
              </div>
            </div>
          )}

          {isConfirmed && (
            <div className="rounded-xl border border-emerald-200 bg-emerald-50 p-4 dark:border-emerald-900 dark:bg-emerald-950/30">
              <p className="text-sm text-emerald-800 dark:text-emerald-300">
                <Check className="inline h-4 w-4 mr-1" />
                Bereits bestätigt als <strong>{categoryLabel(suggestedCategory ?? "")}</strong>
              </p>
            </div>
          )}

          <div className="space-y-2">
            <label className="text-sm font-medium">
              {isSuggested ? "Oder andere Kategorie wählen:" : "Kategorie wählen:"}
            </label>
            <Select value={category} onValueChange={(v) => setCategory(v ?? "")}>
              <SelectTrigger>
                <SelectValue placeholder="Kategorie wählen..." />
              </SelectTrigger>
              <SelectContent>
                {categories.map((cat) => (
                  <SelectItem key={cat} value={cat}>
                    {categoryLabel(cat)}{cat === suggestedCategory ? " (Vorschlag)" : ""}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
        </div>

        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)}>
            Abbrechen
          </Button>
          <Button
            onClick={handleConfirm}
            disabled={!category || mutation.isPending}
          >
            {mutation.isPending && (
              <Loader2 className="mr-2 h-4 w-4 animate-spin" />
            )}
            {category === suggestedCategory ? "Vorschlag bestätigen" : "Kategorie zuweisen"}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
