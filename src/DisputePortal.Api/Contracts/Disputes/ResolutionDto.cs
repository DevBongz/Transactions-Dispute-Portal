namespace DisputePortal.Api.Contracts.Disputes;

/// <summary>
/// Resolution projection shown on dispute detail (TDP-DISP-02 §2.2). Deliberately omits
/// <c>internal_notes</c> — those are ops-only (SPEC §3.2); the customer sees only the
/// plain-language <paramref name="CustomerSummary"/>.
/// </summary>
public sealed record ResolutionDto(
    string Outcome,
    string? CustomerSummary,
    Guid ResolvedById,
    DateTimeOffset ResolvedAt);
