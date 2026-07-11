import { AlertTriangle, Banknote, Clock, Layers, TrendingDown, Wallet } from "lucide-react";
import { Card, CardContent } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import { formatCurrency, formatPercent } from "@/lib/format";
import type { InventorySummary } from "@/lib/types";

interface Kpi {
  label: string;
  value: string;
  sub: string;
  icon: React.ComponentType<{ className?: string }>;
  emphasis?: boolean;
}

function buildKpis(s: InventorySummary): Kpi[] {
  return [
    {
      label: "Capital tied in aged stock",
      value: formatCurrency(s.capitalTiedInAged),
      sub: `${s.agedUnits} unit${s.agedUnits === 1 ? "" : "s"} at 90+ days`,
      icon: AlertTriangle,
      emphasis: true,
    },
    {
      label: "Total inventory value",
      value: formatCurrency(s.totalInventoryValue),
      sub: `${s.totalUnits} unit${s.totalUnits === 1 ? "" : "s"} held`,
      icon: Wallet,
    },
    {
      label: "Aged share of stock",
      value: formatPercent(s.agedPercent),
      sub: "of units are aged/distressed",
      icon: TrendingDown,
    },
    {
      label: "Carrying cost to date",
      value: formatCurrency(s.totalCarryingCostToDate),
      sub: "estimated, accrued across held stock",
      icon: Banknote,
    },
    {
      label: "Avg days in inventory",
      value: `${Math.round(s.avgDaysInInventory)} days`,
      sub: "across held stock",
      icon: Clock,
    },
    {
      label: "Units held",
      value: String(s.totalUnits),
      sub: "InStock + Reserved",
      icon: Layers,
    },
  ];
}

export function KpiCards({
  summary,
  isLoading,
}: {
  summary: InventorySummary | undefined;
  isLoading: boolean;
}) {
  if (isLoading || !summary) {
    return (
      <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3">
        {Array.from({ length: 6 }).map((_, i) => (
          <Skeleton key={i} className="h-[104px]" />
        ))}
      </div>
    );
  }

  const kpis = buildKpis(summary);

  return (
    <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3">
      {kpis.map((kpi) => {
        const Icon = kpi.icon;
        return (
          <Card key={kpi.label} className={kpi.emphasis ? "border-tier-critical/40" : undefined}>
            <CardContent className="flex items-start justify-between p-5">
              <div className="space-y-1">
                <p className="text-xs font-medium text-muted-foreground">{kpi.label}</p>
                <p
                  className={`text-2xl font-semibold tracking-tight ${
                    kpi.emphasis ? "text-tier-critical" : ""
                  }`}
                >
                  {kpi.value}
                </p>
                <p className="text-xs text-muted-foreground">{kpi.sub}</p>
              </div>
              <div
                className={`flex h-9 w-9 shrink-0 items-center justify-center rounded-md ${
                  kpi.emphasis
                    ? "bg-tier-critical/15 text-tier-critical"
                    : "bg-muted text-muted-foreground"
                }`}
              >
                <Icon className="h-4 w-4" />
              </div>
            </CardContent>
          </Card>
        );
      })}
    </div>
  );
}
