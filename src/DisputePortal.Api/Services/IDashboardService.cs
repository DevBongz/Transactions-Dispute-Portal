using DisputePortal.Api.Contracts.Dashboard;

namespace DisputePortal.Api.Services;

/// <summary>
/// Computes the ops dashboard summary (OPS-06). Read-only aggregate over the dispute /
/// resolution tables; refreshed per request (no real-time push, AC-OPS-06).
/// </summary>
public interface IDashboardService
{
    Task<DashboardSummaryDto> GetSummaryAsync(CancellationToken ct);
}
