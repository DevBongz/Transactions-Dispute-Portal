using System.Text.Json.Serialization;

namespace DisputePortal.Api.Messaging.Events;

/// <summary>
/// Published to <c>dispute.submitted</c> the moment a dispute row is committed
/// (TDP-KAFKA-01 §2.3, SPEC §3.4). <c>category</c> is always <c>null</c> on
/// submission (classification happens asynchronously downstream) and is written
/// explicitly. <c>JsonPropertyOrder</c> pins the serialised field order to the SPEC.
/// </summary>
public sealed record DisputeSubmittedEvent(
    [property: JsonPropertyOrder(2)] Guid DisputeId,
    [property: JsonPropertyOrder(3)] string Reference,
    [property: JsonPropertyOrder(4)] Guid TransactionId,
    [property: JsonPropertyOrder(5)] Guid CustomerId,
    [property: JsonPropertyOrder(6)] string? Category,
    [property: JsonPropertyOrder(7)] string Description) : IDomainEvent
{
    [JsonPropertyOrder(0)] public Guid EventId { get; } = Guid.NewGuid();
    [JsonPropertyOrder(1)] public DateTimeOffset OccurredAt { get; } = DateTimeOffset.UtcNow;
    [JsonIgnore] public string Topic => "dispute.submitted";
    [JsonIgnore] public string PartitionKey => DisputeId.ToString();
}
