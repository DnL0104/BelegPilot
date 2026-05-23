"use client";

import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { queryKeys } from "@/lib/query-keys";
import {
  getReceiptItems,
  confirmClassification,
  batchConfirmClassifications,
  reclassifyReceipt,
  getPendingSuggestions,
  saveClassificationRule,
  type SaveRulePayload,
} from "@/lib/api-client";

export function useReceiptItems(receiptId: string) {
  return useQuery({
    queryKey: queryKeys.receipts.items(receiptId),
    queryFn: () => getReceiptItems(receiptId),
    enabled: !!receiptId,
  });
}

export function usePendingSuggestions() {
  return useQuery({
    queryKey: queryKeys.classifications.pendingSuggestions,
    queryFn: getPendingSuggestions,
  });
}

export function useConfirmClassification() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({
      itemId,
      category,
    }: {
      itemId: string;
      category: string;
    }) => confirmClassification(itemId, category),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: queryKeys.receipts.all });
      queryClient.invalidateQueries({ queryKey: queryKeys.classifications.pendingSuggestions });
    },
  });
}

export function useBatchConfirm() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (itemIds: string[]) => batchConfirmClassifications(itemIds),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: queryKeys.receipts.all });
      queryClient.invalidateQueries({ queryKey: queryKeys.classifications.pendingSuggestions });
    },
  });
}

export function useReclassifyReceipt() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (receiptId: string) => reclassifyReceipt(receiptId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: queryKeys.receipts.all });
      queryClient.invalidateQueries({ queryKey: queryKeys.classifications.pendingSuggestions });
    },
  });
}

export function useSaveClassificationRule() {
  return useMutation({
    mutationFn: ({
      itemId,
      payload,
    }: {
      itemId: string;
      payload: SaveRulePayload;
    }) => saveClassificationRule(itemId, payload),
    // Rules don't change existing classifications — no cache invalidation needed.
    // The toast in the dialog signals success to the user.
  });
}
