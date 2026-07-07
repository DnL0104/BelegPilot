"use client";

import { Euro, CheckCircle2 } from "lucide-react";
import { Skeleton } from "@/components/ui/skeleton";
import { formatCurrency } from "@/lib/format";
import type { AnnualSummary } from "@/types/api";

interface AnnualSummaryCardProps {
  summary?: AnnualSummary;
  isLoading: boolean;
}

const iconStyles = {
  primary: "bg-primary/10 text-primary",
  success:
    "bg-emerald-50 text-emerald-600 dark:bg-emerald-500/10 dark:text-emerald-400",
};

export function AnnualSummaryCard({
  summary,
  isLoading,
}: AnnualSummaryCardProps) {
  const confirmedCount =
    summary?.categoryBreakdown.reduce((sum, c) => sum + c.itemCount, 0) ?? 0;

  const stats = [
    {
      title: "Gesamtausgaben",
      value: formatCurrency(summary?.totalAmount ?? 0),
      icon: Euro,
      color: "primary" as const,
    },
    {
      title: "Bestätigte Positionen",
      value: confirmedCount,
      icon: CheckCircle2,
      color: "success" as const,
    },
  ];

  return (
    <div className="grid gap-4 md:grid-cols-2">
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
            <div className="text-[28px] font-bold tracking-tight tabular-nums">
              {stat.value}
            </div>
          )}
        </div>
      ))}
    </div>
  );
}
