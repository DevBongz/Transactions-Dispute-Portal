using System.Text.Json.Serialization;

namespace DisputePortal.Api.Messaging.Events;

/// <summary>
/// Published to <c>dispute.resolved</c> when an ops analyst closes a dispute
/// (TDP-KAFKA-01 §2.3, SPEC §3.4). <c>customerSummaryProvided</c> flags whether a
/// plain-language summary was attached for the customer.
/// </summary>
public sealed record DisputeResolvedEvent(
    [property: JsonPropertyOrder(2)] Guid DisputeId,
    [property: JsonPropertyOrder(3)] string Reference,
    [property: JsonPropertyOrder(4)] string Outcome,
    [property: JsonPropertyOrder(5)] Guid ResolvedById,
    [property: JsonPropertyOrder(6)] bool CustomerSummaryProvided) : IDomainEvent
{
    [JsonPropertyOrder(0)] public Guid EventId { get; } = Guid.NewGuid();
    [JsonPropertyOrder(1)] public DateTimeOffset OccurredAt { get; } = DateTimeOffset.UtcNow;
    [JsonIgnore] public string Topic => "dispute.resolved";
    [JsonIgnore] public string PartitionKey => DisputeId.ToString();
}
