namespace DisputePortal.Api.Contracts.Transactions;

/// <summary>
/// Query-string binding for <c>GET /transactions</c> (TDP-TXN-01 §2.4). Defaults and
/// clamping (page &gt;= 1, pageSize 1..100) and inclusive date-boundary normalisation
/// are applied in <c>TransactionService</c>; <c>From &gt; To</c> yields a 400.
/// </summary>
public sealed class TransactionQuery
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;      // AC-TXN-01 default
    public DateTimeOffset? From { get; set; }
    public DateTimeOffset? To { get; set; }
    public string? Merchant { get; set; }
}
