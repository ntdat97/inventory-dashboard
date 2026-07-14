import { createContext, useCallback, useContext, useEffect, useMemo, useState } from "react";
import { api, ApiError } from "@/lib/api";
import { clearToken, getToken, setToken } from "@/lib/token";
import type { UserProfile } from "@/lib/types";
import { apiScope, getMsal, isMsalConfigured } from "@/auth/msal";
import { queryClient } from "@/lib/queryClient";

interface AuthState {
  user: UserProfile | null;
  isAuthenticated: boolean;
  isLoading: boolean;
  /** Guest/demo login → mints + stores the bearer, then loads the profile. */
  loginAsGuest: () => Promise<void>;
  /** Scoped demo login → same as guest but JWT is tied to a specific dealership. */
  loginAsScoped: () => Promise<void>;
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
    queryClient.clear(); // purge stale data from any previous session before loading new scoped data
    const response = await api.guestLogin();
    setToken(response.accessToken);
    sessionStorage.removeItem(LOGGED_OUT_KEY);
    setUser(response.user);
  }, []);

  const loginAsScoped = useCallback(async () => {
    queryClient.clear(); // purge stale data from any previous session before loading new scoped data
    const response = await api.scopedLogin();
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
    // Real SSO via MSAL SPA popup (single App-registration pattern). If Entra isn't configured on this build we
    // surface the same friendly message the login screen already handles — the guest path remains fully usable.
    if (!isMsalConfigured) {
      throw new ApiError(
        404,
        "Microsoft SSO isn't configured on this deployment. Use “Continue as guest”.",
      );
    }

    const msal = await getMsal();
    if (!msal) {
      throw new ApiError(0, "Couldn't initialise Microsoft sign-in. Use “Continue as guest”.");
    }

    try {
      try {
        // Defensive: MSAL leaves an "interaction.status" lock in sessionStorage if a previous popup didn't close
        // cleanly (React StrictMode double-mount, HMR, user closing the popup window). A stale lock makes the next
        // loginPopup throw `interaction_in_progress` before any network call. Clearing the flag lets the retry proceed.
        for (const key of Object.keys(sessionStorage)) {
          if (key.startsWith("msal.") && key.endsWith(".interaction.status")) {
            sessionStorage.removeItem(key);
          }
        }
      } catch {
        // sessionStorage inaccessible (private mode, sandboxed iframe) — loginPopup will surface its own error.
      }
      const result = await msal.loginPopup({ scopes: [apiScope] });
      if (!result.accessToken) {
        throw new ApiError(0, "Microsoft sign-in returned no access token. Use “Continue as guest”.");
      }
      setToken(result.accessToken);
      sessionStorage.removeItem(LOGGED_OUT_KEY);
      // The backend's /auth/me reads the bearer's claims — same shape as the guest path, so downstream code is uniform.
      const profile = await api.me();
      setUser(profile);
    } catch (err) {
      if (err instanceof ApiError) throw err;
      // MSAL surfaces its own error shapes (BrowserAuthError, InteractionRequiredAuthError). Map the user-cancel case
      // to a quiet no-op; everything else becomes a friendly message on the login screen.
      const name = (err as { errorCode?: string; name?: string })?.errorCode
        ?? (err as { name?: string })?.name
        ?? "";
      if (name === "user_cancelled" || name === "popup_window_error") {
        return;
      }
      console.error("[MSAL] loginPopup failed:", err);
      throw new ApiError(0, "Microsoft sign-in failed. Use “Continue as guest”.");
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
      loginAsScoped,
      loginWithMicrosoft,
      logout,
    }),
    [user, isLoading, loginAsGuest, loginAsScoped, loginWithMicrosoft, logout],
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth(): AuthState {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error("useAuth must be used within an AuthProvider");
  return ctx;
}
