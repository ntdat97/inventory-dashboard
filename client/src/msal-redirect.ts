// Entry script for the MSAL popup redirect landing page (msal-redirect.html). This is the URL Entra ID redirects
// the popup to after sign-in, carrying the auth response in the URL hash (#code=...&state=...).
//
// MSAL v5 does NOT read that hash by polling the popup from the opener (that was the v2/v3 model). Instead the
// opener's loginPopup() waits on a same-origin BroadcastChannel, and THIS page is responsible for reading its own
// hash and broadcasting the raw response back over that channel — which is exactly what broadcastResponseToMainFrame
// does before closing the popup. Without it the opener waits until popupBridgeTimeout and the popup hangs on the
// #code=... URL. See @azure/msal-browser/redirect-bridge.
//
// This deliberately runs as a standalone Vite entry (see vite.config.ts) so the React app and router never boot in
// the popup — nothing here mutates the URL before the bridge reads the response.
import { broadcastResponseToMainFrame } from "@azure/msal-browser/redirect-bridge";

broadcastResponseToMainFrame().catch((err) => {
  // The bridge throws if the URL carries no recognizable auth payload (e.g. someone opened this page directly).
  // Leave a breadcrumb; the opener's loginPopup() surfaces its own timeout error on the login screen.
  console.error("[MSAL] redirect bridge failed:", err);
});
