import { useMutation, useQueryClient } from "@tanstack/react-query";
import { api } from "@/lib/api-client";
import type { DisputeCategory, ExtractDisputeResponse } from "@/types/api";

/** NL extraction (TDP-AI-01). Returns fields + a per-field confidence map. */
export function useExtractDispute() {
  return useMutation({
    mutationFn: async (text: string) =>
      (await api.post<ExtractDisputeResponse>("/ai/extract-dispute", { text })).data,
  });
}

export interface SubmitDisputePayload {
  transactionId: string;
  category: DisputeCategory;
  description: string;
  extractedFields?: Record<string, unknown>;
}

/** Create a dispute (TDP-DISP-01). Invalidates the disputes list so My Disputes refreshes. */
export function useSubmitDispute() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (payload: SubmitDisputePayload) =>
      (await api.post<{ id: string; reference: string; status: string }>("/disputes", payload)).data,
    onSuccess: () => qc.invalidateQueries({ queryKey: ["disputes"] }),
  });
}
