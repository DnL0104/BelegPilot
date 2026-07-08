"use client";

import { Skeleton } from "@/components/ui/skeleton";
import { formatCurrency, categoryLabel } from "@/lib/format";
import type { CategoryTotal } from "@/types/api";

interface CategoryBreakdownProps {
  categories?: CategoryTotal[];
  isLoading: boolean;
}

const categoryColors: Record<string, string> = {
  WerbungskostenArbeitsmittel: "bg-emerald-500",
  WerbungskostenFachliteratur: "bg-purple-500",
  WerbungskostenBueromaterial: "bg-blue-500",
  WerbungskostenReisekosten: "bg-orange-500",
  WerbungskostenFortbildung: "bg-indigo-500",
  WerbungskostenTelekommunikation: "bg-cyan-500",
  SonderausgabenSpenden: "bg-rose-500",
  SonderausgabenVorsorgeaufwendungen: "bg-amber-500",
  AussergewoehnlicheBelastungenKrankheit: "bg-red-500",
  HaushaltsnaheDienstleistung: "bg-lime-500",
  Handwerkerleistung: "bg-yellow-500",
  Privat: "bg-slate-500",
  Unbekannt: "bg-muted-foreground",
};

export function CategoryBreakdown({
  categories,
  isLoading,
}: CategoryBreakdownProps) {
  if (isLoading) {
    return (
      <div className="grid gap-4 md:grid-cols-3">
        {Array.from({ length: 3 }).map((_, i) => (
          <div key={i} className="rounded-xl border border-border bg-card p-6 shadow-sm">
            <Skeleton className="h-20 w-full" />
          </div>
        ))}
      </div>
    );
  }

  if (!categories || categories.length === 0) {
    return (
      <div className="rounded-xl border border-border bg-card p-6 shadow-sm">
        <p className="text-sm text-muted-foreground">
          Keine Kategoriedaten für dieses Jahr vorhanden.
        </p>
      </div>
    );
  }

  const maxAmount = Math.max(...categories.map((c) => c.totalAmount), 1);

  return (
    <>
      {/* Mobile: simple stacked card rows (md:hidden) */}
      <div className="space-y-2 md:hidden">
        {categories.map((cat) => (
          <div
            key={cat.category}
            className="flex items-center justify-between rounded-xl border border-border bg-card p-4 shadow-sm"
          >
            <span className="text-sm font-medium">
              {categoryLabel(cat.category)}
            </span>
            <span className="text-sm font-semibold tabular-nums">
              {formatCurrency(cat.totalAmount)}
            </span>
          </div>
        ))}
      </div>

      {/* Desktop: detailed card grid with progress bar (hidden md:block equivalent) */}
      <div className="hidden gap-4 md:grid md:grid-cols-2 lg:grid-cols-3">
        {categories.map((cat) => (
          <div
            key={cat.category}
            className="rounded-xl border border-border bg-card p-5 shadow-sm transition-shadow hover:shadow-md"
          >
            <h4 className="text-[15px] font-semibold mb-3">
              {categoryLabel(cat.category)}
            </h4>
            <div className="text-2xl font-bold tracking-tight tabular-nums mb-1">
              {formatCurrency(cat.totalAmount)}
            </div>
            <p className="text-[13px] text-muted-foreground mb-3">
              {cat.itemCount} Artikel
            </p>
            <div className="h-2 rounded-full bg-muted overflow-hidden">
              <div
                className={`h-full rounded-full transition-all ${categoryColors[cat.category] ?? "bg-muted-foreground"}`}
                style={{
                  width: `${(cat.totalAmount / maxAmount) * 100}%`,
                }}
              />
            </div>
          </div>
        ))}
      </div>
    </>
  );
}
