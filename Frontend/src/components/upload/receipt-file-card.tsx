"use client";

import Link from "next/link";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import {
  useCancelReceiptFile,
  useReceiptFileStatus,
  useRetryReceiptFile,
} from "@/hooks/use-receipt-files";
import { isTerminal } from "@/types/api";
import { ReceiptFileStatusBadge } from "./receipt-file-status-badge";

export function ReceiptFileCard({
  receiptFileId,
  fileName,
}: {
  receiptFileId: string;
  fileName: string;
}) {
  const { data, isLoading, isError } = useReceiptFileStatus(receiptFileId);
  const cancelMutation = useCancelReceiptFile();
  const retryMutation = useRetryReceiptFile();

  return (
    <Card>
      <CardHeader className="flex flex-row items-center justify-between gap-2">
        <CardTitle className="truncate text-sm font-medium">
          {fileName}
        </CardTitle>
        {data && <ReceiptFileStatusBadge status={data.status} />}
      </CardHeader>
      <CardContent className="space-y-3">
        {isLoading && <Skeleton className="h-4 w-3/4" />}

        {isError && (
          <Alert variant="destructive">
            <AlertTitle>Status konnte nicht geladen werden</AlertTitle>
            <AlertDescription>
              Bitte laden Sie die Seite neu — der Beleg wird im Hintergrund
              weiterverarbeitet.
            </AlertDescription>
          </Alert>
        )}

        {data &&
          (data.status === "Failed" || data.status === "Cancelled") &&
          data.errorMessage && (
            <Alert variant="destructive">
              <AlertTitle>Verarbeitung fehlgeschlagen</AlertTitle>
              <AlertDescription>{data.errorMessage}</AlertDescription>
            </Alert>
          )}

        {data && data.status === "Completed" && data.receiptId && (
          <Link
            href={`/receipts/${data.receiptId}`}
            className="text-sm text-primary underline-offset-4 hover:underline"
          >
            Beleg ansehen
          </Link>
        )}

        {data && !isTerminal(data.status) && (
          <div className="flex items-center gap-2">
            <Button
              variant="outline"
              size="sm"
              disabled={cancelMutation.isPending}
              onClick={() => cancelMutation.mutate(receiptFileId)}
            >
              {cancelMutation.isPending ? "Abbruch läuft…" : "Abbrechen"}
            </Button>
            <Button
              variant="ghost"
              size="sm"
              disabled={retryMutation.isPending}
              onClick={() => retryMutation.mutate(receiptFileId)}
            >
              {retryMutation.isPending ? "Wird ausgelöst…" : "Erneut versuchen"}
            </Button>
          </div>
        )}
      </CardContent>
    </Card>
  );
}
