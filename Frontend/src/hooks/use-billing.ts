"use client";

import { useMutation, useQuery } from "@tanstack/react-query";
import {
  createCheckoutSession,
  createPortalSession,
  getInvoices,
} from "@/lib/api-client";
import type { CreateCheckoutSessionRequest } from "@/types/api";

export function useCreateCheckoutSession() {
  return useMutation({
    mutationFn: (req: CreateCheckoutSessionRequest) => createCheckoutSession(req),
  });
}

export function useInvoices() {
  return useQuery({
    queryKey: ["billing", "invoices"],
    queryFn: getInvoices,
  });
}

export function useCreatePortalSession() {
  return useMutation({
    mutationFn: () => createPortalSession(),
  });
}
