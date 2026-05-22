"use client";

import Link from "next/link";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import {
  useCancelReceiptFile,
  useReceiptFileStatus,
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

        {data && data.status === "Completed" && (
          <Link
            href={`/receipts/${receiptFileId}`}
            className="text-sm text-primary underline-offset-4 hover:underline"
          >
            Beleg ansehen
          </Link>
        )}

        {data && !isTerminal(data.status) && (
          <Button
            variant="outline"
            size="sm"
            disabled={cancelMutation.isPending}
            onClick={() => cancelMutation.mutate(receiptFileId)}
          >
            {cancelMutation.isPending ? "Abbruch läuft…" : "Abbrechen"}
          </Button>
        )}
      </CardContent>
    </Card>
  );
}
