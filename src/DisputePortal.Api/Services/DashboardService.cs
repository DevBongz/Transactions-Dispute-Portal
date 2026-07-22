using DisputePortal.Api.Contracts.Dashboard;
using DisputePortal.Api.Data;
using DisputePortal.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace DisputePortal.Api.Services;

/// <summary>
/// <see cref="IDashboardService"/> implementation (OPS-06 / AC-OPS-06). "Open" means any
/// non-resolved dispute. Priority/category breakdowns are computed over the open set; the
/// average resolution time is the mean of (resolvedAt − createdAt) for resolutions in the
/// last 30 days. Aggregation is done in memory over a minimal projection — the data volume in
/// this system is small and it avoids provider-specific GROUP BY translation concerns.
/// </summary>
public sealed class DashboardService(DisputePortalDbContext db) : IDashboardService
{
    public async Task<DashboardSummaryDto> GetSummaryAsync(CancellationToken ct)
    {
        var open = await db.Disputes.AsNoTracking()
            .Where(d => d.Status != DisputeStatus.RESOLVED)
            .Select(d => new { d.Priority, d.Category })
            .ToListAsync(ct);

        var byPriority = Enum.GetValues<DisputePriority>().ToDictionary(p => p.ToString(), _ => 0);
        var byCategory = Enum.GetValues<DisputeCategory>().ToDictionary(c => c.ToString(), _ => 0);

        foreach (var d in open)
        {
            if (d.Priority is { } p) byPriority[p.ToString()]++;
            if (d.Category is { } c) byCategory[c.ToString()]++;
        }

        var cutoff = DateTimeOffset.UtcNow.AddDays(-30);
        var resolved = await db.Resolutions.AsNoTracking()
            .Where(r => r.ResolvedAt >= cutoff)
            .Select(r => new { r.ResolvedAt, r.Dispute.CreatedAt })
            .ToListAsync(ct);

        var avgHours = resolved.Count == 0
            ? 0.0
            : Math.Round(resolved.Average(r => (r.ResolvedAt - r.CreatedAt).TotalHours), 1);

        return new DashboardSummaryDto(open.Count, byPriority, byCategory, avgHours);
    }
}
