import type { AgingTier, ActionStatus } from "./types";

const currencyFormatter = new Intl.NumberFormat("en-US", {
  style: "currency",
  currency: "USD",
  maximumFractionDigits: 0,
});

const currencyPreciseFormatter = new Intl.NumberFormat("en-US", {
  style: "currency",
  currency: "USD",
  maximumFractionDigits: 2,
});

/** Whole-dollar currency for KPIs and prices (e.g. $27,500). */
export function formatCurrency(value: number): string {
  return currencyFormatter.format(value);
}

/** Two-decimal currency for accrued carrying cost (e.g. $2,314.20). */
export function formatCurrencyPrecise(value: number): string {
  return currencyPreciseFormatter.format(value);
}

export function formatPercent(value: number): string {
  return `${value.toFixed(1)}%`;
}

export function formatDate(iso: string): string {
  return new Date(iso).toLocaleDateString("en-US", {
    year: "numeric",
    month: "short",
    day: "numeric",
    timeZone: "UTC",
  });
}

export function formatDateTime(iso: string): string {
  return new Date(iso).toLocaleString("en-US", {
    year: "numeric",
    month: "short",
    day: "numeric",
    hour: "2-digit",
    minute: "2-digit",
  });
}

/** Insert spaces before capitals so PascalCase enums read as words (PriceReduction → "Price Reduction"). */
export function humanizeEnum(value: string): string {
  return value.replace(/([a-z])([A-Z])/g, "$1 $2");
}

interface TierStyle {
  label: string;
  /** The tier's canonical hex-var fill, used by swatches and the Recharts spectrum. */
  fill: string;
  /** Tailwind text-color utility for the tier hue. */
  text: string;
}

export const TIER_STYLES: Record<AgingTier, TierStyle> = {
  Fresh: { label: "Fresh", fill: "hsl(var(--tier-fresh))", text: "text-tier-fresh" },
  Watch: { label: "Watch", fill: "hsl(var(--tier-watch))", text: "text-tier-watch" },
  Aging: { label: "Aging", fill: "hsl(var(--tier-aging))", text: "text-tier-aging" },
  Critical: { label: "Critical", fill: "hsl(var(--tier-critical))", text: "text-tier-critical" },
};

/** Lifecycle status → mono-pill classes. A subtle progression proposed (indigo) → resolved (green). */
export const ACTION_STATUS_STYLES: Record<ActionStatus, string> = {
  Proposed: "text-primary border-primary/35 bg-primary/[0.05]",
  Approved: "text-tier-fresh border-tier-fresh/40 bg-tier-fresh/[0.06]",
  InProgress: "text-tier-aging border-tier-aging/40 bg-tier-aging/[0.06]",
  Resolved: "text-tier-fresh border-tier-fresh/40 bg-tier-fresh/[0.06]",
};
