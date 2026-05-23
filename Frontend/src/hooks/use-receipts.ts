"use client";

import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { queryKeys } from "@/lib/query-keys";
import { getReceipts, getReceiptById, acknowledgeSumMismatch } from "@/lib/api-client";

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

export function useAcknowledgeSumMismatch() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (receiptId: string) => acknowledgeSumMismatch(receiptId),
    onSuccess: (_data, receiptId) => {
      queryClient.invalidateQueries({ queryKey: queryKeys.receipts.detail(receiptId) });
      queryClient.invalidateQueries({ queryKey: queryKeys.receipts.all });
    },
  });
}
