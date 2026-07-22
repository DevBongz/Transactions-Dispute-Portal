namespace DisputePortal.Api.Messaging.Events;

/// <summary>
/// Envelope contract shared by every dispute lifecycle event (TDP-KAFKA-01 §2.3).
/// <see cref="Topic"/> and <see cref="PartitionKey"/> are routing metadata kept out
/// of the serialised payload (marked <c>[JsonIgnore]</c> on the concrete records);
/// keying by <c>disputeId</c> guarantees per-dispute ordering on a single partition.
/// </summary>
public interface IDomainEvent
{
    Guid EventId { get; }
    DateTimeOffset OccurredAt { get; }
    string Topic { get; }
    string PartitionKey { get; }   // disputeId — guarantees per-dispute ordering
}
