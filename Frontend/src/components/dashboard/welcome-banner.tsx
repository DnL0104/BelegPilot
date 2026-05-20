"use client";

import Link from "next/link";
import { Plus } from "lucide-react";
import { useAuth } from "@/providers/auth-provider";

interface WelcomeBannerProps {
  unclassifiedCount: number;
}

export function WelcomeBanner({ unclassifiedCount }: WelcomeBannerProps) {
  const { user } = useAuth();
  const firstName = user?.displayName.split(" ")[0] ?? "";

  return (
    <div className="flex items-center justify-between rounded-xl bg-gradient-to-r from-navy-950 to-navy-800 px-6 py-5 text-white">
      <div>
        <h2 className="text-[22px] font-bold tracking-tight">
          Willkommen, {firstName}
        </h2>
        <p className="mt-1 text-white/65 text-[14px]">
          {unclassifiedCount > 0
            ? `Du hast ${unclassifiedCount} unklassifizierte Artikel. Lade Belege hoch um loszulegen.`
            : "Alle Artikel sind klassifiziert. Gut gemacht!"}
        </p>
      </div>
      <Link
        href="/upload"
        className="inline-flex shrink-0 items-center gap-2 rounded-lg bg-emerald-600 px-5 py-2.5 text-[14px] font-semibold text-white transition-colors hover:bg-emerald-700"
      >
        <Plus className="h-4 w-4" />
        Belege hochladen
      </Link>
    </div>
  );
}
