"use client";

import { useState } from "react";
import { toast } from "sonner";
import {
  Loader2,
  Upload,
  CheckCircle2,
  AlertCircle,
  FileText,
  Trash2,
  ExternalLink,
} from "lucide-react";
import Link from "next/link";
import { Button } from "@/components/ui/button";
import { FileDropzone } from "./file-dropzone";
import {
  useUploadFiles,
  useDeleteFile,
} from "@/hooks/use-receipt-files";
import type { Receipt } from "@/types/api";
import { formatFileSize, formatCurrency, formatDate } from "@/lib/format";

type FileState = "processing" | "done" | "error";

interface FileResult {
  id: string;
  fileName: string;
  fileSize: number;
  state: FileState;
  receipt?: Receipt;
  errorMessage?: string;
}

export function UploadForm() {
  const [files, setFiles] = useState<File[]>([]);
  const [results, setResults] = useState<FileResult[]>([]);

  const uploadMutation = useUploadFiles();
  const deleteMutation = useDeleteFile();

  const handleUpload = async () => {
    if (files.length === 0) {
      toast.error("Bitte mindestens eine Datei auswählen");
      return;
    }

    const filesToUpload = files;
    setFiles([]);

    // Show placeholder cards while the server processes the files
    const placeholders: FileResult[] = filesToUpload.map((f) => ({
      id: f.name + f.size,
      fileName: f.name,
      fileSize: f.size,
      state: "processing",
    }));
    setResults((prev) => [...placeholders, ...prev]);

    try {
      const response = await uploadMutation.mutateAsync(filesToUpload);

      // Build final per-file results from the batch response.
      // Match file size back to the original File[] by name — safe enough for
      // the common case; falls back to 0 if we can't find it.
      const successResults: FileResult[] = response.successful.map(
        ({ fileName, receipt }) => ({
          id: receipt.receiptFileId,
          fileName,
          fileSize: filesToUpload.find((f) => f.name === fileName)?.size ?? 0,
          state: "done",
          receipt,
        })
      );

      const failureResults: FileResult[] = response.failed.map((fail, i) => ({
        id: `failed-${Date.now()}-${i}`,
        fileName: fail.fileName,
        fileSize: filesToUpload.find((f) => f.name === fail.fileName)?.size ?? 0,
        state: "error",
        errorMessage: fail.reason,
      }));

      // Replace placeholders with the combined results
      setResults((prev) => {
        const placeholderIds = new Set(placeholders.map((p) => p.id));
        const kept = prev.filter((r) => !placeholderIds.has(r.id));
        return [...successResults, ...failureResults, ...kept];
      });

      // Summarise the batch outcome via toast
      const okCount = response.successful.length;
      const failCount = response.failed.length;

      if (okCount > 0 && failCount === 0) {
        toast.success(
          `${okCount} Beleg${okCount > 1 ? "e" : ""} erfolgreich verarbeitet`
        );
      } else if (okCount === 0 && failCount > 0) {
        const allDuplicates = response.failed.every(
          (f) => f.kind === "Duplicate"
        );
        toast.error(
          allDuplicates
            ? failCount > 1
              ? `${failCount} Dateien bereits hochgeladen`
              : "Datei wurde bereits hochgeladen"
            : `Verarbeitung fehlgeschlagen (${failCount} Datei${failCount > 1 ? "en" : ""})`
        );
      } else {
        // Mixed outcome — the cards tell the full story; toast is a brief summary
        toast(
          `${okCount} erfolgreich, ${failCount} fehlgeschlagen`,
          { description: "Details siehe unten" }
        );
      }
    } catch (err: unknown) {
      // Network error / 5xx — no usable response body; drop placeholders and
      // put the files back so the user can retry.
      setResults((prev) => {
        const placeholderIds = new Set(placeholders.map((p) => p.id));
        return prev.filter((r) => !placeholderIds.has(r.id));
      });

      const axiosErr = err as {
        response?: { data?: { error?: string } };
      };
      const serverMsg = axiosErr?.response?.data?.error;
      toast.error(
        serverMsg ?? "Verarbeitung fehlgeschlagen. Bitte erneut versuchen."
      );
      setFiles(filesToUpload);
    }
  };

  const handleRemoveResult = async (result: FileResult) => {
    // For successful uploads we also delete the underlying receipt file on the
    // backend. Error cards carry a synthetic id (no real file to delete — either
    // a duplicate of an existing file, or a processing failure the user can
    // clean up via the receipts list) — just dismiss them from the UI.
    if (result.state === "done") {
      try {
        await deleteMutation.mutateAsync(result.id);
        setResults((prev) => prev.filter((r) => r.id !== result.id));
        toast.success("Datei entfernt");
      } catch {
        toast.error("Entfernen fehlgeschlagen");
      }
    } else {
      setResults((prev) => prev.filter((r) => r.id !== result.id));
    }
  };

  return (
    <div className="mx-auto w-full max-w-3xl space-y-5">
      <div className="rounded-xl border border-border bg-card p-6 shadow-sm">
        <div className="mb-5">
          <h2 className="text-lg font-semibold">Belege hochladen</h2>
          <p className="mt-1 text-sm text-muted-foreground">
            Wir erkennen Anbieter, Datum und Beträge automatisch – kein Ausfüllen nötig.
          </p>
        </div>
        <div className="space-y-5">
          <FileDropzone files={files} onFilesChange={setFiles} />
          <Button
            onClick={handleUpload}
            disabled={files.length === 0 || uploadMutation.isPending}
            className="w-full sm:w-auto"
            size="lg"
          >
            {uploadMutation.isPending ? (
              <Loader2 className="mr-2 h-4 w-4 animate-spin" />
            ) : (
              <Upload className="mr-2 h-4 w-4" />
            )}
            {files.length > 0
              ? `${files.length} Datei${files.length > 1 ? "en" : ""} hochladen & verarbeiten`
              : "Hochladen & verarbeiten"}
          </Button>
        </div>
      </div>

      {results.length > 0 && (
        <div className="space-y-3">
          <h3 className="px-1 text-[13px] font-semibold uppercase tracking-wide text-muted-foreground">
            Zuletzt verarbeitet
          </h3>
          {results.map((r) => (
            <ResultCard
              key={r.id}
              result={r}
              onRemove={() => handleRemoveResult(r)}
              removing={deleteMutation.isPending}
            />
          ))}
        </div>
      )}
    </div>
  );
}

function ResultCard({
  result,
  onRemove,
  removing,
}: {
  result: FileResult;
  onRemove: () => void;
  removing: boolean;
}) {
  const { state, fileName, fileSize, receipt, errorMessage } = result;

  return (
    <div className="rounded-xl border border-border bg-card p-4 shadow-sm">
      <div className="flex items-start gap-4">
        <div
          className={`flex h-10 w-10 shrink-0 items-center justify-center rounded-lg ${
            state === "done"
              ? "bg-emerald-100 text-emerald-700 dark:bg-emerald-950/50 dark:text-emerald-400"
              : state === "error"
                ? "bg-rose-100 text-rose-700 dark:bg-rose-950/50 dark:text-rose-400"
                : "bg-blue-100 text-blue-700 dark:bg-blue-950/50 dark:text-blue-400"
          }`}
        >
          {state === "processing" && <Loader2 className="h-5 w-5 animate-spin" />}
          {state === "done" && <CheckCircle2 className="h-5 w-5" />}
          {state === "error" && <AlertCircle className="h-5 w-5" />}
        </div>

        <div className="min-w-0 flex-1">
          <div className="flex items-start justify-between gap-3">
            <div className="min-w-0">
              <p className="truncate font-medium">{fileName}</p>
              <p className="text-xs text-muted-foreground">
                <FileText className="mr-1 inline h-3 w-3" />
                {formatFileSize(fileSize)}
              </p>
            </div>
            <Button
              variant="ghost"
              size="icon"
              className="h-8 w-8 shrink-0 text-muted-foreground hover:text-destructive"
              onClick={onRemove}
              disabled={removing || state === "processing"}
              aria-label="Entfernen"
            >
              <Trash2 className="h-4 w-4" />
            </Button>
          </div>

          {state === "processing" && (
            <p className="mt-2 text-sm text-muted-foreground">
              Wird verarbeitet …
            </p>
          )}

          {state === "error" && (
            <p className="mt-2 text-sm text-rose-600 dark:text-rose-400">
              {errorMessage}
            </p>
          )}

          {state === "done" && receipt && (
            <div className="mt-3 flex flex-wrap items-center gap-x-5 gap-y-2 text-sm">
              <div>
                <span className="text-muted-foreground">Anbieter: </span>
                <span className="font-medium">{receipt.vendor}</span>
              </div>
              <div>
                <span className="text-muted-foreground">Datum: </span>
                <span className="font-medium">{formatDate(receipt.purchaseDate)}</span>
              </div>
              <div>
                <span className="text-muted-foreground">Betrag: </span>
                <span className="font-semibold">{formatCurrency(receipt.totalAmount)}</span>
              </div>
              <div>
                <span className="text-muted-foreground">Artikel: </span>
                <span className="font-medium">{receipt.itemCount}</span>
              </div>
              <Link
                href={`/receipts/${receipt.id}`}
                className="ml-auto inline-flex items-center gap-1 text-sm font-medium text-primary hover:underline"
              >
                Öffnen
                <ExternalLink className="h-3.5 w-3.5" />
              </Link>
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
