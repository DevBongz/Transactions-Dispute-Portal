# TDP-DISP-01 — Dispute Submission API & Reference Generator

**Jira summary:** Implement `POST /api/v1/disputes` so a customer can raise a dispute against one of their transactions, receiving a unique human-readable reference in the format `DSP-YYYYMMDD-NNNNN`. The service validates transaction ownership, guards against duplicate disputes on the same transaction, persists the `Dispute` in `OPEN` status with a `SUBMITTED` `DisputeEvent`, and publishes a `dispute.submitted` Kafka event within 1 second of returning HTTP 201 (AC-DISP-04). This is the trigger point for downstream AI classification (TDP-AI-02).

## 1. Context & Motivation

- **Background:** Transactions are viewable (TDP-TXN-01) and the Kafka producer exists (TDP-KAFKA-01), but customers cannot yet raise disputes. Journey 1 (SPEC §2.2) culminates in submission returning a reference like `DSP-20260714-00042` and emitting a `dispute.submitted` event that kicks off asynchronous classification.
- **Business Impact:** This is the core customer action of the whole portal (SPEC §1.1: "Customers can self-serve dispute submissions", success metric ≥ 95%). The reference number is the customer's tracking handle (DISP-04) and the Kafka event is the seam that lets classification hit the "within 5 seconds" objective. A missing or slow event breaks the pre-triage promise to ops.
- **User Story:** As a customer (Maya), I want to raise a dispute against a transaction and immediately receive a reference number so that my case is recorded and I can track it.
- **Dependencies:** TDP-DATA-02 (schema + seed), TDP-AUTH-01 (JWT identity), TDP-KAFKA-01 (`IEventPublisher` + `DisputeSubmittedEvent`). Blocks TDP-DISP-02, TDP-DISP-03, TDP-AI-02, TDP-FE-03. Milestone: **Day 2** (SPEC §4.1).

## 2. Detailed Description

### 2.1 Directory layout

```
src/DisputePortal.Api/
  Controllers/DisputesController.cs        # POST /disputes (this ticket); other verbs in TDP-DISP-02/03
  Services/IDisputeService.cs
  Services/DisputeService.cs
  Services/DisputeReferenceGenerator.cs
  Repositories/IDisputeRepository.cs
  Repositories/DisputeRepository.cs
  Contracts/Disputes/
    SubmitDisputeRequest.cs
    SubmitDisputeResponse.cs
```

### 2.2 Endpoint (SPEC §3.3)

`POST /api/v1/disputes` — `[Authorize(Roles = "CUSTOMER")]`

Request body:

```json
{
  "transactionId": "b2c3d4e5-...",
  "category": null,
  "description": "I was charged R450 twice at Shoprite on 14 July but I only shopped once.",
  "extractedFields": {
    "merchantName": "Shoprite",
    "amount": 450.00,
    "transactionDate": "2026-07-14",
    "confidence": { "merchantName": 0.94, "amount": 0.88 }
  }
}
```

Response `201 Created` (with `Location: /api/v1/disputes/{id}`):

```json
{ "id": "8a1b...", "reference": "DSP-20260714-00042", "status": "OPEN" }
```

### 2.3 DTOs

```csharp
public sealed record SubmitDisputeRequest(
    Guid TransactionId,
    string? Category,                    // usually null at submit; set later by classification
    [property: Required, MinLength(10)] string Description,
    JsonElement? ExtractedFields);       // stored verbatim into dispute.extracted_fields_json (JSONB)

public sealed record SubmitDisputeResponse(Guid Id, string Reference, string Status);
```

`Category`, if supplied, must be one of `UNAUTHORISED, DUPLICATE_CHARGE, MERCHANT_ERROR, WRONG_AMOUNT, OTHER` (SPEC §3.2); otherwise `400`. Normally it is left null and populated by TDP-AI-02.

### 2.4 Reference generator — `DSP-YYYYMMDD-NNNNN`

`NNNNN` is a zero-padded, per-day monotonic sequence (5 digits). The format matches SPEC §3.2 (`reference VARCHAR(30) UNIQUE`) and the example `DSP-20260714-00042`.

```csharp
public interface IDisputeReferenceGenerator
{
    Task<string> GenerateAsync(DateOnly date, CancellationToken ct);
}

public sealed class DisputeReferenceGenerator : IDisputeReferenceGenerator
{
    private readonly IDisputeRepository _repo;

    public async Task<string> GenerateAsync(DateOnly date, CancellationToken ct)
    {
        // Count existing disputes created on this date to derive the next sequence.
        var datePrefix = $"DSP-{date:yyyyMMdd}-";
        var nextSeq = await _repo.CountByReferencePrefixAsync(datePrefix, ct) + 1;
        return $"{datePrefix}{nextSeq:D5}";   // e.g. DSP-20260714-00042
    }
}
```

To make the sequence race-safe under concurrent submissions, generation and insert happen inside a single transaction, and the `disputes.reference` UNIQUE constraint is the backstop: on a `23505` unique-violation the service retries generation up to 3 times (see §2.6).

### 2.5 Service — validation, ownership, duplicate guard, persistence, event

```csharp
public async Task<SubmitDisputeResponse> SubmitDisputeAsync(
    Guid customerId, SubmitDisputeRequest req, CancellationToken ct)
{
    // 1. Ownership + existence: transaction must exist AND belong to caller.
    var txn = await _txnRepo.GetOwnedAsync(customerId, req.TransactionId, ct)
        ?? throw new NotFoundException("Transaction not found.");

    // 2. Duplicate-dispute guard: a transaction has zero-or-one dispute (SPEC §3.2).
    if (await _disputeRepo.ExistsForTransactionAsync(req.TransactionId, ct))
        throw new ConflictException("A dispute already exists for this transaction.");

    await using var tx = await _db.Database.BeginTransactionAsync(ct);

    var now = DateTimeOffset.UtcNow;
    var reference = await _refGen.GenerateAsync(DateOnly.FromDateTime(now.UtcDateTime), ct);

    var dispute = new Dispute
    {
        Id = Guid.NewGuid(),
        Reference = reference,
        TransactionId = req.TransactionId,
        CustomerId = customerId,
        Status = "OPEN",
        Category = req.Category,               // usually null
        Priority = null,
        CustomerDescription = req.Description,
        ExtractedFieldsJson = req.ExtractedFields?.GetRawText(),
        CreatedAt = now,
        UpdatedAt = now
    };
    _db.Disputes.Add(dispute);

    _db.DisputeEvents.Add(new DisputeEvent
    {
        Id = Guid.NewGuid(),
        DisputeId = dispute.Id,
        EventType = "SUBMITTED",
        ActorId = customerId,
        Description = $"Dispute {reference} submitted by customer.",
        CreatedAt = now
    });

    await _db.SaveChangesAsync(ct);
    await tx.CommitAsync(ct);

    // 3. Publish AFTER commit so the event never references an uncommitted row.
    await _publisher.PublishAsync(new DisputeSubmittedEvent(
        dispute.Id, dispute.Reference, dispute.TransactionId,
        dispute.CustomerId, category: null, dispute.CustomerDescription), ct);

    return new SubmitDisputeResponse(dispute.Id, dispute.Reference, "OPEN");
}
```

### 2.6 Controller

```csharp
[HttpPost]
[Authorize(Roles = "CUSTOMER")]
public async Task<ActionResult<SubmitDisputeResponse>> Submit(
    [FromBody] SubmitDisputeRequest req, CancellationToken ct)
{
    var customerId = User.GetUserId();
    var result = await _service.SubmitDisputeAsync(customerId, req, ct);
    return CreatedAtAction("GetById", new { id = result.Id }, result);   // GetById lives in TDP-DISP-02
}
```

Exception → HTTP mapping (via a shared exception-handling middleware): `NotFoundException` → `404`, `ConflictException` → `409`, validation → `400`.

### 2.7 Event ordering & the 1-second guarantee

The `dispute.submitted` publish is awaited on the request path immediately after commit, so by the time `201` is serialised the event has a delivery report (topic/partition/offset logged by TDP-KAFKA-01). This satisfies AC-DISP-04's "within 1 second of HTTP 201". If the publish throws, the dispute is already persisted; the error is logged and surfaced — a compensating reconcile is out of scope for this milestone, but the row is never lost.

## 3. Acceptance Criteria

- `POST /disputes` returns `201` with a body `{ id, reference, status: "OPEN" }` and a `Location` header (AC-DISP-04, SPEC §3.3).
- The `reference` matches the regex `^DSP-\d{8}-\d{5}$` and is unique (AC-DISP-04).
- A `dispute.submitted` Kafka event is published within 1 second of the `201` response, carrying the fields defined in SPEC §3.4 with `category: null` (AC-DISP-04).
- A `DisputeEvent` of type `SUBMITTED` is written with the customer as actor.
- Submitting a dispute for a transaction the caller does not own returns `404`.
- Submitting a second dispute for a transaction that already has one returns `409` (duplicate-dispute guard).
- Only `CUSTOMER`-role callers may submit; ops roles receive `403`; unauthenticated `401`.
- `description` is required (min length 10); an out-of-enum `category` yields `400`.
- New disputes are created in `OPEN` status with `category`/`priority` null until classification (SPEC §3.2).

## 4. Technical Notes

- **Concurrency on the reference sequence:** the count-then-format approach can collide under simultaneous same-day submissions. Mitigations: (a) wrap generate+insert in one transaction; (b) rely on the `reference` UNIQUE index and retry generation up to 3 times on Postgres `23505`; (c) optionally use a Postgres sequence or advisory lock keyed on the date for stricter guarantees. Retry-on-unique is sufficient for the expected demo volume.
- **Publish after commit:** never publish inside the DB transaction — a rollback would leave a phantom event. Await the publish only after `CommitAsync`.
- **`extractedFields` storage:** store the raw JSON (`JsonElement.GetRawText()`) into the `extracted_fields_json` JSONB column; do not reshape it. It is the AI-extracted payload from TDP-AI-01 that the customer confirmed (DISP-03).
- **Idempotency:** the duplicate-dispute guard doubles as basic idempotency for accidental double-clicks; the frontend should also disable the submit button, but the server is authoritative.
- **Performance:** submission (validation + insert + await publish) must stay within the P95 < 300ms non-AI target plus the ≤1s publish budget; keep the transaction short and the event payload minimal.
- **Security:** transaction ownership is validated server-side from the JWT subject, never trusted from the body (SPEC §3.6).

## 5. Definition of Done

- [ ] `DisputesController.Submit`, `DisputeService.SubmitDisputeAsync`, `DisputeReferenceGenerator`, and repository methods implemented and DI-registered.
- [ ] Reference format `DSP-YYYYMMDD-NNNNN` produced and enforced unique; unit test asserts the format and per-day increment.
- [ ] Duplicate-dispute guard and ownership checks covered by unit tests (`DisputeService.SubmitDisputeAsync` happy path + duplicate, per SPEC §4.4).
- [ ] Integration test (`WebApplicationFactory<Program>` + Testcontainers): `POST /api/v1/disputes` → `201`, reference regex, `SUBMITTED` event row, and a `dispute.submitted` message published (fake or embedded `IEventPublisher`).
- [ ] Endpoint documented in Swagger with request/response schemas.
- [ ] Manual verification: full Journey 1 tail — submit dispute, observe reference in response and `dispute.submitted` in Kafka logs within 1s.
- [ ] Reviewed and merged to `main`.
