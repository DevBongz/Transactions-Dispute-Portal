import { keepPreviousData, useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api } from "@/lib/api-client";
import type {
  DashboardSummary,
  Dispute,
  DisputeCategory,
  DisputeStatus,
  Outcome,
  Paged,
  Priority,
  Resolution,
} from "@/types/api";

export interface OpsFilters {
  page: number;
  pageSize: number;
  status?: DisputeStatus;
  priority?: Priority;
  category?: DisputeCategory;
}

export function useOpsDisputes(filters: OpsFilters) {
  return useQuery({
    queryKey: ["disputes", "ops", filters],
    queryFn: async () =>
      (
        await api.get<Paged<Dispute>>("/disputes", {
          params: {
            page: filters.page,
            pageSize: filters.pageSize,
            status: filters.status || undefined,
            priority: filters.priority || undefined,
            category: filters.category || undefined,
          },
        })
      ).data,
    placeholderData: keepPreviousData,
  });
}

/** Defensive priority-desc comparator (older first within a tier) — applied per page only. */
const PRIORITY_RANK: Record<Priority, number> = { CRITICAL: 3, HIGH: 2, MEDIUM: 1, LOW: 0 };
export function byPriorityDesc(a: Dispute, b: Dispute): number {
  return (
    PRIORITY_RANK[b.priority ?? "LOW"] - PRIORITY_RANK[a.priority ?? "LOW"] ||
    +new Date(a.createdAt) - +new Date(b.createdAt)
  );
}

export function useUpdateStatus(id: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (status: DisputeStatus) =>
      (await api.patch<Dispute>(`/disputes/${id}/status`, { status })).data,
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ["dispute", id] });
      qc.invalidateQueries({ queryKey: ["disputes"] });
    },
  });
}

export function useGenerateSummary() {
  return useMutation({
    mutationFn: async (v: { disputeId: string; outcome: Outcome; internalNotes: string }) =>
      (await api.post<{ summary: string }>("/ai/generate-summary", v)).data.summary,
  });
}

export function useResolveDispute(id: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (v: { outcome: Outcome; internalNotes: string; customerSummary: string }) =>
      (await api.post<Resolution>(`/disputes/${id}/resolve`, v)).data,
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ["dispute", id] });
      qc.invalidateQueries({ queryKey: ["disputes"] });
      qc.invalidateQueries({ queryKey: ["dashboard-summary"] });
    },
  });
}

export function useDashboardSummary() {
  return useQuery({
    queryKey: ["dashboard-summary"],
    queryFn: async () => (await api.get<DashboardSummary>("/dashboard/summary")).data,
  });
}
