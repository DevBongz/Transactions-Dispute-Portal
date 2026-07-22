namespace DisputePortal.Api.Domain;

public sealed class Resolution
{
    public Guid Id { get; set; }
    public Guid DisputeId { get; set; }                      // UNIQUE — one per dispute
    public ResolutionOutcome Outcome { get; set; }
    public string InternalNotes { get; set; } = default!;
    public string? CustomerSummary { get; set; }             // AI-generated
    public Guid ResolvedById { get; set; }
    public DateTimeOffset ResolvedAt { get; set; }

    public Dispute Dispute { get; set; } = default!;
    public User ResolvedBy { get; set; } = default!;
}
