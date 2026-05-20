"use client";

import { Header } from "@/components/layout/header";
import { WelcomeBanner } from "@/components/dashboard/welcome-banner";
import { DashboardStats } from "@/components/dashboard/dashboard-stats";
import { RecentReceipts } from "@/components/dashboard/recent-receipts";
import { CategoryOverview } from "@/components/dashboard/category-overview";
import { QuickActions } from "@/components/dashboard/quick-actions";
import { PendingSuggestions } from "@/components/dashboard/pending-suggestions";
import { useAnnualSummary } from "@/hooks/use-reports";

export default function DashboardPage() {
  const currentYear = new Date().getFullYear();
  const { data: summary } = useAnnualSummary(currentYear);

  return (
    <>
      <Header title="Dashboard" />
      <div className="flex-1 space-y-5 p-6 overflow-auto">
        <WelcomeBanner
          unclassifiedCount={summary?.unclassifiedItemCount ?? 0}
        />
        <DashboardStats year={currentYear} />
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
