using DisputePortal.Api.Common;
using DisputePortal.Api.Contracts.Transactions;
using DisputePortal.Api.Repositories;

namespace DisputePortal.Api.Services;

/// <summary>
/// <see cref="ITransactionService"/> implementation (TDP-TXN-01 §2.4). Coerces
/// <c>page &gt;= 1</c> and clamps <c>pageSize</c> to 1..100 (protects the P95 &lt; 300ms
/// NFR), and normalises a date-only <c>to</c> to end-of-day so the boundary is
/// inclusive per AC-TXN-01 (a bare date parses to midnight, which with <c>&lt;=</c>
/// would otherwise drop same-day afternoon transactions).
/// <para>
/// Filter dates are treated as UTC calendar dates: the wall-clock components of the
/// bound value are stamped as UTC. This is both a correctness requirement — Npgsql
/// maps <c>timestamptz</c> to UTC and rejects a non-zero offset parameter — and a
/// determinism one: a bare <c>from=2026-07-15</c> means that calendar day regardless
/// of the server's local timezone, and matches the UTC-stored transaction dates.
/// </para>
/// </summary>
public sealed class TransactionService(ITransactionRepository repository) : ITransactionService
{
    private const int MaxPageSize = 100;

    public Task<PagedResult<TransactionDto>> ListAsync(
        Guid customerId, TransactionQuery query, CancellationToken ct) =>
        repository.ListAsync(customerId, Normalise(query), ct);

    public Task<TransactionDto?> GetByIdAsync(Guid customerId, Guid id, CancellationToken ct) =>
        repository.GetByIdAsync(customerId, id, ct);

    private static TransactionQuery Normalise(TransactionQuery q) => new()
    {
        Page = q.Page < 1 ? 1 : q.Page,
        PageSize = Math.Clamp(q.PageSize < 1 ? 20 : q.PageSize, 1, MaxPageSize),
        From = q.From is { } from ? AsUtc(from) : null,
        To = NormaliseTo(q.To),
        Merchant = q.Merchant
    };

    // Reinterpret the value's wall-clock date/time as UTC (offset 0) — see class remarks.
    private static DateTimeOffset AsUtc(DateTimeOffset value) =>
        new(value.DateTime, TimeSpan.Zero);

    // A date-only `to` (time component at midnight) is treated as "through the whole
    // day" — bumped to the last tick of that UTC day so the upper bound stays inclusive.
    // Callers passing a full timestamp keep their exact (UTC-normalised) bound.
    private static DateTimeOffset? NormaliseTo(DateTimeOffset? to)
    {
        if (to is not { } value) return null;
        var utc = AsUtc(value);
        return utc.TimeOfDay == TimeSpan.Zero ? utc.AddDays(1).AddTicks(-1) : utc;
    }
}
