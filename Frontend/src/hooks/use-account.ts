"use client";

import { useMutation } from "@tanstack/react-query";
import { deleteAccount, downloadExportBundle } from "@/lib/api-client";

export function useDeleteAccount() {
  return useMutation({
    mutationFn: (password: string) => deleteAccount(password),
  });
}

export function useDownloadExportBundle() {
  return useMutation({
    mutationFn: (exportToken: string) => downloadExportBundle(exportToken),
  });
}
