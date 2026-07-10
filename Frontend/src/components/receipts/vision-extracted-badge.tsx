"use client";

import { ScanEye } from "lucide-react";

/**
 * Provenance badge for receipts processed via the Claude vision fallback
 * (extractionSource === "Vision") instead of OCR/text extraction. Informational
 * only — deliberately muted, not accent-colored, per 05-UI-SPEC.md.
 */
export function VisionExtractedBadge() {
  return (
    <span
      className="inline-flex items-center gap-1 rounded-md bg-muted px-2.5 py-0.5 text-[12px] font-medium text-muted-foreground"
      title="Dieser Beleg wurde per KI-Bildanalyse statt Texterkennung verarbeitet. Bitte prüfen Sie die Artikel besonders sorgfältig."
    >
      <ScanEye className="h-3 w-3" aria-hidden="true" />
      Per Bildanalyse erkannt
    </span>
  );
}
