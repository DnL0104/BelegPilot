"use client";

import { use } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { ChevronRight, Trash2, Loader2, RefreshCw, AlertTriangle, SquarePen } from "lucide-react";
import { toast } from "sonner";
import { Header } from "@/components/layout/header";
import { Button } from "@/components/ui/button";
import { Skeleton } from "@/components/ui/skeleton";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
  AlertDialogTrigger,
} from "@/components/ui/alert-dialog";
import { ReceiptItemsTable } from "@/components/receipts/receipt-items-table";
import { VisionExtractedBadge } from "@/components/receipts/vision-extracted-badge";
import { useReceiptById, useAcknowledgeSumMismatch } from "@/hooks/use-receipts";
import { useDeleteFile, useReceiptFileStatus } from "@/hooks/use-receipt-files";
import { useReclassifyReceipt } from "@/hooks/use-receipt-items";
import { formatCurrency, formatDate } from "@/lib/format";
import { isTerminal } from "@/types/api";

export function ReceiptDetailClient({
  params,
}: {
  params: Promise<{ id: string }>;
}) {
  const { id } = use(params);
  const router = useRouter();
  const { data: receipt, isLoading } = useReceiptById(id);
  // Status polling uses the receipt FILE id, not the receipt id.
  // The receipt file is the parent entity; the receipt is derived from it.
  const { data: statusData } = useReceiptFileStatus(
    receipt?.receiptFileId ?? "",
    { enabled: !!receipt?.receiptFileId }
  );
  const deleteMutation = useDeleteFile();
  const reclassifyMutation = useReclassifyReceipt();
  const acknowledgeMutation = useAcknowledgeSumMismatch();

  const processingStatus = statusData?.status;
  const isProcessing = processingStatus && !isTerminal(processingStatus);
  const isFailed =
    processingStatus === "Failed" || processingStatus === "Cancelled";

  const handleReclassify = async () => {
    try {
      await reclassifyMutation.mutateAsync(id);
      toast.success("Artikel wurden neu klassifiziert");
    } catch {
      toast.error("Klassifizierung fehlgeschlagen. Bitte erneut versuchen.");
    }
  };

  const handleDelete = async () => {
    if (!receipt) return;
    try {
      await deleteMutation.mutateAsync(receipt.receiptFileId);
      toast.success("Beleg gelöscht");
      router.push("/receipts");
    } catch {
      toast.error("Löschen fehlgeschlagen. Bitte erneut versuchen.");
    }
  };

  return (
    <>
      <Header title="Belegdetails" />
      <div className="flex-1 space-y-5 p-6 overflow-auto">
        <nav aria-label="Breadcrumb" className="flex items-center gap-1.5 text-sm text-muted-foreground">
          <Link href="/receipts" className="hover:text-foreground hover:underline">
            Meine Belege
          </Link>
          <ChevronRight className="h-3.5 w-3.5 shrink-0" aria-hidden="true" />
          <span className="truncate font-medium text-foreground">
            {receipt?.vendor ?? "Belegdetails"}
          </span>
        </nav>

        <div className="flex flex-wrap items-center justify-between gap-4">
          <div className="flex items-center gap-2 flex-wrap">
            <h1 className="text-2xl font-semibold tracking-tight text-balance">
              {receipt?.vendor ?? "Belegdetails"}
            </h1>
            {receipt?.extractionSource === "Vision" && <VisionExtractedBadge />}
          </div>
          {receipt && (
            <div className="flex items-center gap-2">
              <Button
                variant="outline"
                size="sm"
                onClick={handleReclassify}
                disabled={reclassifyMutation.isPending}
              >
                {reclassifyMutation.isPending ? (
                  <Loader2 className="mr-1.5 h-3.5 w-3.5 animate-spin" />
                ) : (
                  <RefreshCw className="mr-1.5 h-3.5 w-3.5" />
                )}
                Erneut klassifizieren
              </Button>
              <AlertDialog>
                <AlertDialogTrigger
                  render={
                    <Button
                      variant="destructive"
                      size="sm"
                      disabled={deleteMutation.isPending}
                    />
                  }
                >
                  {deleteMutation.isPending ? (
                    <Loader2 className="mr-1 h-3 w-3 animate-spin" />
                  ) : (
                    <Trash2 className="mr-1 h-3 w-3" />
                  )}
                  Beleg löschen
                </AlertDialogTrigger>
                <AlertDialogContent>
                  <AlertDialogHeader>
                    <AlertDialogTitle>Beleg löschen</AlertDialogTitle>
                    <AlertDialogDescription>
                      Dieser Beleg und alle zugehörigen Klassifizierungen werden unwiderruflich
                      gelöscht. Diese Aktion kann nicht rückgängig gemacht werden.
                    </AlertDialogDescription>
                  </AlertDialogHeader>
                  <AlertDialogFooter>
                    <AlertDialogCancel>Abbrechen</AlertDialogCancel>
                    <AlertDialogAction variant="destructive" onClick={handleDelete}>
                      Endgültig löschen
                    </AlertDialogAction>
                  </AlertDialogFooter>
                </AlertDialogContent>
              </AlertDialog>
            </div>
          )}
        </div>

        {isLoading ? (
          <div className="rounded-xl border border-border bg-card p-6 shadow-sm">
            <div className="space-y-3">
              <Skeleton className="h-8 w-48" />
              <Skeleton className="h-5 w-32" />
              <div className="flex gap-8">
                <Skeleton className="h-12 w-24" />
                <Skeleton className="h-12 w-24" />
                <Skeleton className="h-12 w-24" />
              </div>
            </div>
          </div>
        ) : receipt ? (
          <>
            <div className="rounded-xl border border-border bg-card p-6 shadow-sm">
              <div className="grid gap-6 sm:grid-cols-2 lg:grid-cols-5">
                <div>
                  <p className="text-[13px] text-muted-foreground mb-1">Datum</p>
                  <p className="text-lg font-medium">
                    {formatDate(receipt.purchaseDate)}
                  </p>
                </div>
                <div>
                  <p className="text-[13px] text-muted-foreground mb-1">Zwischensumme</p>
                  <p className="text-lg font-medium">
                    {formatCurrency(receipt.subTotal)}
                  </p>
                </div>
                <div>
                  <p className="text-[13px] text-muted-foreground mb-1">MwSt</p>
                  <p className="text-lg font-medium">
                    {formatCurrency(receipt.taxAmount)}
                  </p>
                </div>
                <div>
                  <p className="text-[13px] text-muted-foreground mb-1">Gesamt</p>
                  <p className="text-2xl font-bold text-primary">
                    {formatCurrency(receipt.totalAmount)}
                  </p>
                </div>
                <div>
                  <p className="text-[13px] text-muted-foreground mb-1">Währung</p>
                  <p className="text-lg font-medium">{receipt.currency}</p>
                </div>
              </div>
            </div>

            {receipt.hasSumMismatch && (
              <Alert className="border-amber-200 bg-amber-50 dark:border-amber-900 dark:bg-amber-950/30">
                <AlertTriangle className="h-4 w-4 text-amber-600 dark:text-amber-400" />
                <AlertTitle className="text-amber-800 dark:text-amber-300">
                  Summe stimmt nicht überein
                </AlertTitle>
                <AlertDescription className="flex items-center justify-between gap-4">
                  <span>
                    Die Summe der Artikel weicht von der Belegsumme ab. Passen Sie die
                    betroffenen Artikel an oder markieren Sie die Abweichung als geprüft.
                  </span>
                  <div className="flex items-center gap-2">
                    <Button
                      size="sm"
                      onClick={() =>
                        document
                          .getElementById("artikel-liste")
                          ?.scrollIntoView({ block: "start" })
                      }
                    >
                      <SquarePen className="mr-1.5 h-3.5 w-3.5" aria-hidden="true" />
                      Artikel korrigieren
                    </Button>
                    <Button
                      size="sm"
                      variant="outline"
                      onClick={() => acknowledgeMutation.mutate(id)}
                      disabled={acknowledgeMutation.isPending}
                    >
                      {acknowledgeMutation.isPending && (
                        <Loader2 className="mr-1.5 h-3.5 w-3.5 animate-spin" />
                      )}
                      Als geprüft markieren
                    </Button>
                  </div>
                </AlertDescription>
              </Alert>
            )}

            <div className="rounded-xl border border-border bg-card shadow-sm overflow-hidden">
              <div id="artikel-liste" className="border-b border-border px-6 py-4">
                <h3 className="text-[15px] font-semibold">
                  Artikel ({receipt.itemCount})
                </h3>
                <p className="text-xs text-muted-foreground mt-1">
                  Warum wurde das so eingeordnet? Die Begründung erscheint unter jeder Kategorie.
                </p>
              </div>

              {isFailed ? (
                <div className="p-6">
                  <Alert variant="destructive">
                    <AlertTitle>Verarbeitung fehlgeschlagen</AlertTitle>
                    <AlertDescription>
                      {statusData?.errorMessage ?? "Unbekannter Fehler."}
                    </AlertDescription>
                  </Alert>
                </div>
              ) : isProcessing ? (
                <div className="p-6 space-y-3">
                  <Skeleton className="h-32 w-full" />
                  <Skeleton className="h-8 w-2/3" />
                </div>
              ) : (
                <ReceiptItemsTable receiptId={id} vendor={receipt.vendor} />
              )}
            </div>
          </>
        ) : (
          <div className="rounded-xl border border-border bg-card p-6 shadow-sm">
            <p className="text-muted-foreground">Beleg nicht gefunden.</p>
          </div>
        )}
      </div>
    </>
  );
}
