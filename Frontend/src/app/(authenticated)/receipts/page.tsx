"use client";

import { useState } from "react";
import { Header } from "@/components/layout/header";
import { ReceiptsTable } from "@/components/receipts/receipts-table";
import { YearFilter } from "@/components/receipts/year-filter";
import { ReceiptFileStatusBadge } from "@/components/upload/receipt-file-status-badge";
import { useReceiptFiles, useReceiptFileStatus } from "@/hooks/use-receipt-files";
import { isTerminal, type ProcessingStatus } from "@/types/api";

/**
 * Per-file processing row shown while a file is still in-flight.
 * Polls at 2s cadence via useReceiptFileStatus; disappears once terminal.
 */
function ProcessingFileRow({ fileId, fileName }: { fileId: string; fileName: string }) {
  const { data } = useReceiptFileStatus(fileId);
  if (!data || isTerminal(data.status)) return null;
  return (
    <div className="flex items-center justify-between gap-3 rounded-lg border border-border bg-muted/30 px-4 py-2.5 text-sm">
      <span className="truncate text-muted-foreground">{fileName}</span>
      <ReceiptFileStatusBadge status={data.status} />
    </div>
  );
}

export default function ReceiptsPage() {
  const [year, setYear] = useState<number | undefined>(new Date().getFullYear());
  const { data: receiptFiles } = useReceiptFiles();

  // Determine which files are still in-flight to show the processing section
  // and drive the list-level refetch cadence.
  // We use a derived flag from the files list; individual rows self-manage via useReceiptFileStatus.
  const hasNonTerminal = (receiptFiles ?? []).some(
    (f) => !isTerminal(f.status as ProcessingStatus)
  );

  return (
    <>
      <Header title="Belege" />
      <div className="flex-1 p-6 overflow-auto space-y-4">
        {hasNonTerminal && (
          <div className="rounded-xl border border-border bg-card p-4 shadow-sm space-y-2">
            <p className="text-[13px] font-semibold uppercase tracking-wide text-muted-foreground">
              In Bearbeitung
            </p>
            {(receiptFiles ?? [])
              .filter((f) => !isTerminal(f.status as ProcessingStatus))
              .map((f) => (
                <ProcessingFileRow
                  key={f.id}
                  fileId={f.id}
                  fileName={f.originalFileName}
                />
              ))}
          </div>
        )}

        <div className="rounded-xl border border-border bg-card shadow-sm overflow-hidden">
          <div className="flex items-center justify-between border-b border-border px-6 py-4">
            <h2 className="text-[15px] font-semibold">Alle Belege</h2>
            <YearFilter value={year} onChange={setYear} />
          </div>
          <div className="p-0">
            <ReceiptsTable year={year} refetchInterval={hasNonTerminal ? 2000 : false} />
          </div>
        </div>
      </div>
    </>
  );
}
