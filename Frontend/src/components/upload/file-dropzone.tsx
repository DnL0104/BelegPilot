"use client";

import { useCallback, useRef, useState, type DragEvent, type KeyboardEvent } from "react";
import { Upload, X, FileText, ImageIcon } from "lucide-react";
import { formatFileSize } from "@/lib/format";

const ACCEPTED_TYPES = new Set([
  "application/pdf",
  "image/jpeg",
  "image/png",
  "image/webp",
]);

function isAccepted(file: File) {
  // Prefer MIME type; fall back to extension for browsers that omit it
  if (file.type && ACCEPTED_TYPES.has(file.type)) return true;
  const ext = file.name.split(".").pop()?.toLowerCase() ?? "";
  return ["pdf", "jpg", "jpeg", "png", "webp"].includes(ext);
}

function isImage(file: File) {
  return file.type.startsWith("image/") ||
    ["jpg", "jpeg", "png", "webp"].includes(
      file.name.split(".").pop()?.toLowerCase() ?? ""
    );
}

function deduplicateFiles(existing: File[], incoming: File[]): File[] {
  const existingKeys = new Set(existing.map((f) => `${f.name}:${f.size}`));
  const unique = incoming.filter((f) => !existingKeys.has(`${f.name}:${f.size}`));
  return [...existing, ...unique];
}

interface FileDropzoneProps {
  files: File[];
  onFilesChange: (files: File[]) => void;
}

export function FileDropzone({ files, onFilesChange }: FileDropzoneProps) {
  const [isDragOver, setIsDragOver] = useState(false);
  const inputRef = useRef<HTMLInputElement>(null);

  const handleDragOver = useCallback((e: DragEvent) => {
    e.preventDefault();
    setIsDragOver(true);
  }, []);

  const handleDragLeave = useCallback((e: DragEvent) => {
    e.preventDefault();
    setIsDragOver(false);
  }, []);

  const handleDrop = useCallback(
    (e: DragEvent) => {
      e.preventDefault();
      setIsDragOver(false);
      const droppedFiles = Array.from(e.dataTransfer.files).filter(isAccepted);
      const newFiles = deduplicateFiles(files, droppedFiles);
      onFilesChange(newFiles);
    },
    [files, onFilesChange]
  );

  const handleFileInput = useCallback(
    (e: React.ChangeEvent<HTMLInputElement>) => {
      if (e.target.files) {
        const selected = Array.from(e.target.files);
        const newFiles = deduplicateFiles(files, selected);
        onFilesChange(newFiles);
      }
    },
    [files, onFilesChange]
  );

  const removeFile = useCallback(
    (index: number) => {
      onFilesChange(files.filter((_, i) => i !== index));
    },
    [files, onFilesChange]
  );

  const handleKeyDown = useCallback((e: KeyboardEvent<HTMLDivElement>) => {
    if (e.key === "Enter" || e.key === " ") {
      e.preventDefault();
      inputRef.current?.click();
    }
  }, []);

  return (
    <div className="space-y-4">
      <div
        onDragOver={handleDragOver}
        onDragLeave={handleDragLeave}
        onDrop={handleDrop}
        onClick={() => inputRef.current?.click()}
        onKeyDown={handleKeyDown}
        tabIndex={0}
        role="button"
        aria-label="Datei-Upload-Bereich. Klicken oder Dateien hierher ziehen."
        className={`relative flex cursor-pointer flex-col items-center justify-center rounded-xl border-2 border-dashed p-12 transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary focus-visible:ring-offset-2 ${
          isDragOver
            ? "border-primary bg-primary/5"
            : "border-muted-foreground/25 hover:border-primary/50"
        }`}
      >
        <div className="flex h-14 w-14 items-center justify-center rounded-full bg-primary/10 mb-4">
          <Upload className="h-6 w-6 text-primary" />
        </div>
        <p className="mb-1 text-lg font-semibold">
          {isDragOver
            ? "Dateien hier loslassen"
            : "Dateien hier ablegen oder klicken zum Auswählen"}
        </p>
        <p className="mb-4 text-sm text-muted-foreground">
          PDF, JPG, PNG – maximal 10 MB pro Datei
        </p>
        <span className="inline-flex items-center rounded-lg bg-secondary px-4 py-2 text-sm font-medium text-secondary-foreground">
          Dateien durchsuchen
        </span>
        <input
          ref={inputRef}
          type="file"
          accept=".pdf,.jpg,.jpeg,.png,.webp"
          multiple
          className="hidden"
          onClick={(e) => e.stopPropagation()}
          onChange={handleFileInput}
        />
      </div>

      {files.length > 0 && (
        <div className="space-y-2">
          {files.map((file, index) => (
            <div
              key={`${file.name}-${index}`}
              className="flex items-center gap-3 rounded-xl border border-border p-3"
            >
              <div className="flex h-10 w-10 shrink-0 items-center justify-center rounded-lg bg-blue-50 dark:bg-blue-500/10">
                {isImage(file)
                  ? <ImageIcon className="h-5 w-5 text-blue-600 dark:text-blue-400" />
                  : <FileText className="h-5 w-5 text-blue-600 dark:text-blue-400" />
                }
              </div>
              <div className="flex-1 min-w-0">
                <p className="truncate text-sm font-medium">{file.name}</p>
                <p className="text-xs text-muted-foreground">
                  {formatFileSize(file.size)}
                </p>
              </div>
              <button
                type="button"
                onClick={() => removeFile(index)}
                aria-label={`Datei ${file.name} entfernen`}
                className="flex h-8 w-8 shrink-0 items-center justify-center rounded-lg text-muted-foreground transition-colors hover:bg-destructive/10 hover:text-destructive"
              >
                <X className="h-4 w-4" />
              </button>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}
