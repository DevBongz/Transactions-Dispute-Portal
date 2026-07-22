import { QueryClient } from "@tanstack/react-query";

/**
 * Shared query client. Do not retry auth failures (401 drives a redirect); keep a short
 * stale window so revisiting a list is instant from cache (TDP-FE-01 §2.7).
 */
export const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      staleTime: 30_000,
      retry: (count, err: unknown) => {
        const status = (err as { response?: { status?: number } })?.response?.status;
        return status !== 401 && status !== 403 && count < 2;
      },
      refetchOnWindowFocus: false,
    },
  },
});
