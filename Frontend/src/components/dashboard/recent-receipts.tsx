"use client";

import Link from "next/link";
import { ArrowRight } from "lucide-react";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import { Skeleton } from "@/components/ui/skeleton";
import { useReceipts } from "@/hooks/use-receipts";
import { formatCurrency, formatDate } from "@/lib/format";

const vendorColors: Record<string, string> = {
  Amazon: "bg-emerald-500",
  Eduki: "bg-blue-500",
  Thalia: "bg-purple-500",
};

export function RecentReceipts() {
  const { data: receipts, isLoading } = useReceipts();
  const recent = receipts?.slice(0, 5) ?? [];

  return (
    <div className="rounded-xl border border-border bg-card shadow-sm overflow-hidden">
      <div className="flex items-center justify-between border-b border-border px-6 py-4">
        <h3 className="text-[15px] font-semibold">Letzte Belege</h3>
        <Link
          href="/receipts"
          className="inline-flex items-center gap-1 text-[13px] font-medium text-primary hover:underline"
        >
          Alle anzeigen
          <ArrowRight className="h-3.5 w-3.5" />
        </Link>
      </div>
      {isLoading ? (
        <div className="space-y-3 p-6">
          {Array.from({ length: 3 }).map((_, i) => (
            <Skeleton key={i} className="h-10 w-full" />
          ))}
        </div>
      ) : recent.length === 0 ? (
        <div className="p-6">
          <p className="text-sm text-muted-foreground">
            Noch keine Belege vorhanden. Lade PDFs hoch, um loszulegen.
          </p>
        </div>
      ) : (
        <Table>
          <TableHeader>
            <TableRow className="bg-muted/50">
              <TableHead className="text-[12px] font-semibold uppercase tracking-wider text-muted-foreground">
                Anbieter
              </TableHead>
              <TableHead className="text-[12px] font-semibold uppercase tracking-wider text-muted-foreground">
                Datum
              </TableHead>
              <TableHead className="text-right text-[12px] font-semibold uppercase tracking-wider text-muted-foreground">
                Artikel
              </TableHead>
              <TableHead className="text-right text-[12px] font-semibold uppercase tracking-wider text-muted-foreground">
                Betrag
              </TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {recent.map((receipt) => (
              <TableRow
                key={receipt.id}
                className="cursor-pointer transition-colors hover:bg-muted/30"
                onClick={() => {
                  window.location.href = `/receipts/${receipt.id}`;
                }}
              >
                <TableCell>
                  <div className="flex items-center gap-2.5">
                    <div
                      className={`h-2 w-2 rounded-full ${vendorColors[receipt.vendor] ?? "bg-muted-foreground"}`}
                    />
                    <span className="font-medium">{receipt.vendor}</span>
                  </div>
                </TableCell>
                <TableCell className="text-muted-foreground">
                  {formatDate(receipt.purchaseDate)}
                </TableCell>
                <TableCell className="text-right">
                  {receipt.itemCount}
                </TableCell>
                <TableCell className="text-right font-semibold">
                  {formatCurrency(receipt.totalAmount)}
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      )}
    </div>
  );
}
