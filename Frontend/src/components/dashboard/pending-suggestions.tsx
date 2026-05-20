"use client";

import { useState } from "react";
import Link from "next/link";
import { toast } from "sonner";
import {
  Sparkles,
  ArrowRight,
  Check,
  Loader2,
  X,
  ChevronDown,
  ChevronUp,
} from "lucide-react";
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
import { ClassificationBadge } from "@/components/receipts/classification-badge";
import { usePendingSuggestions, useBatchConfirm } from "@/hooks/use-receipt-items";
import { formatCurrency, formatDate } from "@/lib/format";

export function PendingSuggestions() {
  const { data: suggestions, isLoading } = usePendingSuggestions();
  const batchConfirmMutation = useBatchConfirm();
  const [selected, setSelected] = useState<Set<string>>(new Set());
  const [expanded, setExpanded] = useState(true);

  const count = suggestions?.length ?? 0;

  const toggleOne = (id: string) => {
    setSelected((prev) => {
      const next = new Set(prev);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });
  };

  const toggleAll = () => {
    if (!suggestions) return;
    if (selected.size === suggestions.length) {
      setSelected(new Set());
    } else {
      setSelected(new Set(suggestions.map((s) => s.itemId)));
    }
  };

  const handleBatchConfirm = async () => {
    if (selected.size === 0) return;
    try {
      const result = await batchConfirmMutation.mutateAsync([...selected]);
      toast.success(`${result.confirmed} Klassifizierung(en) bestätigt`);
      setSelected(new Set());
    } catch {
      toast.error("Bestätigung fehlgeschlagen");
    }
  };

  if (isLoading) {
    return (
      <div className="rounded-xl border border-border bg-card shadow-sm overflow-hidden">
        <div className="flex items-center gap-2 border-b border-border px-6 py-4">
          <Skeleton className="h-5 w-5 rounded" />
          <Skeleton className="h-5 w-48" />
        </div>
        <div className="space-y-3 p-6">
          {Array.from({ length: 2 }).map((_, i) => (
            <Skeleton key={i} className="h-10 w-full" />
          ))}
        </div>
      </div>
    );
  }

  if (count === 0) {
    return (
      <div className="rounded-xl border border-border bg-card shadow-sm overflow-hidden">
        <div className="flex items-center gap-2 border-b border-border px-6 py-4">
          <div className="flex h-7 w-7 items-center justify-center rounded-lg bg-emerald-50 text-emerald-600 dark:bg-emerald-500/10 dark:text-emerald-400">
            <Check className="h-4 w-4" />
          </div>
          <h3 className="text-[15px] font-semibold">Offene Vorschläge</h3>
        </div>
        <div className="px-6 py-8 text-center">
          <p className="text-sm text-muted-foreground">
            Alle Vorschläge wurden bestätigt. Alles erledigt!
          </p>
        </div>
      </div>
    );
  }

  return (
    <div className="rounded-xl border border-amber-200 bg-card shadow-sm overflow-hidden dark:border-amber-500/20">
      {/* Header */}
      <div className="flex items-center justify-between border-b border-border px-6 py-4">
        <button
          onClick={() => setExpanded(!expanded)}
          className="flex items-center gap-2 hover:opacity-80 transition-opacity"
        >
          <div className="flex h-7 w-7 items-center justify-center rounded-lg bg-amber-50 text-amber-600 dark:bg-amber-500/10 dark:text-amber-400">
            <Sparkles className="h-4 w-4" />
          </div>
          <h3 className="text-[15px] font-semibold">
            Offene Vorschläge
          </h3>
          <span className="flex h-5 min-w-5 items-center justify-center rounded-full bg-amber-100 px-1.5 text-[11px] font-bold text-amber-700 dark:bg-amber-500/20 dark:text-amber-400">
            {count}
          </span>
          {expanded ? (
            <ChevronUp className="h-4 w-4 text-muted-foreground" />
          ) : (
            <ChevronDown className="h-4 w-4 text-muted-foreground" />
          )}
        </button>
        {selected.size > 0 && (
          <div className="flex items-center gap-2">
            <button
              onClick={() => setSelected(new Set())}
              className="text-xs text-muted-foreground hover:text-foreground"
            >
              <X className="inline h-3 w-3 mr-0.5" />
              Abwählen
            </button>
            <Button
              size="sm"
              onClick={handleBatchConfirm}
              disabled={batchConfirmMutation.isPending}
            >
              {batchConfirmMutation.isPending ? (
                <Loader2 className="mr-1.5 h-3.5 w-3.5 animate-spin" />
              ) : (
                <Check className="mr-1.5 h-3.5 w-3.5" />
              )}
              {selected.size} bestätigen
            </Button>
          </div>
        )}
      </div>

      {/* Table */}
      {expanded && (
        <Table>
          <TableHeader>
            <TableRow className="bg-muted/50">
              <TableHead className="w-12 pl-6">
                <input
                  type="checkbox"
                  checked={count > 0 && selected.size === count}
                  onChange={toggleAll}
                  className="h-4 w-4 rounded border-border accent-primary cursor-pointer"
                  title="Alle auswählen"
                />
              </TableHead>
              <TableHead className="text-[12px] font-semibold uppercase tracking-wider">
                Artikel
              </TableHead>
              <TableHead className="text-[12px] font-semibold uppercase tracking-wider">
                Anbieter
              </TableHead>
              <TableHead className="text-[12px] font-semibold uppercase tracking-wider">
                Datum
              </TableHead>
              <TableHead className="text-right text-[12px] font-semibold uppercase tracking-wider">
                Betrag
              </TableHead>
              <TableHead className="text-[12px] font-semibold uppercase tracking-wider">
                Vorschlag
              </TableHead>
              <TableHead className="w-10" />
            </TableRow>
          </TableHeader>
          <TableBody>
            {suggestions!.map((s) => {
              const isSelected = selected.has(s.itemId);
              return (
                <TableRow
                  key={s.itemId}
                  className={`transition-colors ${
                    isSelected
                      ? "bg-emerald-50/50 hover:bg-emerald-50 dark:bg-emerald-950/10 dark:hover:bg-emerald-950/20"
                      : "hover:bg-muted/30"
                  }`}
                >
                  <TableCell className="pl-6">
                    <input
                      type="checkbox"
                      checked={isSelected}
                      onChange={() => toggleOne(s.itemId)}
                      className="h-4 w-4 rounded border-border accent-primary cursor-pointer"
                    />
                  </TableCell>
                  <TableCell className="max-w-[200px]">
                    <span className="font-medium truncate block" title={s.description}>
                      {s.description.length > 40
                        ? s.description.slice(0, 40) + "..."
                        : s.description}
                    </span>
                  </TableCell>
                  <TableCell className="text-muted-foreground">
                    {s.vendor}
                  </TableCell>
                  <TableCell className="text-muted-foreground">
                    {formatDate(s.purchaseDate)}
                  </TableCell>
                  <TableCell className="text-right font-semibold">
                    {formatCurrency(s.totalPrice)}
                  </TableCell>
                  <TableCell>
                    <ClassificationBadge
                      classification={{
                        id: "",
                        category: s.category,
                        method: "AI",
                        status: "Suggested",
                        reason: s.reason,
                        classifiedAt: s.classifiedAt,
                      }}
                    />
                  </TableCell>
                  <TableCell>
                    <Link
                      href={`/receipts/${s.receiptId}`}
                      className="flex h-8 w-8 items-center justify-center rounded-lg text-muted-foreground transition-colors hover:bg-muted hover:text-foreground"
                      title="Beleg öffnen"
                    >
                      <ArrowRight className="h-3.5 w-3.5" />
                    </Link>
                  </TableCell>
                </TableRow>
              );
            })}
          </TableBody>
        </Table>
      )}
    </div>
  );
}
