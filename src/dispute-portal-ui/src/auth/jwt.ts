import type { Role } from "@/types/api";

interface JwtClaims {
  sub: string;
  role: Role;
  name?: string;
  exp: number;
}

/** Decode a JWT payload without verifying the signature (display-only; the API is authoritative). */
export function decodeJwt(token: string): JwtClaims | null {
  try {
    const payload = token.split(".")[1];
    if (!payload) return null;
    const json = atob(payload.replace(/-/g, "+").replace(/_/g, "/"));
    return JSON.parse(json) as JwtClaims;
  } catch {
    return null;
  }
}

/** A token is expired (or unreadable) when its `exp` claim is at or before now. */
export function isExpired(token: string): boolean {
  const c = decodeJwt(token);
  return !c || c.exp * 1000 <= Date.now();
}
