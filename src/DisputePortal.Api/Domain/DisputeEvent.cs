namespace DisputePortal.Api.Domain;

public sealed class DisputeEvent
{
    public Guid Id { get; set; }
    public Guid DisputeId { get; set; }
    public DisputeEventType EventType { get; set; }
    public Guid? ActorId { get; set; }                       // null for system events
    public string Description { get; set; } = default!;
    public DateTimeOffset CreatedAt { get; set; }

    public Dispute Dispute { get; set; } = default!;
    public User? Actor { get; set; }
}
