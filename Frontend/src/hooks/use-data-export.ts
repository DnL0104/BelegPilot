"use client";

import { useState } from "react";
import { useQuery, useMutation } from "@tanstack/react-query";
import { requestDataExport, getExportStatus, type ExportStatus } from "@/lib/api-client";

/**
 * Hook for DSGVO Art. 20 data export flow.
 * Exposes requestExport() mutation, the current exportToken, polled status, and a download helper.
 * LEG-07 / D-09 / D-12.
 */
export function useDataExport() {
  const [exportToken, setExportToken] = useState<string | null>(null);

  const requestMutation = useMutation({
    mutationFn: requestDataExport,
    onSuccess: (data) => {
      setExportToken(data.exportToken);
    },
  });

  const statusQuery = useQuery({
    queryKey: ["export-status", exportToken],
    queryFn: () => getExportStatus(exportToken!),
    enabled: exportToken !== null,
    // Poll every 3 seconds while Generating; stop when Ready or Expired
    refetchInterval: (query) => {
      const status = query.state.data?.status;
      if (status === "Generating") return 3000;
      return false;
    },
  });

  const status: ExportStatus | null = statusQuery.data?.status ?? null;

  const resetExport = () => {
    setExportToken(null);
  };

  return {
    requestExport: requestMutation.mutate,
    isRequesting: requestMutation.isPending,
    requestError: requestMutation.isError,
    exportToken,
    status,
    isPolling: statusQuery.isFetching && status === "Generating",
    resetExport,
  };
}
