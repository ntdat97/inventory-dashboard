import { useMemo, useState } from "react";
import { RunwayRibbon } from "@/components/RunwayRibbon";
import {
  VehicleFilterBar,
  applyFilters,
  getStatusOptions,
  type VehicleFilters,
} from "@/components/VehicleFilterBar";
import { VehicleGrid, type SortState } from "@/components/VehicleGrid";
import { VehicleDetailSheet } from "@/components/VehicleDetailSheet";
import { useSummary, useVehicles } from "@/lib/hooks";
import type { AgingTier, VehicleQuery, VehicleStatus } from "@/lib/types";

const PAGE_SIZE = 10;

// The resting sort when no column is actively chosen: oldest stock first (highest carrying-cost risk on top).
const DEFAULT_SORT: SortState = { field: "daysInInventory", desc: true };

/** The single dashboard screen: runway-ribbon hero on top, filterable inventory ledger below, detail Sheet on click. */
export function Dashboard() {
  const [filters, setFilters] = useState<VehicleFilters>({
    search: "",
    scope: "Active",
    tier: [],
    status: [],
  });
  const [sort, setSort] = useState<SortState>(DEFAULT_SORT);
  const [page, setPage] = useState(1);
  const [selectedVehicleId, setSelectedVehicleId] = useState<string | null>(null);

  const summaryQuery = useSummary();

  // Assemble the API query from filter + sort + page state. Sort is serialised as the API expects: "-field" = desc.
  const query = useMemo<VehicleQuery>(() => {
    const base: VehicleQuery = {
      page,
      pageSize: PAGE_SIZE,
      sort: `${sort.desc ? "-" : ""}${sort.field}`,
    };
    return applyFilters(base, filters);
  }, [filters, sort, page]);

  const vehiclesQuery = useVehicles(query);

  function updateFilters(next: VehicleFilters) {
    setFilters(next);
    setPage(1); // any filter change resets to the first page
  }

  function updateSort(next: SortState) {
    // VehicleGrid's header cycles a column desc → asc → (desc again). We turn that repeating flip into a
    // three-state cycle: the click that would send an asc column back to desc instead clears the sort,
    // dropping back to the default resting order. Net effect per column: desc → asc → off.
    const isThirdClick = next.field === sort.field && !sort.desc && next.desc;
    setSort(isThirdClick ? DEFAULT_SORT : next);
    setPage(1);
  }

  function selectTier(tier: AgingTier) {
    // Ribbon acts like a radio group: clicking a segment focuses that one tier and drops any others.
    // Clicking the tier that's already the sole selection clears the filter (back to the full fleet).
    const isSole = filters.tier.length === 1 && filters.tier[0] === tier;
    updateFilters({ ...filters, tier: isSole ? [] : [tier] });
  }

  return (
    <div className="space-y-14">
      {/* 01 — Capital exposure: the runway ribbon hero */}
      <section>
        <div className="eyebrow mb-3.5">
          <span className="idx">01</span> Capital exposure
        </div>
        <RunwayRibbon
          summary={summaryQuery.data}
          isLoading={summaryQuery.isLoading}
          onSelectTier={selectTier}
          activeTiers={filters.tier}
        />
      </section>

      {/* 02 — Inventory ledger */}
      <section>
        <div className="eyebrow mb-3.5">
          <span className="idx">02</span> Inventory ledger — {ledgerScopeLabel(filters.scope)}
        </div>
        <div className="mb-4">
          <VehicleFilterBar filters={filters} onChange={updateFilters} />
        </div>
        <VehicleGrid
          data={vehiclesQuery.data}
          isLoading={vehiclesQuery.isLoading}
          isFetching={vehiclesQuery.isFetching}
          sort={sort}
          onSortChange={updateSort}
          onPageChange={setPage}
          onRowClick={(v) => setSelectedVehicleId(v.id)}
          activeVehicleId={selectedVehicleId}
          tierFilter={filters.tier}
          statusFilter={filters.status}
          statusOptions={getStatusOptions(filters.scope)}
          onTierFilterChange={(tier) => updateFilters({ ...filters, tier })}
          onStatusFilterChange={(status: VehicleStatus[]) =>
            updateFilters({ ...filters, status })
          }
        />
      </section>

      <VehicleDetailSheet
        vehicleId={selectedVehicleId}
        onOpenChange={(open) => {
          if (!open) setSelectedVehicleId(null);
        }}
      />
    </div>
  );
}

function ledgerScopeLabel(scope: VehicleFilters["scope"]) {
  if (scope === "Closed") return "closed history";
  if (scope === "All") return "all vehicles";
  return "active capital at risk";
}
