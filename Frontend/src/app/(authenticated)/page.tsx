"use client";

import { Header } from "@/components/layout/header";
import { WelcomeBanner } from "@/components/dashboard/welcome-banner";
import { DashboardStats } from "@/components/dashboard/dashboard-stats";
import { RecentReceipts } from "@/components/dashboard/recent-receipts";
import { CategoryOverview } from "@/components/dashboard/category-overview";
import { QuickActions } from "@/components/dashboard/quick-actions";
import { PendingSuggestions } from "@/components/dashboard/pending-suggestions";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { Skeleton } from "@/components/ui/skeleton";
import { Button } from "@/components/ui/button";
import { useAnnualSummary } from "@/hooks/use-reports";
import { useReceipts } from "@/hooks/use-receipts";

export default function DashboardPage() {
  const currentYear = new Date().getFullYear();
  const {
    data: summary,
    isLoading: summaryLoading,
    isError: summaryError,
    refetch: refetchSummary,
  } = useAnnualSummary(currentYear);
  const { data: receipts, isLoading: receiptsLoading } = useReceipts();

  const hasReceipts = (receipts?.length ?? 0) > 0;

  return (
    <>
      <Header title="Dashboard" />
      <div className="flex-1 space-y-5 p-6 overflow-auto">
        <WelcomeBanner
          unclassifiedCount={summary?.unclassifiedItemCount ?? 0}
        />

        {summaryError ? (
          <Alert variant="destructive">
            <AlertTitle>Daten konnten nicht geladen werden</AlertTitle>
            <AlertDescription>
              Bitte versuchen Sie es erneut.
            </AlertDescription>
            <Button
              variant="outline"
              size="sm"
              className="mt-2"
              onClick={() => refetchSummary()}
            >
              Erneut versuchen
            </Button>
          </Alert>
        ) : summaryLoading ? (
          <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
            {Array.from({ length: 4 }).map((_, i) => (
              <Skeleton key={i} className="h-24 w-full" />
            ))}
          </div>
        ) : !receiptsLoading && !hasReceipts ? (
          <div className="rounded-xl border border-border bg-card p-6 shadow-sm">
            <p className="text-sm text-muted-foreground">
              Noch keine Belege vorhanden — laden Sie Ihren ersten Beleg hoch.
            </p>
          </div>
        ) : (
          <DashboardStats year={currentYear} />
        )}

        <PendingSuggestions />
        <div className="grid grid-cols-1 gap-5 xl:grid-cols-[2fr_1fr]">
          <RecentReceipts />
          <div className="flex flex-col gap-5">
            <CategoryOverview year={currentYear} />
            <QuickActions />
          </div>
        </div>
      </div>
    </>
  );
}
