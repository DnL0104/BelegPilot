"use client";

import { toast } from "sonner";
import { Loader2 } from "lucide-react";
import { Button } from "@/components/ui/button";
import { useExportReport } from "@/hooks/use-reports";

interface ExportButtonsProps {
  year: number;
}

export function ExportButtons({ year }: ExportButtonsProps) {
  const pdfExport = useExportReport();
  const csvExport = useExportReport();

  const handleExport = async (format: "csv" | "pdf") => {
    const mutation = format === "csv" ? csvExport : pdfExport;

    try {
      const blob = await mutation.mutateAsync({ format, year });
      const url = URL.createObjectURL(blob);
      const a = document.createElement("a");
      a.href = url;
      a.download = `BelegPilot_Export_${year}.${format}`;
      document.body.appendChild(a);
      a.click();
      document.body.removeChild(a);
      URL.revokeObjectURL(url);

      toast.success(
        format === "csv"
          ? "CSV-Export heruntergeladen"
          : "PDF-Export heruntergeladen"
      );
    } catch {
      toast.error("Export fehlgeschlagen. Bitte erneut versuchen.");
    }
  };

  return (
    <div className="flex items-center gap-2">
      <Button
        size="sm"
        onClick={() => handleExport("pdf")}
        disabled={pdfExport.isPending}
      >
        {pdfExport.isPending && (
          <Loader2 className="mr-1.5 h-3.5 w-3.5 animate-spin" />
        )}
        PDF exportieren
      </Button>
      <Button
        variant="outline"
        size="sm"
        onClick={() => handleExport("csv")}
        disabled={csvExport.isPending}
      >
        {csvExport.isPending && (
          <Loader2 className="mr-1.5 h-3.5 w-3.5 animate-spin" />
        )}
        CSV exportieren
      </Button>
    </div>
  );
}
