import { Badge } from "@/components/ui/badge";
import { cn } from "@/lib/utils";
import { TIER_STYLES } from "@/lib/format";
import type { AgingTier } from "@/lib/types";

/** The aging tier rendered as its canonical coloured pill — reused by the grid, detail header and spectrum legend. */
export function TierBadge({ tier, className }: { tier: AgingTier; className?: string }) {
  const style = TIER_STYLES[tier];
  return (
    <Badge variant="outline" className={cn(style.badge, className)}>
      {style.label}
    </Badge>
  );
}
