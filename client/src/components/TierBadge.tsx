import { cn } from "@/lib/utils";
import { TIER_STYLES } from "@/lib/format";
import type { AgingTier } from "@/lib/types";

/**
 * The aging tier as a swatch + label — the tier hue is the data, so it's shown literally as a colour chip rather than
 * a filled pill. Reused by the grid, the detail header and the ribbon legend.
 */
export function TierBadge({ tier, className }: { tier: AgingTier; className?: string }) {
  const style = TIER_STYLES[tier];
  return (
    <span className={cn("inline-flex items-center gap-2 text-[12.5px] font-semibold", className)}>
      <span
        className="h-[11px] w-[11px] shrink-0 rounded-sm"
        style={{ backgroundColor: style.fill }}
      />
      {style.label}
    </span>
  );
}
