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

    // Consumer settings for the dispute-classification background service (TDP-AI-02 §2.2).
    // A fixed GroupId lets multiple API instances share the partitions safely (SPEC §3.6
    // Scalability). Auto-commit is off — the consumer commits only after a message is fully
    // handled (at-least-once).
    public string ConsumerGroupId { get; init; } = "dispute-classification";
    public string AutoOffsetReset { get; init; } = "Earliest";

    // Set false to disable the classification consumer (e.g. in integration tests that do not
    // run a broker); the producer/topics wiring is unaffected.
    public bool EnableClassificationConsumer { get; init; } = true;
}

/// <summary>Topic names for the three dispute lifecycle events (SPEC §3.4).</summary>
public sealed class KafkaTopics
{
    public string DisputeSubmitted { get; init; } = "dispute.submitted";
    public string DisputeClassified { get; init; } = "dispute.classified";
    public string DisputeResolved { get; init; } = "dispute.resolved";
}
