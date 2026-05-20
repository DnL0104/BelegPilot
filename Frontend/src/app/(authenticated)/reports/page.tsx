"use client";

import { useState } from "react";
import { Header } from "@/components/layout/header";
import { YearSelector } from "@/components/reports/year-selector";
import { AnnualSummaryCard } from "@/components/reports/annual-summary-card";
import { CategoryBreakdown } from "@/components/reports/category-breakdown";
import { ExportButtons } from "@/components/reports/export-buttons";
import { useAnnualSummary, useCategoryTotals } from "@/hooks/use-reports";

export default function ReportsPage() {
  const [year, setYear] = useState(new Date().getFullYear());

  const { data: summary, isLoading: summaryLoading } = useAnnualSummary(year);
  const { data: categories, isLoading: categoriesLoading } =
    useCategoryTotals(year);

  return (
    <>
      <Header title="Berichte" />
      <div className="flex-1 space-y-5 p-6 overflow-auto">
        <div className="flex items-center justify-between">
          <h2 className="text-xl font-bold tracking-tight">Jahresbericht</h2>
          <div className="flex items-center gap-3">
            <ExportButtons year={year} />
            <YearSelector value={year} onChange={setYear} />
          </div>
        </div>

        <AnnualSummaryCard summary={summary} isLoading={summaryLoading} />

        <div className="space-y-4">
          <h3 className="text-lg font-semibold">Kategorien</h3>
          <CategoryBreakdown
            categories={categories}
            isLoading={categoriesLoading}
          />
        </div>
      </div>
    </>
  );
}
