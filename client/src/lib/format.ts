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
  /** Tailwind classes for a filled badge. */
  badge: string;
  /** The CSS var-backed fill used by the Recharts spectrum. */
  fill: string;
}

export const TIER_STYLES: Record<AgingTier, TierStyle> = {
  Fresh: {
    label: "Fresh",
    badge: "bg-tier-fresh/15 text-tier-fresh border-tier-fresh/30",
    fill: "hsl(var(--tier-fresh))",
  },
  Watch: {
    label: "Watch",
    badge: "bg-tier-watch/15 text-tier-watch border-tier-watch/30",
    fill: "hsl(var(--tier-watch))",
  },
  Aging: {
    label: "Aging",
    badge: "bg-tier-aging/15 text-tier-aging border-tier-aging/30",
    fill: "hsl(var(--tier-aging))",
  },
  Critical: {
    label: "Critical",
    badge: "bg-tier-critical/15 text-tier-critical border-tier-critical/30",
    fill: "hsl(var(--tier-critical))",
  },
};

/** Lifecycle status → badge classes; a subtle progression from proposed (neutral) to resolved (green). */
export const ACTION_STATUS_STYLES: Record<ActionStatus, string> = {
  Proposed: "bg-muted text-muted-foreground border-border",
  Approved: "bg-tier-watch/15 text-tier-watch border-tier-watch/30",
  InProgress: "bg-tier-aging/15 text-tier-aging border-tier-aging/30",
  Resolved: "bg-tier-fresh/15 text-tier-fresh border-tier-fresh/30",
};
