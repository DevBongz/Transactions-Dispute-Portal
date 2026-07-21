# TDP-FE-05 — Operations Dashboard, Resolve Modal & Metrics

**Jira summary:** Build the operations surface for analysts and managers: a dispute queue sorted by priority (descending) with status / priority / category filters, a single-screen dispute detail combining customer, transaction, AI classification, description and timeline, and a resolve modal that captures outcome + internal notes, calls the AI summary endpoint to preview a customer-facing summary, and confirms resolution. Managers additionally see summary metrics from `GET /api/v1/dashboard/summary`. Implements Journey 2 and user stories OPS-01..OPS-06, AI-03 (AC-OPS-04/AC-AI-03, AC-OPS-06).

## 1. Context & Motivation

- **Background:** With customer flows complete (TDP-FE-02..04), the ops team needs to work the dispute backlog. The backend exposes ops-scoped `GET /api/v1/disputes` (all disputes, filterable by `status`/`priority`/`category`), `GET /disputes/{id}` (detail + timeline), `PATCH /disputes/{id}/status`, `POST /disputes/{id}/resolve`, `POST /ai/generate-summary`, and `GET /dashboard/summary` (SPEC.md §3.3). Journey 2 (SPEC.md §2.2) prescribes the priority-sorted queue → single-screen detail → resolve modal → Generate Summary preview → Confirm flow, publishing `dispute.resolved` (SPEC.md §3.4).
- **Business Impact:** This delivers ops throughput — "disputes can be reviewed and resolved without leaving the portal" (SPEC.md §1.1 objective) — and the auto-summary that saves analysts writing separate customer communications (OPS-05/AI-03). Priority sorting + pre-triaged categories (from AI classification, TDP-AI-02) let Sipho work the most critical cases first (OPS-01), and the manager metrics give Zanele backlog visibility (OPS-06). Per SPEC.md §4.3 this is the last-built, highest-risk surface; core customer flows take priority.
- **User Story:** As an ops analyst (Sipho), I want a priority-ranked, filterable queue of disputes and a single screen to investigate and formally resolve each one with an auto-generated customer summary, and as an ops manager (Zanele) I want a metrics overview of volumes and resolution time, so that the team resolves the right cases quickly and I can monitor performance (OPS-01..OPS-06, AI-03).
- **Dependencies:** Depends on **TDP-FE-01** (routing, API client, role guards, shared types), **TDP-DISP-02** (listing/detail/status), **TDP-DISP-03** (resolve API + `dispute.resolved`), and **TDP-AI-03** (`POST /ai/generate-summary`). Reuses `StatusBadge`/formatters from **TDP-FE-04**. Milestone: **Day 6 (21 Jul) — Frontend — Ops Dashboard**.

## 2. Detailed Description

### 2.1 Routes & role gating

Ops routes (`/ops`, `/ops/disputes/:id`) are gated to `OPS_ANALYST` and `OPS_MANAGER` by `ProtectedRoute allow={[...]}` from TDP-FE-01. The metrics panel is visible to both roles but framed for managers (OPS-06); resolve actions are available to analysts (OPS-04). A `CUSTOMER` hitting these routes is redirected to `/forbidden`.

### 2.2 Ops queue hook & filters

```ts
// src/features/ops/api.ts
export interface OpsFilters {
  page: number; pageSize: number;
  status?: DisputeStatus; priority?: Priority; category?: DisputeCategory;
}
export function useOpsDisputes(filters: OpsFilters) {
  return useQuery({
    queryKey: ["disputes", "ops", filters],
    queryFn: async () => (await api.get<Paged<Dispute>>("/disputes", { params: filters })).data,
    placeholderData: keepPreviousData,
  });
}
```

The queue is sorted by priority descending (`CRITICAL > HIGH > MEDIUM > LOW`, OPS-01, Journey 2 step 2). If the API already sorts, respect it; otherwise apply a client-side comparator as a defensive default:

```ts
const PRIORITY_RANK: Record<Priority, number> = { CRITICAL: 3, HIGH: 2, MEDIUM: 1, LOW: 0 };
const byPriorityDesc = (a: Dispute, b: Dispute) =>
  (PRIORITY_RANK[b.priority ?? "LOW"] - PRIORITY_RANK[a.priority ?? "LOW"])
  || (+new Date(a.createdAt) - +new Date(b.createdAt)); // older first within a tier
```

### 2.3 Ops dashboard page

Route `/ops`. Top: a metrics strip (`§2.7`). Below: a filter bar (three `Select`s — Status / Priority / Category, all bound to URL params) over a `Table`. Columns: **Priority** (`PriorityBadge`), **Reference**, **Customer**, **Merchant**, **Category** (label or "Pending"), **Status** (`StatusBadge` from FE-04), **Submitted**. Rows link to `/ops/disputes/{id}`. Default filter surfaces the working queue (e.g. non-resolved) but all combinations are available (OPS-02). Loading/error/empty states as elsewhere.

```tsx
// PriorityBadge — colour + text (never colour alone)
const PRIORITY: Record<Priority, { label: string; variant: BadgeProps["variant"] }> = {
  CRITICAL: { label: "Critical", variant: "destructive" },
  HIGH:     { label: "High",     variant: "default"     },
  MEDIUM:   { label: "Medium",   variant: "secondary"   },
  LOW:      { label: "Low",      variant: "outline"     },
};
```

### 2.4 Single-screen ops dispute detail

Route `/ops/disputes/:id`. One screen, no system-switching (OPS-03, Journey 2 step 4), laid out in cards:

- **Customer** — full name, email (from `DisputeDetail`).
- **Original transaction** — merchant, amount (ZAR), date, reference, status.
- **AI classification** — category + priority badges (or a "Pending"/"Needs triage" note for `CLASSIFICATION_FAILED`).
- **Customer's description** — raw `customer_description`.
- **Timeline** — reuse `DisputeTimeline` (TDP-FE-04).
- **Actions** — a **Resolve** button (opens the modal) when the dispute is not already resolved; a secondary control to move status to `UNDER_REVIEW` via `PATCH /disputes/{id}/status` (Journey 2 implies analysts pick up cases). Once resolved, the resolution (outcome + customer summary) is shown read-only.

### 2.5 Status update hook

```ts
export function useUpdateStatus(id: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (status: DisputeStatus) =>
      (await api.patch<Dispute>(`/disputes/${id}/status`, { status })).data,
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ["dispute", id] });
      qc.invalidateQueries({ queryKey: ["disputes"] });
    },
  });
}
```

### 2.6 Resolve modal (Journey 2 steps 5–10)

A shadcn `Dialog` with a small state machine: **capture → preview → confirming**.

1. **Capture:** `Select` for **Outcome** (`UPHELD` / `DECLINED` / `PARTIAL`, required) and a `Textarea` for **Internal Notes** (required, min 20 chars per AC-OPS-04). "Generate Summary" is disabled until both are valid.
2. **Generate Summary:** calls `POST /ai/generate-summary` with `{ disputeId, outcome, internalNotes }`, shows a loading state (`aria-busy`), then renders the returned `summary` in an **editable** `Textarea` preview (Journey 2 step 8; analyst may edit, step 9).
3. **Confirm Resolution:** disabled until a summary is present (per AC on the resolve modal); calls `POST /disputes/{id}/resolve` with `{ outcome, internalNotes, customerSummary }`. On success, closes the modal, toasts success, and invalidates the dispute + queue + dashboard queries. This publishes `dispute.resolved` server-side (SPEC.md §3.4) and makes the summary visible to the customer (TDP-FE-04, Journey 2 step 11).

```ts
export function useGenerateSummary() {
  return useMutation({
    mutationFn: async (v: { disputeId: string; outcome: string; internalNotes: string }) =>
      (await api.post<{ summary: string }>("/ai/generate-summary", v)).data.summary,
  });
}
export function useResolveDispute(id: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (v: { outcome: string; internalNotes: string; customerSummary: string }) =>
      (await api.post<Resolution>(`/disputes/${id}/resolve`, v)).data,
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ["dispute", id] });
      qc.invalidateQueries({ queryKey: ["disputes"] });
      qc.invalidateQueries({ queryKey: ["dashboard-summary"] });
    },
  });
}
```

```tsx
// src/features/ops/OpsResolveModal.tsx (structure)
const [outcome, setOutcome] = useState<"UPHELD"|"DECLINED"|"PARTIAL"|"">("");
const [notes, setNotes] = useState("");
const [summary, setSummary] = useState("");
const gen = useGenerateSummary();
const resolve = useResolveDispute(disputeId);
const canGenerate = outcome !== "" && notes.trim().length >= 20;
// <Dialog>...<DialogTitle>Resolve dispute {reference}</DialogTitle>
//   <Label>Outcome</Label><Select value={outcome} .../>
//   <Label htmlFor="notes">Internal notes</Label>
//   <Textarea id="notes" value={notes} onChange=... aria-describedby="notes-hint"/>
//   <p id="notes-hint">Minimum 20 characters. Not shown to the customer.</p>
//   <Button disabled={!canGenerate || gen.isPending} aria-busy={gen.isPending}
//           onClick={()=>gen.mutate({disputeId, outcome, internalNotes: notes}, {onSuccess:setSummary})}>
//     {gen.isPending ? "Generating…" : "Generate Summary"}</Button>
//   {summary && (<>
//     <Label htmlFor="summary">Customer summary (editable)</Label>
//     <Textarea id="summary" value={summary} onChange=.../> </>)}
//   <Button disabled={!summary || resolve.isPending}
//     onClick={()=>resolve.mutate({outcome, internalNotes: notes, customerSummary: summary},
//       {onSuccess: onResolved})}>Confirm Resolution</Button>
```

### 2.7 Dashboard summary metrics (OPS-06 / AC-OPS-06)

A metrics strip at the top of `/ops` fed by `GET /dashboard/summary`, refreshed on page load (no real-time push, AC-OPS-06).

```ts
export function useDashboardSummary() {
  return useQuery({
    queryKey: ["dashboard-summary"],
    queryFn: async () => (await api.get<{
      totalOpen: number;
      byPriority: Record<Priority, number>;
      byCategory: Record<DisputeCategory, number>;
      avgResolutionHours: number;
    }>("/dashboard/summary")).data,
  });
}
```

Rendered as accessible stat `Card`s: **Total open**, **By priority** (Critical/High/Medium/Low counts), **By category** (five categories), **Avg resolution time** (`avgResolutionHours`, formatted e.g. "18.5 hrs" or "over the last 30 days"). Any chart uses text labels + values, not colour-only encoding.

## 3. Acceptance Criteria

- `/ops` is accessible only to `OPS_ANALYST`/`OPS_MANAGER`; a `CUSTOMER` is redirected to `/forbidden` (role gating).
- The queue lists disputes sorted by priority descending (Critical first), with a per-row priority badge, and is paginated (OPS-01, Journey 2 step 2).
- Filtering by status, priority, and category (individually and combined) narrows the queue; filters persist in the URL (OPS-02).
- Clicking a dispute opens a single-screen detail showing customer info, original transaction, AI category/priority, the customer's description, and the submission timeline — no navigation to other systems required (OPS-03, Journey 2 step 4).
- The resolve modal requires an outcome (`UPHELD`/`DECLINED`/`PARTIAL`) and internal notes of at least 20 characters before Generate Summary is enabled (AC-OPS-04).
- Generate Summary calls `POST /api/v1/ai/generate-summary`, shows a loading state, and displays the returned 2–4 sentence plain-language summary in an editable field (AI-03, AC-AI-03, Journey 2 step 8).
- Confirm Resolution is disabled until a summary is present, calls `POST /api/v1/disputes/{id}/resolve`, and on success closes the modal, marks the dispute Resolved, and stores the (possibly edited) summary (AC-OPS-04, Journey 2 steps 9–10).
- After resolution the dispute leaves the open queue, the customer-facing summary becomes visible on the customer detail page (TDP-FE-04), and the `dispute.resolved` event is published server-side (Journey 2 step 11, SPEC.md §3.4).
- The dashboard metrics strip shows total open disputes, counts by priority, counts by category, and average resolution time (last 30 days), refreshed on page load (OPS-06, AC-OPS-06).
- Loading/error/empty states are handled for queue, detail, summary generation, and metrics.
- All ops views and the modal are fully keyboard-operable, the dialog traps focus and closes on `Esc`, priority/status are conveyed by text (not colour alone), fields are labelled, and busy states set `aria-busy` — WCAG 2.1 AA (SPEC.md §3.6).

## 4. Technical Notes

- **Priority sort is defensive:** apply the client comparator only if the API does not guarantee ordering; do not double-sort against an already-sorted, paginated response in a way that breaks pages. Prefer server-side ordering + `page` params.
- **Modal state machine:** guard against resolving without a generated summary (the AC hinges on it) and against double-submit — disable Confirm while `resolve.isPending`. Editing notes/outcome after generating should not silently keep a stale summary tied to different notes; either regenerate or warn (keep it simple: re-enable Generate and require a fresh summary if outcome changes).
- **Notes vs summary boundary:** `internalNotes` are never shown to the customer (SPEC.md §2.2, §3.5 — "Do not reveal internal investigation details"); only `customerSummary` is customer-facing. Label the internal notes field explicitly.
- **AI latency & failure:** summary generation uses `claude-sonnet-5` (SPEC.md §3.5) and may take longer than extraction; show a clear loading state and, on failure, allow retry — the analyst can also type the summary manually into the editable field so a Claude outage doesn't block resolution (graceful degradation, SPEC.md §4.3).
- **Cache invalidation contract:** resolving/patching must invalidate `["dispute", id]`, `["disputes"]` (all variants incl. `["disputes","ops",...]`), and `["dashboard-summary"]` so the queue, the customer's view (FE-04), and metrics all reflect the change.
- **No AI key on client:** the frontend calls `/ai/generate-summary` only; Anthropic is server-side (SPEC.md §3.6).
- **Reuse:** `StatusBadge`, `DisputeTimeline`, currency/date formatters and `TransactionSummary` come from TDP-FE-02/FE-04 — import, don't duplicate. Add `PriorityBadge` to the shared components.
- **Scope/risk note:** per SPEC.md §4.3 the ops dashboard is built last and treated as nice-to-have relative to core customer flows; if time-constrained, prioritise queue + resolve over the metrics strip. Metrics are read-only and low-risk.

## 5. Definition of Done

- [ ] `OpsDashboardPage`, `OpsDisputeDetailPage`, `OpsResolveModal`, `PriorityBadge`, metrics strip, and the `useOpsDisputes`/`useDisputeDetail`/`useUpdateStatus`/`useGenerateSummary`/`useResolveDispute`/`useDashboardSummary` hooks implemented and merged.
- [ ] Priority-sorted, filterable, paginated queue working against ops-scoped `GET /api/v1/disputes`; role gating verified for customer/analyst/manager.
- [ ] Single-screen detail combining customer, transaction, classification, description and timeline (OPS-03).
- [ ] Resolve modal enforces outcome + ≥20-char notes, generates an editable summary via `POST /ai/generate-summary`, and confirms via `POST /disputes/{id}/resolve`; Confirm disabled until a summary exists (AC-OPS-04/AC-AI-03).
- [ ] Post-resolution invalidation verified: dispute leaves the open queue, customer sees the summary (FE-04), metrics update on reload; `dispute.resolved` published server-side.
- [ ] Dashboard metrics render total open, by-priority, by-category, and avg resolution time from `GET /dashboard/summary` on page load (AC-OPS-06).
- [ ] AI failure path verified: summary generation error is retryable and the analyst can type a manual summary.
- [ ] Accessibility verified: focus-trapped dialog, `Esc` to close, labelled fields, `aria-busy` on generation, non-colour priority/status — WCAG 2.1 AA.
- [ ] Component tests (Vitest + RTL) cover: outcome dropdown required; Generate Summary calls the AI endpoint; Confirm disabled until summary present; queue renders priority-sorted rows (feeds TDP-TEST-02, mirrors §4.4 `OpsResolveModal` cases).
- [ ] `tsc --noEmit` and ESLint clean; reviewed and merged to `main`; demonstrated end-to-end as Journey 2.
