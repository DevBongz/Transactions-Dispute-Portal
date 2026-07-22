namespace DisputePortal.Api.Contracts.Disputes;

/// <summary>Body for <c>200 OK</c> from <c>POST /disputes/{id}/resolve</c> (TDP-DISP-03 §2.2).</summary>
public sealed record ResolutionResponse(
    Guid Id,
    Guid DisputeId,
    string Outcome,
    string? CustomerSummary,
    Guid ResolvedById,
    DateTimeOffset ResolvedAt);
