namespace DisputePortal.Api.Common;

/// <summary>
/// Standard pagination envelope returned by list endpoints (TDP-TXN-01 §2.3,
/// SPEC §3.3): <c>{ items, total, page, pageSize }</c>.
/// </summary>
public sealed class PagedResult<T>
{
    public IReadOnlyList<T> Items { get; init; } = Array.Empty<T>();
    public int Total { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
}
