import { Navigate, Outlet, useLocation } from "react-router-dom";
import { useAuth } from "@/auth/auth-context";
import type { Role } from "@/types/api";

/**
 * UX-only route guard (defence-in-depth; the API is the source of truth for authz, SPEC §3.6).
 * Unauthenticated users are bounced to /login with a redirect back; authenticated users with the
 * wrong role are sent to /forbidden (a 403 state, which must not clear the session).
 */
export function ProtectedRoute({ allow }: { allow?: Role[] }) {
  const { isAuthenticated, user } = useAuth();
  const loc = useLocation();

  if (!isAuthenticated) {
    return <Navigate to={`/login?redirect=${encodeURIComponent(loc.pathname + loc.search)}`} replace />;
  }
  if (allow && user && !allow.includes(user.role)) {
    return <Navigate to="/forbidden" replace />;
  }
  return <Outlet />;
}
