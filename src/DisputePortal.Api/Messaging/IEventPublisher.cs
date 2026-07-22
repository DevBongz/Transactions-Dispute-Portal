using DisputePortal.Api.Messaging.Events;

namespace DisputePortal.Api.Messaging;

/// <summary>
/// Abstraction over the Kafka producer (TDP-KAFKA-01 §2.4). Services depend only on
/// this interface — never on <c>IProducer&lt;,&gt;</c> directly — so tests can
/// substitute a fake and no direct producer usage leaks outside <c>Messaging/</c>.
/// </summary>
public interface IEventPublisher
{
    Task PublishAsync(IDomainEvent domainEvent, CancellationToken ct = default);
}
