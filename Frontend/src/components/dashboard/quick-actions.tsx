"use client";

import Link from "next/link";
import { Upload, BarChart3, Coins } from "lucide-react";

const actions = [
  {
    label: "Neue Belege hochladen",
    href: "/upload",
    icon: Upload,
    iconBg: "bg-emerald-50 text-emerald-600 dark:bg-emerald-500/10 dark:text-emerald-400",
  },
  {
    label: "Steuerbericht ansehen",
    href: "/reports",
    icon: BarChart3,
    iconBg: "bg-blue-50 text-blue-600 dark:bg-blue-500/10 dark:text-blue-400",
  },
  {
    label: "Credits aufladen",
    href: "#credits",
    icon: Coins,
    iconBg: "bg-amber-50 text-amber-600 dark:bg-amber-500/10 dark:text-amber-400",
  },
];

export function QuickActions() {
  return (
    <div className="rounded-xl border border-border bg-card shadow-sm overflow-hidden">
      <div className="border-b border-border px-6 py-4">
        <h3 className="text-[15px] font-semibold">Schnellaktionen</h3>
      </div>
      <div className="flex flex-col gap-2.5 p-5">
        {actions.map((action) => (
          <Link
            key={action.label}
            href={action.href}
            className="flex items-center gap-3 rounded-[10px] border border-border bg-card p-3 text-[13px] font-medium transition-all hover:border-primary hover:bg-accent"
          >
            <div
              className={`flex h-8 w-8 shrink-0 items-center justify-center rounded-lg ${action.iconBg}`}
            >
              <action.icon className="h-4 w-4" />
            </div>
            {action.label}
          </Link>
        ))}
      </div>
    </div>
  );
}
