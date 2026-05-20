"use client";

import { Receipt, DollarSign, AlertCircle } from "lucide-react";
import { Skeleton } from "@/components/ui/skeleton";
import { formatCurrency } from "@/lib/format";
import type { AnnualSummary } from "@/types/api";

interface AnnualSummaryCardProps {
  summary?: AnnualSummary;
  isLoading: boolean;
}

const iconStyles = {
  green: "bg-emerald-50 text-emerald-600 dark:bg-emerald-500/10 dark:text-emerald-400",
  purple: "bg-purple-50 text-purple-600 dark:bg-purple-500/10 dark:text-purple-400",
  amber: "bg-amber-50 text-amber-600 dark:bg-amber-500/10 dark:text-amber-400",
};

export function AnnualSummaryCard({
  summary,
  isLoading,
}: AnnualSummaryCardProps) {
  const stats = [
    {
      title: "Belege gesamt",
      value: summary?.totalReceipts ?? 0,
      icon: Receipt,
      color: "green" as const,
    },
    {
      title: "Gesamtbetrag",
      value: formatCurrency(summary?.totalAmount ?? 0),
      icon: DollarSign,
      color: "purple" as const,
    },
    {
      title: "Offene Klassifizierungen",
      value: summary?.unclassifiedItemCount ?? 0,
      icon: AlertCircle,
      color: "amber" as const,
    },
  ];

  return (
    <div className="grid gap-4 md:grid-cols-3">
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
          {isLoading ? (
            <Skeleton className="h-8 w-24" />
          ) : (
            <div className="text-[28px] font-bold tracking-tight">
              {stat.value}
            </div>
          )}
        </div>
      ))}
    </div>
  );
}
