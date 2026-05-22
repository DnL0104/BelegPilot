"use client";

import { useQuery } from "@tanstack/react-query";
import { queryKeys } from "@/lib/query-keys";
import { getReceipts, getReceiptById } from "@/lib/api-client";

export function useReceipts(
  year?: number,
  options: { refetchInterval?: number | false } = {}
) {
  return useQuery({
    queryKey: queryKeys.receipts.list(year),
    queryFn: () => getReceipts(year),
    refetchInterval: options.refetchInterval,
  });
}

export function useReceiptById(id: string) {
  return useQuery({
    queryKey: queryKeys.receipts.detail(id),
    queryFn: () => getReceiptById(id),
    enabled: !!id,
  });
}
