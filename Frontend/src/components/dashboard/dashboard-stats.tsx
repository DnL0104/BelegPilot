"use client";

import { Receipt, CheckCircle2, Clock } from "lucide-react";
import { Skeleton } from "@/components/ui/skeleton";
import { useAnnualSummary } from "@/hooks/use-reports";

interface DashboardStatsProps {
  year: number;
}

const iconStyles = {
  primary: "bg-primary/10 text-primary",
  success:
    "bg-emerald-50 text-emerald-600 dark:bg-emerald-500/10 dark:text-emerald-400",
  muted: "bg-muted text-muted-foreground",
};

export function DashboardStats({ year }: DashboardStatsProps) {
  const { data: summary, isLoading } = useAnnualSummary(year);

  const confirmedCount =
    summary?.categoryBreakdown.reduce((sum, c) => sum + c.itemCount, 0) ?? 0;
  const openCount = summary?.unclassifiedItemCount ?? 0;

  const stats = [
    {
      title: "Belege gesamt",
      value: summary?.totalReceipts ?? 0,
      icon: Receipt,
      color: "primary" as const,
    },
    {
      title: "Bestätigt",
      value: confirmedCount,
      icon: CheckCircle2,
      color: "success" as const,
    },
    {
      title: "Offen",
      value: openCount,
      icon: Clock,
      color: "muted" as const,
      highlight: openCount > 0,
    },
  ];

  return (
    <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
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
            <>
              <div className="text-[28px] font-bold tracking-tight tabular-nums">
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
