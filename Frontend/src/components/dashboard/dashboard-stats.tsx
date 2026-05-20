"use client";

import { FileText, Receipt, DollarSign, AlertCircle } from "lucide-react";
import { Skeleton } from "@/components/ui/skeleton";
import { useAnnualSummary } from "@/hooks/use-reports";
import { useReceiptFiles } from "@/hooks/use-receipt-files";
import { formatCurrency } from "@/lib/format";

interface DashboardStatsProps {
  year: number;
}

const iconStyles = {
  green: "bg-emerald-50 text-emerald-600 dark:bg-emerald-500/10 dark:text-emerald-400",
  blue: "bg-blue-50 text-blue-600 dark:bg-blue-500/10 dark:text-blue-400",
  purple: "bg-purple-50 text-purple-600 dark:bg-purple-500/10 dark:text-purple-400",
  amber: "bg-amber-50 text-amber-600 dark:bg-amber-500/10 dark:text-amber-400",
};

export function DashboardStats({ year }: DashboardStatsProps) {
  const { data: summary, isLoading: summaryLoading } = useAnnualSummary(year);
  const { data: files, isLoading: filesLoading } = useReceiptFiles();

  const stats = [
    {
      title: "Hochgeladene Dateien",
      value: files?.length ?? 0,
      icon: FileText,
      color: "green" as const,
      loading: filesLoading,
    },
    {
      title: "Verarbeitete Belege",
      value: summary?.totalReceipts ?? 0,
      icon: Receipt,
      color: "blue" as const,
      loading: summaryLoading,
    },
    {
      title: `Gesamtbetrag ${year}`,
      value: formatCurrency(summary?.totalAmount ?? 0),
      icon: DollarSign,
      color: "purple" as const,
      loading: summaryLoading,
    },
    {
      title: "Offene Klassifizierungen",
      value: summary?.unclassifiedItemCount ?? 0,
      icon: AlertCircle,
      color: "amber" as const,
      loading: summaryLoading,
      highlight: (summary?.unclassifiedItemCount ?? 0) > 0,
    },
  ];

  return (
    <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
      {stats.map((stat) => (
        <div
          key={stat.title}
          className="rounded-xl border border-border bg-card p-5 shadow-sm transition-shadow hover:shadow-md"
        >
          <div className="flex items-center justify-between mb-3">
            <span className="text-[13px] font-medium text-muted-foreground">
              {stat.title}
            </span>
            <div
              className={`flex h-9 w-9 items-center justify-center rounded-[10px] ${iconStyles[stat.color]}`}
            >
              <stat.icon className="h-[18px] w-[18px]" />
            </div>
          </div>
          {stat.loading ? (
            <Skeleton className="h-8 w-24" />
          ) : (
            <>
              <div className="text-[28px] font-bold tracking-tight">
                {stat.value}
              </div>
              {"highlight" in stat && stat.highlight && (
                <p className="mt-1 text-[12px] font-medium text-amber-500">
                  Aktion erforderlich
                </p>
              )}
            </>
          )}
        </div>
      ))}
    </div>
  );
}
