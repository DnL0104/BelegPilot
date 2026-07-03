"use client";

import { useState, useMemo } from "react";
import { toast } from "sonner";
import { Pencil, Check, Loader2, X, ChevronDown, ChevronRight } from "lucide-react";
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
import { ClassificationBadge } from "./classification-badge";
import { ClassifyDialog } from "./classify-dialog";
import { useReceiptItems, useBatchConfirm } from "@/hooks/use-receipt-items";
import { formatCurrency } from "@/lib/format";
import type { ReceiptItem } from "@/types/api";

interface ReceiptItemsTableProps {
  receiptId: string;
  vendor?: string;
}

export function ReceiptItemsTable({ receiptId, vendor = "" }: ReceiptItemsTableProps) {
  const { data: items, isLoading } = useReceiptItems(receiptId);
  const batchConfirmMutation = useBatchConfirm();
  const [classifyItem, setClassifyItem] = useState<ReceiptItem | null>(null);
  const [selected, setSelected] = useState<Set<string>>(new Set());
  // CLS-04: per-row expand state for reasoning; collapsed by default.
  const [expandedReasons, setExpandedReasons] = useState<Set<string>>(new Set());

  const toggleReason = (itemId: string) => {
    setExpandedReasons((prev) => {
      const next = new Set(prev);
      if (next.has(itemId)) next.delete(itemId);
      else next.add(itemId);
      return next;
    });
  };

  const confirmableIds = useMemo(() => {
    if (!items) return new Set<string>();
    return new Set(
      items
        .filter(
          (i) =>
            i.latestClassification?.status === "Suggested" &&
            i.latestClassification?.category &&
            i.latestClassification.category !== "Unbekannt"
        )
        .map((i) => i.id)
    );
  }, [items]);

  const selectedConfirmable = useMemo(
    () => [...selected].filter((id) => confirmableIds.has(id)),
    [selected, confirmableIds]
  );

  const allConfirmableSelected =
    confirmableIds.size > 0 &&
    [...confirmableIds].every((id) => selected.has(id));

  const toggleOne = (id: string) => {
    setSelected((prev) => {
      const next = new Set(prev);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });
  };

  const toggleAllConfirmable = () => {
    if (allConfirmableSelected) {
      setSelected(new Set());
    } else {
      setSelected(new Set(confirmableIds));
    }
  };

  const handleBatchConfirm = async () => {
    if (selectedConfirmable.length === 0) return;
    try {
      const result = await batchConfirmMutation.mutateAsync(selectedConfirmable);
      toast.success(`${result.confirmed} Klassifizierung(en) bestätigt`);
      setSelected(new Set());
    } catch {
      toast.error("Bestätigung fehlgeschlagen");
    }
  };

  if (isLoading) {
    return (
      <div className="p-4 space-y-3">
        {Array.from({ length: 3 }).map((_, i) => (
          <Skeleton key={i} className="h-16 w-full" />
        ))}
      </div>
    );
  }

  if (!items || items.length === 0) {
    return (
      <div className="p-6">
        <p className="text-sm text-muted-foreground">
          Keine Artikel für diesen Beleg gefunden.
        </p>
      </div>
    );
  }

  return (
    <>
      {/* Batch action bar — desktop: sticky at top of the list */}
      {selectedConfirmable.length > 0 && (
        <div className="sticky top-0 z-10 hidden items-center justify-between border-b border-border bg-primary/5 px-4 py-3 sm:px-6 md:flex">
          <div className="flex items-center gap-3">
            <span className="text-sm font-medium">
              {selectedConfirmable.length} ausgewählt
            </span>
            <button
              onClick={() => setSelected(new Set())}
              className="text-xs text-muted-foreground hover:text-foreground"
            >
              <X className="inline h-3 w-3 mr-0.5" />
              Aufheben
            </button>
          </div>
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
            {selectedConfirmable.length} {selectedConfirmable.length === 1 ? "Vorschlag" : "Vorschläge"} bestätigen
          </Button>
        </div>
      )}

      {/* Batch action bar — mobile: sticky at the bottom of the viewport (z-4 per UI-SPEC scale) */}
      {selectedConfirmable.length > 0 && (
        <div className="fixed inset-x-0 bottom-0 z-40 flex items-center justify-between gap-3 border-t border-border bg-card px-4 py-3 shadow-md md:hidden">
          <div className="flex flex-col">
            <span className="text-sm font-medium">
              {selectedConfirmable.length} ausgewählt
            </span>
            <button
              onClick={() => setSelected(new Set())}
              className="text-left text-xs text-muted-foreground hover:text-foreground"
            >
              Aufheben
            </button>
          </div>
          <Button
            size="sm"
            className="h-11"
            onClick={handleBatchConfirm}
            disabled={batchConfirmMutation.isPending}
          >
            {batchConfirmMutation.isPending ? (
              <Loader2 className="mr-1.5 h-3.5 w-3.5 animate-spin" />
            ) : (
              <Check className="mr-1.5 h-3.5 w-3.5" />
            )}
            {selectedConfirmable.length} {selectedConfirmable.length === 1 ? "Vorschlag" : "Vorschläge"} bestätigen
          </Button>
        </div>
      )}

      {/* ── Mobile: card layout ───────────────────────────────────────── */}
      <div className={`divide-y divide-border md:hidden ${selectedConfirmable.length > 0 ? "pb-20" : ""}`}>
        {confirmableIds.size > 0 && (
          <div className="flex items-center gap-3 bg-muted/40 px-4 py-2.5">
            <input
              type="checkbox"
              checked={allConfirmableSelected}
              onChange={toggleAllConfirmable}
              className="h-4 w-4 rounded border-border accent-primary cursor-pointer"
            />
            <span className="text-xs text-muted-foreground">
              Alle Vorschläge auswählen
            </span>
          </div>
        )}

        {items.map((item) => {
          const isConfirmable = confirmableIds.has(item.id);
          const isSelected = selected.has(item.id);
          const hasReason = !!item.latestClassification?.reason;
          const isReasonExpanded = expandedReasons.has(item.id);
          return (
            <div
              key={item.id}
              className={`flex gap-3 px-4 py-4 transition-colors ${
                isSelected ? "bg-primary/5" : "bg-background"
              }`}
            >
              {/* Checkbox column */}
              {confirmableIds.size > 0 && (
                <div className="flex shrink-0 items-start pt-0.5">
                  {isConfirmable ? (
                    <input
                      type="checkbox"
                      checked={isSelected}
                      onChange={() => toggleOne(item.id)}
                      className="h-4 w-4 rounded border-border accent-primary cursor-pointer"
                    />
                  ) : (
                    <div className="h-4 w-4" />
                  )}
                </div>
              )}

              {/* Content */}
              <div className="min-w-0 flex-1 space-y-2">
                {/* Description + edit button */}
                <div className="flex items-start justify-between gap-2">
                  <p className="text-sm font-medium leading-snug">
                    {item.description}
                  </p>
                  <button
                    type="button"
                    onClick={() => setClassifyItem(item)}
                    className="shrink-0 flex h-7 w-7 items-center justify-center rounded-md text-muted-foreground transition-colors hover:bg-muted hover:text-foreground"
                    aria-label="Klassifizierung bearbeiten"
                  >
                    <Pencil className="h-3.5 w-3.5" />
                  </button>
                </div>

                {/* Price row */}
                <div className="flex flex-wrap items-center gap-x-3 gap-y-1 text-xs text-muted-foreground">
                  <span>{item.quantity} Stk.</span>
                  <span>×</span>
                  <span>{formatCurrency(item.unitPrice)}</span>
                  <span className="font-semibold text-foreground">
                    = {formatCurrency(item.totalPrice)}
                  </span>
                </div>

                {/* Classification */}
                <ClassificationBadge
                  classification={item.latestClassification}
                />

                {/* CLS-04: expand-on-demand reasoning control — collapsed by default.
                    Rendered for all four states (Suggested/Unbekannt/Failed/Confirmed)
                    whenever a classification exists. A title= tooltip does NOT satisfy CLS-04. */}
                {item.latestClassification && (
                  hasReason ? (
                    <div className="text-xs text-muted-foreground mt-0.5">
                      <button
                        type="button"
                        onClick={() => toggleReason(item.id)}
                        aria-label={`Begründung für ${item.description} ${isReasonExpanded ? "ausblenden" : "einblenden"}`}
                        className="flex items-center gap-0.5 font-medium hover:text-foreground transition-colors"
                      >
                        {isReasonExpanded
                          ? <ChevronDown className="h-3 w-3" />
                          : <ChevronRight className="h-3 w-3" />}
                        {isReasonExpanded ? "Begründung ausblenden" : "Begründung einblenden"}
                      </button>
                      {isReasonExpanded && (
                        <p className="mt-1 pl-3.5">
                          {item.latestClassification!.reason}
                        </p>
                      )}
                    </div>
                  ) : (
                    <p className="text-xs text-muted-foreground mt-0.5">
                      Keine Begründung verfügbar.
                    </p>
                  )
                )}
              </div>
            </div>
          );
        })}
      </div>

      {/* ── Desktop: table layout ─────────────────────────────────────── */}
      <div className="hidden md:block">
        <Table>
          <TableHeader>
            <TableRow className="bg-muted/50">
              {confirmableIds.size > 0 && (
                <TableHead className="w-12 pl-6">
                  <input
                    type="checkbox"
                    checked={allConfirmableSelected}
                    onChange={toggleAllConfirmable}
                    className="h-4 w-4 rounded border-border accent-primary cursor-pointer"
                    title="Alle Vorschläge auswählen"
                  />
                </TableHead>
              )}
              <TableHead className="w-12 text-[12px] font-semibold uppercase tracking-wider">#</TableHead>
              <TableHead className="text-[12px] font-semibold uppercase tracking-wider">Beschreibung</TableHead>
              <TableHead className="text-right text-[12px] font-semibold uppercase tracking-wider">Menge</TableHead>
              <TableHead className="text-right text-[12px] font-semibold uppercase tracking-wider">Einzelpreis</TableHead>
              <TableHead className="text-right text-[12px] font-semibold uppercase tracking-wider">Gesamt</TableHead>
              <TableHead className="text-[12px] font-semibold uppercase tracking-wider">Klassifizierung</TableHead>
              <TableHead className="w-12" />
            </TableRow>
          </TableHeader>
          <TableBody>
            {items.map((item) => {
              const isConfirmable = confirmableIds.has(item.id);
              const isSelected = selected.has(item.id);
              const hasReason = !!item.latestClassification?.reason;
              const isReasonExpanded = expandedReasons.has(item.id);
              return (
                <TableRow
                  key={item.id}
                  className={`transition-colors ${
                    isSelected
                      ? "bg-primary/5 hover:bg-primary/10"
                      : "hover:bg-muted/30"
                  }`}
                >
                  {confirmableIds.size > 0 && (
                    <TableCell className="pl-6">
                      {isConfirmable ? (
                        <input
                          type="checkbox"
                          checked={isSelected}
                          onChange={() => toggleOne(item.id)}
                          className="h-4 w-4 rounded border-border accent-primary cursor-pointer"
                        />
                      ) : (
                        <span />
                      )}
                    </TableCell>
                  )}
                  <TableCell className="text-muted-foreground">
                    {item.lineNumber}
                  </TableCell>
                  <TableCell className="font-medium">{item.description}</TableCell>
                  <TableCell className="text-right">{item.quantity}</TableCell>
                  <TableCell className="text-right">
                    {formatCurrency(item.unitPrice)}
                  </TableCell>
                  <TableCell className="text-right font-semibold">
                    {formatCurrency(item.totalPrice)}
                  </TableCell>
                  <TableCell>
                    <ClassificationBadge
                      classification={item.latestClassification}
                    />
                    {/* CLS-04: expand-on-demand reasoning — collapsed by default.
                        Works for all four states (Vorschlag/Unbekannt/Fehler/Bestätigt).
                        The title= on the badge is a hover hint only; this is the auditable control. */}
                    {item.latestClassification && (
                      hasReason ? (
                        <div className="text-xs text-muted-foreground mt-0.5 max-w-xs">
                          <button
                            type="button"
                            onClick={() => toggleReason(item.id)}
                            aria-label={`Begründung für ${item.description} ${isReasonExpanded ? "ausblenden" : "einblenden"}`}
                            className="flex items-center gap-0.5 font-medium hover:text-foreground transition-colors"
                          >
                            {isReasonExpanded
                              ? <ChevronDown className="h-3 w-3" />
                              : <ChevronRight className="h-3 w-3" />}
                            {isReasonExpanded ? "Begründung ausblenden" : "Begründung einblenden"}
                          </button>
                          {isReasonExpanded && (
                            <p className="mt-1 pl-3.5">
                              {item.latestClassification!.reason}
                            </p>
                          )}
                        </div>
                      ) : (
                        <p className="text-xs text-muted-foreground mt-0.5">
                          Keine Begründung verfügbar.
                        </p>
                      )
                    )}
                  </TableCell>
                  <TableCell>
                    <button
                      type="button"
                      onClick={() => setClassifyItem(item)}
                      className="flex h-8 w-8 items-center justify-center rounded-lg text-muted-foreground transition-colors hover:bg-muted hover:text-foreground"
                      aria-label="Klassifizierung bearbeiten"
                    >
                      <Pencil className="h-3.5 w-3.5" />
                    </button>
                  </TableCell>
                </TableRow>
              );
            })}
          </TableBody>
        </Table>
      </div>

      {/* Hint when no batch-confirmable items exist */}
      {confirmableIds.size === 0 && items.some(
        (i) => i.latestClassification?.status === "Suggested"
      ) && (
        <div className="px-4 py-3 text-xs text-muted-foreground border-t border-border sm:px-6">
          Alle Vorschläge sind &quot;Unbekannt&quot; — verwende den Stift-Button, um eine Kategorie manuell zuzuweisen.
        </div>
      )}

      <p className="px-4 py-3 text-[11px] text-muted-foreground/60 border-t border-border sm:px-6">
        Dies sind Vorschläge, keine Steuerberatung gemäß §5 StBerG.
      </p>

      <ClassifyDialog
        item={classifyItem}
        vendor={vendor}
        open={!!classifyItem}
        onOpenChange={(open) => {
          if (!open) setClassifyItem(null);
        }}
      />
    </>
  );
}
