using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Confluent.Kafka;
using DisputePortal.Api.Messaging.Events;
using DisputePortal.Api.Observability;

namespace DisputePortal.Api.Messaging;

/// <summary>
/// <see cref="IEventPublisher"/> backed by a singleton <see cref="IProducer{TKey,TValue}"/>
/// (TDP-KAFKA-01 §2.5). Serialises the event as camelCase JSON with explicit nulls,
/// keys the message by <c>disputeId</c> (co-partitioning → per-dispute ordering),
/// stamps <c>eventId</c>/<c>eventType</c>/<c>correlationId</c> headers, and logs the
/// delivery report (topic/partition/offset) per SPEC §3.6.
/// </summary>
public sealed class KafkaEventPublisher : IEventPublisher
{
    private readonly IProducer<string, string> _producer;
    private readonly ICorrelationAccessor _correlation;
    private readonly ILogger<KafkaEventPublisher> _logger;

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never
    };

    public KafkaEventPublisher(
        IProducer<string, string> producer,
        ICorrelationAccessor correlation,
        ILogger<KafkaEventPublisher> logger)
    {
        _producer = producer;
        _correlation = correlation;
        _logger = logger;
    }

    public async Task PublishAsync(IDomainEvent domainEvent, CancellationToken ct = default)
    {
        // Serialise via the concrete runtime type so derived-record properties are
        // emitted (serialising through IDomainEvent would only write interface members).
        var payload = JsonSerializer.Serialize(domainEvent, domainEvent.GetType(), Json);
        var correlationId = _correlation.Current ?? "-";

        var message = new Message<string, string>
        {
            Key = domainEvent.PartitionKey,
            Value = payload,
            Headers = new Headers
            {
                { "eventId", Encoding.UTF8.GetBytes(domainEvent.EventId.ToString()) },
                { "eventType", Encoding.UTF8.GetBytes(domainEvent.GetType().Name) },
                { "correlationId", Encoding.UTF8.GetBytes(correlationId) }
            }
        };

        try
        {
            var result = await _producer.ProduceAsync(domainEvent.Topic, message, ct);
            _logger.LogInformation(
                "Published {EventType} to {Topic} [partition {Partition}] @ offset {Offset} (eventId {EventId}, dispute {DisputeId}, correlationId {CorrelationId})",
                domainEvent.GetType().Name, result.Topic, result.Partition.Value,
                result.Offset.Value, domainEvent.EventId, domainEvent.PartitionKey, correlationId);
        }
        catch (ProduceException<string, string> ex)
        {
            _logger.LogError(ex,
                "Failed to publish {EventType} to {Topic}: {Reason}",
                domainEvent.GetType().Name, domainEvent.Topic, ex.Error.Reason);
            throw;
        }
    }
}
