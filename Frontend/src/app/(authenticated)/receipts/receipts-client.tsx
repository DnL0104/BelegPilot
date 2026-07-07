"use client";

import { useState } from "react";
import Link from "next/link";
import { Upload } from "lucide-react";
import { Header } from "@/components/layout/header";
import { Button } from "@/components/ui/button";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { ReceiptsTable, type ReceiptStatusFilter } from "@/components/receipts/receipts-table";
import { YearFilter } from "@/components/receipts/year-filter";
import { ReceiptFileStatusBadge } from "@/components/upload/receipt-file-status-badge";
import {
  useReceiptFiles,
  useReceiptFileStatus,
  useRetryReceiptFile,
  useBulkRetryFiles,
} from "@/hooks/use-receipt-files";
import { isTerminal, type ProcessingStatus } from "@/types/api";

/**
 * Per-file processing row shown while a file is still in-flight.
 * Polls at 2s cadence via useReceiptFileStatus; disappears once terminal.
 * Offers a manual retry — files can otherwise get stuck indefinitely with no
 * error surfaced if the background job never advances past its initial state.
 */
function ProcessingFileRow({ fileId, fileName }: { fileId: string; fileName: string }) {
  const { data } = useReceiptFileStatus(fileId);
  const retryMutation = useRetryReceiptFile();
  if (!data || isTerminal(data.status)) return null;
  return (
    <div className="flex items-center justify-between gap-3 rounded-lg border border-border bg-muted/30 px-4 py-2.5 text-sm">
      <span className="truncate text-muted-foreground">{fileName}</span>
      <div className="flex items-center gap-2">
        <ReceiptFileStatusBadge status={data.status} />
        <Button
          variant="ghost"
          size="sm"
          disabled={retryMutation.isPending}
          onClick={() => retryMutation.mutate(fileId)}
        >
          {retryMutation.isPending ? "Wird ausgelöst…" : "Erneut versuchen"}
        </Button>
      </div>
    </div>
  );
}

const STATUS_FILTER_OPTIONS: { value: ReceiptStatusFilter; label: string }[] = [
  { value: "all", label: "Alle" },
  { value: "suggested", label: "Vorschläge" },
  { value: "confirmed", label: "Bestätigt" },
  { value: "failed", label: "Fehler" },
];

export function ReceiptsClient() {
  const [year, setYear] = useState<number | undefined>(new Date().getFullYear());
  const [statusFilter, setStatusFilter] = useState<ReceiptStatusFilter>("all");
  const { data: receiptFiles } = useReceiptFiles();
  const bulkRetryMutation = useBulkRetryFiles();

  // Determine which files are still in-flight to show the processing section
  // and drive the list-level refetch cadence.
  // We use a derived flag from the files list; individual rows self-manage via useReceiptFileStatus.
  const nonTerminalFiles = (receiptFiles ?? []).filter(
    (f) => !isTerminal(f.status as ProcessingStatus)
  );
  const hasNonTerminal = nonTerminalFiles.length > 0;

  // True account-wide emptiness (no files ever uploaded) — distinct from a
  // year/status filter that simply yields zero rows, which ReceiptsTable
  // handles with its own inline message.
  const hasNoReceiptsAtAll = (receiptFiles ?? []).length === 0;

  return (
    <>
      <Header title="Meine Belege" />
      <div className="flex-1 p-6 overflow-auto space-y-4">
        {hasNonTerminal && (
          <div className="rounded-xl border border-border bg-card p-4 shadow-sm space-y-2">
            <div className="flex items-center justify-between gap-3">
              <p className="text-[13px] font-semibold uppercase tracking-wide text-muted-foreground">
                In Bearbeitung
              </p>
              {nonTerminalFiles.length > 1 && (
                <Button
                  variant="outline"
                  size="sm"
                  disabled={bulkRetryMutation.isPending}
                  onClick={() =>
                    bulkRetryMutation.mutate(nonTerminalFiles.map((f) => f.id))
                  }
                >
                  {bulkRetryMutation.isPending
                    ? "Wird ausgelöst…"
                    : "Alle erneut versuchen"}
                </Button>
              )}
            </div>
            {nonTerminalFiles.map((f) => (
              <ProcessingFileRow
                key={f.id}
                fileId={f.id}
                fileName={f.originalFileName}
              />
            ))}
          </div>
        )}

        {hasNoReceiptsAtAll ? (
          <div className="flex flex-col items-center justify-center gap-1.5 rounded-xl border border-border bg-card p-12 text-center shadow-sm">
            <Upload className="mb-4 h-12 w-12 text-muted-foreground" aria-hidden="true" />
            <h2 className="text-2xl font-semibold leading-tight text-balance">
              Noch keine Belege vorhanden
            </h2>
            <p className="mt-2 max-w-sm text-base text-pretty text-muted-foreground">
              Laden Sie Ihre ersten Belege hoch, um mit der Klassifizierung zu beginnen.
            </p>
            <Button className="mt-6" nativeButton={false} render={<Link href="/upload" />}>
              Jetzt hochladen
            </Button>
          </div>
        ) : (
          <div className="rounded-xl border border-border bg-card shadow-sm overflow-hidden">
            <div className="flex flex-wrap items-center justify-between gap-3 border-b border-border px-6 py-4">
              <h2 className="text-[15px] font-semibold">Alle Belege</h2>
              <div className="flex items-center gap-2">
                <Select
                  value={statusFilter}
                  onValueChange={(v) => setStatusFilter(v as ReceiptStatusFilter)}
                >
                  <SelectTrigger className="w-[140px]" aria-label="Status filtern">
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    {STATUS_FILTER_OPTIONS.map((option) => (
                      <SelectItem key={option.value} value={option.value}>
                        {option.label}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
                <YearFilter value={year} onChange={setYear} />
              </div>
            </div>
            <div className="p-0">
              <ReceiptsTable
                year={year}
                statusFilter={statusFilter}
                refetchInterval={hasNonTerminal ? 2000 : false}
              />
            </div>
          </div>
        )}
      </div>
    </>
  );
}
