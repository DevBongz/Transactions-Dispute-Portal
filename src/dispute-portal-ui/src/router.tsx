import { createBrowserRouter, Navigate } from "react-router-dom";
import { AppShell } from "@/components/layout/AppShell";
import { ProtectedRoute } from "@/components/ProtectedRoute";
import { useAuth } from "@/auth/auth-context";
import LoginPage from "@/features/auth/LoginPage";

/** Send authenticated users to their home surface; bounce anonymous users to login. */
function RootRedirect() {
  const { isAuthenticated, user } = useAuth();
  if (!isAuthenticated) return <Navigate to="/login" replace />;
  return <Navigate to={user?.role === "CUSTOMER" ? "/transactions" : "/ops"} replace />;
}

export const router = createBrowserRouter([
  { path: "/login", element: <LoginPage /> },
  {
    element: <ProtectedRoute />, // any authenticated user
    children: [
      {
        element: <AppShell />,
        children: [
          // CUSTOMER surface (TDP-FE-02/03/04)
          {
            element: <ProtectedRoute allow={["CUSTOMER"]} />,
            children: [
              { path: "/transactions", lazy: () => import("@/features/transactions/TransactionsPage") },
              { path: "/transactions/:id", lazy: () => import("@/features/transactions/TransactionDetailPage") },
              { path: "/disputes/new", lazy: () => import("@/features/disputes/DisputeSubmitPage") },
              { path: "/my-disputes", lazy: () => import("@/features/disputes/MyDisputesPage") },
              { path: "/my-disputes/:id", lazy: () => import("@/features/disputes/DisputeDetailPage") },
            ],
          },
          // OPS surface (TDP-FE-05)
          {
            element: <ProtectedRoute allow={["OPS_ANALYST", "OPS_MANAGER"]} />,
            children: [
              { path: "/ops", lazy: () => import("@/features/ops/OpsDashboardPage") },
              { path: "/ops/disputes/:id", lazy: () => import("@/features/ops/OpsDisputeDetailPage") },
            ],
          },
        ],
      },
    ],
  },
  {
    path: "/forbidden",
    element: (
      <div role="alert" className="container py-16 text-center">
        <h1 className="text-2xl font-semibold">403 — Access denied</h1>
        <p className="mt-2 text-muted-foreground">You do not have access to this page.</p>
      </div>
    ),
  },
  { path: "/", element: <RootRedirect /> },
  {
    path: "*",
    element: (
      <div role="alert" className="container py-16 text-center">
        <h1 className="text-2xl font-semibold">404 — Not found</h1>
      </div>
    ),
  },
]);
