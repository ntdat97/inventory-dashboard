import { useEffect, useMemo, useRef, useState } from "react";
import {
  flexRender,
  getCoreRowModel,
  useReactTable,
  type ColumnDef,
} from "@tanstack/react-table";
import { ArrowDown, ArrowUp, ChevronLeft, ChevronRight, ChevronsUpDown } from "lucide-react";
import { Button } from "@/components/ui/button";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import { Skeleton } from "@/components/ui/skeleton";
import { TierBadge } from "@/components/TierBadge";
import { HeaderFilterMenu } from "@/components/HeaderFilterMenu";
import { TIER_STYLES, formatCurrency, formatCurrencyPrecise, humanizeEnum } from "@/lib/format";
import { cn } from "@/lib/utils";
import { AGING_TIERS, type AgingTier, type PagedResult, type VehicleListItem, type VehicleStatus } from "@/lib/types";

// Columns that the API can sort on, mapped to their sort key. Header clicks cycle asc → desc on these.
const SORTABLE: Record<string, string> = {
  make: "make",
  year: "year",
  listPrice: "listPrice",
  daysInInventory: "daysInInventory",
  carryingCostToDate: "carryingCost",
};

// Days runway is drawn against a fixed 120-day horizon so every row's bar is comparable at a glance.
const DAYS_HORIZON = 120;

// Numeric columns are right-aligned in both header and body so figures line up as a readout.
const RIGHT_ALIGNED = new Set(["daysInInventory", "listPrice", "carryingCostToDate"]);

export interface SortState {
  field: string; // sort key (e.g. "daysInInventory")
  desc: boolean;
}

export function VehicleGrid({
  data,
  isLoading,
  isFetching,
  sort,
  onSortChange,
  onRowClick,
  onPageChange,
  activeVehicleId,
  tierFilter,
  statusFilter,
  statusOptions,
  onTierFilterChange,
  onStatusFilterChange,
}: {
  data: PagedResult<VehicleListItem> | undefined;
  isLoading: boolean;
  isFetching: boolean;
  sort: SortState;
  onSortChange: (next: SortState) => void;
  onRowClick: (vehicle: VehicleListItem) => void;
  onPageChange: (page: number) => void;
  activeVehicleId?: string | null;
  tierFilter: AgingTier[];
  statusFilter: VehicleStatus[];
  statusOptions: VehicleStatus[];
  onTierFilterChange: (next: AgingTier[]) => void;
  onStatusFilterChange: (next: VehicleStatus[]) => void;
}) {
  const columns = useMemo<ColumnDef<VehicleListItem>[]>(
    () => [
      {
        accessorKey: "make",
        header: "Vehicle",
        cell: ({ row }) => {
          const v = row.original;
          return (
            <div className="flex flex-col">
              <span className="font-semibold">
                {v.year} {v.make} {v.model}
              </span>
              <span className="mono mt-0.5 text-[11px] text-muted-foreground">
                {v.trim ? `${v.trim} · ` : ""}VIN {v.vin.slice(-6)}
              </span>
            </div>
          );
        },
      },
      {
        accessorKey: "tier",
        header: "Tier",
        cell: ({ row }) => <TierBadge tier={row.original.tier} />,
      },
      {
        accessorKey: "daysInInventory",
        header: "Days in stock",
        cell: ({ row }) => {
          const v = row.original;
          const pct = Math.min((v.daysInInventory / DAYS_HORIZON) * 100, 100);
          return (
            <div className="flex items-center justify-end gap-2">
              <span className="mono text-[13.5px] font-medium">{v.daysInInventory}d</span>
              <span className="relative inline-block h-1 w-[46px] overflow-hidden rounded-sm bg-background">
                <span
                  className="absolute inset-y-0 left-0 rounded-sm"
                  style={{ width: `${pct}%`, backgroundColor: TIER_STYLES[v.tier].fill }}
                />
              </span>
            </div>
          );
        },
      },
      {
        accessorKey: "listPrice",
        header: "List price",
        cell: ({ row }) => (
          <span className="mono text-[13.5px] font-medium">
            {formatCurrency(row.original.listPrice)}
          </span>
        ),
      },
      {
        accessorKey: "carryingCostToDate",
        header: "Carrying cost",
        cell: ({ row }) => (
          <span className="mono text-[13.5px] font-medium text-tier-aging">
            {formatCurrencyPrecise(row.original.carryingCostToDate)}
          </span>
        ),
      },
      {
        accessorKey: "status",
        header: "Status",
        cell: ({ row }) => (
          <span className="mono inline-block rounded-sm border border-border px-2 py-0.5 text-[11.5px] tracking-[0.02em] text-muted-foreground">
            {humanizeEnum(row.original.status)}
          </span>
        ),
      },
    ],
    [],
  );

  const table = useReactTable({
    data: data?.items ?? [],
    columns,
    getCoreRowModel: getCoreRowModel(),
    manualSorting: true,
    manualPagination: true,
  });

  function toggleSort(columnId: string) {
    const sortKey = SORTABLE[columnId];
    if (!sortKey) return;
    if (sort.field === sortKey) {
      onSortChange({ field: sortKey, desc: !sort.desc });
    } else {
      onSortChange({ field: sortKey, desc: true });
    }
  }

  const totalCount = data?.totalCount ?? 0;
  const page = data?.page ?? 1;
  const totalPages = data?.totalPages ?? 1;

  // A signature of the current result set: page + sort + the ids on screen. When any of these change
  // (filter, sort, page, tier) the string changes, so keying each row on it remounts them and replays
  // the row-enter cascade — the visual cue that the ledger below just re-scoped.
  const rows = table.getRowModel().rows;
  const renderKey = `${page}:${sort.field}:${sort.desc}:${rows.map((r) => r.original.id).join(",")}`;

  // Flash the ledger surface once whenever the visible result set changes (a tier click up in the
  // ribbon, a filter, a sort). Skip the very first paint so the table doesn't pulse on load. The
  // toggled class is stripped after the animation so it can re-fire on the next change.
  const [pulse, setPulse] = useState(false);
  const prevKey = useRef<string | null>(null);
  useEffect(() => {
    if (isLoading) return;
    const changed = prevKey.current !== null && prevKey.current !== renderKey;
    prevKey.current = renderKey;
    if (changed) {
      setPulse(true);
      const id = window.setTimeout(() => setPulse(false), 650);
      return () => window.clearTimeout(id);
    }
  }, [renderKey, isLoading]);

  return (
    <div className="space-y-3">
      <div
        className={cn(
          "overflow-hidden rounded-lg border bg-card",
          pulse && "surface-pulse",
        )}
      >
        <Table>
          <TableHeader>
            {table.getHeaderGroups().map((headerGroup) => (
              <TableRow key={headerGroup.id} className="border-b hover:bg-transparent">
                {headerGroup.headers.map((header) => {
                  const columnId = header.column.id;
                  const sortKey = SORTABLE[columnId];
                  const isSorted = sortKey && sort.field === sortKey;
                  const align = RIGHT_ALIGNED.has(columnId) ? "text-right" : "";
                  return (
                    <TableHead
                      key={header.id}
                      className={cn(
                        "h-auto bg-[hsl(var(--muted))] px-5 py-3 text-[10.5px] font-semibold uppercase tracking-[0.08em] text-muted-foreground",
                        align,
                      )}
                    >
                      {sortKey ? (
                        <button
                          type="button"
                          className={cn(
                            "inline-flex items-center gap-1 uppercase tracking-[0.08em] hover:text-foreground",
                            align === "text-right" ? "flex-row-reverse" : "",
                          )}
                          onClick={() => toggleSort(columnId)}
                        >
                          {flexRender(header.column.columnDef.header, header.getContext())}
                          {isSorted ? (
                            sort.desc ? (
                              <ArrowDown className="h-3.5 w-3.5" />
                            ) : (
                              <ArrowUp className="h-3.5 w-3.5" />
                            )
                          ) : (
                            <ChevronsUpDown className="h-3.5 w-3.5 opacity-40" />
                          )}
                        </button>
                      ) : columnId === "tier" ? (
                        <HeaderFilterMenu
                          title="Tier"
                          options={AGING_TIERS.map((t) => ({ value: t, label: t }))}
                          selected={tierFilter}
                          onChange={onTierFilterChange}
                        />
                      ) : columnId === "status" ? (
                        <HeaderFilterMenu
                          title="Status"
                          options={statusOptions.map((s) => ({ value: s, label: humanizeEnum(s) }))}
                          selected={statusFilter}
                          onChange={onStatusFilterChange}
                        />
                      ) : (
                        flexRender(header.column.columnDef.header, header.getContext())
                      )}
                    </TableHead>
                  );
                })}
              </TableRow>
            ))}
          </TableHeader>
          <TableBody>
            {isLoading ? (
              Array.from({ length: 6 }).map((_, i) => (
                <TableRow key={i} className="hover:bg-transparent">
                  {columns.map((_c, ci) => (
                    <TableCell key={ci} className="px-5 py-3.5">
                      <Skeleton className="h-6 w-full" />
                    </TableCell>
                  ))}
                </TableRow>
              ))
            ) : table.getRowModel().rows.length === 0 ? (
              <TableRow className="hover:bg-transparent">
                <TableCell
                  colSpan={columns.length}
                  className="h-24 text-center text-muted-foreground"
                >
                  No vehicles match these filters.
                </TableCell>
              </TableRow>
            ) : (
              table.getRowModel().rows.map((row, ri) => {
                const isActive = row.original.id === activeVehicleId;
                return (
                  <TableRow
                    key={`${renderKey}:${row.id}`}
                    className={cn(
                      "row-enter cursor-pointer border-b transition-colors hover:bg-[hsl(var(--muted))]",
                      isActive && "bg-primary/[0.05] hover:bg-primary/[0.05]",
                    )}
                    style={{ animationDelay: `${Math.min(ri, 9) * 28}ms` }}
                    onClick={() => onRowClick(row.original)}
                  >
                    {row.getVisibleCells().map((cell, ci) => {
                      const align = RIGHT_ALIGNED.has(cell.column.id) ? "text-right" : "";
                      return (
                        <TableCell
                          key={cell.id}
                          className={cn(
                            "px-5 py-3.5 align-middle transition-shadow duration-200",
                            align,
                            ci === 0 &&
                              (isActive
                                ? "shadow-[inset_3px_0_0_hsl(var(--primary))]"
                                : "shadow-[inset_0_0_0_hsl(var(--primary))]"),
                          )}
                        >
                          {flexRender(cell.column.columnDef.cell, cell.getContext())}
                        </TableCell>
                      );
                    })}
                  </TableRow>
                );
              })
            )}
          </TableBody>
        </Table>
      </div>

      <div className="flex items-center justify-between">
        <p className="mono text-[11.5px] text-muted-foreground">
          {totalCount === 0
            ? "No results"
            : `Page ${page} of ${totalPages} · ${totalCount} vehicle${totalCount === 1 ? "" : "s"}`}
          {isFetching ? " · updating…" : ""}
        </p>
        <div className="flex items-center gap-2">
          <Button
            variant="outline"
            size="sm"
            disabled={page <= 1 || isLoading}
            onClick={() => onPageChange(page - 1)}
          >
            <ChevronLeft className="h-4 w-4" />
            Prev
          </Button>
          <Button
            variant="outline"
            size="sm"
            disabled={page >= totalPages || isLoading}
            onClick={() => onPageChange(page + 1)}
          >
            Next
            <ChevronRight className="h-4 w-4" />
          </Button>
        </div>
      </div>
    </div>
  );
}
