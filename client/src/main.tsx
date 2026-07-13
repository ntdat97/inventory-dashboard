import React from "react";
import ReactDOM from "react-dom/client";
// HashRouter (URLs like /#/login) so the app works on any static host with zero server config — a hard refresh at
// any route is served by index.html without SPA-fallback rules. Swap for BrowserRouter if clean URLs are needed.
import { HashRouter } from "react-router-dom";
import { QueryClientProvider } from "@tanstack/react-query";
import { App } from "./App";
import { AuthProvider } from "./auth/AuthContext";
import { queryClient } from "./lib/queryClient";
import "./index.css";

ReactDOM.createRoot(document.getElementById("root")!).render(
  <React.StrictMode>
    <QueryClientProvider client={queryClient}>
      <HashRouter>
        <AuthProvider>
          <App />
        </AuthProvider>
      </HashRouter>
    </QueryClientProvider>
  </React.StrictMode>,
);
