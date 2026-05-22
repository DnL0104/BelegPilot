"use client";

import { Badge } from "@/components/ui/badge";
import type { ProcessingStatus } from "@/types/api";

const statusLabels: Record<ProcessingStatus, string> = {
  Pending: "Wartend",
  Queued: "In Warteschlange",
  Extracting: "Text wird erkannt",
  Parsing: "Daten werden gelesen",
  Classifying: "Wird klassifiziert",
  Completed: "Fertig",
  Failed: "Fehlgeschlagen",
  Cancelled: "Abgebrochen",
};

const statusVariants: Record<
  ProcessingStatus,
  "default" | "secondary" | "destructive" | "outline"
> = {
  Pending: "secondary",
  Queued: "secondary",
  Extracting: "default",
  Parsing: "default",
  Classifying: "default",
  Completed: "outline",
  Failed: "destructive",
  Cancelled: "destructive",
};

export function ReceiptFileStatusBadge({
  status,
}: {
  status: ProcessingStatus;
}) {
  return (
    <Badge variant={statusVariants[status]}>{statusLabels[status]}</Badge>
  );
}
