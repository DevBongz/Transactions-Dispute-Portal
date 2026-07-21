# TDP-FE-04 — Customer Dispute History & Timeline Views

**Jira summary:** Give customers full visibility into their disputes: a **My Disputes** list showing every past and open dispute with a status badge (Open / Under Review / Resolved), and a dispute detail page rendering a chronological `DisputeTimeline` of lifecycle events (Submitted → Under Review → Resolved with timestamps) plus a highlighted resolution summary panel once the case is closed. Implements Journey 3 and user stories TRACK-01, TRACK-02, TRACK-03.

## 1. Context & Motivation

- **Background:** After submitting a dispute (TDP-FE-03), the customer needs to track its progress. The backend exposes `GET /api/v1/disputes` (returns the caller's disputes for a customer) and `GET /api/v1/disputes/{id}` returning a `DisputeDetail` with the event timeline (SPEC.md §3.3). The data model provides `DisputeEvent` rows (`SUBMITTED`, `CLASSIFIED`, `ASSIGNED`, `UNDER_REVIEW`, `RESOLVED`) and a `Resolution` with a `customer_summary` (SPEC.md §3.2). Journey 3 (SPEC.md §2.2) specifies the list-with-badges → detail-with-timeline → highlighted-summary flow.
- **Business Impact:** Clear resolution communication is a stated objective (auto-generated plain-language summaries delivered on every resolved dispute, SPEC.md §1.1). Transparent status tracking reduces inbound branch/support contact — the whole reason the portal exists (SPEC.md §1.3, Maya's persona) — and closes the loop on the customer journey.
- **User Story:** As a customer (Maya), I want to see all my disputes and their current statuses, open any one to follow a timeline of what has happened, and read a plain-language resolution summary when it closes, so that I always know the state of my cases and understand the outcome (TRACK-01, TRACK-02, TRACK-03).
- **Dependencies:** Depends on **TDP-FE-01** (routing, API client, types) and **TDP-DISP-02** (dispute listing, detail with timeline, status). Consumes disputes created by **TDP-FE-03**. Resolution summaries originate from **TDP-DISP-03 / TDP-AI-03** (produced via the ops flow in TDP-FE-05). Milestone: **Day 5 (20 Jul) — Frontend — Disputes**.

## 2. Detailed Description

### 2.1 Types

```ts
// extends src/types/api.ts
export interface Dispute {
  id: string; reference: string; transactionId: string;
  status: DisputeStatus; category: DisputeCategory | null; priority: Priority | null;
  createdAt: string; updatedAt: string;
}

export type DisputeEventType = "SUBMITTED" | "CLASSIFIED" | "ASSIGNED" | "UNDER_REVIEW" | "RESOLVED";
export interface DisputeEvent {
  id: string; eventType: DisputeEventType; description: string; createdAt: string; actorId: string | null;
}
export interface Resolution {
  outcome: "UPHELD" | "DECLINED" | "PARTIAL";
  customerSummary: string; resolvedAt: string;
}
export interface DisputeDetail extends Dispute {
  transaction: Transaction;
  events: DisputeEvent[];
  resolution: Resolution | null;
}
```

### 2.2 Data hooks

```ts
// src/features/disputes/history-api.ts
export function useMyDisputes(filters: { page: number; pageSize: number; status?: DisputeStatus }) {
  return useQuery({
    queryKey: ["disputes", filters],
    queryFn: async () =>
      (await api.get<Paged<Dispute>>("/disputes", { params: filters })).data,
    placeholderData: keepPreviousData,
  });
}
export function useDisputeDetail(id: string) {
  return useQuery({
    queryKey: ["dispute", id],
    queryFn: async () => (await api.get<DisputeDetail>(`/disputes/${id}`)).data,
    enabled: !!id,
  });
}
```

The customer view relies on the backend scoping `GET /disputes` to the caller; the frontend sends no customer id.

### 2.3 Status badge (shared)

A reusable, spec-aligned status badge used here and reused by ops (TDP-FE-05). Status is conveyed by both colour and label text (WCAG — never colour alone).

```tsx
// src/features/disputes/StatusBadge.tsx
const STATUS: Record<DisputeStatus, { label: string; variant: BadgeProps["variant"] }> = {
  OPEN:                  { label: "Open",           variant: "secondary" },
  UNDER_REVIEW:          { label: "Under Review",   variant: "default"   },
  RESOLVED:              { label: "Resolved",       variant: "outline"   },
  CLASSIFICATION_FAILED: { label: "Needs Triage",   variant: "destructive" },
};
export function StatusBadge({ status }: { status: DisputeStatus }) {
  const s = STATUS[status];
  return <Badge variant={s.variant} aria-label={`Status: ${s.label}`}>{s.label}</Badge>;
}
```

> Journey 3 names three customer-facing badges (Open / Under Review / Resolved). `CLASSIFICATION_FAILED` is an internal state (SPEC.md §3.2); for customers it is displayed as "Under Review" so a classification failure never leaks as an alarming state — map it in the customer list only. The raw value is still available to ops.

### 2.4 My Disputes list page

Route `/my-disputes`. A `Card`-wrapped table (or responsive card list on narrow viewports) with columns: **Reference**, **Merchant** (from the linked transaction — resolved via the list payload or a lightweight lookup), **Submitted** (`createdAt`), **Category** (human label or "Pending" when null/unclassified), **Status** (`StatusBadge`). Rows link to `/my-disputes/{id}`. Optional status filter (`OPEN` / `UNDER_REVIEW` / `RESOLVED`) via a `Select` bound to a URL param. Includes loading skeleton, error alert with retry, and an empty state ("You haven't raised any disputes yet.") with a link back to transactions.

```tsx
// src/features/disputes/MyDisputesPage.tsx (structure)
export function Component() {
  const [sp, setSp] = useSearchParams();
  const filters = { page: Number(sp.get("page") ?? 1), pageSize: 20,
                    status: (sp.get("status") as DisputeStatus | null) ?? undefined };
  const { data, isLoading, isError, refetch } = useMyDisputes(filters);
  // <h1>My Disputes</h1> + <StatusFilter/> + table of rows -> navigate(`/my-disputes/${d.id}`)
}
```

Category display maps enums to labels (e.g. `DUPLICATE_CHARGE` → "Duplicate charge"); a null category renders a muted "Pending classification" chip since classification is asynchronous (SPEC.md §2.2 step 9).

### 2.5 Dispute detail page

Route `/my-disputes/:id`. Sections top-to-bottom (Journey 3 steps 3–5):

1. **Header** — reference, `StatusBadge`, submitted date, category/priority (or "Pending" chips).
2. **Transaction panel** — merchant, amount (ZAR), date, reference, status (reusing formatting from TDP-FE-02).
3. **Your description** — the customer's original `customer_description`.
4. **Timeline** — `DisputeTimeline` component (below).
5. **Resolution summary panel** — rendered only when `status === "RESOLVED"` and `resolution != null`; visually highlighted (TRACK-03).

### 2.6 DisputeTimeline component

Renders `events` in chronological order (oldest → newest) as an ordered vertical timeline. Each node shows a friendly label for the `eventType`, the human-readable `description`, and a localised timestamp. Uses a semantic `<ol>` so screen readers announce order and count.

```tsx
// src/features/disputes/DisputeTimeline.tsx
const EVENT_LABEL: Record<DisputeEventType, string> = {
  SUBMITTED: "Dispute submitted",
  CLASSIFIED: "Categorised",
  ASSIGNED: "Assigned to an analyst",
  UNDER_REVIEW: "Under review",
  RESOLVED: "Resolved",
};
export function DisputeTimeline({ events }: { events: DisputeEvent[] }) {
  const ordered = [...events].sort((a, b) => +new Date(a.createdAt) - +new Date(b.createdAt));
  return (
    <ol className="relative border-l pl-6" aria-label="Dispute timeline">
      {ordered.map((e) => (
        <li key={e.id} className="mb-6">
          <span className="absolute -left-2 h-4 w-4 rounded-full bg-primary" aria-hidden />
          <h3 className="font-medium">{EVENT_LABEL[e.eventType] ?? e.eventType}</h3>
          <time dateTime={e.createdAt} className="text-sm text-muted-foreground">
            {formatDateTime(e.createdAt)}
          </time>
          <p className="text-sm">{e.description}</p>
        </li>
      ))}
    </ol>
  );
}
```

### 2.7 Resolution summary panel

```tsx
// rendered when resolution present
<Card className="border-primary/40 bg-primary/5" role="region" aria-labelledby="resolution-heading">
  <CardHeader><CardTitle id="resolution-heading">Resolution</CardTitle></CardHeader>
  <CardContent>
    <Badge>{OUTCOME_LABEL[resolution.outcome]}</Badge>
    <p className="mt-2">{resolution.customerSummary}</p>
    <p className="text-sm text-muted-foreground">Resolved on {formatDateTime(resolution.resolvedAt)}</p>
  </CardContent>
</Card>
```

`customerSummary` is the AI-generated plain-language text (TDP-AI-03). Outcome labels: `UPHELD` → "Resolved in your favour", `DECLINED` → "Not upheld", `PARTIAL` → "Partially upheld".

## 3. Acceptance Criteria

- `/my-disputes` lists all of the caller's disputes with, per row, reference, merchant, submitted date, category (or a "Pending" indicator), and a status badge (TRACK-01, Journey 3 step 2).
- Status badges render Open, Under Review, and Resolved distinctly, conveyed by both label text and colour; an internal `CLASSIFICATION_FAILED` is shown to customers as "Under Review".
- The optional status filter narrows the list; the list is paginated (default 20) and paging preserves the populated table.
- Clicking a dispute opens `/my-disputes/{id}` showing header, the linked transaction, the customer's description, the timeline, and (when resolved) the resolution summary.
- `DisputeTimeline` renders events strictly in chronological order with timestamps and friendly labels (TRACK-02, Journey 3 step 4).
- The resolution summary panel appears only for resolved disputes, is visually highlighted, shows the outcome and the AI-generated plain-language `customerSummary`, and the resolved timestamp (TRACK-03, Journey 3 step 5).
- Loading shows skeletons, errors show a retryable alert, and no disputes shows an empty state linking back to transactions.
- Dates are localised/human-readable; amounts are ZAR-formatted; category/priority nulls render as "Pending" rather than blank or an error.
- All views are keyboard-navigable with correct list/table/region semantics, `<ol>`-based timeline order, `<time datetime>` elements, and status conveyed by text — WCAG 2.1 AA (SPEC.md §3.6).

## 4. Technical Notes

- **Chronological order is a hard requirement** (TRACK-02, AC references the timeline order): sort client-side by `createdAt` defensively even if the API returns ordered events, and use a stable sort.
- **Async classification:** a freshly submitted dispute may have `category`/`priority` = null and status `OPEN` until the background consumer runs (SPEC.md §2.2 step 9, §3.2). Render "Pending" chips; consider a short `refetchInterval` (e.g. 5s) on the detail page while status is `OPEN`/unclassified so the customer sees classification land without a manual refresh — bounded and disabled once classified/resolved.
- **Reuse:** `StatusBadge`, currency/date formatters, and `TransactionSummary` are shared with TDP-FE-02/FE-05 — extract into `components/`/`lib/` to avoid divergence.
- **Merchant on the list:** if `GET /disputes` does not embed the transaction, resolve merchant names via the already-cached transactions query or a batched lookup; avoid N+1 per-row requests.
- **Customer scoping:** rely on the API to scope `GET /disputes` to the caller; never trust or send a client-supplied customer id (security, SPEC.md §3.6).
- **Summary trust boundary:** `customerSummary` is server-generated plain text; render as text (React escapes by default) — do not `dangerouslySetInnerHTML`.
- **Cache coherence:** this page shares the `["disputes"]` / `["dispute", id]` keys with TDP-FE-03 and TDP-FE-05; an ops resolution (FE-05) should invalidate `["dispute", id]` so a customer viewing the same dispute sees the update on next fetch.

## 5. Definition of Done

- [ ] `MyDisputesPage`, `DisputeDetailPage`, `DisputeTimeline`, `StatusBadge`, resolution summary panel, and `useMyDisputes`/`useDisputeDetail` hooks implemented and merged.
- [ ] List renders all caller disputes with correct badges and pagination; optional status filter works.
- [ ] Detail page shows header, transaction, description, chronological timeline, and highlighted resolution summary (only when resolved) against live `GET /api/v1/disputes/{id}`.
- [ ] Pending (unclassified) disputes render "Pending" indicators without error; optional bounded refetch surfaces classification.
- [ ] Loading/error/empty states implemented; dates and currency formatted consistently with TDP-FE-02.
- [ ] Accessibility verified: `<ol>` timeline order, `<time>` elements, labelled regions, keyboard nav, non-colour status — WCAG 2.1 AA.
- [ ] Component tests (Vitest + RTL) cover: list renders rows with correct status badges; timeline renders events in chronological order; resolution panel shown only when resolved (feeds TDP-TEST-02).
- [ ] `tsc --noEmit` and ESLint clean; reviewed and merged to `main`; demonstrated as Journey 3.
