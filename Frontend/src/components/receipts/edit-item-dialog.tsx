"use client";

import { useEffect } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { toast } from "sonner";
import { Loader2 } from "lucide-react";
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
import { useCorrectReceiptItem } from "@/hooks/use-receipt-items";
import type { ReceiptItem } from "@/types/api";

interface EditItemDialogProps {
  item: ReceiptItem | null;
  open: boolean;
  onOpenChange: (open: boolean) => void;
}

const editItemSchema = z.object({
  description: z.string().trim().min(1, "Beschreibung darf nicht leer sein."),
  unitPrice: z.number().min(0, "Einzelpreis darf nicht negativ sein."),
  totalPrice: z.number().min(0, "Gesamtpreis darf nicht negativ sein."),
});

type EditItemFormValues = z.infer<typeof editItemSchema>;

export function EditItemDialog({ item, open, onOpenChange }: EditItemDialogProps) {
  const mutation = useCorrectReceiptItem();
  const {
    register,
    handleSubmit,
    reset,
    formState: { errors, isValid },
  } = useForm<EditItemFormValues>({
    resolver: zodResolver(editItemSchema),
    mode: "onChange",
    defaultValues: { description: "", unitPrice: 0, totalPrice: 0 },
  });

  useEffect(() => {
    if (open && item) {
      reset({
        description: item.description,
        unitPrice: item.unitPrice,
        totalPrice: item.totalPrice,
      });
    }
  }, [open, item, reset]);

  const onSubmit = async (values: EditItemFormValues) => {
    if (!item) return;
    try {
      await mutation.mutateAsync({
        itemId: item.id,
        receiptId: item.receiptId,
        description: values.description.trim(),
        unitPrice: values.unitPrice,
        totalPrice: values.totalPrice,
      });
      toast.success("Artikel korrigiert");
      onOpenChange(false);
    } catch {
      toast.error("Korrektur fehlgeschlagen. Bitte erneut versuchen.");
    }
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-lg">
        <DialogHeader>
          <DialogTitle>Artikel korrigieren</DialogTitle>
          <DialogDescription className="line-clamp-2">
            {item?.description}
          </DialogDescription>
        </DialogHeader>

        <form onSubmit={handleSubmit(onSubmit)}>
          <div className="space-y-4 py-2">
            <div className="space-y-1.5">
              <label htmlFor="edit-item-description" className="text-sm font-medium">
                Beschreibung
              </label>
              <Input
                id="edit-item-description"
                {...register("description")}
                aria-invalid={!!errors.description}
              />
              {errors.description && (
                <p className="text-xs text-destructive">{errors.description.message}</p>
              )}
            </div>

            <div className="space-y-1.5">
              <label htmlFor="edit-item-unit-price" className="text-sm font-medium">
                Einzelpreis (€)
              </label>
              <Input
                id="edit-item-unit-price"
                type="number"
                step="0.01"
                {...register("unitPrice", { valueAsNumber: true })}
                aria-invalid={!!errors.unitPrice}
              />
              {errors.unitPrice && (
                <p className="text-xs text-destructive">{errors.unitPrice.message}</p>
              )}
            </div>

            <div className="space-y-1.5">
              <label htmlFor="edit-item-total-price" className="text-sm font-medium">
                Gesamtpreis (€)
              </label>
              <Input
                id="edit-item-total-price"
                type="number"
                step="0.01"
                {...register("totalPrice", { valueAsNumber: true })}
                aria-invalid={!!errors.totalPrice}
              />
              {errors.totalPrice && (
                <p className="text-xs text-destructive">{errors.totalPrice.message}</p>
              )}
            </div>

            <p className="text-sm text-muted-foreground">
              Menge: {item?.quantity ?? 0} Stk. (in dieser Version nicht bearbeitbar)
            </p>
          </div>

          <DialogFooter>
            <Button
              type="button"
              variant="outline"
              onClick={() => onOpenChange(false)}
              disabled={mutation.isPending}
            >
              Abbrechen
            </Button>
            <Button type="submit" disabled={!isValid || mutation.isPending}>
              {mutation.isPending ? (
                <>
                  <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                  Wird gespeichert…
                </>
              ) : (
                "Speichern"
              )}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
