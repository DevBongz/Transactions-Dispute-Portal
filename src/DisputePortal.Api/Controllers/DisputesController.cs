using DisputePortal.Api.Common;
using DisputePortal.Api.Contracts.Disputes;
using DisputePortal.Api.Infrastructure.Auth;
using DisputePortal.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DisputePortal.Api.Controllers;

/// <summary>
/// Dispute lifecycle endpoints (TDP-DISP-01/02/03, SPEC §3.3). Submission is
/// customer-only; listing/detail are role-aware (customers scoped to their own,
/// ops see all); status transitions and resolution are ops-only. Every action
/// requires a valid JWT (global fallback policy).
/// </summary>
[ApiController]
[Route("api/v1/disputes")]
[Authorize]
[Produces("application/json")]
public sealed class DisputesController(IDisputeService service) : ControllerBase
{
    /// <summary>Submit a dispute against one of the caller's transactions (customer only).</summary>
    [HttpPost]
    [Authorize(Roles = "CUSTOMER")]
    [ProducesResponseType(typeof(SubmitDisputeResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SubmitDisputeResponse>> Submit(
        [FromBody] SubmitDisputeRequest req, CancellationToken ct)
    {
        var customerId = User.GetUserId();
        var result = await service.SubmitDisputeAsync(customerId, req, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    /// <summary>List disputes — the caller's own (customer) or all (ops) — filtered and paginated.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<DisputeSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<PagedResult<DisputeSummaryDto>>> List(
        [FromQuery] DisputeQuery query, CancellationToken ct)
    {
        var result = await service.ListAsync(User, query, ct);
        return Ok(result);
    }

    /// <summary>Get dispute detail with the chronological event timeline; 404 if not visible.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(DisputeDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DisputeDetailDto>> GetById(Guid id, CancellationToken ct)
    {
        var detail = await service.GetDetailAsync(User, id, ct);
        return detail is null ? NotFound() : Ok(detail);
    }

    /// <summary>Transition a dispute's status (ops only). Illegal transitions return 409.</summary>
    [HttpPatch("{id:guid}/status")]
    [Authorize(Roles = "OPS_ANALYST,OPS_MANAGER")]
    [ProducesResponseType(typeof(DisputeSummaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<DisputeSummaryDto>> UpdateStatus(
        Guid id, [FromBody] UpdateStatusRequest req, CancellationToken ct)
    {
        var opsUserId = User.GetUserId();
        var result = await service.UpdateStatusAsync(opsUserId, id, req, ct);
        return Ok(result);
    }

    /// <summary>Resolve a dispute with an outcome and internal notes (ops only).</summary>
    [HttpPost("{id:guid}/resolve")]
    [Authorize(Roles = "OPS_ANALYST,OPS_MANAGER")]
    [ProducesResponseType(typeof(ResolutionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ResolutionResponse>> Resolve(
        Guid id, [FromBody] ResolveDisputeRequest req, CancellationToken ct)
    {
        var opsUserId = User.GetUserId();
        var result = await service.ResolveAsync(opsUserId, id, req, ct);
        return Ok(result);
    }
}
