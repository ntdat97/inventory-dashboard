import { Bar, BarChart, Cell, ResponsiveContainer, Tooltip, XAxis, YAxis } from "recharts";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import { TIER_STYLES } from "@/lib/format";
import { AGING_TIERS, type AgingTier, type InventorySummary } from "@/lib/types";

const TIER_HINT: Record<AgingTier, string> = {
  Fresh: "0–30 days",
  Watch: "31–60 days",
  Aging: "61–90 days",
  Critical: "91+ days",
};

/**
 * The aging spectrum: a Fresh→Critical distribution of held units. This is the app's core visual — it turns a binary
 * "aged or not" flag into a spectrum so a manager sees problems forming, not just problems arrived.
 */
export function AgingSpectrum({
  summary,
  isLoading,
  onSelectTier,
}: {
  summary: InventorySummary | undefined;
  isLoading: boolean;
  onSelectTier?: (tier: AgingTier) => void;
}) {
  if (isLoading || !summary) {
    return <Skeleton className="h-[320px]" />;
  }

  const data = AGING_TIERS.map((tier) => ({
    tier,
    label: TIER_STYLES[tier].label,
    hint: TIER_HINT[tier],
    count: summary.tierBreakdown[tier] ?? 0,
    fill: TIER_STYLES[tier].fill,
  }));

  return (
    <Card>
      <CardHeader>
        <CardTitle className="text-base">Aging spectrum</CardTitle>
        <CardDescription>
          Distribution of held units by aging tier. Click a bar to filter the grid.
        </CardDescription>
      </CardHeader>
      <CardContent>
        <div className="h-[220px]">
          <ResponsiveContainer width="100%" height="100%">
            <BarChart data={data} margin={{ top: 8, right: 8, bottom: 0, left: -20 }}>
              <XAxis
                dataKey="label"
                tickLine={false}
                axisLine={false}
                fontSize={12}
                stroke="hsl(var(--muted-foreground))"
              />
              <YAxis
                allowDecimals={false}
                tickLine={false}
                axisLine={false}
                fontSize={12}
                stroke="hsl(var(--muted-foreground))"
              />
              <Tooltip
                cursor={{ fill: "hsl(var(--muted))" }}
                contentStyle={{
                  borderRadius: 8,
                  border: "1px solid hsl(var(--border))",
                  fontSize: 12,
                }}
                formatter={(value: number) => [`${value} units`, "Held"]}
                labelFormatter={(label: string, payload) => {
                  const hint = payload?.[0]?.payload?.hint;
                  return hint ? `${label} · ${hint}` : label;
                }}
              />
              <Bar
                dataKey="count"
                radius={[6, 6, 0, 0]}
                onClick={(entry: { tier?: AgingTier }) =>
                  entry.tier && onSelectTier?.(entry.tier)
                }
                cursor={onSelectTier ? "pointer" : undefined}
              >
                {data.map((entry) => (
                  <Cell key={entry.tier} fill={entry.fill} />
                ))}
              </Bar>
            </BarChart>
          </ResponsiveContainer>
        </div>

        <div className="mt-4 grid grid-cols-2 gap-2 sm:grid-cols-4">
          {data.map((entry) => (
            <button
              key={entry.tier}
              type="button"
              onClick={() => onSelectTier?.(entry.tier)}
              className="flex items-center gap-2 rounded-md border px-2.5 py-2 text-left transition-colors hover:bg-accent"
            >
              <span className="h-2.5 w-2.5 rounded-full" style={{ backgroundColor: entry.fill }} />
              <span className="flex flex-col">
                <span className="text-xs font-medium">{entry.label}</span>
                <span className="text-[11px] text-muted-foreground">{entry.count} units</span>
              </span>
            </button>
          ))}
        </div>
      </CardContent>
    </Card>
  );
}
