"use client";

import { useState } from "react";
import { Header } from "@/components/layout/header";
import { YearSelector } from "@/components/reports/year-selector";
import { AnnualSummaryCard } from "@/components/reports/annual-summary-card";
import { CategoryBreakdown } from "@/components/reports/category-breakdown";
import { ExportButtons } from "@/components/reports/export-buttons";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { Skeleton } from "@/components/ui/skeleton";
import { Button } from "@/components/ui/button";
import { useAnnualSummary, useCategoryTotals } from "@/hooks/use-reports";

export default function ReportsPage() {
  const [year, setYear] = useState(new Date().getFullYear());

  const {
    data: summary,
    isLoading: summaryLoading,
    isError: summaryError,
    refetch: refetchSummary,
  } = useAnnualSummary(year);
  const {
    data: categories,
    isLoading: categoriesLoading,
    isError: categoriesError,
    refetch: refetchCategories,
  } = useCategoryTotals(year);

  const hasData = (summary?.totalReceipts ?? 0) > 0;

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

        {summaryError ? (
          <Alert variant="destructive">
            <AlertTitle>Bericht konnte nicht geladen werden</AlertTitle>
            <AlertDescription>
              Bitte versuchen Sie es erneut.
            </AlertDescription>
            <Button
              variant="outline"
              size="sm"
              className="mt-2"
              onClick={() => {
                refetchSummary();
                refetchCategories();
              }}
            >
              Erneut versuchen
            </Button>
          </Alert>
        ) : summaryLoading ? (
          <Skeleton className="h-32 w-full" />
        ) : !hasData ? (
          <div className="rounded-xl border border-border bg-card p-6 shadow-sm">
            <p className="text-sm text-muted-foreground">
              Für dieses Jahr liegen noch keine bestätigten Belege vor. Bestätigen
              Sie zunächst Klassifizierungen unter Belege.
            </p>
          </div>
        ) : (
          <AnnualSummaryCard summary={summary} isLoading={summaryLoading} />
        )}

        {!summaryError && !summaryLoading && hasData && (
          <div className="space-y-4">
            <h3 className="text-lg font-semibold">Kategorien</h3>
            {categoriesError ? (
              <Alert variant="destructive">
                <AlertTitle>Kategorien konnten nicht geladen werden</AlertTitle>
                <AlertDescription>
                  Bitte versuchen Sie es erneut.
                </AlertDescription>
                <Button
                  variant="outline"
                  size="sm"
                  className="mt-2"
                  onClick={() => refetchCategories()}
                >
                  Erneut versuchen
                </Button>
              </Alert>
            ) : (
              <CategoryBreakdown
                categories={categories}
                isLoading={categoriesLoading}
              />
            )}
          </div>
        )}
      </div>
    </>
  );
}
