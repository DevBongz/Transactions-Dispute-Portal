# TDP-DISP-03 — Dispute Resolution API

**Jira summary:** Implement `POST /api/v1/disputes/{id}/resolve` (ops-only) so an analyst can formally close a dispute with an outcome (`UPHELD` / `DECLINED` / `PARTIAL`) and internal notes (minimum 20 characters), optionally attaching an AI-generated customer summary. The service creates the one-to-one `Resolution` record, transitions the dispute to `RESOLVED`, appends a `RESOLVED` `DisputeEvent`, and publishes a `dispute.resolved` Kafka event (AC-OPS-04 / AC-AI-03). This completes Journey 2 and delivers the customer-facing resolution communication objective (SPEC §1.1).

## 1. Context & Motivation

- **Background:** Disputes can be submitted, listed, inspected, and moved to `UNDER_REVIEW` (TDP-DISP-01/02), but there is no way to close a case. Journey 2 (SPEC §2.2) ends with the analyst confirming a resolution, the dispute becoming `RESOLVED`, a summary being stored, and `dispute.resolved` being published.
- **Business Impact:** Directly serves OPS-04 ("resolve a dispute with an outcome and internal notes so the case is formally closed") and the SPEC §1.1 objectives "Ops team resolution throughput" and "Customers receive clear resolution communication" (auto-generated summary on every resolved dispute). The `dispute.resolved` event feeds the wider DMC settlement/notification layer.
- **User Story:** As an ops analyst (Sipho), I want to resolve a dispute with an outcome and internal notes, storing a customer-facing summary, so that the case is formally closed and the customer is informed in plain language.
- **Dependencies:** TDP-DISP-01 (dispute exists), TDP-KAFKA-01 (`IEventPublisher` + `DisputeResolvedEvent`), TDP-AUTH-01 (ops roles). The `customerSummary` is typically produced by TDP-AI-03 (`POST /ai/generate-summary`) and passed in here; this endpoint accepts a pre-generated summary and does not itself call Claude. Consumed by TDP-FE-05. Milestone: **Day 2** (SPEC §4.1).

## 2. Detailed Description

### 2.1 Endpoint (SPEC §3.3)

`POST /api/v1/disputes/{id}/resolve` — `[Authorize(Roles = "OPS_ANALYST,OPS_MANAGER")]`

Request body:

```json
{
  "outcome": "UPHELD",
  "internalNotes": "Transaction confirmed as duplicate — refund of R450 initiated via settlement-processor.",
  "customerSummary": "We reviewed your dispute and confirmed you were charged twice for the same purchase at Shoprite. We have refunded R450 to your account. You should see it within 2 business days."
}
```

Response `200 OK`:

```json
{
  "id": "e5f6...",
  "disputeId": "8a1b...",
  "outcome": "UPHELD",
  "customerSummary": "We reviewed your dispute ...",
  "resolvedById": "d4e5...",
  "resolvedAt": "2026-07-14T10:14:33Z"
}
```

### 2.2 DTOs

```csharp
public sealed record ResolveDisputeRequest(
    [property: Required] string Outcome,                       // UPHELD | DECLINED | PARTIAL
    [property: Required, MinLength(20)] string InternalNotes,  // AC-OPS-04: min 20 chars
    string? CustomerSummary);                                  // AI-generated (TDP-AI-03), optional but recommended

public sealed record ResolutionResponse(
    Guid Id, Guid DisputeId, string Outcome, string? CustomerSummary,
    Guid ResolvedById, DateTimeOffset ResolvedAt);
```

`Outcome` is validated against `{ UPHELD, DECLINED, PARTIAL }` (SPEC §3.2); anything else → `400`. `InternalNotes` shorter than 20 chars → `400` (AC-OPS-04).

### 2.3 Service — resolve flow

```csharp
public async Task<ResolutionResponse> ResolveAsync(
    Guid opsUserId, Guid disputeId, ResolveDisputeRequest req, CancellationToken ct)
{
    var dispute = await _db.Disputes
        .Include(d => d.Resolution)
        .FirstOrDefaultAsync(d => d.Id == disputeId, ct)
        ?? throw new NotFoundException("Dispute not found.");

    // Guard: a dispute has zero-or-one resolution (SPEC §3.2, resolution.dispute_id UNIQUE).
    if (dispute.Status == "RESOLVED" || dispute.Resolution is not null)
        throw new ConflictException("Dispute is already resolved.");

    await using var tx = await _db.Database.BeginTransactionAsync(ct);
    var now = DateTimeOffset.UtcNow;

    var resolution = new Resolution
    {
        Id = Guid.NewGuid(),
        DisputeId = dispute.Id,
        Outcome = req.Outcome,
        InternalNotes = req.InternalNotes,
        CustomerSummary = req.CustomerSummary,
        ResolvedById = opsUserId,
        ResolvedAt = now
    };
    _db.Resolutions.Add(resolution);

    dispute.Status = "RESOLVED";
    dispute.UpdatedAt = now;

    _db.DisputeEvents.Add(new DisputeEvent
    {
        Id = Guid.NewGuid(),
        DisputeId = dispute.Id,
        EventType = "RESOLVED",
        ActorId = opsUserId,
        Description = $"Dispute resolved as {req.Outcome}.",
        CreatedAt = now
    });

    await _db.SaveChangesAsync(ct);
    await tx.CommitAsync(ct);

    // Publish after commit (see TDP-DISP-01 rationale).
    await _publisher.PublishAsync(new DisputeResolvedEvent(
        dispute.Id, dispute.Reference, req.Outcome,
        opsUserId, customerSummaryProvided: !string.IsNullOrWhiteSpace(req.CustomerSummary)), ct);

    return new ResolutionResponse(resolution.Id, dispute.Id, resolution.Outcome,
        resolution.CustomerSummary, resolution.ResolvedById, resolution.ResolvedAt);
}
```

### 2.4 Controller

```csharp
[HttpPost("{id:guid}/resolve")]
[Authorize(Roles = "OPS_ANALYST,OPS_MANAGER")]
public async Task<ActionResult<ResolutionResponse>> Resolve(
    Guid id, [FromBody] ResolveDisputeRequest req, CancellationToken ct)
{
    var opsUserId = User.GetUserId();
    var result = await _service.ResolveAsync(opsUserId, id, req, ct);
    return Ok(result);
}
```

Exception mapping: `NotFoundException` → `404`, `ConflictException` (already resolved) → `409`, validation → `400`.

### 2.5 `dispute.resolved` event (SPEC §3.4)

```json
{
  "eventId": "9c0d...",
  "occurredAt": "2026-07-14T10:14:33.220Z",
  "disputeId": "8a1b...",
  "reference": "DSP-20260714-00042",
  "outcome": "UPHELD",
  "resolvedById": "d4e5...",
  "customerSummaryProvided": true
}
```

`customerSummaryProvided` reflects whether a non-empty `customerSummary` was stored, so downstream (notification) consumers know whether customer-facing text is available.

### 2.6 Interaction with resolution detail

Once resolved, `GET /disputes/{id}` (TDP-DISP-02) returns the `ResolutionDto` populated (outcome + `customerSummary`, never `internalNotes` to customers), and the customer sees status `RESOLVED` with the summary panel (TRACK-03, Journey 3). The `internal_notes` remain ops-only.

## 3. Acceptance Criteria

- `POST /disputes/{id}/resolve` accepts an `outcome` ∈ {UPHELD, DECLINED, PARTIAL} and `internalNotes` of at least 20 characters; violations return `400` (AC-OPS-04).
- On success: a `Resolution` row is created (one-to-one with the dispute), the dispute transitions to `RESOLVED`, a `RESOLVED` `DisputeEvent` is appended, and `200` returns the resolution (AC-OPS-04).
- A `dispute.resolved` Kafka event is published on resolution, matching SPEC §3.4 fields including `customerSummaryProvided` (AC-OPS-04 / AC-AI-03).
- The stored `customerSummary` is subsequently visible to the customer on their dispute detail page (TRACK-03); `internalNotes` are never exposed to customers.
- Resolving an already-resolved dispute returns `409` (one-to-one resolution guard).
- Only `OPS_ANALYST`/`OPS_MANAGER` may resolve; customers receive `403`; unauthenticated `401`.
- Resolving a non-existent dispute returns `404`.

## 4. Technical Notes

- **One-to-one guard:** `resolution.dispute_id` is UNIQUE (SPEC §3.2). Check both `dispute.Status == "RESOLVED"` and existing `Resolution` before inserting; the UNIQUE constraint is the backstop against a concurrent double-resolve (catch `23505` → `409`).
- **Publish after commit:** identical rationale to TDP-DISP-01 — never emit `dispute.resolved` before the transaction commits.
- **Summary provenance:** this endpoint stores a summary; it does not generate one. The ops UI calls TDP-AI-03 (`POST /ai/generate-summary`) first, lets the analyst edit (OPS-05, Journey 2 step 8-9), then submits the final text here. Accept a null/empty `customerSummary` (some outcomes may not warrant one) but set `customerSummaryProvided` accordingly.
- **Status precondition:** typically a dispute is `UNDER_REVIEW` before resolution, but resolving directly from `OPEN` is allowed (the analyst may resolve without an explicit review step). Do not require `UNDER_REVIEW`.
- **Note length:** the 20-char minimum (AC-OPS-04) is enforced server-side via `[MinLength(20)]`; the frontend should mirror this but the server is authoritative.
- **Performance:** single short transaction + awaited publish; within P95 < 300ms plus publish budget (SPEC §3.6).
- **Auditability:** `resolved_by_id` + the `RESOLVED` event give a full audit trail of who closed the case and when.

## 5. Definition of Done

- [ ] `DisputesController.Resolve`, `DisputeService.ResolveAsync`, `Resolution` persistence, and DTOs implemented and DI-registered.
- [ ] Outcome enum and 20-char note validation enforced (`400` on violation).
- [ ] Resolution creates the one-to-one row, transitions status to `RESOLVED`, appends the `RESOLVED` event; double-resolve returns `409`.
- [ ] `dispute.resolved` published on success with correct SPEC §3.4 shape (verified in logs); `customerSummaryProvided` reflects the summary presence.
- [ ] Ops-only authorization enforced; customer → `403`.
- [ ] Unit test for `ResolveAsync` (happy path + already-resolved guard) and integration test (`WebApplicationFactory<Program>` + Testcontainers): `POST /api/v1/disputes/{id}/resolve` → resolution persisted, event published (per SPEC §4.4).
- [ ] Resolution surfaces in `GET /disputes/{id}` for the customer (summary only) — verified end-to-end with TDP-DISP-02.
- [ ] Endpoint documented in Swagger.
- [ ] Reviewed and merged to `main`.
