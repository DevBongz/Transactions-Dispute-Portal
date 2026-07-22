using DisputePortal.Api.Common;
using DisputePortal.Api.Contracts.Transactions;
using DisputePortal.Api.Infrastructure.Auth;
using DisputePortal.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DisputePortal.Api.Controllers;

/// <summary>
/// Customer-facing transaction endpoints (TDP-TXN-01, SPEC §3.3). Every action
/// requires a valid JWT (global fallback policy) and is scoped to the caller's own
/// transactions — a foreign or unknown id returns 404 (no existence enumeration).
/// </summary>
[ApiController]
[Route("api/v1/transactions")]
[Authorize]
[Produces("application/json")]
public sealed class TransactionsController(ITransactionService service) : ControllerBase
{
    /// <summary>List the caller's transactions, paginated and optionally filtered.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<TransactionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<PagedResult<TransactionDto>>> List(
        [FromQuery] TransactionQuery query, CancellationToken ct)
    {
        if (query.From is { } from && query.To is { } to && from > to)
        {
            ModelState.AddModelError(nameof(query.From), "'from' must not be after 'to'.");
            return ValidationProblem(ModelState);
        }

        var customerId = User.GetUserId();
        var result = await service.ListAsync(customerId, query, ct);
        return Ok(result);
    }

    /// <summary>Get a single transaction the caller owns, or 404.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(TransactionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TransactionDto>> GetById(Guid id, CancellationToken ct)
    {
        var customerId = User.GetUserId();
        var dto = await service.GetByIdAsync(customerId, id, ct);
        return dto is null ? NotFound() : Ok(dto);
    }
}
