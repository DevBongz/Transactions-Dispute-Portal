namespace DisputePortal.Api.Messaging;

/// <summary>
/// Strongly-typed Kafka configuration bound from the <c>Kafka</c> config section
/// (TDP-KAFKA-01 §2.1). Compose injects <c>Kafka__BootstrapServers</c> (the
/// broker's internal listener, <c>kafka:29092</c>); local dev defaults to
/// <c>localhost:9092</c>.
/// </summary>
public sealed class KafkaOptions
{
    public string BootstrapServers { get; init; } = "localhost:9092";
    public string ClientId { get; init; } = "dispute-portal-api";
    public KafkaTopics Topics { get; init; } = new();
    public string ProducerAcks { get; init; } = "All";
    public int MessageTimeoutMs { get; init; } = 5000;
}

/// <summary>Topic names for the three dispute lifecycle events (SPEC §3.4).</summary>
public sealed class KafkaTopics
{
    public string DisputeSubmitted { get; init; } = "dispute.submitted";
    public string DisputeClassified { get; init; } = "dispute.classified";
    public string DisputeResolved { get; init; } = "dispute.resolved";
}
