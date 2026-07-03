"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import { toast } from "sonner";
import { Trash2, Loader2, X, Check, Sparkles, HelpCircle, AlertTriangle, AlertCircle } from "lucide-react";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import { Button } from "@/components/ui/button";
import { Skeleton } from "@/components/ui/skeleton";
import { useReceipts } from "@/hooks/use-receipts";
import { useBulkDeleteFiles } from "@/hooks/use-receipt-files";
import { formatCurrency, formatDate } from "@/lib/format";
import type { Receipt } from "@/types/api";

export type ReceiptStatusFilter = "all" | "suggested" | "confirmed" | "failed";

function matchesStatusFilter(receipt: Receipt, filter: ReceiptStatusFilter): boolean {
  if (filter === "all") return true;
  if (filter === "failed") return receipt.failedCount > 0;
  if (filter === "confirmed") {
    return (
      receipt.itemCount > 0 &&
      receipt.failedCount === 0 &&
      receipt.suggestedCount === 0 &&
      receipt.unknownCount === 0
    );
  }
  // "suggested" — anything not yet fully confirmed and not failed
  return receipt.failedCount === 0 && (receipt.suggestedCount > 0 || receipt.unknownCount > 0);
}

export function ReceiptStatusBadge({
  suggestedCount,
  unknownCount,
  failedCount,
  itemCount,
}: {
  suggestedCount: number;
  unknownCount: number;
  failedCount: number;
  itemCount: number;
}) {
  if (itemCount === 0) return null;

  // Failed items are shown first and distinctly — they indicate a technical error,
  // not a genuine "unknown" classification. Do NOT collapse them into unknownCount.
  if (failedCount > 0) {
    return (
      <span className="inline-flex items-center gap-1 rounded-md bg-red-50 px-2 py-0.5 text-[12px] font-medium text-red-600 dark:bg-red-500/10 dark:text-red-400">
        <AlertCircle className="h-3 w-3" />
        {failedCount} {failedCount === 1 ? "Fehler" : "Fehler"}
      </span>
    );
  }

  if (unknownCount > 0) {
    return (
      <span className="inline-flex items-center gap-1 rounded-md bg-red-50 px-2 py-0.5 text-[12px] font-medium text-red-600 dark:bg-red-500/10 dark:text-red-400">
        <HelpCircle className="h-3 w-3" />
        {unknownCount} unbekannt
      </span>
    );
  }

  if (suggestedCount > 0) {
    return (
      <span className="inline-flex items-center gap-1 rounded-md bg-amber-50 px-2 py-0.5 text-[12px] font-medium text-amber-600 dark:bg-amber-500/10 dark:text-amber-400">
        <Sparkles className="h-3 w-3" />
        {suggestedCount} {suggestedCount === 1 ? "Vorschlag" : "Vorschläge"}
      </span>
    );
  }

  return (
    <span className="inline-flex items-center gap-1 rounded-md bg-emerald-50 px-2 py-0.5 text-[12px] font-medium text-emerald-600 dark:bg-emerald-500/10 dark:text-emerald-400">
      <Check className="h-3 w-3" />
      Vollständig
    </span>
  );
}

const vendorColors: Record<string, string> = {
  Amazon: "bg-emerald-500",
  Eduki: "bg-blue-500",
  Thalia: "bg-purple-500",
};

interface ReceiptsTableProps {
  year?: number;
  statusFilter?: ReceiptStatusFilter;
  refetchInterval?: number | false;
}

export function ReceiptsTable({ year, statusFilter = "all", refetchInterval }: ReceiptsTableProps) {
  const router = useRouter();
  const { data: receipts, isLoading } = useReceipts(year, { refetchInterval });
  const bulkDeleteMutation = useBulkDeleteFiles();
  const [selected, setSelected] = useState<Set<string>>(new Set());

  const filteredReceipts = (receipts ?? []).filter((r) => matchesStatusFilter(r, statusFilter));

  const allIds = filteredReceipts.map((r) => r.receiptFileId);
  const allSelected = allIds.length > 0 && allIds.every((id) => selected.has(id));
  const someSelected = selected.size > 0;

  const toggleOne = (receiptFileId: string) => {
    setSelected((prev) => {
      const next = new Set(prev);
      if (next.has(receiptFileId)) {
        next.delete(receiptFileId);
      } else {
        next.add(receiptFileId);
      }
      return next;
    });
  };

  const toggleAll = () => {
    if (allSelected) {
      setSelected(new Set());
    } else {
      setSelected(new Set(allIds));
    }
  };

  const handleBulkDelete = async () => {
    const ids = [...selected];
    if (ids.length === 0) return;

    try {
      const result = await bulkDeleteMutation.mutateAsync(ids);
      toast.success(`${result.deleted} Beleg(e) gelöscht`);
      setSelected(new Set());
    } catch {
      toast.error("Löschen fehlgeschlagen");
    }
  };

  if (isLoading) {
    return (
      <div className="p-4 space-y-3">
        {Array.from({ length: 5 }).map((_, i) => (
          <Skeleton key={i} className="h-12 w-full" />
        ))}
      </div>
    );
  }

  if (!receipts || receipts.length === 0) {
    return (
      <div className="p-6">
        <p className="text-sm text-muted-foreground">
          Keine Belege gefunden. Laden Sie Belege hoch, um zu beginnen.
        </p>
      </div>
    );
  }

  if (filteredReceipts.length === 0) {
    return (
      <div className="p-6">
        <p className="text-sm text-muted-foreground">
          Keine Belege für den gewählten Filter gefunden.
        </p>
      </div>
    );
  }

  return (
    <div className="relative">
      {/* Bulk action bar */}
      {someSelected && (
        <div className="sticky top-0 z-10 flex items-center justify-between border-b border-border bg-primary/5 px-6 py-3">
          <div className="flex items-center gap-3">
            <span className="text-sm font-medium">
              {selected.size} {selected.size === 1 ? "Beleg" : "Belege"} ausgewählt
            </span>
            <button
              onClick={() => setSelected(new Set())}
              className="text-xs text-muted-foreground hover:text-foreground"
            >
              <X className="inline h-3 w-3 mr-0.5" />
              Auswahl aufheben
            </button>
          </div>
          <Button
            variant="destructive"
            size="sm"
            onClick={handleBulkDelete}
            disabled={bulkDeleteMutation.isPending}
          >
            {bulkDeleteMutation.isPending ? (
              <Loader2 className="mr-1.5 h-3.5 w-3.5 animate-spin" />
            ) : (
              <Trash2 className="mr-1.5 h-3.5 w-3.5" />
            )}
            {selected.size} löschen
          </Button>
        </div>
      )}

      <Table>
        <TableHeader>
          <TableRow className="bg-muted/50">
            <TableHead className="w-12 pl-6">
              <input
                type="checkbox"
                checked={allSelected}
                onChange={toggleAll}
                className="h-4 w-4 rounded border-border accent-primary cursor-pointer"
              />
            </TableHead>
            <TableHead className="text-[12px] font-semibold uppercase tracking-wider">Anbieter</TableHead>
            <TableHead className="text-[12px] font-semibold uppercase tracking-wider">Datum</TableHead>
            <TableHead className="text-right text-[12px] font-semibold uppercase tracking-wider">Zwischensumme</TableHead>
            <TableHead className="text-right text-[12px] font-semibold uppercase tracking-wider">MwSt</TableHead>
            <TableHead className="text-right text-[12px] font-semibold uppercase tracking-wider">Gesamt</TableHead>
            <TableHead className="text-right text-[12px] font-semibold uppercase tracking-wider">Artikel</TableHead>
            <TableHead className="text-[12px] font-semibold uppercase tracking-wider">Status</TableHead>
          </TableRow>
        </TableHeader>
        <TableBody>
          {filteredReceipts.map((receipt) => {
            const isSelected = selected.has(receipt.receiptFileId);
            return (
              <TableRow
                key={receipt.id}
                className={`cursor-pointer transition-colors ${
                  isSelected
                    ? "bg-primary/5 hover:bg-primary/10"
                    : "hover:bg-muted/30"
                }`}
                onClick={() => router.push(`/receipts/${receipt.id}`)}
              >
                <TableCell className="pl-6">
                  <input
                    type="checkbox"
                    checked={isSelected}
                    onChange={(e) => {
                      e.stopPropagation();
                      toggleOne(receipt.receiptFileId);
                    }}
                    onClick={(e) => e.stopPropagation()}
                    className="h-4 w-4 rounded border-border accent-primary cursor-pointer"
                  />
                </TableCell>
                <TableCell>
                  <div className="flex items-center gap-2.5">
                    <div
                      className={`h-2 w-2 rounded-full ${vendorColors[receipt.vendor] ?? "bg-muted-foreground"}`}
                    />
                    <span className="font-medium">{receipt.vendor}</span>
                    {receipt.hasSumMismatch && (
                      <AlertTriangle
                        className="inline h-3.5 w-3.5 text-amber-500 ml-1"
                        aria-label="Summe stimmt nicht überein"
                      />
                    )}
                  </div>
                </TableCell>
                <TableCell className="text-muted-foreground">
                  {formatDate(receipt.purchaseDate)}
                </TableCell>
                <TableCell className="text-right">
                  {formatCurrency(receipt.subTotal)}
                </TableCell>
                <TableCell className="text-right">
                  {formatCurrency(receipt.taxAmount)}
                </TableCell>
                <TableCell className="text-right font-semibold">
                  {formatCurrency(receipt.totalAmount)}
                </TableCell>
                <TableCell className="text-right">{receipt.itemCount}</TableCell>
                <TableCell>
                  <ReceiptStatusBadge
                    suggestedCount={receipt.suggestedCount}
                    unknownCount={receipt.unknownCount}
                    failedCount={receipt.failedCount}
                    itemCount={receipt.itemCount}
                  />
                </TableCell>
              </TableRow>
            );
          })}
        </TableBody>
      </Table>
    </div>
  );
}
