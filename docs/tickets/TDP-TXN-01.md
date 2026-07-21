# TDP-TXN-01 — Transaction Listing & Detail API

**Jira summary:** Implement the customer-facing transaction endpoints — `GET /api/v1/transactions` (paginated, filterable by date range and merchant) and `GET /api/v1/transactions/{id}` — with strict per-customer ownership enforcement, a consistent pagination envelope, and EF Core parameterised queries. These endpoints give customers the account-activity view they need before raising a dispute (SPEC user stories TXN-01/02/03) and are the data source for the transaction UI (TDP-FE-02).

## 1. Context & Motivation

- **Background:** The `transactions` table and seed data exist (TDP-DATA-02) and JWT auth resolves the caller's identity and role (TDP-AUTH-01), but there is no API to read transactions. Journey 1 (SPEC §2.2) begins with the customer landing on a transaction list and drilling into a suspicious charge before disputing it.
- **Business Impact:** Transaction visibility is the entry point to the entire dispute flow — a customer must locate and inspect a transaction (amount, merchant, date, reference, status) before they can dispute it. Poor filtering directly harms the ≥95% dispute-submission-success objective (SPEC §1.1) because customers who cannot find a transaction cannot dispute it.
- **User Story:** As a customer (Maya), I want a paginated, date- and merchant-filterable list of my transactions and a detail view so that I can review my account activity and gather full context before raising a dispute.
- **Dependencies:** TDP-DATA-02 (schema + seed transactions), TDP-AUTH-01 (JWT identity + `[Authorize]`). Consumed by TDP-FE-02, and referenced by TDP-DISP-01 (dispute submission validates the transaction belongs to the caller). Milestone: **Day 2** (SPEC §4.1).

## 2. Detailed Description

### 2.1 Directory layout

```
src/DisputePortal.Api/
  Controllers/TransactionsController.cs
  Services/ITransactionService.cs
  Services/TransactionService.cs
  Repositories/ITransactionRepository.cs
  Repositories/TransactionRepository.cs
  Contracts/Transactions/
    TransactionDto.cs
    TransactionQuery.cs
  Common/PagedResult.cs
```

### 2.2 Endpoints (SPEC §3.3)

| Method | Path | Query params | Response |
|---|---|---|---|
| GET | `/api/v1/transactions` | `page`, `pageSize`, `from`, `to`, `merchant` | `{ items: TransactionDto[], total, page, pageSize }` |
| GET | `/api/v1/transactions/{id}` | — | `TransactionDto` |

Both require a valid JWT (SPEC §3.6 security NFR). Callers only ever see their own transactions.

### 2.3 DTOs & pagination envelope

```csharp
public sealed record TransactionDto(
    Guid Id,
    string Reference,
    string MerchantName,
    string MerchantCategory,
    decimal Amount,
    string Currency,
    DateTimeOffset TransactionDate,
    string Status,
    bool HasDispute);        // true if a Dispute already exists for this txn

public sealed class PagedResult<T>
{
    public IReadOnlyList<T> Items { get; init; } = Array.Empty<T>();
    public int Total { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
}
```

`HasDispute` is a convenience flag so the UI can disable the "Dispute this transaction" action when a dispute already exists (a Transaction has zero-or-one Dispute per SPEC §3.2 relationships), reinforcing the duplicate-dispute guard in TDP-DISP-01.

### 2.4 Query binding & validation

```csharp
public sealed class TransactionQuery
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;      // AC-TXN-01 default
    public DateTimeOffset? From { get; set; }
    public DateTimeOffset? To { get; set; }
    public string? Merchant { get; set; }
}
```

Guard rails: `Page` coerced to `>= 1`; `PageSize` clamped to `1..100` (protects the P95 < 300ms NFR, SPEC §3.6); `from`/`to` parsed as ISO-8601. If `from > to`, return `400` with a validation problem detail.

### 2.5 Controller

```csharp
[ApiController]
[Route("api/v1/transactions")]
[Authorize]
public sealed class TransactionsController : ControllerBase
{
    private readonly ITransactionService _service;
    public TransactionsController(ITransactionService service) => _service = service;

    [HttpGet]
    public async Task<ActionResult<PagedResult<TransactionDto>>> List(
        [FromQuery] TransactionQuery query, CancellationToken ct)
    {
        var customerId = User.GetUserId();          // extension over ClaimTypes.NameIdentifier (TDP-AUTH-01)
        var result = await _service.ListAsync(customerId, query, ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<TransactionDto>> GetById(Guid id, CancellationToken ct)
    {
        var customerId = User.GetUserId();
        var dto = await _service.GetByIdAsync(customerId, id, ct);
        return dto is null ? NotFound() : Ok(dto);
    }
}
```

### 2.6 Repository — ownership + filtering + pagination

Ownership is enforced in the query itself (`WHERE customer_id = @callerId`), never after materialisation — this is the security control, and it also keeps SQL injection off the table via EF Core parameterisation (SPEC §3.6).

```csharp
public async Task<PagedResult<TransactionDto>> ListAsync(
    Guid customerId, TransactionQuery q, CancellationToken ct)
{
    IQueryable<Transaction> query = _db.Transactions
        .AsNoTracking()
        .Where(t => t.CustomerId == customerId);

    if (q.From is { } from) query = query.Where(t => t.TransactionDate >= from);
    if (q.To is { } to)     query = query.Where(t => t.TransactionDate <= to);   // inclusive (AC-TXN-01)
    if (!string.IsNullOrWhiteSpace(q.Merchant))
        query = query.Where(t => EF.Functions.ILike(t.MerchantName, $"%{q.Merchant}%"));

    var total = await query.CountAsync(ct);

    var items = await query
        .OrderByDescending(t => t.TransactionDate)
        .Skip((q.Page - 1) * q.PageSize)
        .Take(q.PageSize)
        .Select(t => new TransactionDto(
            t.Id, t.Reference, t.MerchantName, t.MerchantCategory,
            t.Amount, t.Currency, t.TransactionDate, t.Status,
            _db.Disputes.Any(d => d.TransactionId == t.Id)))
        .ToListAsync(ct);

    return new PagedResult<TransactionDto> { Items = items, Total = total, Page = q.Page, PageSize = q.PageSize };
}
```

`GetByIdAsync` applies the same ownership filter:

```csharp
public Task<TransactionDto?> GetByIdAsync(Guid customerId, Guid id, CancellationToken ct) =>
    _db.Transactions.AsNoTracking()
       .Where(t => t.Id == id && t.CustomerId == customerId)
       .Select(t => new TransactionDto(/* ... */))
       .FirstOrDefaultAsync(ct);
```

A transaction that exists but belongs to another customer returns `404` (not `403`) to avoid leaking existence — consistent with the no-enumeration stance in AC-AUTH-01.

### 2.7 Example request/response

`GET /api/v1/transactions?page=1&pageSize=20&from=2026-07-01&to=2026-07-14&merchant=shoprite`

```json
{
  "items": [
    {
      "id": "b2c3d4e5-...",
      "reference": "TXN-20260714-00001",
      "merchantName": "Shoprite Sea Point",
      "merchantCategory": "Grocery Stores",
      "amount": 450.00,
      "currency": "ZAR",
      "transactionDate": "2026-07-14T10:22:00Z",
      "status": "SETTLED",
      "hasDispute": false
    }
  ],
  "total": 1,
  "page": 1,
  "pageSize": 20
}
```

## 3. Acceptance Criteria

- `GET /transactions` returns paginated results with a default `pageSize` of 20 (AC-TXN-01).
- Each returned record includes transaction reference, merchant name, amount, currency, date, and status (AC-TXN-01 / TXN-03).
- Filtering by `from`/`to` returns only transactions within the range, **inclusive of the boundary dates** (AC-TXN-01).
- Filtering by `merchant` is case-insensitive and partial-match.
- `page` and `pageSize` are validated (`page >= 1`, `pageSize` clamped `1..100`); `from > to` yields `400`.
- A customer never sees another customer's transactions: list is scoped by `customer_id`; a foreign or non-existent `{id}` returns `404`.
- All endpoints require a valid JWT; an expired/missing token yields `401` (AC-AUTH-01).
- List queries use EF Core parameterised queries only (SPEC §3.6 SQL-injection prevention).

## 4. Technical Notes

- **Inclusive `to` boundary:** because `transaction_date` is `TIMESTAMPTZ` and a bare date parses to midnight, a `to=2026-07-14` filter using `<=` would exclude same-day afternoon transactions. Normalise a date-only `to` to end-of-day (`23:59:59.999` UTC) in the service, or document that clients must pass a full timestamp. Prefer end-of-day normalisation for the customer-friendly behaviour AC-TXN-01 implies.
- **`ILike`:** `EF.Functions.ILike` maps to Postgres `ILIKE`, giving case-insensitive merchant search without pulling rows into memory. Escape `%`/`_` in user input if strictness matters.
- **Total count cost:** the `CountAsync` + paged fetch is two round-trips; acceptable at seed-data volume and within the P95 < 300ms target. An index on `(customer_id, transaction_date DESC)` should back the common ordering (add in TDP-DATA-01/02 if absent).
- **`AsNoTracking`:** all reads are no-tracking for throughput; DTO projection avoids over-fetching columns.
- **Stable ordering:** order by `transaction_date DESC` then `id` as a tiebreaker to keep pagination deterministic when timestamps collide.

## 5. Definition of Done

- [ ] `TransactionsController`, `TransactionService`, `TransactionRepository`, DTOs, and `PagedResult<T>` implemented and DI-registered.
- [ ] Date-range (inclusive) and merchant filters working; pagination envelope matches SPEC §3.3 shape.
- [ ] Ownership enforced at query level; cross-customer access returns `404`; unauthenticated returns `401`.
- [ ] Unit tests for filter/clamp logic and integration tests (`WebApplicationFactory<Program>` + Testcontainers Postgres) covering pagination, date-boundary inclusivity, merchant filter, and ownership isolation (seed two customers).
- [ ] Endpoints appear in Swagger with documented query params and response schema.
- [ ] Manual check against seed data returns the seeded transactions for the seeded customer only.
- [ ] Reviewed and merged to `main`.
