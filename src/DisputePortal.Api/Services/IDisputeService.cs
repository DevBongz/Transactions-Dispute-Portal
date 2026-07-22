using System.Security.Claims;
using DisputePortal.Api.Common;
using DisputePortal.Api.Contracts.Disputes;

namespace DisputePortal.Api.Services;

/// <summary>
/// Application service for the dispute domain (TDP-DISP-01/02/03): submission (+reference
/// generation, ownership + duplicate guards, event publish), role-aware listing, detail with
/// timeline, ops status transitions, and resolution. Owns enum validation and orchestration;
/// EF reads are delegated to <see cref="Repositories.IDisputeRepository"/>.
/// </summary>
public interface IDisputeService
{
    Task<SubmitDisputeResponse> SubmitDisputeAsync(Guid customerId, SubmitDisputeRequest req, CancellationToken ct);
    Task<PagedResult<DisputeSummaryDto>> ListAsync(ClaimsPrincipal caller, DisputeQuery query, CancellationToken ct);
    Task<DisputeDetailDto?> GetDetailAsync(ClaimsPrincipal caller, Guid id, CancellationToken ct);
    Task<DisputeSummaryDto> UpdateStatusAsync(Guid opsUserId, Guid id, UpdateStatusRequest req, CancellationToken ct);
    Task<ResolutionResponse> ResolveAsync(Guid opsUserId, Guid id, ResolveDisputeRequest req, CancellationToken ct);
}
