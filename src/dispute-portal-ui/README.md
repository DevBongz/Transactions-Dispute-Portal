# Dispute Portal UI

React + TypeScript SPA for the Transactions Dispute Portal (SPEC §3.1). Vite + Tailwind +
shadcn-style primitives, TanStack Query for server state, React Router (data router) for routing.

## Scripts

```bash
npm install       # install deps
npm run dev       # dev server on http://localhost:3000 (proxies /api → :5000)
npm run build     # type-check (tsc --noEmit) then production build to dist/
npm run typecheck # tsc --noEmit only
npm run test      # Vitest (jsdom + Testing Library)
```

## Architecture

- `src/lib/api-client.ts` — the single axios instance. Injects `Authorization: Bearer <jwt>`;
  on `401` it clears the session and redirects to `/login?redirect=…`. A `403` is left intact
  (authenticated but wrong role) and surfaces as the `/forbidden` state.
- `src/auth/` — `AuthProvider` (login/logout, hydrate-on-reload), token + user storage, JWT expiry.
- `src/router.tsx` — route tree with `ProtectedRoute` guards (UX-only; the API is authoritative).
- `src/features/*` — one folder per surface (transactions, disputes, ops) with its query hooks.
- `src/components/` — shared primitives (`ui/`), `StatusBadge`, `PriorityBadge`, `TransactionSummary`,
  list state helpers (`DataStates`), and `Pagination`.

## Query-key conventions (invalidate consistently)

| Key                              | Owner                         |
| -------------------------------- | ----------------------------- |
| `["transactions", filters]`      | transactions list             |
| `["transaction", id]`            | transaction detail            |
| `["disputes", filters]`          | customer My Disputes list     |
| `["disputes", "ops", filters]`   | ops queue                     |
| `["dispute", id]`                | dispute detail (customer/ops) |
| `["dashboard-summary"]`          | ops metrics                   |

Submitting, patching status, or resolving invalidates `["disputes"]` (all variants),
`["dispute", id]`, and — on resolve — `["dashboard-summary"]`.

## Security notes / trade-offs

- **Token storage:** the 60-minute JWT lives in `localStorage` (no refresh flow — out of scope,
  SPEC §1.2). This accepts an XSS trade-off in exchange for simplicity; mitigations are strict
  same-origin API access (nginx proxy) and no third-party scripts. The token is never logged.
- **No AI key on the client:** the browser only calls `/ai/extract-dispute` and
  `/ai/generate-summary`; Anthropic is server-side only (SPEC §3.6).

## Deliberate deviations from the ticket text

These keep the bundle small and the a11y story simple without changing behaviour:

- **Native `<select>`** wrapper instead of the Radix listbox (fully keyboard/AT accessible).
- **Native `<input type="date">`** for the transaction date-range filter instead of a
  calendar popover (boundary dates are still passed through unchanged and inclusively).
- **Controlled forms** with manual validation instead of `react-hook-form` + `zod`.
- The low-confidence handling follows AC-DISP-02 **show-and-flag**: the AI's best guess is shown
  but flagged (amber ring + "please confirm" + `aria-describedby`); editing clears the flag.
