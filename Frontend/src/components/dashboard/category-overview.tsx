"use client";

import { Skeleton } from "@/components/ui/skeleton";
import { useCategoryTotals } from "@/hooks/use-reports";
import { formatCurrency, categoryLabel } from "@/lib/format";

interface CategoryOverviewProps {
  year: number;
}

const categoryBarColors: Record<string, string> = {
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

export function CategoryOverview({ year }: CategoryOverviewProps) {
  const { data: categories, isLoading } = useCategoryTotals(year);

  const maxAmount = Math.max(
    ...(categories?.map((c) => c.totalAmount) ?? []),
    1
  );

  return (
    <div className="rounded-xl border border-border bg-card shadow-sm overflow-hidden">
      <div className="border-b border-border px-6 py-4">
        <h3 className="text-[15px] font-semibold">Kategorien {year}</h3>
      </div>
      <div className="px-6 py-4 space-y-0">
        {isLoading ? (
          <div className="space-y-4">
            {Array.from({ length: 3 }).map((_, i) => (
              <Skeleton key={i} className="h-8 w-full" />
            ))}
          </div>
        ) : !categories || categories.length === 0 ? (
          <p className="text-sm text-muted-foreground py-2">
            Keine Kategoriedaten für {year} vorhanden.
          </p>
        ) : (
          categories.map((cat) => (
            <div
              key={cat.category}
              className="flex items-center gap-3 border-b border-border py-3 last:border-b-0"
            >
              <span className="w-40 shrink-0 text-[13px] font-medium">
                {categoryLabel(cat.category)}
              </span>
              <div className="flex-1 h-2 rounded-full bg-muted overflow-hidden">
                <div
                  className={`h-full rounded-full transition-all ${categoryBarColors[cat.category] ?? "bg-muted-foreground"}`}
                  style={{
                    width: `${(cat.totalAmount / maxAmount) * 100}%`,
                  }}
                />
              </div>
              <span className="w-20 shrink-0 text-right text-[13px] font-semibold">
                {formatCurrency(cat.totalAmount)}
              </span>
            </div>
          ))
        )}
      </div>
    </div>
  );
}
