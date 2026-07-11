// Small persisted token store. The demo bearer is kept in localStorage so a refresh keeps you signed in; the auth
// context is the single reader/writer in the app. Kept deliberately minimal — no refresh-token dance for the demo.

const TOKEN_KEY = "inventory.accessToken";

export function getToken(): string | null {
  return localStorage.getItem(TOKEN_KEY);
}

export function setToken(token: string): void {
  localStorage.setItem(TOKEN_KEY, token);
}

export function clearToken(): void {
  localStorage.removeItem(TOKEN_KEY);
}
