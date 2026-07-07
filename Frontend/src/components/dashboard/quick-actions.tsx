"use client";

import Link from "next/link";
import { Upload, BarChart3, CreditCard } from "lucide-react";
import { Button } from "@/components/ui/button";

const secondaryActions = [
  {
    label: "Steuerbericht ansehen",
    href: "/reports",
    icon: BarChart3,
  },
  {
    label: "Credits verwalten",
    href: "/billing",
    icon: CreditCard,
  },
];

export function QuickActions() {
  return (
    <div className="rounded-xl border border-border bg-card shadow-sm overflow-hidden">
      <div className="border-b border-border px-6 py-4">
        <h3 className="text-[15px] font-semibold">Schnellaktionen</h3>
      </div>
      <div className="flex flex-col gap-2.5 p-5">
        <Button
          className="h-11 w-full justify-start gap-3 px-3 text-[13px] font-medium"
          nativeButton={false}
          render={<Link href="/upload" />}
        >
          <Upload className="h-4 w-4" />
          Belege hochladen
        </Button>
        {secondaryActions.map((action) => (
          <Link
            key={action.label}
            href={action.href}
            className="flex items-center gap-3 rounded-[10px] border border-border bg-card p-3 text-[13px] font-medium transition-all hover:border-primary hover:bg-accent"
          >
            <div className="flex h-8 w-8 shrink-0 items-center justify-center rounded-lg bg-muted text-muted-foreground">
              <action.icon className="h-4 w-4" />
            </div>
            {action.label}
          </Link>
        ))}
      </div>
    </div>
  );
}
