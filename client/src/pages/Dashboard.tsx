import { useMemo, useState } from "react";
import { KpiCards } from "@/components/KpiCards";
import { AgingSpectrum } from "@/components/AgingSpectrum";
import {
  VehicleFilterBar,
  applyFilters,
  type VehicleFilters,
} from "@/components/VehicleFilterBar";
import { VehicleGrid, type SortState } from "@/components/VehicleGrid";
import { VehicleDetailSheet } from "@/components/VehicleDetailSheet";
import { useSummary, useVehicles } from "@/lib/hooks";
import type { AgingTier, VehicleQuery } from "@/lib/types";

const PAGE_SIZE = 10;

/** The single dashboard screen: KPIs + aging spectrum on top, filterable vehicle grid below, detail Sheet on click. */
export function Dashboard() {
  const [filters, setFilters] = useState<VehicleFilters>({
    make: "",
    model: "",
    tier: undefined,
    status: undefined,
  });
  const [sort, setSort] = useState<SortState>({ field: "daysInInventory", desc: true });
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
    setSort(next);
    setPage(1);
  }

  function selectTier(tier: AgingTier) {
    updateFilters({ ...filters, tier });
  }

  return (
    <div className="space-y-6">
      <section className="space-y-4">
        <div>
          <h1 className="text-lg font-semibold">Where is my money stuck?</h1>
          <p className="text-sm text-muted-foreground">
            Capital-at-risk overview across held inventory.
          </p>
        </div>
        <KpiCards summary={summaryQuery.data} isLoading={summaryQuery.isLoading} />
      </section>

      <section className="grid grid-cols-1 gap-4 lg:grid-cols-2">
        <AgingSpectrum
          summary={summaryQuery.data}
          isLoading={summaryQuery.isLoading}
          onSelectTier={selectTier}
        />
        <div className="rounded-lg border bg-card p-5">
          <h2 className="text-base font-semibold">Reading the spectrum</h2>
          <p className="mt-1 text-sm text-muted-foreground">
            A vehicle past ~90 days is aged/distressed stock — very likely selling at a loss once
            floorplan interest, depreciation and holding costs are counted. The spectrum shows
            problems <em>forming</em> (Watch → Aging) so you can act before Critical.
          </p>
          <ul className="mt-3 space-y-1 text-sm text-muted-foreground">
            <li>• Click a tier to filter the grid below.</li>
            <li>• Open a vehicle for its carrying cost, AI recommendation and action history.</li>
            <li>• Log an action and advance it Proposed → Approved → In&nbsp;Progress → Resolved.</li>
          </ul>
        </div>
      </section>

      <section className="space-y-4">
        <div>
          <h2 className="text-base font-semibold">Inventory</h2>
          <p className="text-sm text-muted-foreground">
            Filter, sort and open a vehicle to drill in.
          </p>
        </div>
        <VehicleFilterBar filters={filters} onChange={updateFilters} />
        <VehicleGrid
          data={vehiclesQuery.data}
          isLoading={vehiclesQuery.isLoading}
          isFetching={vehiclesQuery.isFetching}
          sort={sort}
          onSortChange={updateSort}
          onPageChange={setPage}
          onRowClick={(v) => setSelectedVehicleId(v.id)}
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
