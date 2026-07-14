import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";
import path from "node:path";
import { fileURLToPath } from "node:url";

// The API base is proxied in dev so the browser talks to same-origin `/api` (no CORS in local dev), while in
// production the built app reads VITE_API_BASE_URL (see .env.example) to reach the deployed API origin.
export default defineConfig({
  plugins: [react()],
  resolve: {
    alias: {
      "@": path.resolve(__dirname, "./src"),
    },
  },
  build: {
    rollupOptions: {
      input: {
        // Main SPA entry.
        main: fileURLToPath(new URL("./index.html", import.meta.url)),
        // Standalone MSAL popup redirect landing page. Built as its own entry so the React app never boots in the
        // sign-in popup; it only runs the redirect bridge that hands the auth response back to the opener. This
        // emits dist/msal-redirect.html, which is the redirect URI registered in Azure.
        "msal-redirect": fileURLToPath(new URL("./msal-redirect.html", import.meta.url)),
      },
    },
  },
  server: {
    port: 5173,
    proxy: {
      "/api": {
        target: process.env.VITE_DEV_API_TARGET ?? "http://localhost:5002",
        changeOrigin: true,
      },
    },
  },
});
