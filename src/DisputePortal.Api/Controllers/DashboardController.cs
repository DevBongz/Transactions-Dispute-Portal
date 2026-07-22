using DisputePortal.Api.Contracts.Dashboard;
using DisputePortal.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DisputePortal.Api.Controllers;

/// <summary>
/// Ops dashboard metrics (OPS-06, SPEC §3.3). Ops-only; refreshed on each request.
/// </summary>
[ApiController]
[Route("api/v1/dashboard")]
[Authorize(Roles = "OPS_ANALYST,OPS_MANAGER")]
[Produces("application/json")]
public sealed class DashboardController(IDashboardService dashboard) : ControllerBase
{
    /// <summary>Aggregated dispute stats: open count, by-priority, by-category, avg resolution time.</summary>
    [HttpGet("summary")]
    [ProducesResponseType(typeof(DashboardSummaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<DashboardSummaryDto>> GetSummary(CancellationToken ct) =>
        Ok(await dashboard.GetSummaryAsync(ct));
}
