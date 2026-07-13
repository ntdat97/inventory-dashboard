import { useAuth } from "@/auth/AuthContext";
import { BrandMark } from "@/components/BrandMark";

/** App chrome: the spectrum brand-mark + wordmark on the left, dealer/user identity + sign-out on the right. */
export function Header() {
  const { user, logout } = useAuth();

  const displayName = user?.name ?? user?.email ?? "Guest";
  const initials = displayName
    .split(/[\s.@]+/)
    .filter(Boolean)
    .slice(0, 2)
    .map((p) => p[0]?.toUpperCase())
    .join("");

  return (
    <header className="sticky top-0 z-30 border-b bg-card">
      <div className="mx-auto flex h-16 max-w-[1400px] items-center justify-between px-4 sm:px-8">
        <div className="flex items-center gap-3.5">
          <BrandMark className="h-[34px] w-[34px]" />
          <div className="leading-none">
            <p className="font-display text-[17px] font-extrabold tracking-[-0.02em]">
              Inventory Dashboard
            </p>
            <p className="mt-[3px] text-[11px] font-medium uppercase tracking-[0.03em] text-muted-foreground">
              Capital-at-risk decision support
            </p>
          </div>
        </div>

        <div className="flex items-center gap-5">
          <div className="hidden text-right leading-tight sm:block">
            <p className="text-[13px] font-semibold">Northgate Auto Group</p>
            <p className="mono text-[11.5px] text-muted-foreground">
              {displayName}
              {user?.role ? ` — ${user.role}` : ""}
            </p>
          </div>
          <div className="grid h-9 w-9 place-items-center rounded-full bg-primary font-display text-[13px] font-bold tracking-[0.02em] text-primary-foreground">
            {initials || "G"}
          </div>
          <button
            type="button"
            onClick={logout}
            className="rounded border border-border px-3 py-1.5 text-xs font-semibold text-primary transition-colors hover:border-primary hover:bg-primary/[0.04]"
          >
            Sign out
          </button>
        </div>
      </div>
    </header>
  );
}
