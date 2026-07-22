namespace DisputePortal.Api.Contracts.Disputes;

/// <summary>
/// Query-string binding for <c>GET /disputes</c> (TDP-DISP-02 §2.1). Filter values are
/// validated against the SPEC §3.2 enumerations in the service (unknown value → 400);
/// paging is clamped there too (page ≥ 1, pageSize 1..100).
/// </summary>
public sealed class DisputeQuery
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? Status { get; set; }
    public string? Priority { get; set; }
    public string? Category { get; set; }
}
