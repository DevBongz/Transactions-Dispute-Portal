namespace DisputePortal.Api.Contracts.Dashboard;

/// <summary>
/// Aggregated dispute statistics for the ops manager dashboard (OPS-06, AC-OPS-06, SPEC §3.3).
/// <paramref name="ByPriority"/> and <paramref name="ByCategory"/> always contain an entry for
/// every enum member (zero when none) so the UI can render a stable grid.
/// <paramref name="AvgResolutionHours"/> is the mean time-to-resolution over the last 30 days.
/// </summary>
public sealed record DashboardSummaryDto(
    int TotalOpen,
    IReadOnlyDictionary<string, int> ByPriority,
    IReadOnlyDictionary<string, int> ByCategory,
    double AvgResolutionHours);
