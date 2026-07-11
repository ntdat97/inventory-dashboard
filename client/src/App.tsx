import { Loader2 } from "lucide-react";
import { useAuth } from "@/auth/AuthContext";
import { Header } from "@/components/Header";
import { LoginScreen } from "@/components/LoginScreen";
import { Dashboard } from "@/pages/Dashboard";

/** Top-level gate: rehydrating → spinner; unauthenticated → login; authenticated → the dashboard shell. */
export function App() {
  const { isAuthenticated, isLoading } = useAuth();

  if (isLoading) {
    return (
      <div className="flex min-h-screen items-center justify-center bg-background">
        <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
      </div>
    );
  }

  if (!isAuthenticated) {
    return <LoginScreen />;
  }

  return (
    <div className="min-h-screen bg-background">
      <Header />
      <main className="mx-auto max-w-[1400px] px-4 py-6 sm:px-6">
        <Dashboard />
      </main>
    </div>
  );
}
