import { Search, X } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { humanizeEnum } from "@/lib/format";
import {
  AGING_TIERS,
  VEHICLE_STATUSES,
  type AgingTier,
  type VehicleQuery,
  type VehicleStatus,
} from "@/lib/types";

// Sentinel for "no filter" — Radix Select can't hold an empty-string value, so map it to undefined on the way out.
const ANY = "__any__";

export interface VehicleFilters {
  make: string;
  model: string;
  tier?: AgingTier;
  status?: VehicleStatus;
}

export function VehicleFilterBar({
  filters,
  onChange,
}: {
  filters: VehicleFilters;
  onChange: (next: VehicleFilters) => void;
}) {
  const hasActive =
    filters.make !== "" || filters.model !== "" || filters.tier || filters.status;

  return (
    <div className="grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-5 lg:items-end">
      <div className="space-y-1">
        <Label htmlFor="make">Make</Label>
        <div className="relative">
          <Search className="absolute left-2.5 top-2.5 h-4 w-4 text-muted-foreground" />
          <Input
            id="make"
            className="pl-8"
            placeholder="e.g. Toyota"
            value={filters.make}
            onChange={(e) => onChange({ ...filters, make: e.target.value })}
          />
        </div>
      </div>

      <div className="space-y-1">
        <Label htmlFor="model">Model</Label>
        <Input
          id="model"
          placeholder="e.g. Camry"
          value={filters.model}
          onChange={(e) => onChange({ ...filters, model: e.target.value })}
        />
      </div>

      <div className="space-y-1">
        <Label>Aging tier</Label>
        <Select
          value={filters.tier ?? ANY}
          onValueChange={(v) =>
            onChange({ ...filters, tier: v === ANY ? undefined : (v as AgingTier) })
          }
        >
          <SelectTrigger>
            <SelectValue placeholder="Any tier" />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value={ANY}>Any tier</SelectItem>
            {AGING_TIERS.map((tier) => (
              <SelectItem key={tier} value={tier}>
                {tier}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
      </div>

      <div className="space-y-1">
        <Label>Status</Label>
        <Select
          value={filters.status ?? ANY}
          onValueChange={(v) =>
            onChange({ ...filters, status: v === ANY ? undefined : (v as VehicleStatus) })
          }
        >
          <SelectTrigger>
            <SelectValue placeholder="Any status" />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value={ANY}>Any status</SelectItem>
            {VEHICLE_STATUSES.map((status) => (
              <SelectItem key={status} value={status}>
                {humanizeEnum(status)}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
      </div>

      <Button
        variant="ghost"
        className="justify-start lg:justify-center"
        disabled={!hasActive}
        onClick={() => onChange({ make: "", model: "", tier: undefined, status: undefined })}
      >
        <X className="h-4 w-4" />
        Clear filters
      </Button>
    </div>
  );
}

/** Merge the filter-bar state into the query object the API client consumes (empty strings dropped). */
export function applyFilters(base: VehicleQuery, filters: VehicleFilters): VehicleQuery {
  return {
    ...base,
    make: filters.make.trim() || undefined,
    model: filters.model.trim() || undefined,
    tier: filters.tier,
    status: filters.status,
  };
}
