// TypeScript mirror of the API's DTOs (src/Inventory.Api/Application/Dtos) and domain enums. Enums are serialized
// as strings by the API (JsonStringEnumConverter), so they are modelled as string-literal unions here.

export type VehicleStatus = "InStock" | "Reserved" | "Sold" | "Transferred" | "AtAuction";
export const VEHICLE_STATUSES: VehicleStatus[] = [
  "InStock",
  "Reserved",
  "Sold",
  "Transferred",
  "AtAuction",
];

export type AgingTier = "Fresh" | "Watch" | "Aging" | "Critical";
export const AGING_TIERS: AgingTier[] = ["Fresh", "Watch", "Aging", "Critical"];

export type ActionType =
  | "PriceReduction"
  | "Transfer"
  | "Auction"
  | "Promote"
  | "Recondition"
  | "Other";
export const ACTION_TYPES: ActionType[] = [
  "PriceReduction",
  "Transfer",
  "Auction",
  "Promote",
  "Recondition",
  "Other",
];

export type ActionStatus = "Proposed" | "Approved" | "InProgress" | "Resolved";
export const ACTION_STATUSES: ActionStatus[] = ["Proposed", "Approved", "InProgress", "Resolved"];

export type ActionOutcome = "Sold" | "NotSold";

export type RecommendationSource = "Baseline" | "Ai";

export interface VehicleListItem {
  id: string;
  vin: string;
  dealershipId: string;
  make: string;
  model: string;
  year: number;
  trim: string | null;
  color: string | null;
  mileage: number | null;
  acquisitionDate: string;
  acquisitionCost: number;
  listPrice: number;
  status: VehicleStatus;
  daysInInventory: number;
  tier: AgingTier;
  daysUntilAging: number | null;
  carryingCostToDate: number;
}

export interface InventoryAction {
  id: string;
  vehicleId: string;
  type: ActionType;
  status: ActionStatus;
  proposedValue: number | null;
  note: string;
  outcome: ActionOutcome | null;
  createdAt: string;
  updatedAt: string;
}

export interface VehicleDetail extends VehicleListItem {
  dealershipName: string | null;
  history: InventoryAction[];
}

export interface InventorySummary {
  totalUnits: number;
  totalInventoryValue: number;
  agedUnits: number;
  agedPercent: number;
  capitalTiedInAged: number;
  avgDaysInInventory: number;
  totalCarryingCostToDate: number;
  tierBreakdown: Record<AgingTier, number>;
}

export interface Recommendation {
  vehicleId: string;
  recommendedAction: ActionType;
  proposedValue: number | null;
  rationale: string;
  source: RecommendationSource;
  groundingFacts: string[];
}

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export interface UserProfile {
  userId: string | null;
  email: string | null;
  name: string | null;
  role: string | null;
  dealershipId: string | null;
}

export interface DevLoginResponse {
  accessToken: string;
  tokenType: string;
  expiresAtUtc: string;
  user: UserProfile;
}

export interface VehicleQuery {
  dealershipId?: string;
  make?: string;
  model?: string;
  tier?: AgingTier;
  status?: VehicleStatus;
  minDays?: number;
  maxDays?: number;
  sort?: string;
  page?: number;
  pageSize?: number;
}

export interface CreateActionRequest {
  type: ActionType;
  proposedValue: number | null;
  note: string;
}

export interface UpdateActionRequest {
  status: ActionStatus;
  outcome: ActionOutcome | null;
}
