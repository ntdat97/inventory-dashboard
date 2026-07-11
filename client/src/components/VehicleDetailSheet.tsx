import { useState } from "react";
import { Loader2 } from "lucide-react";
import {
  Sheet,
  SheetContent,
  SheetDescription,
  SheetHeader,
  SheetTitle,
} from "@/components/ui/sheet";
import { Card, CardContent } from "@/components/ui/card";
import { TierBadge } from "@/components/TierBadge";
import { ActionPanel, type ActionDraft } from "@/components/ActionPanel";
import { RecommendationPanel } from "@/components/RecommendationPanel";
import { useVehicle } from "@/lib/hooks";
import { formatCurrency, formatCurrencyPrecise, formatDate, humanizeEnum } from "@/lib/format";
import type { ActionType, VehicleDetail } from "@/lib/types";

/**
 * The vehicle detail Sheet: the demo's drill-down. Header + key figures, then the AI recommendation panel, then the
 * action/history panel. "Use this recommendation" prefills the action draft so the demo arc flows in one motion.
 */
export function VehicleDetailSheet({
  vehicleId,
  onOpenChange,
}: {
  vehicleId: string | null;
  onOpenChange: (open: boolean) => void;
}) {
  const { data, isLoading } = useVehicle(vehicleId);
  const [draft, setDraft] = useState<ActionDraft>({
    type: "PriceReduction",
    proposedValue: "",
    note: "",
  });

  function useRecommendation(type: ActionType, proposedValue: number | null) {
    setDraft({
      type,
      proposedValue: proposedValue !== null ? String(proposedValue) : "",
      note: "From AI recommendation",
    });
  }

  return (
    <Sheet open={vehicleId !== null} onOpenChange={onOpenChange}>
      <SheetContent>
        {isLoading || !data ? (
          <div className="flex h-full items-center justify-center">
            <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
          </div>
        ) : (
          <DetailBody
            data={data}
            draft={draft}
            onDraftChange={setDraft}
            onUseRecommendation={useRecommendation}
          />
        )}
      </SheetContent>
    </Sheet>
  );
}

function DetailBody({
  data,
  draft,
  onDraftChange,
  onUseRecommendation,
}: {
  data: VehicleDetail;
  draft: ActionDraft;
  onDraftChange: (draft: ActionDraft) => void;
  onUseRecommendation: (type: ActionType, proposedValue: number | null) => void;
}) {
  const figures: { label: string; value: string; accent?: boolean }[] = [
    { label: "Days in stock", value: String(data.daysInInventory) },
    { label: "List price", value: formatCurrency(data.listPrice) },
    { label: "Acquisition cost", value: formatCurrency(data.acquisitionCost) },
    {
      label: "Carrying cost to date",
      value: formatCurrencyPrecise(data.carryingCostToDate),
      accent: true,
    },
  ];

  return (
    <>
      <SheetHeader>
        <div className="flex items-center gap-2">
          <SheetTitle>
            {data.year} {data.make} {data.model}
          </SheetTitle>
          <TierBadge tier={data.tier} />
        </div>
        <SheetDescription>
          {data.trim ? `${data.trim} · ` : ""}
          {humanizeEnum(data.status)} · VIN {data.vin}
          {data.dealershipName ? ` · ${data.dealershipName}` : ""}
          {" · acquired "}
          {formatDate(data.acquisitionDate)}
        </SheetDescription>
      </SheetHeader>

      <Card>
        <CardContent className="grid grid-cols-2 gap-4 p-4 sm:grid-cols-4">
          {figures.map((f) => (
            <div key={f.label} className="space-y-0.5">
              <p className="text-xs text-muted-foreground">{f.label}</p>
              <p
                className={`text-sm font-semibold tabular-nums ${
                  f.accent ? "text-tier-aging" : ""
                }`}
              >
                {f.value}
              </p>
            </div>
          ))}
        </CardContent>
      </Card>

      <RecommendationPanel vehicleId={data.id} onUseAction={onUseRecommendation} />

      <ActionPanel
        vehicleId={data.id}
        history={data.history}
        draft={draft}
        onDraftChange={onDraftChange}
      />
    </>
  );
}
