"use client";

import { SidebarTrigger } from "@/components/ui/sidebar";
import { Separator } from "@/components/ui/separator";
import { ThemeToggle } from "./theme-toggle";
import { TokenBalanceBadge } from "@/components/tokens/token-balance-badge";

interface HeaderProps {
  title: string;
}

export function Header({ title }: HeaderProps) {
  return (
    <header className="flex h-14 shrink-0 items-center justify-between border-b bg-card px-6">
      <div className="flex items-center gap-2">
        <SidebarTrigger className="-ml-1" />
        <Separator orientation="vertical" className="mr-2 !h-4" />
        <h1 className="text-lg font-bold tracking-tight">{title}</h1>
      </div>
      <div className="flex items-center gap-3">
        <TokenBalanceBadge />
        <ThemeToggle />
      </div>
    </header>
  );
}
