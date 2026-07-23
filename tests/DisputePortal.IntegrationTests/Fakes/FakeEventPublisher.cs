using DisputePortal.Api.Messaging;
using DisputePortal.Api.Messaging.Events;

namespace DisputePortal.IntegrationTests;

/// <summary>In-memory <see cref="IEventPublisher"/> that records topics for assertions.</summary>
public sealed class FakeEventPublisher : IEventPublisher
{
    private readonly List<(string Topic, IDomainEvent Event)> _published = new();
    private readonly object _gate = new();

    public IReadOnlyList<(string Topic, IDomainEvent Event)> Published
    {
        get { lock (_gate) return _published.ToList(); }
    }

    public Task PublishAsync(IDomainEvent domainEvent, CancellationToken ct = default)
    {
        lock (_gate) _published.Add((domainEvent.Topic, domainEvent));
        return Task.CompletedTask;
    }
}
