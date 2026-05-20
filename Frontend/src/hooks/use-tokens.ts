"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { queryKeys } from "@/lib/query-keys";
import {
  getTokenBalance,
  getTokenTransactions,
  purchaseTokens,
} from "@/lib/api-client";

export function useTokenBalance() {
  return useQuery({
    queryKey: queryKeys.tokens.balance,
    queryFn: getTokenBalance,
    refetchInterval: 30_000,
  });
}

export function useTokenTransactions(take = 20) {
  return useQuery({
    queryKey: queryKeys.tokens.transactions(take),
    queryFn: () => getTokenTransactions(take),
  });
}

export function usePurchaseTokens() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (amount: number) => purchaseTokens(amount),
    onSuccess: (data) => {
      queryClient.setQueryData(queryKeys.tokens.balance, data);
      queryClient.invalidateQueries({ queryKey: ["tokens"] });
    },
  });
}
