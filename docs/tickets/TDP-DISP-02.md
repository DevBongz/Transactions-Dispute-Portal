# TDP-DISP-02 — Dispute Listing, Detail & Status Update API

**Jira summary:** Implement the read and status-transition surface for disputes: `GET /api/v1/disputes` with role-aware scoping (customers see only their own; ops see all) plus `status`/`priority`/`category` filters and priority-ordered results; `GET /api/v1/disputes/{id}` returning the full detail including the original transaction, resolution (if any), and the chronological `DisputeEvent` timeline; and `PATCH /api/v1/disputes/{id}/status` for ops-only status transitions that append a timeline event. These endpoints power the customer history views (TDP-FE-04) and the ops queue (TDP-FE-05).

## 1. Context & Motivation

- **Background:** Disputes can be created (TDP-DISP-01) but not listed, inspected, or moved through their lifecycle. Journey 2 (ops resolves) and Journey 3 (customer views history) in SPEC §2.2 both start here: the ops queue sorted by priority, and the customer timeline of Submitted → Under Review → Resolved.
- **Business Impact:** Serves OPS-01/02/03 (analyst triage: see open disputes ranked by priority, filter by category/priority/status, investigate on one screen) and TRACK-01/02/03 (customer sees statuses, timeline, and resolution). Role-aware scoping is a hard security boundary — a customer must never see another customer's dispute, and only ops may change status.
- **User Story:** As an ops analyst (Sipho), I want to list and filter all disputes by status/priority/category and open a single-screen detail with the full timeline so that I can work my queue efficiently; and as a customer (Maya), I want to see my disputes and their event history so that I know each case's state.
- **Dependencies:** TDP-DISP-01 (disputes + events exist), TDP-AUTH-01 (roles), TDP-TXN-01 (transaction projection reuse). Consumed by TDP-FE-04, TDP-FE-05. Resolution detail comes from TDP-DISP-03. Milestone: **Day 2** (SPEC §4.1).

## 2. Detailed Description

### 2.1 Endpoints (SPEC §3.3)

| Method | Path | Auth | Query / Body | Response |
|---|---|---|---|---|
| GET | `/api/v1/disputes` | any authenticated | `page`,`pageSize`,`status`,`priority`,`category` | `PagedResult<DisputeSummaryDto>` |
| GET | `/api/v1/disputes/{id}` | owner (customer) or any ops | — | `DisputeDetailDto` |
| PATCH | `/api/v1/disputes/{id}/status` | `OPS_ANALYST`, `OPS_MANAGER` | `{ status }` | `200 DisputeSummaryDto` |

### 2.2 DTOs

```csharp
public sealed record DisputeSummaryDto(
    Guid Id, string Reference, Guid TransactionId, Guid CustomerId,
    string? CustomerName,          // populated for ops list only
    string Status, string? Category, string? Priority,
    DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);

public sealed record DisputeDetailDto(
    Guid Id, string Reference, string Status, string? Category, string? Priority,
    string CustomerDescription, JsonElement? ExtractedFields,
    Guid? AssignedToId,
    TransactionDto Transaction,                       // reused from TDP-TXN-01
    ResolutionDto? Resolution,                        // null until resolved (TDP-DISP-03)
    IReadOnlyList<DisputeEventDto> Timeline);

public sealed record DisputeEventDto(
    string EventType, string? Description, Guid? ActorId,
    string? ActorName, DateTimeOffset CreatedAt);

public sealed record ResolutionDto(
    string Outcome, string? CustomerSummary,
    Guid ResolvedById, DateTimeOffset ResolvedAt);    // InternalNotes NOT exposed to customers — see §2.6

public sealed record UpdateStatusRequest(
    [property: Required] string Status);
```

### 2.3 Role-aware list scoping

The list endpoint branches on the caller's role claim. Customers are filtered to their own `customer_id`; ops see everything. Ops results default to **priority descending** so the queue surfaces `CRITICAL` first (OPS-01), with a `CASE`-based ordinal since priority is a string enum.

```csharp
public async Task<PagedResult<DisputeSummaryDto>> ListAsync(
    ClaimsPrincipal caller, DisputeQuery q, CancellationToken ct)
{
    IQueryable<Dispute> query = _db.Disputes.AsNoTracking();

    if (caller.IsInRole("CUSTOMER"))
        query = query.Where(d => d.CustomerId == caller.GetUserId());
    // OPS_ANALYST / OPS_MANAGER: no owner filter — full visibility.

    if (!string.IsNullOrWhiteSpace(q.Status))   query = query.Where(d => d.Status == q.Status);
    if (!string.IsNullOrWhiteSpace(q.Priority)) query = query.Where(d => d.Priority == q.Priority);
    if (!string.IsNullOrWhiteSpace(q.Category)) query = query.Where(d => d.Category == q.Category);

    var total = await query.CountAsync(ct);

    // Priority ordinal for CRITICAL > HIGH > MEDIUM > LOW > null.
    var items = await query
        .OrderByDescending(d => d.Priority == "CRITICAL" ? 4
                              : d.Priority == "HIGH"     ? 3
                              : d.Priority == "MEDIUM"   ? 2
                              : d.Priority == "LOW"      ? 1 : 0)
        .ThenByDescending(d => d.CreatedAt)
        .Skip((q.Page - 1) * q.PageSize).Take(q.PageSize)
        .Select(d => new DisputeSummaryDto(
            d.Id, d.Reference, d.TransactionId, d.CustomerId,
            caller.IsInRole("CUSTOMER") ? null : d.Customer.FullName,
            d.Status, d.Category, d.Priority, d.CreatedAt, d.UpdatedAt))
        .ToListAsync(ct);

    return new PagedResult<DisputeSummaryDto> { Items = items, Total = total, Page = q.Page, PageSize = q.PageSize };
}
```

Valid filter values are validated against the SPEC §3.2 enumerations (`status` ∈ {OPEN, UNDER_REVIEW, RESOLVED, CLASSIFICATION_FAILED}; `priority` ∈ {LOW, MEDIUM, HIGH, CRITICAL}; `category` ∈ {UNAUTHORISED, DUPLICATE_CHARGE, MERCHANT_ERROR, WRONG_AMOUNT, OTHER}); an unknown value returns `400`.

### 2.4 Detail endpoint with timeline

```csharp
[HttpGet("{id:guid}")]
public async Task<ActionResult<DisputeDetailDto>> GetById(Guid id, CancellationToken ct)
{
    var detail = await _service.GetDetailAsync(User, id, ct);
    return detail is null ? NotFound() : Ok(detail);
}
```

`GetDetailAsync` loads the dispute with its `Transaction`, optional `Resolution`, and `DisputeEvents` ordered by `created_at ASC` (Submitted first, per Journey 3). A customer requesting a dispute that is not theirs gets `404` (no existence leak); ops may view any.

### 2.5 Status update (ops only)

`PATCH /disputes/{id}/status` transitions a dispute's `status` and appends a `DisputeEvent`. Allowed transitions are validated to prevent illegal jumps (e.g. cannot move a `RESOLVED` dispute back to `OPEN`; `RESOLVED` is only reachable via the resolve endpoint in TDP-DISP-03, not via this PATCH):

```
OPEN            -> UNDER_REVIEW
UNDER_REVIEW    -> OPEN            (re-open for more info)
CLASSIFICATION_FAILED -> OPEN | UNDER_REVIEW   (manual triage recovery, per AC-AI-02)
```

Any other target (including `-> RESOLVED`) via this endpoint returns `409 Conflict`. On a valid transition:

```csharp
dispute.Status = req.Status;
dispute.UpdatedAt = DateTimeOffset.UtcNow;
_db.DisputeEvents.Add(new DisputeEvent {
    Id = Guid.NewGuid(), DisputeId = dispute.Id,
    EventType = req.Status,                       // e.g. "UNDER_REVIEW"
    ActorId = User.GetUserId(),
    Description = $"Status changed to {req.Status} by ops.",
    CreatedAt = DateTimeOffset.UtcNow });
await _db.SaveChangesAsync(ct);
```

Moving to `UNDER_REVIEW` also sets `assigned_to_id` to the acting analyst if currently null (supports OPS-03 "assign").

### 2.6 Example detail response

```json
{
  "id": "8a1b...",
  "reference": "DSP-20260714-00042",
  "status": "UNDER_REVIEW",
  "category": "DUPLICATE_CHARGE",
  "priority": "HIGH",
  "customerDescription": "I was charged R450 twice at Shoprite...",
  "extractedFields": { "merchantName": "Shoprite", "amount": 450.00 },
  "assignedToId": "d4e5...",
  "transaction": { "reference": "TXN-20260714-00001", "merchantName": "Shoprite Sea Point", "amount": 450.00, "currency": "ZAR", "status": "SETTLED" },
  "resolution": null,
  "timeline": [
    { "eventType": "SUBMITTED", "description": "Dispute submitted by customer.", "createdAt": "2026-07-14T09:31:22Z" },
    { "eventType": "CLASSIFIED", "description": "Auto-classified as DUPLICATE_CHARGE / HIGH.", "createdAt": "2026-07-14T09:31:25Z" },
    { "eventType": "UNDER_REVIEW", "actorName": "Sipho M.", "description": "Status changed to UNDER_REVIEW by ops.", "createdAt": "2026-07-14T10:02:00Z" }
  ]
}
```

**Note:** `ResolutionDto` intentionally omits `internal_notes` — those are ops-only per SPEC §3.2 (the customer sees only `customer_summary`). For an ops caller the internal notes may be included via a role-gated variant if needed by TDP-FE-05; the customer projection must never include them.

## 3. Acceptance Criteria

- `GET /disputes` returns the caller's own disputes when role is `CUSTOMER`, and all disputes when role is `OPS_ANALYST`/`OPS_MANAGER` (OPS-01/02, TRACK-01).
- Ops list is sorted by priority descending (CRITICAL → HIGH → MEDIUM → LOW → unclassified), then newest first (OPS-01).
- `status`, `priority`, and `category` filters work and combine; invalid enum values return `400` (OPS-02).
- `GET /disputes/{id}` returns dispute detail with the original transaction, resolution (if resolved), and the event timeline in chronological order (OPS-03, TRACK-02).
- A customer requesting a dispute they do not own receives `404`; ops may view any dispute.
- `PATCH /disputes/{id}/status` is restricted to ops roles (`403` for customers); illegal transitions (including any move to `RESOLVED`) return `409`; a valid transition updates `status`, appends a matching `DisputeEvent`, and returns the updated summary.
- Customer-visible responses never expose `internal_notes`.
- Pagination envelope matches SPEC §3.3 (`items`, `total`, `page`, `pageSize`).

## 4. Technical Notes

- **Priority ordering:** because `priority` is a `VARCHAR` enum, ordering needs a `CASE` ordinal (shown above) — do not order alphabetically. Consider a computed/persisted ordinal column later if performance demands, but the `CASE` translates to SQL fine at demo volume.
- **N+1 avoidance:** project directly to DTOs in the query (`.Select(...)`) rather than materialising entities then mapping; for detail, `Include(d => d.Transaction)`, `Include(d => d.Resolution)`, `Include(d => d.DisputeEvents)` (or explicit projections) in one round-trip.
- **Timeline source of truth:** the `DisputeEvent` table is the audit log; `SUBMITTED` is written by TDP-DISP-01, `CLASSIFIED` by TDP-AI-02, `RESOLVED` by TDP-DISP-03, and `UNDER_REVIEW`/`ASSIGNED` by this endpoint. Order strictly by `created_at ASC`.
- **Role checks:** rely on `[Authorize(Roles = ...)]` for coarse gating and the query-level owner filter for row-level security; never trust a client-supplied customer id.
- **Idempotent PATCH:** setting the same status again is a no-op returning `200` (do not append a duplicate event) — or `409` if you prefer strictness; document the chosen behaviour in Swagger.
- **Performance:** list within P95 < 300ms (SPEC §3.6); index `(status, priority)` and `(customer_id, created_at)` to back the common filters/orderings.

## 5. Definition of Done

- [ ] `GET /disputes`, `GET /disputes/{id}`, and `PATCH /disputes/{id}/status` implemented, DI-registered, and behind correct `[Authorize]` policies.
- [ ] Role-aware scoping verified: customer sees only own disputes; ops see all; cross-customer detail returns `404`.
- [ ] Priority-descending ordering and status/priority/category filters working with `400` on invalid enums.
- [ ] Detail returns transaction + timeline (chronological) + resolution when present; internal notes excluded from customer view.
- [ ] Status transition validation enforced; illegal/`RESOLVED` targets rejected with `409`; valid transition appends timeline event and assigns analyst.
- [ ] Integration tests (Testcontainers) cover role scoping, filters, ordering, timeline chronology, and status-transition rules.
- [ ] Endpoints documented in Swagger.
- [ ] Reviewed and merged to `main`.
