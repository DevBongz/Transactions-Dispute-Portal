import { createContext, useContext, useMemo, useState, type ReactNode } from "react";
import { api } from "@/lib/api-client";
import { tokenStorage } from "./token-storage";
import { isExpired } from "./jwt";
import type { AuthUser, LoginResponse } from "@/types/api";

interface AuthState {
  user: AuthUser | null;
  isAuthenticated: boolean;
  login: (email: string, password: string) => Promise<AuthUser>;
  logout: () => void;
}

const AuthContext = createContext<AuthState | undefined>(undefined);

/** Restore the session from storage, failing closed if the token is missing or expired. */
function bootstrap(): AuthUser | null {
  const t = tokenStorage.get();
  if (!t || isExpired(t)) {
    tokenStorage.clear();
    return null;
  }
  return tokenStorage.getUser();
}

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<AuthUser | null>(bootstrap);

  const login = async (email: string, password: string): Promise<AuthUser> => {
    const { data } = await api.post<LoginResponse>("/auth/login", { email, password });
    tokenStorage.set(data.token);
    tokenStorage.setUser(data.user);
    setUser(data.user);
    return data.user;
  };

  const logout = () => {
    api.post("/auth/logout").catch(() => {}); // best-effort; token is discarded client-side
    tokenStorage.clear();
    setUser(null);
    window.location.assign("/login");
  };

  const value = useMemo<AuthState>(
    () => ({ user, isAuthenticated: !!user, login, logout }),
    [user],
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

// eslint-disable-next-line react-refresh/only-export-components
export function useAuth(): AuthState {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error("useAuth must be used within AuthProvider");
  return ctx;
}
