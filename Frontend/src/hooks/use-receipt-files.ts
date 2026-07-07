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
  bulkRetryReceiptFiles,
  getReceiptFileStatus,
  cancelReceiptFile,
  retryReceiptFile,
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

export function useBulkRetryFiles() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (ids: string[]) => bulkRetryReceiptFiles(ids),
    onSuccess: (data, ids) => {
      queryClient.invalidateQueries({ queryKey: queryKeys.receiptFiles.all });
      ids.forEach((id) => {
        queryClient.invalidateQueries({ queryKey: ["receiptFileStatus", id] });
      });
      if (data.retried === ids.length) {
        toast.success(
          `${data.retried} Beleg${data.retried > 1 ? "e" : ""} werden erneut versucht.`
        );
      } else if (data.retried > 0) {
        toast.success(
          `${data.retried} von ${ids.length} Belegen werden erneut versucht — die übrigen sind bereits abgeschlossen.`
        );
      } else {
        toast.warning("Keiner der Belege konnte erneut versucht werden — bereits abgeschlossen.");
      }
    },
    onError: () => {
      toast.error("Erneuter Versuch fehlgeschlagen — bitte später erneut versuchen.");
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

export function useRetryReceiptFile() {
  const queryClient = useQueryClient();
  return useMutation<void, AxiosError, string>({
    mutationFn: (receiptFileId: string) => retryReceiptFile(receiptFileId),
    onSuccess: async (_data, receiptFileId) => {
      await queryClient.invalidateQueries({
        queryKey: ["receiptFileStatus", receiptFileId],
      });
      toast.success("Verarbeitung wird erneut versucht.");
    },
    onError: (error) => {
      const status = error.response?.status;
      if (status === 409) {
        toast.warning(
          "Beleg befindet sich nicht in einem Status, der eine erneute Verarbeitung erlaubt."
        );
      } else if (status === 404) {
        toast.error("Beleg nicht gefunden.");
      } else {
        toast.error("Erneuter Versuch fehlgeschlagen — bitte später erneut versuchen.");
      }
    },
  });
}
