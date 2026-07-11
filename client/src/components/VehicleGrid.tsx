import { useMemo } from "react";
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
import { formatCurrency, formatCurrencyPrecise, humanizeEnum } from "@/lib/format";
import type { PagedResult, VehicleListItem } from "@/lib/types";

// Columns that the API can sort on, mapped to their sort key. Header clicks cycle asc → desc on these.
const SORTABLE: Record<string, string> = {
  make: "make",
  year: "year",
  listPrice: "listPrice",
  daysInInventory: "daysInInventory",
  carryingCostToDate: "carryingCost",
};

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
}: {
  data: PagedResult<VehicleListItem> | undefined;
  isLoading: boolean;
  isFetching: boolean;
  sort: SortState;
  onSortChange: (next: SortState) => void;
  onRowClick: (vehicle: VehicleListItem) => void;
  onPageChange: (page: number) => void;
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
              <span className="font-medium">
                {v.year} {v.make} {v.model}
              </span>
              <span className="text-xs text-muted-foreground">
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
          return (
            <div className="flex flex-col">
              <span className="tabular-nums">{v.daysInInventory}</span>
              {v.daysUntilAging !== null ? (
                <span className="text-xs text-muted-foreground">
                  {v.daysUntilAging}d until aging
                </span>
              ) : null}
            </div>
          );
        },
      },
      {
        accessorKey: "listPrice",
        header: "List price",
        cell: ({ row }) => (
          <span className="tabular-nums">{formatCurrency(row.original.listPrice)}</span>
        ),
      },
      {
        accessorKey: "carryingCostToDate",
        header: "Carrying cost",
        cell: ({ row }) => (
          <span className="tabular-nums text-tier-aging">
            {formatCurrencyPrecise(row.original.carryingCostToDate)}
          </span>
        ),
      },
      {
        accessorKey: "status",
        header: "Status",
        cell: ({ row }) => (
          <span className="text-sm text-muted-foreground">{humanizeEnum(row.original.status)}</span>
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

  return (
    <div className="space-y-3">
      <div className="rounded-lg border bg-card">
        <Table>
          <TableHeader>
            {table.getHeaderGroups().map((headerGroup) => (
              <TableRow key={headerGroup.id} className="hover:bg-transparent">
                {headerGroup.headers.map((header) => {
                  const columnId = header.column.id;
                  const sortKey = SORTABLE[columnId];
                  const isSorted = sortKey && sort.field === sortKey;
                  return (
                    <TableHead key={header.id}>
                      {sortKey ? (
                        <button
                          type="button"
                          className="inline-flex items-center gap-1 font-medium hover:text-foreground"
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
                    <TableCell key={ci}>
                      <Skeleton className="h-6 w-full" />
                    </TableCell>
                  ))}
                </TableRow>
              ))
            ) : table.getRowModel().rows.length === 0 ? (
              <TableRow className="hover:bg-transparent">
                <TableCell colSpan={columns.length} className="h-24 text-center text-muted-foreground">
                  No vehicles match these filters.
                </TableCell>
              </TableRow>
            ) : (
              table.getRowModel().rows.map((row) => (
                <TableRow
                  key={row.id}
                  className="cursor-pointer"
                  onClick={() => onRowClick(row.original)}
                >
                  {row.getVisibleCells().map((cell) => (
                    <TableCell key={cell.id}>
                      {flexRender(cell.column.columnDef.cell, cell.getContext())}
                    </TableCell>
                  ))}
                </TableRow>
              ))
            )}
          </TableBody>
        </Table>
      </div>

      <div className="flex items-center justify-between">
        <p className="text-xs text-muted-foreground">
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

