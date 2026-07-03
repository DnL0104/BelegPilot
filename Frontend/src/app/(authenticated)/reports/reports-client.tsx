"use client";

import { useState } from "react";
import Link from "next/link";
import { Header } from "@/components/layout/header";
import { YearSelector } from "@/components/reports/year-selector";
import { AnnualSummaryCard } from "@/components/reports/annual-summary-card";
import { CategoryBreakdown } from "@/components/reports/category-breakdown";
import { ExportButtons } from "@/components/reports/export-buttons";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { Skeleton } from "@/components/ui/skeleton";
import { Button } from "@/components/ui/button";
import { useAnnualSummary, useCategoryTotals } from "@/hooks/use-reports";

export function ReportsClient() {
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
        <div className="flex flex-wrap items-center justify-between gap-4">
          <h1 className="text-2xl font-semibold tracking-tight text-balance">
            Jahresübersicht {year}
          </h1>
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
          <div className="flex flex-col items-center justify-center rounded-xl border border-border bg-card p-12 text-center shadow-sm">
            <h2 className="text-2xl font-semibold tracking-tight text-balance">
              Keine Daten für {year}
            </h2>
            <p className="mt-2 max-w-sm text-base text-pretty text-muted-foreground">
              Für das Jahr {year} wurden noch keine Belege klassifiziert.
              Laden Sie Belege hoch und bestätigen Sie die Vorschläge.
            </p>
            <Button
              className="mt-6"
              nativeButton={false}
              render={<Link href="/receipts" />}
            >
              Belege verwalten
            </Button>
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

        <p className="border-t border-border pt-4 text-[11px] text-muted-foreground">
          Vorschlag – keine Steuerberatung gemäß §5 StBerG.
        </p>
      </div>
    </>
  );
}
