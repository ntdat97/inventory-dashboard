import { Filter } from "lucide-react";
import { Popover, PopoverContent, PopoverTrigger } from "@/components/ui/popover";
import { cn } from "@/lib/utils";

export interface FilterOption<T extends string> {
  value: T;
  label: string;
}

/**
 * A column-header filter control: renders the header label plus a funnel button that opens a checkbox
 * popover. Multi-select — each toggle OR-adds to the active set; an empty set means "no filter" (all rows).
 * Used in the inventory ledger header for Aging tier and Status, replacing the standalone filter-bar Selects.
 */
export function HeaderFilterMenu<T extends string>({
  title,
  options,
  selected,
  onChange,
  align = "start",
}: {
  title: string;
  options: FilterOption<T>[];
  selected: T[];
  onChange: (next: T[]) => void;
  align?: "start" | "end";
}) {
  const count = selected.length;

  function toggle(value: T) {
    onChange(
      selected.includes(value) ? selected.filter((v) => v !== value) : [...selected, value],
    );
  }

  return (
    <Popover>
      <PopoverTrigger
        className={cn(
          "inline-flex items-center gap-1.5 uppercase tracking-[0.08em] outline-none hover:text-foreground focus-visible:text-foreground",
          count > 0 && "text-foreground",
        )}
      >
        {title}
        <span className="relative inline-flex">
          <Filter className={cn("h-3.5 w-3.5", count > 0 ? "opacity-100" : "opacity-40")} />
          {count > 0 ? (
            <span className="mono absolute -right-2 -top-1.5 flex h-3.5 min-w-3.5 items-center justify-center rounded-full bg-primary px-1 text-[8.5px] font-semibold leading-none text-primary-foreground">
              {count}
            </span>
          ) : null}
        </span>
      </PopoverTrigger>
      <PopoverContent align={align} className="w-52">
        <div className="flex items-center justify-between px-2 pb-1.5 pt-1">
          <span className="text-[10.5px] font-semibold uppercase tracking-[0.08em] text-muted-foreground">
            {title}
          </span>
          <button
            type="button"
            disabled={count === 0}
            onClick={() => onChange([])}
            className="text-[11px] font-medium text-muted-foreground hover:text-foreground disabled:opacity-40"
          >
            Clear
          </button>
        </div>
        <div className="space-y-0.5">
          {options.map((opt) => {
            const checked = selected.includes(opt.value);
            return (
              <button
                key={opt.value}
                type="button"
                onClick={() => toggle(opt.value)}
                className="flex w-full items-center gap-2.5 rounded-sm px-2 py-1.5 text-left text-[13px] normal-case tracking-normal hover:bg-[hsl(var(--muted))]"
              >
                <span
                  className={cn(
                    "flex h-4 w-4 shrink-0 items-center justify-center rounded-[4px] border transition-colors",
                    checked
                      ? "border-primary bg-primary text-primary-foreground"
                      : "border-input bg-card",
                  )}
                >
                  {checked ? <CheckIcon /> : null}
                </span>
                <span className="font-medium text-foreground">{opt.label}</span>
              </button>
            );
          })}
        </div>
      </PopoverContent>
    </Popover>
  );
}

function CheckIcon() {
  return (
    <svg viewBox="0 0 16 16" className="h-3 w-3" fill="none" stroke="currentColor" strokeWidth={2.5}>
      <path d="M3.5 8.5l3 3 6-7" strokeLinecap="round" strokeLinejoin="round" />
    </svg>
  );
}
