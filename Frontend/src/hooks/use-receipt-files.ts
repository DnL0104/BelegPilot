"use client";

import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { queryKeys } from "@/lib/query-keys";
import {
  getReceiptFiles,
  uploadReceiptFiles,
  deleteReceiptFile,
  bulkDeleteReceiptFiles,
} from "@/lib/api-client";

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
