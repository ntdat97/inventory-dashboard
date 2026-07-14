import { useEffect, useRef, useState } from "react";
import { Loader2, Lock, RotateCcw, ShieldCheck } from "lucide-react";
import {
  Sheet,
  SheetContent,
  SheetDescription,
  SheetHeader,
  SheetTitle,
} from "@/components/ui/sheet";
import { Button } from "@/components/ui/button";
import { TierBadge } from "@/components/TierBadge";
import { ActionPanel, type ActionDraft } from "@/components/ActionPanel";
import { RecommendationPanel } from "@/components/RecommendationPanel";
import { ApiError } from "@/lib/api";
import { isClosedStatus } from "@/lib/lifecycle";
import { useVehicle, useVehicleReservation } from "@/lib/hooks";
import { formatCurrency, formatCurrencyPrecise, formatDate, humanizeEnum } from "@/lib/format";
import type { ActionType, VehicleDetail } from "@/lib/types";

/**
 * The vehicle detail Sheet: the demo's drill-down. Header + key figures, then the operational action/history panel,
 * then the AI-assisted recommendation as supporting input. "Use this recommendation" prefills the action draft.
 */
export function VehicleDetailSheet({
  vehicleId,
  onOpenChange,
}: {
  vehicleId: string | null;
  onOpenChange: (open: boolean) => void;
}) {
  const { data, isLoading } = useVehicle(vehicleId);
  const contentRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    contentRef.current?.scrollTo({ top: 0 });
  }, [vehicleId]);

  return (
    <Sheet open={vehicleId !== null} onOpenChange={onOpenChange}>
      <SheetContent
        ref={contentRef}
        onOpenAutoFocus={(event) => {
          event.preventDefault();
          contentRef.current?.focus();
        }}
      >
        {isLoading || !data ? (
          <div className="flex h-full items-center justify-center">
            <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
          </div>
        ) : (
          // Keyed by vehicle id: switching vehicles remounts the body, resetting the action draft to blank
          // defaults so a proposed value typed for one car never bleeds into the next.
          <DetailBody key={data.id} data={data} />
        )}
      </SheetContent>
    </Sheet>
  );
}

function DetailBody({ data }: { data: VehicleDetail }) {
  const [draft, setDraft] = useState<ActionDraft>({
    type: "PriceReduction",
    proposedValue: "",
    note: "",
  });
  // Bumped only when the user pulls in the AI recommendation; the action panel keys the proposed-value
  // field off it to replay a one-shot flash, so the prefilled number visibly announces itself.
  const [flashKey, setFlashKey] = useState(0);

  // A closed unit's record is frozen: metrics stopped accruing at ClosedDate, so there's no live decision to make —
  // the recommendation panel and action form are hidden and the history reads as an archived, review-only log.
  const closed = isClosedStatus(data.status);
  const reserved = data.status === "Reserved";

  function applyRecommendation(type: ActionType, proposedValue: number | null) {
    setDraft({
      type,
      proposedValue: proposedValue !== null ? String(proposedValue) : "",
      note: "From AI recommendation",
    });
    setFlashKey((k) => k + 1);
  }

  const figures: { label: string; value: string; accent?: "crit" | "warn" }[] = [
    {
      label: closed ? "Days in stock (final)" : "Days in stock",
      value: String(data.daysInInventory),
      accent: data.tier === "Critical" ? "crit" : undefined,
    },
    { label: "List price", value: formatCurrency(data.listPrice) },
    { label: "Acquisition cost", value: formatCurrency(data.acquisitionCost) },
    {
      label: closed ? "Carrying cost (final)" : "Carrying cost",
      value: formatCurrencyPrecise(data.carryingCostToDate),
      accent: "warn",
    },
  ];

  return (
    <>
      <SheetHeader>
        <div className="eyebrow mb-1">
          <span className="idx">·</span> Vehicle inspection
        </div>
        <div className="flex items-start justify-between gap-4">
          <div>
            <SheetTitle className="font-display text-[24px] font-extrabold leading-[1.05] tracking-[-0.02em]">
              {data.year} {data.make} {data.model}
            </SheetTitle>
            <SheetDescription className="mono mt-2 text-[11.5px] leading-relaxed tracking-[0.02em]">
              {data.trim ? `${data.trim} · ` : ""}VIN {data.vin}
              {data.dealershipName ? ` · ${data.dealershipName}` : ""} ·{" "}
              {humanizeEnum(data.status)} · acquired {formatDate(data.acquisitionDate)}
            </SheetDescription>
          </div>
          <TierBadge
            tier={data.tier}
            className="shrink-0 rounded border border-tier-critical/30 bg-tier-critical/[0.08] px-3 py-1.5 uppercase tracking-[0.04em]"
          />
        </div>
      </SheetHeader>

      {/* Stat readout — bordered gauge cells, mono figures */}
      <div className="grid min-h-[92px] grid-cols-2 overflow-hidden rounded-lg border sm:grid-cols-4">
        {figures.map((f, i) => (
          <div
            key={f.label}
            className={`grid min-h-[92px] grid-rows-[24px_1fr] border-b border-r px-5 py-3 last:border-r-0 sm:border-b-0 ${
              i % 2 === 1 ? "border-r-0 sm:border-r" : ""
            }`}
          >
            <p className="text-[10px] font-semibold uppercase leading-[1.2] tracking-[0.07em] text-muted-foreground">
              {f.label}
            </p>
            <p
              className={`mono self-end text-[20px] font-semibold leading-none tracking-[-0.02em] ${
                f.accent === "crit"
                  ? "text-tier-critical"
                  : f.accent === "warn"
                    ? "text-tier-aging"
                    : ""
              }`}
            >
              {f.value}
            </p>
          </div>
        ))}
      </div>

      {closed ? (
        <div className="flex items-start gap-2.5 rounded-lg border border-border bg-muted/40 px-4 py-3">
          <Lock className="mt-0.5 h-3.5 w-3.5 shrink-0 text-muted-foreground" />
          <p className="text-[12.5px] leading-relaxed text-muted-foreground">
            <span className="font-medium text-foreground">{humanizeEnum(data.status)}</span>
            {data.closedDate ? ` on ${formatDate(data.closedDate)}` : ""} — this unit has left inventory. Its aging and
            carrying cost are frozen at that date and the action history below is read-only.
          </p>
        </div>
      ) : reserved ? (
        <div className="flex items-start gap-2.5 rounded-lg border border-amber-300/60 bg-amber-50/70 px-4 py-3 text-amber-950">
          <ShieldCheck className="mt-0.5 h-3.5 w-3.5 shrink-0" />
          <p className="text-[12.5px] leading-relaxed">
            This vehicle is reserved for a pending deal. Release the hold before starting a new pricing,
            promotion, transfer, or auction action.
          </p>
        </div>
      ) : null}

      <ActionPanel
        vehicleId={data.id}
        history={data.history}
        draft={draft}
        onDraftChange={setDraft}
        flashKey={flashKey}
        readOnly={closed || reserved}
      />

      {!closed && !reserved ? (
        <RecommendationPanel vehicleId={data.id} onUseAction={applyRecommendation} />
      ) : null}

      {!closed ? <ReservationPanel vehicle={data} /> : null}
    </>
  );
}

function ReservationPanel({ vehicle }: { vehicle: VehicleDetail }) {
  const reservation = useVehicleReservation(vehicle.id);
  const [error, setError] = useState<string | null>(null);
  const reserved = vehicle.status === "Reserved";

  async function mutate() {
    setError(null);
    try {
      await reservation.mutateAsync(reserved ? "release" : "reserve");
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Couldn't update the reservation.");
    }
  }

  return (
    <div className="flex items-start justify-between gap-3 rounded-lg border bg-card px-4 py-3">
      <div className="space-y-1">
        <p className="text-[10.5px] font-semibold uppercase tracking-[0.08em] text-muted-foreground">
          Availability
        </p>
        <p className="text-[12.5px] leading-relaxed text-muted-foreground">
          {reserved
            ? "Held for a pending customer deal; still counted as active inventory."
            : "Available for action. Reserve it when a customer hold is pending."}
        </p>
        {error ? (
          <p className="text-xs text-destructive" role="alert">
            {error}
          </p>
        ) : null}
      </div>
      <Button size="sm" variant="outline" disabled={reservation.isPending} onClick={mutate}>
        {reservation.isPending ? (
          <Loader2 className="h-3.5 w-3.5 animate-spin" />
        ) : reserved ? (
          <RotateCcw className="h-3.5 w-3.5" />
        ) : (
          <ShieldCheck className="h-3.5 w-3.5" />
        )}
        {reserved ? "Release hold" : "Reserve"}
      </Button>
    </div>
  );
}
