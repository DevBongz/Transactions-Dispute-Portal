using System.Text.Json.Serialization;

namespace DisputePortal.Api.Messaging.Events;

/// <summary>
/// Published to <c>dispute.classified</c> once AI triage assigns a category and
/// priority (TDP-KAFKA-01 §2.3, SPEC §3.4). Produced from the background consumer
/// (TDP-AI-02), not the request path. <c>classifiedBy</c> records the model id.
/// </summary>
public sealed record DisputeClassifiedEvent(
    [property: JsonPropertyOrder(2)] Guid DisputeId,
    [property: JsonPropertyOrder(3)] string Reference,
    [property: JsonPropertyOrder(4)] string Category,
    [property: JsonPropertyOrder(5)] string Priority,
    [property: JsonPropertyOrder(6)] string ClassifiedBy) : IDomainEvent
{
    [JsonPropertyOrder(0)] public Guid EventId { get; } = Guid.NewGuid();
    [JsonPropertyOrder(1)] public DateTimeOffset OccurredAt { get; } = DateTimeOffset.UtcNow;
    [JsonIgnore] public string Topic => "dispute.classified";
    [JsonIgnore] public string PartitionKey => DisputeId.ToString();
}
