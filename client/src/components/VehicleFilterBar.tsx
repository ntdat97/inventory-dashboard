import { useEffect, useRef, useState } from "react";
import { Search, X } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import {
  ACTIVE_VEHICLE_STATUSES,
  CLOSED_VEHICLE_STATUSES,
  VEHICLE_SCOPES,
  VEHICLE_STATUSES,
  type AgingTier,
  type VehicleQuery,
  type VehicleScope,
  type VehicleStatus,
} from "@/lib/types";
import { cn } from "@/lib/utils";

export interface VehicleFilters {
  search: string;
  scope: VehicleScope;
  tier: AgingTier[];
  status: VehicleStatus[];
}

export function VehicleFilterBar({
  filters,
  onChange,
}: {
  filters: VehicleFilters;
  onChange: (next: VehicleFilters) => void;
}) {
  const hasActive =
    filters.search !== "" ||
    filters.scope !== "Active" ||
    filters.tier.length > 0 ||
    filters.status.length > 0;

  // Local draft for the search box: keystrokes update it instantly (snappy input), and we only push
  // the value up to the parent — which triggers the API query — 300ms after typing stops.
  const [draft, setDraft] = useState(filters.search);

  // Reverse-sync: when the search value is changed from the outside (e.g. "Clear filters" resets it to
  // ""), pull that into the draft. Guarded so it doesn't clobber the draft mid-typing when the parent
  // simply echoes back the value we ourselves just debounced up.
  useEffect(() => {
    if (filters.search !== draft) {
      setDraft(filters.search);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [filters.search]);

  // Debounce the draft → parent push. Skip the push while draft already matches (avoids a redundant
  // update on mount and right after a reverse-sync).
  const onChangeRef = useRef(onChange);
  onChangeRef.current = onChange;
  useEffect(() => {
    if (draft === filters.search) return;
    const id = window.setTimeout(() => {
      onChangeRef.current({ ...filters, search: draft });
    }, 300);
    return () => window.clearTimeout(id);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [draft]);

  return (
    <div className="grid grid-cols-1 gap-3 sm:grid-cols-[2fr_auto_auto] sm:items-end">
      <div className="space-y-1.5">
        <Label htmlFor="search" className="block">
          Search
        </Label>
        <div className="relative">
          <Search className="absolute left-2.5 top-2.5 h-4 w-4 text-muted-foreground" />
          <Input
            id="search"
            className="pl-8"
            placeholder="Make, model, trim, VIN, colour, year…"
            value={draft}
            onChange={(e) => setDraft(e.target.value)}
          />
        </div>
      </div>

      <div className="space-y-1.5">
        <Label className="block">Scope</Label>
        <div className="inline-grid h-9 grid-cols-3 overflow-hidden rounded-md border bg-card">
          {VEHICLE_SCOPES.map((scope) => {
            const selected = filters.scope === scope;
            return (
              <button
                key={scope}
                type="button"
                aria-pressed={selected}
                className={cn(
                  "px-3 text-[12px] font-semibold transition-colors hover:bg-muted",
                  selected
                    ? "bg-primary text-primary-foreground hover:bg-primary"
                    : "text-muted-foreground",
                )}
                onClick={() => onChange(changeScope(filters, scope))}
              >
                {scope}
              </button>
            );
          })}
        </div>
      </div>

      <Button
        variant="ghost"
        className="justify-start sm:justify-center"
        disabled={!hasActive}
        onClick={() => onChange({ search: "", scope: "Active", tier: [], status: [] })}
      >
        <X className="h-4 w-4" />
        Clear filters
      </Button>
    </div>
  );
}

/** Merge the filter-bar state into the query object the API client consumes (empty strings/arrays dropped). */
export function applyFilters(base: VehicleQuery, filters: VehicleFilters): VehicleQuery {
  return {
    ...base,
    search: filters.search.trim() || undefined,
    scope: filters.scope,
    tier: filters.tier.length > 0 ? filters.tier : undefined,
    status: filters.status.length > 0 ? filters.status : undefined,
  };
}

export function getStatusOptions(scope: VehicleScope): VehicleStatus[] {
  if (scope === "Active") return ACTIVE_VEHICLE_STATUSES;
  if (scope === "Closed") return CLOSED_VEHICLE_STATUSES;
  return VEHICLE_STATUSES;
}

function changeScope(filters: VehicleFilters, scope: VehicleScope): VehicleFilters {
  const statusOptions = getStatusOptions(scope);
  return {
    ...filters,
    scope,
    // Drop any selected statuses that no longer belong to the new scope.
    status: filters.status.filter((s) => statusOptions.includes(s)),
  };
}
