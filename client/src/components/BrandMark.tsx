import { cn } from "@/lib/utils";

/**
 * The signature identity element: a blueprint-framed square holding the Fresh→Critical aging spectrum as four fixed
 * bands. It states the whole product thesis — capital aging left-to-right — in 34px. Reused on the header and login.
 */
export function BrandMark({ className }: { className?: string }) {
  return (
    <div
      className={cn(
        "relative grid shrink-0 place-items-center rounded-[3px] border-[1.5px] border-primary",
        className,
      )}
    >
      <span
        className="absolute inset-[6px] rounded-[1px]"
        style={{
          background:
            "linear-gradient(90deg, hsl(var(--tier-fresh)) 0 25%, hsl(var(--tier-watch)) 25% 50%, hsl(var(--tier-aging)) 50% 75%, hsl(var(--tier-critical)) 75% 100%)",
        }}
      />
    </div>
  );
}
