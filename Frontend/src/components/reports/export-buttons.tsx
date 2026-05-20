"use client";

import { useState } from "react";
import { toast } from "sonner";
import { Download, FileSpreadsheet, FileText, Loader2 } from "lucide-react";
import { Button } from "@/components/ui/button";
import { getAccessToken } from "@/lib/api-client";

interface ExportButtonsProps {
  year: number;
}

export function ExportButtons({ year }: ExportButtonsProps) {
  const [loadingCsv, setLoadingCsv] = useState(false);
  const [loadingPdf, setLoadingPdf] = useState(false);

  const handleExport = async (format: "csv" | "pdf") => {
    const setLoading = format === "csv" ? setLoadingCsv : setLoadingPdf;
    setLoading(true);

    try {
      const token = getAccessToken();
      const response = await fetch(
        `/api/v1/reports/export/${format}?year=${year}`,
        {
          headers: {
            Authorization: `Bearer ${token}`,
          },
        }
      );

      if (!response.ok) {
        throw new Error(`Export fehlgeschlagen (${response.status})`);
      }

      const blob = await response.blob();
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
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="flex items-center gap-2">
      <Button
        variant="outline"
        size="sm"
        onClick={() => handleExport("csv")}
        disabled={loadingCsv}
      >
        {loadingCsv ? (
          <Loader2 className="mr-1.5 h-3.5 w-3.5 animate-spin" />
        ) : (
          <FileSpreadsheet className="mr-1.5 h-3.5 w-3.5" />
        )}
        CSV
      </Button>
      <Button
        variant="outline"
        size="sm"
        onClick={() => handleExport("pdf")}
        disabled={loadingPdf}
      >
        {loadingPdf ? (
          <Loader2 className="mr-1.5 h-3.5 w-3.5 animate-spin" />
        ) : (
          <FileText className="mr-1.5 h-3.5 w-3.5" />
        )}
        PDF
      </Button>
    </div>
  );
}
