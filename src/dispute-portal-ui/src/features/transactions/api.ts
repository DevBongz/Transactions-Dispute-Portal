import { keepPreviousData, useQuery } from "@tanstack/react-query";
import { api } from "@/lib/api-client";
import type { Paged, Transaction } from "@/types/api";

export interface TxnFilters {
  page: number;
  pageSize: number;
  from?: string; // ISO yyyy-MM-dd
  to?: string; // ISO yyyy-MM-dd
  merchant?: string;
}

export function useTransactions(filters: TxnFilters) {
  return useQuery({
    queryKey: ["transactions", filters],
    queryFn: async () => {
      const { data } = await api.get<Paged<Transaction>>("/transactions", {
        params: {
          page: filters.page,
          pageSize: filters.pageSize,
          from: filters.from || undefined,
          to: filters.to || undefined,
          merchant: filters.merchant || undefined,
        },
      });
      return data;
    },
    placeholderData: keepPreviousData,
  });
}

export function useTransaction(id: string) {
  return useQuery({
    queryKey: ["transaction", id],
    queryFn: async () => (await api.get<Transaction>(`/transactions/${id}`)).data,
    enabled: !!id,
  });
}
