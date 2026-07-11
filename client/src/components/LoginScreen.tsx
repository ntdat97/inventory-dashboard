import { useState } from "react";
import { Loader2, LogIn } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { useAuth } from "@/auth/AuthContext";
import { ApiError } from "@/lib/api";

/** Full-screen sign-in. Two paths mirror the backend: real Entra SSO (Microsoft) and the flag-gated guest demo. */
export function LoginScreen() {
  const { loginAsGuest, loginWithMicrosoft } = useAuth();
  const [busy, setBusy] = useState<"guest" | "microsoft" | null>(null);
  const [error, setError] = useState<string | null>(null);

  async function run(kind: "guest" | "microsoft", fn: () => Promise<void>) {
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
      <Card className="w-full max-w-md">
        <CardHeader className="space-y-2 text-center">
          <div className="mx-auto flex h-11 w-11 items-center justify-center rounded-lg bg-primary text-primary-foreground">
            <LogIn className="h-5 w-5" />
          </div>
          <CardTitle className="text-xl">Intelligent Inventory Dashboard</CardTitle>
          <CardDescription>
            Capital-at-risk decision support for dealership inventory. Sign in to continue.
          </CardDescription>
        </CardHeader>
        <CardContent className="space-y-3">
          <Button
            className="w-full"
            variant="outline"
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
            disabled={busy !== null}
            onClick={() => run("guest", loginAsGuest)}
          >
            {busy === "guest" ? <Loader2 className="h-4 w-4 animate-spin" /> : null}
            Continue as guest (demo)
          </Button>

          {error ? (
            <p className="rounded-md bg-destructive/10 px-3 py-2 text-sm text-destructive" role="alert">
              {error}
            </p>
          ) : null}

          <p className="pt-2 text-center text-xs text-muted-foreground">
            The guest path mints the same bearer as real SSO, so reviewers can explore with seeded,
            non-sensitive data — no tenant required.
          </p>
        </CardContent>
      </Card>
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
