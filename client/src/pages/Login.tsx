import { useState } from "react";
import { Loader2 } from "lucide-react";
import { Button } from "@/components/ui/button";
import { BrandMark } from "@/components/BrandMark";
import { useAuth } from "@/auth/AuthContext";
import { ApiError } from "@/lib/api";

/** The /login page — full-screen sign-in. Two paths mirror the backend: real Entra SSO and the flag-gated guest demo. */
export function Login() {
  const { loginAsGuest, loginAsScoped, loginWithMicrosoft } = useAuth();
  const [busy, setBusy] = useState<"guest" | "scoped" | "microsoft" | null>(null);
  const [error, setError] = useState<string | null>(null);

  async function run(kind: "guest" | "scoped" | "microsoft", fn: () => Promise<void>) {
    setBusy(kind);
    setError(null);
    try {
      await fn();
    } catch (err) {
      const message =
        err instanceof ApiError ? err.message : "Something went wrong signing in. Please try again.";
      setError(message);
    } finally {
      setBusy(null);
    }
  }

  return (
    <div className="flex min-h-screen items-center justify-center bg-background p-4">
      <div className="relative w-full max-w-[420px] overflow-hidden rounded-[10px] border bg-card p-9 text-center shadow-[0_8px_30px_rgba(20,32,30,0.05)]">
        {/* Spectrum ribbon crowning the card — the same data language as the dashboard */}
        <span
          className="absolute inset-x-0 top-0 h-1"
          style={{
            background:
              "linear-gradient(90deg, hsl(var(--tier-fresh)) 0 25%, hsl(var(--tier-watch)) 25% 50%, hsl(var(--tier-aging)) 50% 75%, hsl(var(--tier-critical)) 75% 100%)",
          }}
        />
        <BrandMark className="mx-auto mb-5 mt-1.5 h-11 w-11" />
        <h1 className="font-display text-[22px] font-extrabold tracking-[-0.02em]">
          Inventory Dashboard
        </h1>
        <p className="mx-auto mb-6 mt-2 max-w-[300px] text-[13px] leading-relaxed text-muted-foreground">
          See exactly where capital is stuck across your lot — and act before it bleeds.
        </p>

        <div className="space-y-2.5 text-left">
          <Button
            className="w-full bg-foreground text-background hover:bg-foreground/90"
            disabled={busy !== null}
            onClick={() => run("microsoft", loginWithMicrosoft)}
          >
            {busy === "microsoft" ? (
              <Loader2 className="h-4 w-4 animate-spin" />
            ) : (
              <MicrosoftLogo className="h-4 w-4" />
            )}
            Sign in with Microsoft
          </Button>
          <Button
            className="w-full"
            variant="outline"
            disabled={busy !== null}
            onClick={() => run("guest", loginAsGuest)}
          >
            {busy === "guest" ? <Loader2 className="h-4 w-4 animate-spin" /> : null}
            Continue as manager account
          </Button>

          {/* Scoped demo — same as guest but the JWT is tied to a specific dealership so the
               dealership-scoping feature can be exercised without needing a real user account. */}
          <Button
            className="w-full"
            variant="outline"
            disabled={busy !== null}
            onClick={() => run("scoped", loginAsScoped)}
          >
            {busy === "scoped" ? <Loader2 className="h-4 w-4 animate-spin" /> : null}
            Continue as scoped manager
          </Button>

          {error ? (
            <p
              className="rounded-md bg-destructive/10 px-3 py-2 text-sm text-destructive"
              role="alert"
            >
              {error}
            </p>
          ) : null}

          <p className="pt-2 text-center text-xs text-muted-foreground">
            The guest path mints the same bearer as real SSO, so reviewers can explore with seeded,
            non-sensitive data — no tenant required.
          </p>
        </div>
      </div>
    </div>
  );
}

function MicrosoftLogo({ className }: { className?: string }) {
  return (
    <svg className={className} viewBox="0 0 21 21" xmlns="http://www.w3.org/2000/svg" aria-hidden>
      <rect x="1" y="1" width="9" height="9" fill="#f25022" />
      <rect x="11" y="1" width="9" height="9" fill="#7fba00" />
      <rect x="1" y="11" width="9" height="9" fill="#00a4ef" />
      <rect x="11" y="11" width="9" height="9" fill="#ffb900" />
    </svg>
  );
}
