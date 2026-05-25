"use client";

import { useState, useEffect } from "react";
import { toast } from "sonner";
import { Loader2, BookmarkPlus } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { useSaveClassificationRule } from "@/hooks/use-receipt-items";
import type { ReceiptItem } from "@/types/api";
import { categoryLabel } from "@/lib/format";

interface SaveRuleDialogProps {
  item: ReceiptItem | null;
  category: string;        // the newly-selected category from classify-dialog
  vendor: string;          // receipt vendor name — pre-populates VendorPattern
  open: boolean;
  onOpenChange: (open: boolean) => void;
}

export function SaveRuleDialog({ item, category, vendor, open, onOpenChange }: SaveRuleDialogProps) {
  const [vendorPattern, setVendorPattern] = useState(vendor);
  const [descPattern, setDescPattern] = useState(item?.description ?? "");
  const [includeVendor, setIncludeVendor] = useState(true);
  const [includeDesc, setIncludeDesc] = useState(true);
  const mutation = useSaveClassificationRule();

  // WR-05: reset patterns when the dialog opens so stale values from a previous item
  // are not shown if the component stays mounted between uses.
  useEffect(() => {
    if (open) {
      setVendorPattern(vendor);
      setDescPattern(item?.description ?? "");
      setIncludeVendor(true);
      setIncludeDesc(true);
    }
  }, [open, vendor, item?.description]);

  const handleSave = async () => {
    if (!item) return;
    if (!includeVendor && !includeDesc) {
      toast.error("Bitte mindestens ein Musterfeld auswählen.");
      return;
    }
    try {
      await mutation.mutateAsync({
        itemId: item.id,
        payload: {
          vendorPattern: includeVendor ? vendorPattern.trim() || undefined : undefined,
          descriptionPattern: includeDesc ? descPattern.trim() || undefined : undefined,
          category,
        },
      });
      toast.success("Regel gespeichert");
      onOpenChange(false);
    } catch {
      toast.error("Regel konnte nicht gespeichert werden.");
    }
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-md">
        <DialogHeader>
          <DialogTitle>
            <BookmarkPlus className="inline mr-1.5 h-4 w-4" />
            Regel speichern
          </DialogTitle>
          <DialogDescription>
            Zukünftige Positionen werden automatisch als{" "}
            <strong>{categoryLabel(category)}</strong> eingeordnet.
          </DialogDescription>
        </DialogHeader>

        <div className="space-y-4 py-2">
          <div className="flex items-start gap-3">
            <input
              id="include-vendor"
              type="checkbox"
              checked={includeVendor}
              onChange={(e) => setIncludeVendor(e.target.checked)}
              className="mt-1 h-4 w-4 rounded border-border accent-primary cursor-pointer"
            />
            <div className="flex-1 space-y-1">
              <label htmlFor="include-vendor" className="text-sm font-medium cursor-pointer">
                Anbieter
              </label>
              <Input
                value={vendorPattern}
                onChange={(e) => setVendorPattern(e.target.value)}
                disabled={!includeVendor || mutation.isPending}
                placeholder="z. B. Amazon"
              />
            </div>
          </div>

          <div className="flex items-start gap-3">
            <input
              id="include-desc"
              type="checkbox"
              checked={includeDesc}
              onChange={(e) => setIncludeDesc(e.target.checked)}
              className="mt-1 h-4 w-4 rounded border-border accent-primary cursor-pointer"
            />
            <div className="flex-1 space-y-1">
              <label htmlFor="include-desc" className="text-sm font-medium cursor-pointer">
                Beschreibung
              </label>
              <Input
                value={descPattern}
                onChange={(e) => setDescPattern(e.target.value)}
                disabled={!includeDesc || mutation.isPending}
                placeholder="z. B. Buch"
              />
            </div>
          </div>
        </div>

        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)} disabled={mutation.isPending}>
            Abbrechen
          </Button>
          <Button onClick={handleSave} disabled={mutation.isPending}>
            {mutation.isPending && <Loader2 className="mr-1.5 h-3.5 w-3.5 animate-spin" />}
            Speichern
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
