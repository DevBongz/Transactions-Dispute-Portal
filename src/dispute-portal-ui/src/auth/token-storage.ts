import type { AuthUser } from "@/types/api";

const TOKEN_KEY = "dpp.jwt";
const USER_KEY = "dpp.user";

/**
 * Single-key JWT storage plus the display user profile. `localStorage` is chosen for scope
 * simplicity (60-min self-contained JWT, no refresh flow — see README trade-off). The user
 * object is persisted so a page reload restores identity without decoding provider-specific
 * claim keys; it is always re-gated by token expiry on bootstrap.
 */
export const tokenStorage = {
  get: () => localStorage.getItem(TOKEN_KEY),
  set: (t: string) => localStorage.setItem(TOKEN_KEY, t),
  getUser: (): AuthUser | null => {
    const raw = localStorage.getItem(USER_KEY);
    if (!raw) return null;
    try {
      return JSON.parse(raw) as AuthUser;
    } catch {
      return null;
    }
  },
  setUser: (u: AuthUser) => localStorage.setItem(USER_KEY, JSON.stringify(u)),
  clear: () => {
    localStorage.removeItem(TOKEN_KEY);
    localStorage.removeItem(USER_KEY);
  },
};
