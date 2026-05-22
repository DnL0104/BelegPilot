"use client";

import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";
import type { AxiosError } from "axios";
import { queryKeys } from "@/lib/query-keys";
import {
  getReceiptFiles,
  uploadReceiptFiles,
  deleteReceiptFile,
  bulkDeleteReceiptFiles,
  getReceiptFileStatus,
  cancelReceiptFile,
} from "@/lib/api-client";
import { type ReceiptFileStatus, isTerminal } from "@/types/api";

export function useReceiptFiles() {
  return useQuery({
    queryKey: queryKeys.receiptFiles.all,
    queryFn: getReceiptFiles,
  });
}

export function useUploadFiles() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (files: File[]) => uploadReceiptFiles(files),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: queryKeys.receiptFiles.all });
      queryClient.invalidateQueries({ queryKey: queryKeys.receipts.all });
      queryClient.invalidateQueries({ queryKey: queryKeys.classifications.pendingSuggestions });
    },
  });
}

export function useDeleteFile() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (id: string) => deleteReceiptFile(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: queryKeys.receiptFiles.all });
      queryClient.invalidateQueries({ queryKey: queryKeys.receipts.all });
    },
  });
}

export function useBulkDeleteFiles() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (ids: string[]) => bulkDeleteReceiptFiles(ids),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: queryKeys.receiptFiles.all });
      queryClient.invalidateQueries({ queryKey: queryKeys.receipts.all });
    },
  });
}

export function useReceiptFileStatus(
  receiptFileId: string,
  options: { enabled?: boolean } = {}
) {
  const enabled = options.enabled ?? true;
  return useQuery<ReceiptFileStatus>({
    queryKey: ["receiptFileStatus", receiptFileId],
    queryFn: () => getReceiptFileStatus(receiptFileId),
    enabled,
    refetchInterval: (query) =>
      query.state.data && isTerminal(query.state.data.status)
        ? false
        : 2000,
    refetchIntervalInBackground: false,
    staleTime: 1000,
  });
}

export function useCancelReceiptFile() {
  const queryClient = useQueryClient();
  return useMutation<void, AxiosError, string>({
    mutationFn: (receiptFileId: string) => cancelReceiptFile(receiptFileId),
    onSuccess: async (_data, receiptFileId) => {
      await queryClient.invalidateQueries({
        queryKey: ["receiptFileStatus", receiptFileId],
      });
      await queryClient.invalidateQueries({ queryKey: queryKeys.receipts.all });
      toast.success("Vorgang abgebrochen.");
    },
    onError: (error) => {
      const status = error.response?.status;
      if (status === 409) {
        toast.warning(
          "Beleg ist bereits fertig verarbeitet — Abbruch nicht möglich."
        );
      } else if (status === 404) {
        toast.error("Beleg nicht gefunden.");
      } else {
        toast.error("Abbruch fehlgeschlagen — bitte erneut versuchen.");
      }
    },
  });
}
