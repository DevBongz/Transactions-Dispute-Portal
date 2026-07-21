# TDP-FE-01 — Frontend Scaffold, Routing, Auth Context & API Client

**Jira summary:** Stand up the React + TypeScript single-page application that fronts the Transactions Dispute Portal: initialise the Vite project, wire up shadcn/ui, TanStack Query and react-router, and build the cross-cutting plumbing every subsequent frontend ticket depends on — an `AuthContext` that persists the JWT, a typed API client that injects the `Bearer` token and transparently handles `401` responses, and role-aware protected routes. This ticket delivers no business screens on its own; it delivers the foundation and a working login → protected-shell flow so that TDP-FE-02..05 can be built in parallel on top of it.

## 1. Context & Motivation

- **Background:** The backend (Groups A–C) exposes a JWT-secured REST API under `/api/v1` (SPEC.md §3.3). There is currently no frontend. Per SPEC.md §3.1 the client is a React (TypeScript) SPA using shadcn/ui and TanStack Query, served via nginx in Docker on port 3000. Every downstream frontend ticket (transactions, disputes, ops dashboard) needs a consistent way to authenticate, call the API, cache server state, and route between screens.
- **Business Impact:** This is the load-bearing scaffold for Day 4–6 of delivery (SPEC.md §4.1). Getting auth, the API client and routing right once — with the JWT handled centrally and never leaked — de-risks the entire frontend build, enforces the security NFRs (`ANTHROPIC_API_KEY` never touches the browser; all calls carry a valid JWT; SPEC.md §3.6), and lets the customer and ops journeys be layered on cleanly.
- **User Story:** As any user (customer or ops), I want to log in once and move between the portal's screens without re-authenticating, and I want to be safely returned to the login page when my session expires, so that the app is secure and usable on shared devices (AUTH-01, AUTH-02, AUTH-03).
- **Dependencies:** Depends on **TDP-AUTH-01** (`POST /api/v1/auth/login` contract, JWT shape, roles `CUSTOMER` / `OPS_ANALYST` / `OPS_MANAGER`). Consumed by **TDP-FE-02**, **TDP-FE-03**, **TDP-FE-04**, **TDP-FE-05**. Deployed by **TDP-INFRA-02** (nginx container, frontend :3000). Milestone: **Day 4 (19 Jul) — Frontend — Auth & Transactions**.

## 2. Detailed Description

### 2.1 Project initialisation

Scaffold the app at `src/dispute-portal-ui` using Vite with the `react-ts` template (Node 20 LTS, npm).

```bash
cd src
npm create vite@latest dispute-portal-ui -- --template react-ts
cd dispute-portal-ui
npm i
# runtime deps
npm i @tanstack/react-query axios react-router-dom
npm i class-variance-authority clsx tailwind-merge lucide-react
# tailwind + shadcn prerequisites
npm i -D tailwindcss@3 postcss autoprefixer @types/node
npx tailwindcss init -p
# shadcn/ui
npx shadcn@latest init
```

`shadcn init` answers: TypeScript = yes, style = `new-york`, base colour = `slate`, CSS variables = yes, import alias `@/*`. Add the components used across the app now so downstream tickets can import them:

```bash
npx shadcn@latest add button input label card table badge tabs \
  dialog dropdown-menu sonner form select textarea skeleton \
  pagination alert avatar separator popover calendar
```

Configure the `@` alias in both `tsconfig.json` (`"paths": { "@/*": ["./src/*"] }`) and `vite.config.ts` (`resolve.alias`), and set the dev server to proxy the API so local dev matches the nginx behaviour:

```ts
// vite.config.ts
import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";
import path from "node:path";

export default defineConfig({
  plugins: [react()],
  resolve: { alias: { "@": path.resolve(__dirname, "./src") } },
  server: {
    port: 3000,
    proxy: { "/api": { target: "http://localhost:5000", changeOrigin: true } },
  },
});
```

### 2.2 Directory structure

```
src/dispute-portal-ui/
├── src/
│   ├── main.tsx                 # ReactDOM root, providers
│   ├── App.tsx                  # <RouterProvider>
│   ├── router.tsx               # route tree + guards
│   ├── index.css                # tailwind layers + shadcn tokens
│   ├── lib/
│   │   ├── utils.ts             # cn() from shadcn
│   │   ├── api-client.ts        # axios instance + interceptors
│   │   └── query-client.ts      # QueryClient config
│   ├── auth/
│   │   ├── auth-context.tsx     # AuthProvider + useAuth
│   │   ├── token-storage.ts     # get/set/clear JWT
│   │   └── jwt.ts               # decode + expiry helpers
│   ├── components/
│   │   ├── layout/
│   │   │   ├── AppShell.tsx     # nav + <Outlet/>
│   │   │   └── NavBar.tsx
│   │   ├── ProtectedRoute.tsx
│   │   └── ui/                  # shadcn-generated components
│   ├── features/
│   │   ├── auth/LoginPage.tsx   # implemented here (thin) / expanded in FE-02
│   │   └── ...                  # transactions, disputes, ops (FE-02..05)
│   └── types/
│       └── api.ts               # shared DTO types
├── .env / .env.production
├── Dockerfile
├── nginx.conf
└── vite.config.ts
```

### 2.3 Shared API types

Mirror the SPEC.md §3.2/§3.3 contracts in `src/types/api.ts` so all features share one source of truth:

```ts
export type Role = "CUSTOMER" | "OPS_ANALYST" | "OPS_MANAGER";

export interface AuthUser { id: string; fullName: string; role: Role; }
export interface LoginResponse { token: string; expiresAt: string; user: AuthUser; }

export type TxnStatus = "SETTLED" | "PENDING" | "REVERSED";
export interface Transaction {
  id: string; reference: string; merchantName: string; merchantCategory: string;
  amount: number; currency: string; transactionDate: string; status: TxnStatus;
}

export type DisputeStatus = "OPEN" | "UNDER_REVIEW" | "RESOLVED" | "CLASSIFICATION_FAILED";
export type DisputeCategory =
  | "UNAUTHORISED" | "DUPLICATE_CHARGE" | "MERCHANT_ERROR" | "WRONG_AMOUNT" | "OTHER";
export type Priority = "LOW" | "MEDIUM" | "HIGH" | "CRITICAL";

export interface Paged<T> { items: T[]; total: number; page: number; pageSize: number; }
```

### 2.4 Token storage & JWT helpers

The JWT is stored in `localStorage` under a single key. (Rationale in §4.) Expiry is read from the `exp` claim so the client can proactively treat a stale token as logged-out without a round trip.

```ts
// src/auth/token-storage.ts
const KEY = "dpp.jwt";
export const tokenStorage = {
  get: () => localStorage.getItem(KEY),
  set: (t: string) => localStorage.setItem(KEY, t),
  clear: () => localStorage.removeItem(KEY),
};
```

```ts
// src/auth/jwt.ts
import type { Role } from "@/types/api";
interface JwtClaims { sub: string; role: Role; name?: string; exp: number; }

export function decodeJwt(token: string): JwtClaims | null {
  try {
    const payload = token.split(".")[1];
    return JSON.parse(atob(payload.replace(/-/g, "+").replace(/_/g, "/")));
  } catch { return null; }
}
export function isExpired(token: string): boolean {
  const c = decodeJwt(token);
  return !c || c.exp * 1000 <= Date.now();
}
```

### 2.5 AuthContext

`AuthProvider` hydrates from storage on mount, exposes `login()` / `logout()` and the current user, and drops any expired token so protected routes fail closed.

```tsx
// src/auth/auth-context.tsx
import { createContext, useContext, useMemo, useState, ReactNode } from "react";
import { api } from "@/lib/api-client";
import { tokenStorage } from "./token-storage";
import { decodeJwt, isExpired } from "./jwt";
import type { AuthUser, LoginResponse } from "@/types/api";

interface AuthState {
  user: AuthUser | null;
  isAuthenticated: boolean;
  login: (email: string, password: string) => Promise<void>;
  logout: () => void;
}
const AuthContext = createContext<AuthState | undefined>(undefined);

function bootstrap(): AuthUser | null {
  const t = tokenStorage.get();
  if (!t || isExpired(t)) { tokenStorage.clear(); return null; }
  const c = decodeJwt(t);
  return c ? { id: c.sub, fullName: c.name ?? "", role: c.role } : null;
}

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<AuthUser | null>(bootstrap);

  const login = async (email: string, password: string) => {
    const { data } = await api.post<LoginResponse>("/auth/login", { email, password });
    tokenStorage.set(data.token);
    setUser(data.user);
  };
  const logout = () => {
    api.post("/auth/logout").catch(() => {}); // best-effort; token is discarded client-side
    tokenStorage.clear();
    setUser(null);
    window.location.assign("/login");
  };

  const value = useMemo<AuthState>(
    () => ({ user, isAuthenticated: !!user, login, logout }), [user]);
  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth() {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error("useAuth must be used within AuthProvider");
  return ctx;
}
```

### 2.6 API client with Bearer injection + 401 handling

A single axios instance is the only way features talk to the backend. A request interceptor attaches the token; a response interceptor clears the session and redirects to `/login` on `401` (covers expired-JWT per AC-AUTH-01).

```ts
// src/lib/api-client.ts
import axios from "axios";
import { tokenStorage } from "@/auth/token-storage";

export const api = axios.create({
  baseURL: import.meta.env.VITE_API_BASE_URL ?? "/api/v1",
  headers: { "Content-Type": "application/json" },
});

api.interceptors.request.use((config) => {
  const token = tokenStorage.get();
  if (token) config.headers.Authorization = `Bearer ${token}`;
  return config;
});

api.interceptors.response.use(
  (res) => res,
  (error) => {
    if (error.response?.status === 401) {
      tokenStorage.clear();
      if (window.location.pathname !== "/login") {
        const from = encodeURIComponent(window.location.pathname);
        window.location.assign(`/login?redirect=${from}`);
      }
    }
    return Promise.reject(error);
  },
);
```

### 2.7 Query client & providers

```ts
// src/lib/query-client.ts
import { QueryClient } from "@tanstack/react-query";
export const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      staleTime: 30_000,
      retry: (count, err: any) => err?.response?.status !== 401 && count < 2,
      refetchOnWindowFocus: false,
    },
  },
});
```

```tsx
// src/main.tsx
import { QueryClientProvider } from "@tanstack/react-query";
import { RouterProvider } from "react-router-dom";
import { AuthProvider } from "@/auth/auth-context";
import { Toaster } from "@/components/ui/sonner";
import { queryClient } from "@/lib/query-client";
import { router } from "@/router";

ReactDOM.createRoot(document.getElementById("root")!).render(
  <QueryClientProvider client={queryClient}>
    <AuthProvider>
      <RouterProvider router={router} />
      <Toaster richColors position="top-right" />
    </AuthProvider>
  </QueryClientProvider>,
);
```

### 2.8 Routing & role-based protected routes

Routes are declared with `createBrowserRouter`. `ProtectedRoute` gates on authentication and (optionally) an allowed-role set; ops-only routes reject `CUSTOMER` and vice versa.

```tsx
// src/components/ProtectedRoute.tsx
import { Navigate, Outlet, useLocation } from "react-router-dom";
import { useAuth } from "@/auth/auth-context";
import type { Role } from "@/types/api";

export function ProtectedRoute({ allow }: { allow?: Role[] }) {
  const { isAuthenticated, user } = useAuth();
  const loc = useLocation();
  if (!isAuthenticated)
    return <Navigate to={`/login?redirect=${encodeURIComponent(loc.pathname)}`} replace />;
  if (allow && user && !allow.includes(user.role))
    return <Navigate to="/forbidden" replace />;
  return <Outlet />;
}
```

```tsx
// src/router.tsx
import { createBrowserRouter, Navigate } from "react-router-dom";
import { AppShell } from "@/components/layout/AppShell";
import { ProtectedRoute } from "@/components/ProtectedRoute";
import LoginPage from "@/features/auth/LoginPage";

export const router = createBrowserRouter([
  { path: "/login", element: <LoginPage /> },
  {
    element: <ProtectedRoute />,            // any authenticated user
    children: [
      {
        element: <AppShell />,
        children: [
          // CUSTOMER surface (TDP-FE-02/03/04)
          { element: <ProtectedRoute allow={["CUSTOMER"]} />, children: [
            { path: "/transactions", lazy: () => import("@/features/transactions/TransactionsPage") },
            { path: "/transactions/:id", lazy: () => import("@/features/transactions/TransactionDetailPage") },
            { path: "/disputes/new", lazy: () => import("@/features/disputes/DisputeSubmitPage") },
            { path: "/my-disputes", lazy: () => import("@/features/disputes/MyDisputesPage") },
            { path: "/my-disputes/:id", lazy: () => import("@/features/disputes/DisputeDetailPage") },
          ]},
          // OPS surface (TDP-FE-05)
          { element: <ProtectedRoute allow={["OPS_ANALYST", "OPS_MANAGER"]} />, children: [
            { path: "/ops", lazy: () => import("@/features/ops/OpsDashboardPage") },
            { path: "/ops/disputes/:id", lazy: () => import("@/features/ops/OpsDisputeDetailPage") },
          ]},
        ],
      },
    ],
  },
  { path: "/forbidden", element: <div role="alert">403 — You do not have access to this page.</div> },
  { path: "/", element: <Navigate to="/transactions" replace /> },
  { path: "*", element: <div role="alert">404 — Not found</div> },
]);
```

> Note: `lazy` route modules referenced above are stubs delivered by this ticket (each exports a placeholder `Component`) and are fully implemented in the corresponding downstream ticket. The stubs let the full route tree compile and be navigated on Day 4.

### 2.9 App shell & role-aware navigation

`AppShell` renders the top nav and an `<Outlet/>`. Nav links are filtered by role: customers see *Transactions* and *My Disputes*; ops roles see *Operations*. A landing redirect after login sends customers to `/transactions` and ops users to `/ops` based on `user.role`.

```tsx
// LoginPage post-login redirect logic
const dest = user.role === "CUSTOMER" ? "/transactions" : "/ops";
navigate(params.get("redirect") ?? dest, { replace: true });
```

### 2.10 Docker build (nginx)

Multi-stage Dockerfile producing a static bundle served by nginx on port 80 (mapped to :3000 by Compose, SPEC.md §3.1). `nginx.conf` adds SPA history-fallback and proxies `/api` to the `api` service so the browser only ever talks to same-origin.

```dockerfile
# src/dispute-portal-ui/Dockerfile
FROM node:20-alpine AS build
WORKDIR /app
COPY package*.json ./
RUN npm ci
COPY . .
RUN npm run build
FROM nginx:1.27-alpine
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist /usr/share/nginx/html
EXPOSE 80
```

```nginx
# nginx.conf
server {
  listen 80;
  location /api/ { proxy_pass http://api:8080/api/; proxy_set_header Host $host; }
  location / { root /usr/share/nginx/html; try_files $uri $uri/ /index.html; }
}
```

## 3. Acceptance Criteria

- `npm run dev` starts the SPA on `http://localhost:3000`; `npm run build` produces a `dist/` bundle with no TypeScript errors.
- shadcn/ui is initialised, Tailwind tokens compile, and the `@/*` import alias resolves in both TS and Vite.
- `POST /api/v1/auth/login` with valid seed credentials stores the JWT and populates `AuthContext.user`; the app redirects a `CUSTOMER` to `/transactions` and an ops user to `/ops` (AUTH-01, AUTH-02).
- Every outbound API request carries an `Authorization: Bearer <token>` header when a token is present; no request carries a hard-coded key and the Anthropic key never appears in any bundle (SPEC.md §3.6 security).
- Any API response with HTTP `401` clears the stored token and redirects the browser to `/login?redirect=...` (AC-AUTH-01 expired-JWT behaviour, AUTH-03).
- An unauthenticated visit to any protected route redirects to `/login`; after login the user is returned to the originally requested path.
- A `CUSTOMER` navigating to `/ops/*` is redirected to `/forbidden`; an `OPS_ANALYST`/`OPS_MANAGER` navigating to customer-only routes is likewise blocked (role-based routing).
- A token whose `exp` claim is in the past is treated as logged-out on app load without any network call.
- Reloading the page while authenticated keeps the user signed in (session hydrated from storage); `logout()` clears the token and returns to `/login`.
- The nginx image builds and serves the SPA with history fallback (deep links like `/my-disputes/123` load correctly).
- Base layout and interactive controls meet WCAG 2.1 AA: keyboard-navigable nav and forms, visible focus rings, and a skip-to-content link (SPEC.md §3.6 accessibility).

## 4. Technical Notes

- **Versions:** React 18, Vite 5, `@tanstack/react-query` v5, `react-router-dom` v6.4+ (data router APIs), `axios` v1, Tailwind CSS 3, shadcn/ui (`new-york`). Pin in `package.json` to keep the Docker build reproducible.
- **Token storage trade-off:** `localStorage` is chosen for simplicity given the project scope (no refresh-token flow, out-of-scope IdP per SPEC.md §1.2) and the 60-minute self-contained JWT. Document the XSS trade-off in the README; strict same-origin API access via the nginx proxy and no third-party scripts keep the surface small. Do not log the token.
- **401 vs 403:** The 401 interceptor triggers the redirect-to-login (expired/invalid session). A 403 (authenticated but wrong role) must NOT clear the token — surface it as a "Forbidden" state, since the backend enforces role separation independently of the client route guard.
- **Client guard is UX only:** `ProtectedRoute` is defence-in-depth for UX; the API is the source of truth for authorisation (SPEC.md §3.6). Never rely on the route guard for security.
- **Env config:** `VITE_API_BASE_URL` defaults to `/api/v1` so production goes through the nginx proxy (same-origin); the Vite dev proxy provides the same path locally. Only `VITE_`-prefixed vars are exposed to the browser — never place secrets there.
- **CORS:** The backend restricts CORS to the known frontend origin (SPEC.md §3.6). In Docker the browser hits nginx same-origin, so CORS is largely a dev-only concern via the Vite proxy.
- **Accessibility baseline:** shadcn/ui (Radix) primitives are accessible by default; the responsibility here is correct `<label htmlFor>` associations, `aria-live` for the future toasts, focus management on route change, and colour-contrast tokens.
- **Query invalidation contract:** Establish query-key conventions now (`["transactions", filters]`, `["disputes", filters]`, `["dispute", id]`, `["dashboard-summary"]`) so downstream tickets invalidate consistently.

## 5. Definition of Done

- [ ] Vite React-TS app scaffolded at `src/dispute-portal-ui` with shadcn/ui, Tailwind, TanStack Query and react-router installed and building cleanly (`npm run build` green, `tsc --noEmit` clean, ESLint passing).
- [ ] `AuthContext`, `token-storage`, JWT helpers, `api-client` (with request + 401 response interceptors), `query-client`, `ProtectedRoute`, `AppShell`, and the full route tree are implemented and merged.
- [ ] Login → protected shell → logout flow demonstrable against the live API; role-based redirects and `/forbidden` verified for both customer and ops seed accounts.
- [ ] Expired-token-on-load and mid-session `401` both drive a clean redirect to `/login` without a crash.
- [ ] Dockerfile + `nginx.conf` build and serve the SPA on :3000 with SPA history fallback and `/api` proxying to the `api` service; wired into `docker compose` (TDP-INFRA-02).
- [ ] Query-key conventions and shared `types/api.ts` documented in a short `src/dispute-portal-ui/README.md` section for downstream tickets.
- [ ] Baseline accessibility checks pass (keyboard nav, focus visibility, skip link, labelled login form); reviewed and approved by a second engineer; branch merged to `main`.
