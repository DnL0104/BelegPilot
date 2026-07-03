"use client";

import Link from "next/link";
import { Upload } from "lucide-react";
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
import { useTokenBalance } from "@/hooks/use-tokens";

export function DashboardClient() {
  const currentYear = new Date().getFullYear();
  const {
    data: summary,
    isLoading: summaryLoading,
    isError: summaryError,
    refetch: refetchSummary,
  } = useAnnualSummary(currentYear);
  const { data: receipts, isLoading: receiptsLoading } = useReceipts();
  const { data: tokenBalance } = useTokenBalance();

  const hasReceipts = (receipts?.length ?? 0) > 0;
  const showEmptyState = !receiptsLoading && !hasReceipts;
  const balance = tokenBalance?.balance ?? 0;

  return (
    <>
      <Header title="Übersicht" />
      <div className="flex-1 space-y-5 p-6 overflow-auto">
        {balance <= 0 ? (
          <Alert variant="destructive">
            <AlertTitle>Keine Credits mehr</AlertTitle>
            <AlertDescription>
              Kaufen Sie Credits, um Belege klassifizieren zu können.{" "}
              <Link href="/billing" className="font-medium underline">
                Credits kaufen
              </Link>
            </AlertDescription>
          </Alert>
        ) : balance <= 3 ? (
          <Alert className="border-amber-200 bg-amber-50 text-amber-800 [&>svg]:text-amber-600 dark:border-amber-500/30 dark:bg-amber-500/10 dark:text-amber-400">
            <AlertTitle>Ihr Guthaben wird knapp</AlertTitle>
            <AlertDescription>
              Jetzt{" "}
              <Link href="/billing" className="font-medium underline">
                Credits kaufen
              </Link>
              .
            </AlertDescription>
          </Alert>
        ) : null}

        {showEmptyState ? (
          <div className="flex flex-col items-center justify-center rounded-xl border border-border bg-card p-12 text-center shadow-sm">
            <Upload className="mb-4 h-12 w-12 text-muted-foreground" />
            <h2 className="text-2xl font-semibold tracking-tight text-balance">
              Willkommen bei BelegPilot
            </h2>
            <p className="mt-2 max-w-sm text-base text-pretty text-muted-foreground">
              Laden Sie Ihre ersten Belege hoch und lassen Sie sie automatisch
              klassifizieren.
            </p>
            <Button
              className="mt-6"
              nativeButton={false}
              render={<Link href="/upload" />}
            >
              Belege hochladen
            </Button>
          </div>
        ) : (
          <>
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
              <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
                {Array.from({ length: 3 }).map((_, i) => (
                  <Skeleton key={i} className="h-24 w-full" />
                ))}
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
          </>
        )}
      </div>
    </>
  );
}
