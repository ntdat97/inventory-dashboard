import { Sparkles, Lightbulb, RefreshCw, ShieldCheck } from "lucide-react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
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
      <CardHeader>
        <div className="flex items-center justify-between">
          <CardTitle className="flex items-center gap-2 text-base">
            <Sparkles className="h-4 w-4 text-primary" />
            Recommended action
          </CardTitle>
          {data ? <SourceBadge source={data.source} /> : null}
        </div>
        <CardDescription>
          Grounded in this vehicle's aging, pricing and segment signals.
        </CardDescription>
      </CardHeader>
      <CardContent className="space-y-4">
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
          <RecommendationBody data={data} isFetching={isFetching} onUseAction={onUseAction} />
        )}
      </CardContent>
    </Card>
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
      <div className="flex flex-wrap items-center gap-2">
        <Badge className="bg-primary/10 text-primary" variant="outline">
          <Lightbulb className="mr-1 h-3.5 w-3.5" />
          {humanizeEnum(data.recommendedAction)}
        </Badge>
        {data.proposedValue !== null ? (
          <span className="text-sm font-medium">
            Target: {formatCurrency(data.proposedValue)}
          </span>
        ) : null}
        {isFetching ? <RefreshCw className="h-3.5 w-3.5 animate-spin text-muted-foreground" /> : null}
      </div>

      <p className="text-sm leading-relaxed">{data.rationale}</p>

      {data.groundingFacts.length > 0 ? (
        <div className="space-y-1.5 rounded-md bg-muted/60 p-3">
          <p className="text-xs font-medium text-muted-foreground">Grounding facts</p>
          <ul className="space-y-1">
            {data.groundingFacts.map((fact, i) => (
              <li key={i} className="flex items-start gap-2 text-xs">
                <ShieldCheck className="mt-0.5 h-3.5 w-3.5 shrink-0 text-tier-fresh" />
                <span>{fact}</span>
              </li>
            ))}
          </ul>
        </div>
      ) : null}

      <Button
        size="sm"
        onClick={() => onUseAction(data.recommendedAction, data.proposedValue)}
      >
        Use this recommendation
      </Button>
    </>
  );
}

function SourceBadge({ source }: { source: Recommendation["source"] }) {
  return source === "Ai" ? (
    <Badge variant="outline" className="border-primary/30 bg-primary/10 text-primary">
      AI-enriched
    </Badge>
  ) : (
    <Badge variant="outline" className="text-muted-foreground">
      Baseline
    </Badge>
  );
}
