"use client";

import Link from "next/link";
import { Coins } from "lucide-react";
import { SidebarTrigger } from "@/components/ui/sidebar";
import { Separator } from "@/components/ui/separator";
import { ThemeToggle } from "./theme-toggle";
import { TokenBalanceBadge } from "@/components/tokens/token-balance-badge";
import { useTokenBalance } from "@/hooks/use-tokens";

interface HeaderProps {
  title: string;
}

// Mobile-only token badge: links to /billing instead of opening the TopUpDialog.
// The existing <TokenBalanceBadge> is used on desktop where the dialog flow is appropriate.
function MobileTokenLink() {
  const { data, isLoading } = useTokenBalance();
  const balance = data?.balance ?? 0;
  const isNegative = balance < 0;
  const isLow = balance <= 3;

  return (
    <Link
      href="/billing"
      className={`inline-flex items-center gap-1.5 rounded-full border px-3 py-1.5 text-[13px] font-semibold transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary focus-visible:ring-offset-2 ${
        isNegative
          ? "border-destructive/30 bg-destructive/10 text-destructive hover:border-destructive"
          : isLow
          ? "border-amber-500/30 bg-amber-50 text-amber-700 hover:border-amber-500 dark:bg-amber-500/10 dark:text-amber-400"
          : "border-emerald-500/20 bg-emerald-50 text-emerald-700 hover:border-emerald-500 dark:bg-emerald-500/10 dark:text-emerald-400"
      }`}
      aria-label={`${isLoading ? "Credits laden" : `${balance} Credits`} – Credits & Abrechnung öffnen`}
    >
      <Coins className="h-4 w-4" aria-hidden="true" />
      {isLoading ? "…" : `${balance} Credits`}
    </Link>
  );
}

export function Header({ title }: HeaderProps) {
  return (
    <header className="flex h-14 shrink-0 items-center border-b bg-card">
      {/* Mobile (< md): hamburger | BelegPilot wordmark | token badge → /billing */}
      <div className="flex flex-1 items-center justify-between px-4 md:hidden">
        <div className="flex items-center gap-2">
          <SidebarTrigger aria-label="Menü öffnen" />
          <Link href="/" className="flex items-center gap-2">
            <div className="flex size-7 items-center justify-center rounded-lg bg-primary text-xs font-bold text-white">
              B
            </div>
            <span className="text-base font-bold">BelegPilot</span>
          </Link>
        </div>
        <MobileTokenLink />
      </div>

      {/* Desktop (≥ md): SidebarTrigger | separator | page title | token badge | theme toggle */}
      <div className="hidden flex-1 items-center justify-between px-6 md:flex">
        <div className="flex items-center gap-2">
          <SidebarTrigger aria-label="Menü öffnen" className="-ml-1" />
          <Separator orientation="vertical" className="mr-2 !h-4" />
          <h1 className="text-lg font-bold tracking-tight">{title}</h1>
        </div>
        <div className="flex items-center gap-3">
          <TokenBalanceBadge />
          <ThemeToggle />
        </div>
      </div>
    </header>
  );
}
