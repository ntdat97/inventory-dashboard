import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";
import path from "node:path";

// The API base is proxied in dev so the browser talks to same-origin `/api` (no CORS in local dev), while in
// production the built app reads VITE_API_BASE_URL (see .env.example) to reach the deployed API origin.
export default defineConfig({
  plugins: [react()],
  resolve: {
    alias: {
      "@": path.resolve(__dirname, "./src"),
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
