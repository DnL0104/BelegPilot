"use client";

import { useState } from "react";
import { Header } from "@/components/layout/header";
import { ReceiptsTable } from "@/components/receipts/receipts-table";
import { YearFilter } from "@/components/receipts/year-filter";

export default function ReceiptsPage() {
  const [year, setYear] = useState<number | undefined>(new Date().getFullYear());

  return (
    <>
      <Header title="Belege" />
      <div className="flex-1 p-6 overflow-auto">
        <div className="rounded-xl border border-border bg-card shadow-sm overflow-hidden">
          <div className="flex items-center justify-between border-b border-border px-6 py-4">
            <h2 className="text-[15px] font-semibold">Alle Belege</h2>
            <YearFilter value={year} onChange={setYear} />
          </div>
          <div className="p-0">
            <ReceiptsTable year={year} />
          </div>
        </div>
      </div>
    </>
  );
}
