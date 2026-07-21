# TDP-FE-02 — Transaction List & Detail Views

**Jira summary:** Deliver the customer-facing login page and transaction surface: a paginated, filterable transaction table (date-range + merchant filters) and a transaction detail view that surfaces all fields required before raising a dispute, with a prominent "Dispute this transaction" call-to-action that launches the submission flow (TDP-FE-03). This is the first business surface the customer sees after authenticating and covers user stories TXN-01, TXN-02 and TXN-03.

## 1. Context & Motivation

- **Background:** After TDP-FE-01 established auth, routing and the API client, the customer needs a way to review account activity. The backend exposes `GET /api/v1/transactions` (paginated, with `from`/`to`/`merchant` query params) and `GET /api/v1/transactions/{id}` (SPEC.md §3.3). Journey 1 (SPEC.md §2.2) begins with the customer landing on the Transactions page and drilling into a suspicious transaction.
- **Business Impact:** Transaction review is the entry point for the portal's core value — self-service disputes (SPEC.md §1.1 objective: dispute submission success rate ≥ 95%). Fast, filterable access to the right transaction (TXN-02) directly reduces friction before the dispute funnel, and full transaction context (TXN-03) is a prerequisite for accurate dispute submission and downstream AI classification.
- **User Story:** As a customer (Maya), I want to see a paginated, searchable list of my transactions and open any one to view its full detail, so that I can quickly locate a transaction I do not recognise and start a dispute with full context (TXN-01, TXN-02, TXN-03).
- **Dependencies:** Depends on **TDP-FE-01** (auth, API client, routing, shared types) and **TDP-TXN-01** (Transaction listing & detail API). Feeds **TDP-FE-03** (the "Dispute this transaction" CTA routes into the submission UI). Milestone: **Day 4 (19 Jul) — Frontend — Auth & Transactions**.

## 2. Detailed Description

### 2.1 Login page (finalised)

TDP-FE-01 provides a thin `LoginPage`; this ticket delivers the finished, accessible version. An accessible shadcn `Form` with labelled `email` / `password` inputs, inline validation, a generic error on failure (no credential enumeration, AC-AUTH-01), and a loading state on submit.

```tsx
// src/features/auth/LoginPage.tsx (excerpt)
export default function LoginPage() {
  const { login } = useAuth();
  const [params] = useSearchParams();
  const navigate = useNavigate();
  const [error, setError] = useState<string | null>(null);
  const mutation = useMutation({
    mutationFn: (v: { email: string; password: string }) => login(v.email, v.password),
    onError: () => setError("Invalid email or password."), // generic, no enumeration
    onSuccess: () => navigate(params.get("redirect") ?? "/transactions", { replace: true }),
  });
  // <Card><form aria-describedby={error ? "login-error" : undefined} ...>
  //   <Label htmlFor="email"> / <Input id="email" type="email" autoComplete="username" required />
  //   <Label htmlFor="password"> / <Input id="password" type="password" autoComplete="current-password" required />
  //   {error && <p id="login-error" role="alert" className="text-destructive">{error}</p>}
  //   <Button disabled={mutation.isPending}>{mutation.isPending ? "Signing in…" : "Sign in"}</Button>
}
```

### 2.2 Transactions data hooks (TanStack Query)

A typed filter object drives the query key so pagination and filtering are cache-correct. Filters map 1:1 to the API query params.

```ts
// src/features/transactions/api.ts
import { keepPreviousData, useQuery } from "@tanstack/react-query";
import { api } from "@/lib/api-client";
import type { Paged, Transaction } from "@/types/api";

export interface TxnFilters {
  page: number; pageSize: number;
  from?: string; to?: string; merchant?: string;   // from/to are ISO yyyy-MM-dd
}

export function useTransactions(filters: TxnFilters) {
  return useQuery({
    queryKey: ["transactions", filters],
    queryFn: async () => {
      const { data } = await api.get<Paged<Transaction>>("/transactions", {
        params: {
          page: filters.page, pageSize: filters.pageSize,
          from: filters.from || undefined, to: filters.to || undefined,
          merchant: filters.merchant || undefined,
        },
      });
      return data;
    },
    placeholderData: keepPreviousData,   // keep table populated while paging
  });
}

export function useTransaction(id: string) {
  return useQuery({
    queryKey: ["transaction", id],
    queryFn: async () => (await api.get<Transaction>(`/transactions/${id}`)).data,
    enabled: !!id,
  });
}
```

### 2.3 Transactions list page

Layout: filter bar (date-range popover + merchant search input) above a shadcn `Table`, with pagination controls beneath. Filter state is held in URL search params so a filtered view is shareable/bookmarkable and survives reload. Default `pageSize` is 20 (AC-TXN-01).

```tsx
// src/features/transactions/TransactionsPage.tsx (structure)
export function Component() {
  const [sp, setSp] = useSearchParams();
  const filters: TxnFilters = {
    page: Number(sp.get("page") ?? 1),
    pageSize: 20,
    from: sp.get("from") ?? undefined,
    to: sp.get("to") ?? undefined,
    merchant: sp.get("merchant") ?? undefined,
  };
  const { data, isLoading, isError, refetch } = useTransactions(filters);
  const navigate = useNavigate();
  // <TransactionFilters value={filters} onChange={next => setSp(toParams(next))} />
  // isLoading -> <TableSkeleton rows={10}/>
  // isError   -> <Alert role="alert">Couldn't load transactions <Button onClick={refetch}>Retry</Button></Alert>
  // data.items.length === 0 -> empty state "No transactions match your filters."
  // else -> <Table> ... rows ...
}
```

Table columns (each row shows the AC-TXN-01 required fields): **Date** (`transactionDate`, localised `d MMM yyyy`), **Merchant** (`merchantName`), **Reference** (`reference`, monospace), **Amount** (`amount` + `currency`, right-aligned, formatted via `Intl.NumberFormat("en-ZA", { style: "currency", currency })`), **Status** (`status` badge). Each row is a keyboard-activatable link/button navigating to `/transactions/{id}`.

```tsx
// status badge mapping
const TXN_STATUS: Record<TxnStatus, { label: string; variant: string }> = {
  SETTLED:  { label: "Settled",  variant: "default"   },
  PENDING:  { label: "Pending",  variant: "secondary" },
  REVERSED: { label: "Reversed", variant: "outline"   },
};
```

### 2.4 Filters component

- **Date range:** shadcn `Popover` + `Calendar` (range mode) producing `from`/`to` as `yyyy-MM-dd`. Boundary dates are inclusive per AC-TXN-01 — the API owns inclusivity; the UI must pass the selected boundary dates unchanged. A "Clear dates" affordance resets both.
- **Merchant:** debounced (300ms) `Input` that updates the `merchant` param; typing resets `page` to 1.
- Changing any filter resets pagination to page 1. All controls have associated `<Label>`s and are reachable by keyboard.

### 2.5 Transaction detail page

Route `/transactions/:id`. Renders a `Card` with all fields (TXN-03): reference, merchant name, merchant category, amount + currency, transaction date/time, and status. Includes a back link to the list (preserving prior filters via history) and the primary CTA.

```tsx
// src/features/transactions/TransactionDetailPage.tsx (excerpt)
export function Component() {
  const { id = "" } = useParams();
  const { data: txn, isLoading, isError } = useTransaction(id);
  const navigate = useNavigate();
  // header: <Button variant="ghost" onClick={() => navigate(-1)}>← Back</Button>
  // definition list of fields, then:
  // <Button size="lg" onClick={() => navigate(`/disputes/new?transactionId=${txn.id}`)}>
  //   Dispute this transaction
  // </Button>
}
```

### 2.6 "Dispute this transaction" CTA

The CTA appears on the detail view (primary) and optionally as a per-row action on the list. It navigates to `/disputes/new?transactionId={id}` (implemented by TDP-FE-03), passing the transaction id so the dispute flow is pre-bound to the correct transaction. A `REVERSED` transaction still permits dispute (backend enforces any business rules); the UI does not block it.

## 3. Acceptance Criteria

- The login page authenticates a seeded `CUSTOMER`, and on success lands on `/transactions`; invalid credentials show a single generic error with no field-level enumeration (AC-AUTH-01).
- `/transactions` renders a paginated table defaulting to 20 rows per page; pagination controls move between pages and the table stays populated during page transitions (AC-TXN-01, TXN-01).
- Each row displays transaction reference, merchant name, amount, currency, date and status (AC-TXN-01, TXN-03).
- Filtering by date range returns only transactions within the range inclusive of boundary dates; filtering by merchant name narrows the list; combining both filters works; clearing filters restores the full list (AC-TXN-01, TXN-02).
- Filter and page state are reflected in the URL and survive a page reload.
- Clicking a row opens `/transactions/{id}` showing all detail fields (TXN-03).
- The transaction detail view shows a prominent "Dispute this transaction" button that navigates to `/disputes/new?transactionId={id}`.
- Loading states show skeletons, API errors show a retryable alert, and an empty result shows an explanatory empty state.
- Amounts are formatted as ZAR currency (`Intl.NumberFormat`, `en-ZA`) and dates are localised and human-readable.
- The table, filters and CTA are fully keyboard-operable with visible focus, correct `<th scope>` headers, labelled filter controls, and status conveyed by text (not colour alone) — WCAG 2.1 AA (SPEC.md §3.6).

## 4. Technical Notes

- **Filters ↔ URL:** Keep filter/pagination state in `useSearchParams` (single source of truth) rather than component state, so back/forward navigation and reloads behave correctly and TanStack Query keys stay stable.
- **`keepPreviousData`:** Use `placeholderData: keepPreviousData` (v5) so paging/filtering doesn't blank the table; show a subtle `isFetching` indicator instead.
- **Date handling:** Send `from`/`to` as calendar dates (`yyyy-MM-dd`), not full timestamps, to match AC-TXN-01 boundary-inclusive semantics; do not apply timezone shifts client-side. `transactionDate` from the API is `TIMESTAMPTZ` (ISO 8601) — render in the user's locale but keep the raw value for sorting.
- **Currency:** Format with the record's own `currency` field (default `ZAR`, SPEC.md §3.2); do not hard-code the symbol.
- **Auth is implicit:** The API scopes transactions to the caller (`GET /transactions` returns "caller's transactions"); the frontend must not send a customer id.
- **Debounce merchant input** to avoid a request per keystroke; cancel in-flight via TanStack Query's automatic key-change cancellation.
- **Performance:** With `staleTime: 30s` from TDP-FE-01, revisiting the list within the window is instant from cache; detail pages prefetch on row hover are a nice-to-have, not required.
- **Accessibility:** Use a real `<table>` with `<caption>`; make rows navigable via an anchor or button (not a bare `onClick` div); ensure the date picker popover traps focus and is dismissible with `Esc`.

## 5. Definition of Done

- [ ] `LoginPage`, `TransactionsPage`, `TransactionDetailPage`, `TransactionFilters`, and the `useTransactions`/`useTransaction` hooks implemented and merged.
- [ ] Paginated table with date-range + merchant filters working against the live `GET /api/v1/transactions`; detail view working against `GET /api/v1/transactions/{id}`.
- [ ] Filter/page state persisted in the URL; loading/error/empty states implemented.
- [ ] "Dispute this transaction" CTA navigates to the FE-03 submission route with `transactionId`.
- [ ] Currency and date formatting verified for ZAR/`en-ZA`.
- [ ] Keyboard navigation, focus visibility, table semantics and non-colour status indicators verified against WCAG 2.1 AA.
- [ ] Component tests (Vitest + RTL) cover: table renders rows with correct fields and badges, filter changes update the query, and the CTA navigates with the right param (feeds TDP-TEST-02).
- [ ] `tsc --noEmit` and ESLint clean; reviewed and merged to `main`; demonstrated as part of the full customer journey.
