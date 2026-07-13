import { useState } from "react";
import { HelpCircle, RefreshCw } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import { formatCurrency, humanizeEnum } from "@/lib/format";
import { useRecommendation } from "@/lib/hooks";
import type { ActionType, Recommendation } from "@/lib/types";

/**
 * The AI decision-support panel. Loads on demand (cost guard mirrors the server-side per-vehicle cache + rate limit).
 * Always shows a recommendation because the server degrades to the deterministic baseline when the LLM is unreachable;
 * the Source badge makes the provenance explicit (AI-enriched vs baseline).
 */
export function RecommendationPanel({
  vehicleId,
  onUseAction,
}: {
  vehicleId: string;
  onUseAction: (type: ActionType, proposedValue: number | null) => void;
}) {
  const { data, isLoading, isError, refetch, isFetching } = useRecommendation(vehicleId, true);

  return (
    <Card className="border-primary/20">
      <CardHeader className="p-5 pb-3">
        <div className="flex items-center justify-between">
          <CardTitle className="font-display flex items-center gap-1.5 text-[15px] font-bold tracking-[-0.01em]">
            Recommended action
            <RecommendationHint />
          </CardTitle>
          {data ? (
            <span className="ai-reveal-badge">
              <SourceBadge source={data.source} />
            </span>
          ) : null}
        </div>
      </CardHeader>
      <CardContent className="space-y-4 p-5 pt-0">
        {isLoading ? (
          <div className="space-y-2">
            <Skeleton className="h-6 w-40" />
            <Skeleton className="h-16 w-full" />
          </div>
        ) : isError || !data ? (
          <div className="space-y-3">
            <p className="text-sm text-muted-foreground">
              Couldn't load a recommendation right now.
            </p>
            <Button variant="outline" size="sm" onClick={() => refetch()}>
              <RefreshCw className="h-4 w-4" />
              Retry
            </Button>
          </div>
        ) : (
          <div className="ai-reveal-panel space-y-4">
            <RecommendationBody data={data} isFetching={isFetching} onUseAction={onUseAction} />
          </div>
        )}
      </CardContent>
    </Card>
  );
}

function RecommendationHint() {
  const [open, setOpen] = useState(false);
  const tooltipId = "recommendation-hint";

  return (
    <span className="relative inline-flex">
      <button
        type="button"
        aria-label="Recommendation grounding"
        aria-describedby={open ? tooltipId : undefined}
        aria-expanded={open}
        className="inline-flex cursor-help rounded-sm outline-none transition-colors focus-visible:ring-2 focus-visible:ring-ring"
        onMouseEnter={() => setOpen(true)}
        onMouseLeave={() => setOpen(false)}
        onClick={() => setOpen((value) => !value)}
        onBlur={() => setOpen(false)}
      >
        <HelpCircle
          className={`h-3.5 w-3.5 transition-colors ${
            open ? "text-muted-foreground" : "text-muted-foreground/60"
          }`}
        />
      </button>
      <span
        id={tooltipId}
        role="tooltip"
        className={`pointer-events-none absolute left-1/2 top-[calc(100%+8px)] z-50 w-56 -translate-x-1/2 rounded-md border border-border bg-card px-3 py-2 text-[11.5px] font-normal normal-case leading-snug tracking-normal text-card-foreground shadow-md transition-opacity duration-150 ${
          open ? "opacity-100" : "opacity-0"
        }`}
      >
        Grounded in this vehicle's aging, pricing and segment signals.
      </span>
    </span>
  );
}

function RecommendationBody({
  data,
  isFetching,
  onUseAction,
}: {
  data: Recommendation;
  isFetching: boolean;
  onUseAction: (type: ActionType, proposedValue: number | null) => void;
}) {
  return (
    <>
      <div className="flex items-baseline gap-3 border-y py-4">
        <span className="w-[70px] shrink-0 pt-0.5 text-[10.5px] font-semibold uppercase tracking-[0.08em] text-muted-foreground">
          Action
        </span>
        <div>
          <div className="font-display text-[18px] font-bold tracking-[-0.01em]">
            {humanizeEnum(data.recommendedAction)}
          </div>
          {data.proposedValue !== null ? (
            <div className="mono mt-1 text-[13px] font-medium text-tier-fresh">
              Target {formatCurrency(data.proposedValue)}
            </div>
          ) : null}
        </div>
        {isFetching ? (
          <RefreshCw className="ml-auto h-3.5 w-3.5 animate-spin text-muted-foreground" />
        ) : null}
      </div>

      <p className="text-[13px] leading-relaxed">{data.rationale}</p>

      {data.marketRead ? (
        <div className="rounded-md border border-primary/15 bg-primary/[0.035] p-3">
          <p className="text-[10.5px] font-semibold uppercase tracking-[0.08em] text-primary">
            Market read
          </p>
          <p className="mt-1.5 text-[12.5px] leading-relaxed text-muted-foreground">
            {data.marketRead}
          </p>
        </div>
      ) : null}

      {data.groundingFacts.length > 0 ? (
        <div className="space-y-1">
          <p className="text-[10.5px] font-semibold uppercase tracking-[0.08em] text-muted-foreground">
            Grounding facts
          </p>
          <ul className="space-y-1">
            {data.groundingFacts.map((fact, i) => (
              <li key={i} className="mono flex items-center gap-2.5 py-1 text-[12.5px]">
                <span className="grid h-4 w-4 shrink-0 place-items-center rounded-sm bg-tier-fresh/15 text-[11px] font-bold text-tier-fresh">
                  ✓
                </span>
                <span>{fact}</span>
              </li>
            ))}
          </ul>
        </div>
      ) : null}

      <Button
        className="w-full"
        onClick={() => onUseAction(data.recommendedAction, data.proposedValue)}
      >
        Use this recommendation
      </Button>
    </>
  );
}

function SourceBadge({ source }: { source: Recommendation["source"] }) {
  return source === "Ai" ? (
    <span className="mono inline-flex items-center gap-1.5 rounded-sm border border-primary/30 bg-primary/[0.05] px-2.5 py-1 text-[10.5px] uppercase tracking-[0.06em] text-primary">
      <span className="h-1.5 w-1.5 rounded-full bg-primary" />
      AI-enriched
    </span>
  ) : (
    <span className="mono inline-flex items-center rounded-sm border border-border px-2.5 py-1 text-[10.5px] uppercase tracking-[0.06em] text-muted-foreground">
      Baseline
    </span>
  );
}
