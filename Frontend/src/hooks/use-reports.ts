"use client";

import { useQuery } from "@tanstack/react-query";
import { queryKeys } from "@/lib/query-keys";
import { getCategoryTotals, getAnnualSummary } from "@/lib/api-client";

export function useCategoryTotals(year: number) {
  return useQuery({
    queryKey: queryKeys.reports.categoryTotals(year),
    queryFn: () => getCategoryTotals(year),
    enabled: year > 0,
  });
}

export function useAnnualSummary(year: number) {
  return useQuery({
    queryKey: queryKeys.reports.annualSummary(year),
    queryFn: () => getAnnualSummary(year),
    enabled: year > 0,
  });
}
