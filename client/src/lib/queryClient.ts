import { QueryClient } from "@tanstack/react-query";

// One client for the app. Sensible demo defaults: a short stale window so the dashboard feels live after a write,
// no refetch-on-focus noise during a walkthrough, and a single retry (the recommendation path degrades server-side).
export const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      staleTime: 15_000,
      refetchOnWindowFocus: false,
      retry: 1,
    },
  },
});
