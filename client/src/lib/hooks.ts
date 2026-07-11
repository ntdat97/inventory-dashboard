import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api } from "@/lib/api";
import type {
  CreateActionRequest,
  UpdateActionRequest,
  VehicleQuery,
} from "@/lib/types";

// Centralised query keys so mutations can invalidate precisely.
export const keys = {
  summary: (dealershipId?: string) => ["summary", dealershipId ?? "all"] as const,
  vehicles: (query: VehicleQuery) => ["vehicles", query] as const,
  vehicle: (id: string) => ["vehicle", id] as const,
  recommendation: (id: string) => ["recommendation", id] as const,
};

export function useSummary(dealershipId?: string) {
  return useQuery({
    queryKey: keys.summary(dealershipId),
    queryFn: () => api.summary(dealershipId),
  });
}

export function useVehicles(query: VehicleQuery) {
  return useQuery({
    queryKey: keys.vehicles(query),
    queryFn: () => api.vehicles(query),
    placeholderData: (prev) => prev, // keep the current page visible while the next page/filter loads
  });
}

export function useVehicle(id: string | null) {
  return useQuery({
    queryKey: keys.vehicle(id ?? ""),
    queryFn: () => api.vehicle(id as string),
    enabled: id !== null,
  });
}

export function useRecommendation(id: string | null, enabled: boolean) {
  return useQuery({
    queryKey: keys.recommendation(id ?? ""),
    queryFn: () => api.recommendation(id as string),
    enabled: id !== null && enabled,
    staleTime: 5 * 60_000, // recommendations are cached server-side per vehicle; mirror that on the client
  });
}

/** Log a new action, then refresh the vehicle detail (history) and the dashboard KPIs. */
export function useCreateAction(vehicleId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (body: CreateActionRequest) => api.createAction(vehicleId, body),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: keys.vehicle(vehicleId) });
      qc.invalidateQueries({ queryKey: ["summary"] });
      qc.invalidateQueries({ queryKey: ["vehicles"] });
    },
  });
}

/** Advance an action through its lifecycle; refresh the owning vehicle's history on success. */
export function useTransitionAction(vehicleId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ actionId, body }: { actionId: string; body: UpdateActionRequest }) =>
      api.transitionAction(actionId, body),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: keys.vehicle(vehicleId) });
    },
  });
}
