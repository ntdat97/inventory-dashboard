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

export function AuthProvider({ children }: { children: React.ReactNode }) {
  const [user, setUser] = useState<UserProfile | null>(null);
  // Start in "loading" only if a token is already present — otherwise the app renders the login screen immediately.
  const [isLoading, setIsLoading] = useState<boolean>(() => getToken() !== null);

  // Rehydrate the session on mount: if a stored bearer is still valid, /auth/me returns the profile; a 401 means it
  // expired, so we clear it and fall back to the login screen.
  useEffect(() => {
    if (getToken() === null) return;
    let cancelled = false;
    (async () => {
      try {
        const profile = await api.me();
        if (!cancelled) setUser(profile);
      } catch {
        clearToken();
        if (!cancelled) setUser(null);
      } finally {
        if (!cancelled) setIsLoading(false);
      }
    })();
    return () => {
      cancelled = true;
    };
  }, []);

  const loginAsGuest = useCallback(async () => {
    const response = await api.devLogin();
    setToken(response.accessToken);
    setUser(response.user);
  }, []);

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
