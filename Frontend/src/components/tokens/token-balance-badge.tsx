"use client";

import { useState } from "react";
import { Coins, Plus } from "lucide-react";
import { useTokenBalance } from "@/hooks/use-tokens";
import { TopUpDialog } from "./top-up-dialog";

export function TokenBalanceBadge() {
  const { data, isLoading } = useTokenBalance();
  const [open, setOpen] = useState(false);

  const balance = data?.balance ?? 0;
  const isNegative = balance < 0;
  const isLow = balance <= 3;

  return (
    <>
      <button
        type="button"
        onClick={() => setOpen(true)}
        className={`inline-flex items-center gap-1.5 rounded-full border px-3 py-1.5 text-[13px] font-semibold transition-colors ${
          isNegative
            ? "border-destructive/30 bg-destructive/10 text-destructive hover:border-destructive"
            : isLow
            ? "border-amber-500/30 bg-amber-50 text-amber-700 hover:border-amber-500 dark:bg-amber-500/10 dark:text-amber-400"
            : "border-emerald-500/20 bg-emerald-50 text-emerald-700 hover:border-emerald-500 dark:bg-emerald-500/10 dark:text-emerald-400"
        }`}
      >
        <Coins className="h-4 w-4" />
        {isLoading ? "…" : `${balance} Credits`}
        <Plus className="h-3.5 w-3.5 opacity-60" />
      </button>
      <TopUpDialog open={open} onOpenChange={setOpen} />
    </>
  );
}
