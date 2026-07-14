import { PublicClientApplication, type Configuration } from "@azure/msal-browser";

// Single App-registration SPA flow. When TenantId + ClientId are present the button routes through Entra ID; when
// they are absent (zero-setup demo) `getMsal()` returns null and `loginWithMicrosoft` surfaces a friendly "not
// configured, use guest" message — the backend already treats SSO as optional.
const tenantId = import.meta.env.VITE_AZURE_AD_TENANT_ID?.trim();
const clientId = import.meta.env.VITE_AZURE_AD_CLIENT_ID?.trim();

export const isMsalConfigured = Boolean(tenantId && clientId);

// The scope the SPA requests. In the single-App-registration pattern the SPA calls the same app's exposed API, so
// the scope URI is `api://<clientId>/<scopeName>` — matching the "Expose an API" → scope you added in the portal.
export const apiScope = isMsalConfigured ? `api://${clientId}/access_as_user` : "";

let instance: PublicClientApplication | null = null;
let initPromise: Promise<PublicClientApplication> | null = null;

/**
 * Lazily creates and initialises the MSAL instance. Returns null if Entra is not configured on this build — callers
 * treat that as "SSO unavailable" and fall back to the guest path.
 */
export async function getMsal(): Promise<PublicClientApplication | null> {
  if (!isMsalConfigured) return null;
  if (instance) return instance;
  if (!initPromise) {
    const config: Configuration = {
      auth: {
        clientId: clientId!,
        authority: `https://login.microsoftonline.com/${tenantId}`,
        // Dedicated blank landing page: MSAL's popup lands here so the SPA's React Router doesn't run in the popup
        // and strip the auth response from the URL hash before the opener can read it. The file lives in /public.
        redirectUri: `${window.location.origin}/msal-redirect.html`,
      },
      cache: {
        cacheLocation: "sessionStorage",
      },
    };
    const created = new PublicClientApplication(config);
    initPromise = created.initialize().then(() => {
      instance = created;
      return created;
    });
  }
  return initPromise;
}
