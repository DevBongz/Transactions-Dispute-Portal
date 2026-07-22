import { keepPreviousData, useQuery } from "@tanstack/react-query";
import { api } from "@/lib/api-client";
import type { Dispute, DisputeDetail, DisputeStatus, Paged } from "@/types/api";

export interface MyDisputesFilters {
  page: number;
  pageSize: number;
  status?: DisputeStatus;
}

export function useMyDisputes(filters: MyDisputesFilters) {
  return useQuery({
    queryKey: ["disputes", filters],
    queryFn: async () =>
      (
        await api.get<Paged<Dispute>>("/disputes", {
          params: { page: filters.page, pageSize: filters.pageSize, status: filters.status || undefined },
        })
      ).data,
    placeholderData: keepPreviousData,
  });
}

export function useDisputeDetail(id: string, options?: { pollWhileOpen?: boolean }) {
  return useQuery({
    queryKey: ["dispute", id],
    queryFn: async () => (await api.get<DisputeDetail>(`/disputes/${id}`)).data,
    enabled: !!id,
    // Bounded polling so a freshly-submitted dispute shows classification landing without a
    // manual refresh; stops once classified/resolved (TDP-FE-04 §4).
    refetchInterval: (query) => {
      if (!options?.pollWhileOpen) return false;
      const d = query.state.data as DisputeDetail | undefined;
      if (!d) return 5_000;
      const stillPending = d.status === "OPEN" && d.category == null;
      return stillPending ? 5_000 : false;
    },
  });
}
