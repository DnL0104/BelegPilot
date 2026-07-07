"use client";

import { useState } from "react";
import { toast } from "sonner";
import { Loader2, Upload } from "lucide-react";
import { Button } from "@/components/ui/button";
import { FileDropzone } from "./file-dropzone";
import { ReceiptFileCard } from "./receipt-file-card";
import { useUploadFiles } from "@/hooks/use-receipt-files";
import type { UploadAcceptedFile } from "@/types/api";

export function UploadForm() {
  const [files, setFiles] = useState<File[]>([]);
  const [uploadedFiles, setUploadedFiles] = useState<UploadAcceptedFile[]>([]);

  const uploadMutation = useUploadFiles();

  const handleUpload = async () => {
    if (files.length === 0) {
      toast.error("Bitte mindestens eine Datei auswählen");
      return;
    }

    const filesToUpload = files;
    setFiles([]);

    try {
      const response = await uploadMutation.mutateAsync(filesToUpload);
      setUploadedFiles((prev) => [...response.files, ...prev]);

      if (response.files.length > 0) {
        toast.success(
          `${response.files.length} Beleg${response.files.length > 1 ? "e" : ""} werden verarbeitet`
        );
      }

      response.duplicates.forEach((duplicate) => {
        toast.error(`"${duplicate.fileName}" übersprungen: ${duplicate.reason}`);
      });
    } catch (err: unknown) {
      const axiosErr = err as {
        response?: { data?: { error?: string } };
      };
      const serverMsg = axiosErr?.response?.data?.error;
      toast.error(
        serverMsg ?? "Upload fehlgeschlagen. Bitte erneut versuchen."
      );
      setFiles(filesToUpload);
    }
  };

  return (
    <div className="mx-auto w-full max-w-3xl space-y-5">
      <div className="rounded-xl border border-border bg-card p-6 shadow-sm">
        <div className="mb-5">
          <p className="text-sm text-muted-foreground">
            PDF, JPG, PNG – maximal 10 MB pro Datei
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
            {uploadMutation.isPending ? "Wird hochgeladen…" : "Hochladen"}
          </Button>
        </div>
      </div>

      {uploadedFiles.length > 0 && (
        <div className="space-y-3">
          <h3 className="px-1 text-[13px] font-semibold uppercase tracking-wide text-muted-foreground">
            Zuletzt hochgeladen
          </h3>
          {uploadedFiles.map((f) => (
            <ReceiptFileCard
              key={f.receiptFileId}
              receiptFileId={f.receiptFileId}
              fileName={f.fileName}
            />
          ))}
        </div>
      )}
    </div>
  );
}
