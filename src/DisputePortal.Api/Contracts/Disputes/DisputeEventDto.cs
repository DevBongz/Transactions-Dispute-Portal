namespace DisputePortal.Api.Contracts.Disputes;

/// <summary>A single audit-log entry in a dispute's timeline (TDP-DISP-02 §2.2).</summary>
public sealed record DisputeEventDto(
    string EventType,
    string? Description,
    Guid? ActorId,
    string? ActorName,
    DateTimeOffset CreatedAt);
