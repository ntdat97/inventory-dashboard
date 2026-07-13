import { getToken } from "./token";
import type {
  CreateActionRequest,
  GuestLoginResponse,
  InventoryAction,
  InventorySummary,
  PagedResult,
  Recommendation,
  UpdateActionRequest,
  UserProfile,
  VehicleDetail,
  VehicleListItem,
  VehicleQuery,
} from "./types";

// In dev, VITE_API_BASE_URL is empty and calls hit same-origin "/api" (Vite proxies to the backend). In production
// it is the deployed API origin. Trailing slash is trimmed so we can safely concatenate paths that start with "/api".
const API_BASE = (import.meta.env.VITE_API_BASE_URL ?? "").replace(/\/$/, "");

/** An API error carrying the HTTP status and the parsed RFC 7807 ProblemDetails (when the body was one). */
export class ApiError extends Error {
  constructor(
    readonly status: number,
    message: string,
    readonly problem?: ProblemDetails,
    readonly correlationId?: string | null,
  ) {
    super(message);
    this.name = "ApiError";
  }
}

export interface ProblemDetails {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  correlationId?: string;
  errors?: Record<string, string[]>;
}

async function request<T>(path: string, init: RequestInit = {}): Promise<T> {
  const token = getToken();
  const headers = new Headers(init.headers);
  if (token) headers.set("Authorization", `Bearer ${token}`);
  if (init.body && !headers.has("Content-Type")) headers.set("Content-Type", "application/json");

  const response = await fetch(`${API_BASE}${path}`, { ...init, headers });
  const correlationId = response.headers.get("X-Correlation-Id");

  if (response.status === 204) return undefined as T;

  const text = await response.text();
  const body = text ? safeJson(text) : undefined;

  if (!response.ok) {
    const problem = body as ProblemDetails | undefined;
    const message =
      problem?.detail ?? problem?.title ?? `${response.status} ${response.statusText}`;
    throw new ApiError(response.status, message, problem, correlationId);
  }

  return body as T;
}

function safeJson(text: string): unknown {
  try {
    return JSON.parse(text);
  } catch {
    return text;
  }
}

function toQueryString(query: VehicleQuery): string {
  const params = new URLSearchParams();
  Object.entries(query).forEach(([key, value]) => {
    if (value === undefined || value === null || value === "") return;
    // Arrays (tier, status) repeat the key: ?tier=Aging&tier=Critical — matches the API's List binding.
    if (Array.isArray(value)) {
      value.forEach((v) => params.append(key, String(v)));
    } else {
      params.set(key, String(value));
    }
  });
  const qs = params.toString();
  return qs ? `?${qs}` : "";
}

export const api = {
  // Auth
  guestLogin: () => request<GuestLoginResponse>("/api/auth/guest-login", { method: "POST" }),
  me: () => request<UserProfile>("/api/auth/me"),

  // Inventory / vehicles
  summary: (dealershipId?: string) =>
    request<InventorySummary>(
      `/api/inventory/summary${dealershipId ? `?dealershipId=${dealershipId}` : ""}`,
    ),
  aging: (dealershipId?: string) =>
    request<VehicleListItem[]>(
      `/api/inventory/aging${dealershipId ? `?dealershipId=${dealershipId}` : ""}`,
    ),
  vehicles: (query: VehicleQuery) =>
    request<PagedResult<VehicleListItem>>(`/api/vehicles${toQueryString(query)}`),
  vehicle: (id: string) => request<VehicleDetail>(`/api/vehicles/${id}`),
  recommendation: (id: string) => request<Recommendation>(`/api/vehicles/${id}/recommendation`),
  reserveVehicle: (id: string) =>
    request<VehicleDetail>(`/api/vehicles/${id}/reserve`, { method: "POST" }),
  releaseVehicle: (id: string) =>
    request<VehicleDetail>(`/api/vehicles/${id}/release`, { method: "POST" }),

  // Actions
  createAction: (vehicleId: string, body: CreateActionRequest) =>
    request<InventoryAction>(`/api/vehicles/${vehicleId}/actions`, {
      method: "POST",
      body: JSON.stringify(body),
    }),
  transitionAction: (actionId: string, body: UpdateActionRequest) =>
    request<InventoryAction>(`/api/actions/${actionId}`, {
      method: "PATCH",
      body: JSON.stringify(body),
    }),
};
