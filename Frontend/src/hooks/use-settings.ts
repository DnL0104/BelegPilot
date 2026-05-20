"use client";

import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { queryKeys } from "@/lib/query-keys";
import {
  getUserSettings,
  updateUserSettings,
  type UserSettings,
} from "@/lib/api-client";

export function useSettings() {
  return useQuery({
    queryKey: queryKeys.settings.all,
    queryFn: getUserSettings,
  });
}

export function useUpdateSettings() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (settings: UserSettings) => updateUserSettings(settings),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: queryKeys.settings.all });
    },
  });
}
