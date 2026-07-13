import { createContext, useCallback, useContext, useEffect, useMemo, useState } from "react";
import { api, ApiError } from "@/lib/api";
import { clearToken, getToken, setToken } from "@/lib/token";
import type { UserProfile } from "@/lib/types";

interface AuthState {
  user: UserProfile | null;
  isAuthenticated: boolean;
  isLoading: boolean;
  /** Guest/demo login → mints + stores the bearer, then loads the profile. */
  loginAsGuest: () => Promise<void>;
  /** Real SSO entry point; degrades gracefully when Entra is not wired on this deployment. */
  loginWithMicrosoft: () => Promise<void>;
  logout: () => void;
}

const AuthContext = createContext<AuthState | undefined>(undefined);

// Per-tab flag set on explicit logout. It keeps the auto-login below from immediately signing us back in, so
// "Logout" actually lands you on the login screen (where SSO can be exercised). A fresh tab has no flag and
// auto-logs-in again — logout is a within-session choice, not a persisted one.
const LOGGED_OUT_KEY = "inventory.loggedOut";
const wasLoggedOut = () => sessionStorage.getItem(LOGGED_OUT_KEY) === "1";

export function AuthProvider({ children }: { children: React.ReactNode }) {
  const [user, setUser] = useState<UserProfile | null>(null);
  // We open in "loading" whenever there's boot work to do: an existing token to rehydrate, or an auto-login to run.
  // The only case that renders immediately is a deliberate logout within this tab.
  const [isLoading, setIsLoading] = useState<boolean>(() => !wasLoggedOut());

  const loginAsGuest = useCallback(async () => {
    const response = await api.guestLogin();
    setToken(response.accessToken);
    sessionStorage.removeItem(LOGGED_OUT_KEY);
    setUser(response.user);
  }, []);

  // Boot: rehydrate a stored bearer if present, otherwise auto-login as the demo guest so the app opens straight on
  // the dashboard. A prior logout in this tab short-circuits both paths and drops us on the login screen.
  useEffect(() => {
    if (wasLoggedOut()) return;
    let cancelled = false;
    (async () => {
      try {
        if (getToken() !== null) {
          // Stored bearer: valid → profile; 401/expired → clear and fall through to a fresh guest login.
          try {
            const profile = await api.me();
            if (!cancelled) setUser(profile);
            return;
          } catch {
            clearToken();
          }
        }
        await loginAsGuest();
      } catch {
        // Auto-login failed (API down / SSO-only deployment): clear state and let the login screen take over.
        clearToken();
        if (!cancelled) setUser(null);
      } finally {
        if (!cancelled) setIsLoading(false);
      }
    })();
    return () => {
      cancelled = true;
    };
  }, [loginAsGuest]);

  const loginWithMicrosoft = useCallback(async () => {
    // The real SSO path is a server-driven OIDC challenge. On the zero-setup demo Entra is not configured, so
    // /auth/login answers 404 — probe it first so we can show a clear, non-fatal message instead of navigating the
    // browser to a raw 404 ProblemDetails page. A configured tenant answers with a redirect, which we then follow.
    const base = (import.meta.env.VITE_API_BASE_URL ?? "").replace(/\/$/, "");
    const loginUrl = `${base}/api/auth/login?returnUrl=${encodeURIComponent(window.location.origin)}`;
    try {
      const res = await fetch(loginUrl, { redirect: "manual" });
      if (res.status === 404) {
        throw new ApiError(
          404,
          "Microsoft SSO isn't configured on this demo deployment. Use “Continue as guest”.",
        );
      }
      window.location.href = loginUrl;
    } catch (err) {
      if (err instanceof ApiError) throw err;
      throw new ApiError(0, "Couldn't reach the sign-in endpoint. Use “Continue as guest”.");
    }
  }, []);

  const logout = useCallback(() => {
    clearToken();
    // Mark this tab as intentionally signed out so boot doesn't auto-login us back in — the login screen stays put
    // until the user picks a sign-in method (or opens a fresh tab).
    sessionStorage.setItem(LOGGED_OUT_KEY, "1");
    setUser(null);
  }, []);

  const value = useMemo<AuthState>(
    () => ({
      user,
      isAuthenticated: user !== null,
      isLoading,
      loginAsGuest,
      loginWithMicrosoft,
      logout,
    }),
    [user, isLoading, loginAsGuest, loginWithMicrosoft, logout],
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth(): AuthState {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error("useAuth must be used within an AuthProvider");
  return ctx;
}
