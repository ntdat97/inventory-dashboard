import { Loader2 } from "lucide-react";
import { Navigate, Route, Routes } from "react-router-dom";
import { useAuth } from "@/auth/AuthContext";
import { Header } from "@/components/Header";
import { Dashboard } from "@/pages/Dashboard";
import { Login } from "@/pages/Login";

/** Centered spinner shown while the auth context rehydrates a stored bearer or runs the demo auto-login. */
function BootSpinner() {
  return (
    <div className="flex min-h-screen items-center justify-center bg-background">
      <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
    </div>
  );
}

/** The authenticated dashboard shell (chrome + main). */
function DashboardLayout() {
  return (
    <div className="min-h-screen bg-background">
      <Header />
      <main className="mx-auto max-w-[1400px] px-4 py-10 sm:px-8">
        <Dashboard />
      </main>
    </div>
  );
}

/**
 * Route table. Two routes only — deliberately minimal:
 *   /login  the sign-in screen (SSO + guest demo)
 *   /       the protected dashboard
 * Each redirects to the other based on auth state; the boot spinner covers the rehydrate/auto-login window so we
 * never flash the wrong screen. A prior in-tab "Sign out" keeps you on /login until you pick a sign-in method.
 */
export function App() {
  const { isAuthenticated, isLoading } = useAuth();

  if (isLoading) {
    return <BootSpinner />;
  }

  return (
    <Routes>
      <Route
        path="/login"
        element={isAuthenticated ? <Navigate to="/" replace /> : <Login />}
      />
      <Route
        path="/"
        element={isAuthenticated ? <DashboardLayout /> : <Navigate to="/login" replace />}
      />
      {/* Unknown paths fall back to the root gate, which routes by auth state. */}
      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  );
}
