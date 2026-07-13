import { useEffect, useRef, useState } from "react";
import { Skeleton } from "@/components/ui/skeleton";
import { formatCurrency, formatPercent } from "@/lib/format";
import { AGING_TIERS, type AgingTier, type InventorySummary } from "@/lib/types";

/** Fixed spectrum metadata: the day-range each tier covers and its per-segment styling. Order = Fresh→Critical. */
const TIER_META: Record<
  AgingTier,
  { range: string; bar: string; text: string }
> = {
  Fresh: { range: "0–30d", bar: "bg-tier-fresh", text: "text-white" },
  Watch: { range: "31–60d", bar: "bg-tier-watch", text: "text-white" },
  Aging: { range: "61–90d", bar: "bg-tier-aging", text: "text-white" },
  Critical: { range: "91+d", bar: "bg-tier-critical", text: "text-white" },
};

interface Gauge {
  label: string;
  value: string;
  warn?: boolean;
}

/** A single tier's laid-out slice of the runway: its metadata plus the computed count/share for this dataset. */
interface Segment {
  tier: AgingTier;
  count: number;
  share: number;
  range: string;
  bar: string;
  text: string;
}

/** Below this rendered width a segment is too narrow to fit both its title and the trailing chip without clipping. */
const CHIP_MIN_WIDTH = 124;

/**
 * The runway ribbon: the app's signature visualization and hero in one. Held units laid out left→right along the aging
 * runway; the further right the capital sits, the faster it bleeds carry. It replaces the six-KPI card grid — the
 * headline figure (capital tied in aged stock) is the alarm on the right, secondary metrics read out below the band.
 */
export function RunwayRibbon({
  summary,
  isLoading,
  onSelectTier,
  activeTiers = [],
}: {
  summary: InventorySummary | undefined;
  isLoading: boolean;
  onSelectTier?: (tier: AgingTier) => void;
  activeTiers?: AgingTier[];
}) {
  // Animate the segments open on first paint (respecting reduced-motion); start collapsed, then flush to real widths.
  const [open, setOpen] = useState(false);
  useEffect(() => {
    const reduce = window.matchMedia?.("(prefers-reduced-motion: reduce)").matches;
    if (reduce) {
      setOpen(true);
      return;
    }
    const id = requestAnimationFrame(() => setOpen(true));
    return () => cancelAnimationFrame(id);
  }, []);

  if (isLoading || !summary) {
    return (
      <div className="space-y-3">
        <Skeleton className="h-[132px] rounded-lg" />
        <Skeleton className="h-[62px] rounded-lg" />
      </div>
    );
  }

  const total = summary.totalUnits || 1;
  const segments = AGING_TIERS.map((tier) => {
    const count = summary.tierBreakdown[tier] ?? 0;
    return { tier, count, share: (count / total) * 100, ...TIER_META[tier] };
  });

  const gauges: Gauge[] = [
    { label: "Total inventory value", value: formatCurrency(summary.totalInventoryValue) },
    { label: "Aged share of stock", value: formatPercent(summary.agedPercent), warn: true },
    { label: "Carrying cost to date", value: formatCurrency(summary.totalCarryingCostToDate) },
    { label: "Avg days in inventory", value: `${Math.round(summary.avgDaysInInventory)} days` },
    { label: "Units held", value: `${summary.totalUnits} units` },
  ];

  return (
    <div>
      {/* Hero head: the question + the one alarming figure */}
      <div className="mb-6 flex flex-col gap-5 sm:flex-row sm:items-end sm:justify-between sm:gap-10">
        <div className="max-w-[560px]">
          <h1 className="font-display text-[30px] font-extrabold leading-[1.04] tracking-[-0.03em] sm:text-[36px]">
            Where is my money stuck?
          </h1>
          <p className="mt-2.5 max-w-[460px] text-[13.5px] text-muted-foreground">
            {summary.totalUnits} units on the ground, aging left to right. The further right the
            capital sits, the faster it bleeds carrying cost.
          </p>
        </div>
        <div className="shrink-0 border-l-2 border-tier-critical pl-6 sm:text-right">
          <div className="mono text-[40px] font-semibold leading-[0.95] tracking-[-0.03em] text-tier-critical sm:text-[46px]">
            {formatCurrency(summary.capitalTiedInAged)}
          </div>
          <div className="mt-2.5 text-[11px] font-semibold uppercase tracking-[0.08em] text-muted-foreground">
            Capital tied in aged stock · 91+ days
          </div>
        </div>
      </div>

      {/* Ribbon frame */}
      <div className="rounded-t-lg border border-border bg-card px-6 pb-4 pt-5">
        <div className="mono mb-2 flex justify-between px-px text-[10.5px] tracking-[0.04em] text-muted-foreground">
          <span>0 days</span>
          <span>30</span>
          <span>60</span>
          <span>90</span>
          <span>120+ days</span>
        </div>
        <div className="flex h-[118px] gap-0.5 overflow-hidden rounded bg-background">
          {segments.map((s) => (
            <RibbonSegment
              key={s.tier}
              segment={s}
              open={open}
              clickable={Boolean(onSelectTier)}
              isActive={activeTiers.includes(s.tier)}
              dimmed={activeTiers.length > 0 && !activeTiers.includes(s.tier)}
              onSelect={() => onSelectTier?.(s.tier)}
            />
          ))}
        </div>
      </div>

      {/* Instrument readout strip */}
      <div className="flex flex-wrap overflow-hidden rounded-b-lg border border-t-0 border-border bg-card">
        {gauges.map((g) => (
          <div
            key={g.label}
            className="flex-1 basis-40 border-l border-border px-5 py-3.5 first:border-l-0"
          >
            <div className="mb-1.5 text-[10.5px] font-semibold uppercase tracking-[0.08em] text-muted-foreground">
              {g.label}
            </div>
            <div
              className={`mono text-[21px] font-semibold tracking-[-0.02em] ${
                g.warn ? "text-tier-aging" : ""
              }`}
            >
              {g.value}
            </div>
          </div>
        ))}
      </div>
    </div>
  );
}

/**
 * One tier slice of the runway. It measures its own rendered width and drops the trailing chip (the Critical
 * "PEAK RISK" flag, or the day-range on the others) once it gets too narrow to hold the title + chip on one line —
 * otherwise the chip clips mid-word ("PEA…"). The tier name always wins the space; the chip is the expendable flourish.
 */
function RibbonSegment({
  segment: s,
  open,
  clickable,
  isActive,
  dimmed,
  onSelect,
}: {
  segment: Segment;
  open: boolean;
  clickable: boolean;
  isActive: boolean;
  dimmed: boolean;
  onSelect: () => void;
}) {
  const ref = useRef<HTMLButtonElement>(null);
  const [showChip, setShowChip] = useState(true);

  useEffect(() => {
    const el = ref.current;
    if (!el || typeof ResizeObserver === "undefined") return;
    const ro = new ResizeObserver(([entry]) => {
      setShowChip(entry.contentRect.width >= CHIP_MIN_WIDTH);
    });
    ro.observe(el);
    return () => ro.disconnect();
  }, []);

  return (
    <button
      ref={ref}
      type="button"
      disabled={!clickable}
      aria-pressed={clickable ? isActive : undefined}
      onClick={onSelect}
      title={clickable ? (isActive ? `Clear ${s.tier} filter` : `Filter to ${s.tier}`) : undefined}
      style={{
        width: open ? `${Math.max(s.share, s.count > 0 ? 7 : 0)}%` : "0%",
        transition:
          "width 1.1s cubic-bezier(0.22, 1, 0.36, 1), opacity 0.2s ease, box-shadow 0.2s ease",
      }}
      className={`group relative flex min-w-0 flex-col justify-between overflow-hidden px-3.5 py-3 text-left ${s.bar} ${s.text} ${
        clickable ? "cursor-pointer" : ""
      } ${isActive ? "z-10 ring-[3px] ring-inset ring-white" : ""} ${dimmed ? "opacity-40" : ""}`}
    >
      <div className="flex items-center justify-between gap-1.5">
        <span className="font-display min-w-0 truncate text-[13px] font-bold uppercase tracking-[0.02em]">
          {s.tier}
        </span>
        {showChip ? (
          s.tier === "Critical" ? (
            <span className="mono shrink-0 whitespace-nowrap rounded-sm bg-black/20 px-1.5 py-0.5 text-[8.5px] tracking-[0.12em]">
              PEAK RISK
            </span>
          ) : (
            <span className="mono shrink-0 whitespace-nowrap text-[10px] opacity-80">{s.range}</span>
          )
        ) : null}
      </div>
      <div>
        <div className="mono text-[26px] font-semibold leading-none tracking-[-0.02em]">
          {s.count}
          <span className="ml-1 text-[12px] font-medium opacity-85">units</span>
        </div>
        <div className="mono mt-1 whitespace-nowrap text-[12px] font-medium opacity-90">
          {formatPercent(s.share)} of fleet
        </div>
      </div>
    </button>
  );
}
