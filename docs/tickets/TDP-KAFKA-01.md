# TDP-KAFKA-01 — Kafka Producer & Domain Event Publishing

**Jira summary:** Stand up the Kafka producer infrastructure for the Dispute Portal API: a `Confluent.Kafka`-based producer, a clean `IEventPublisher` abstraction that the dispute services publish through, strongly-typed domain event contracts for `dispute.submitted`, `dispute.classified`, and `dispute.resolved` (matching SPEC §3.4), topic bootstrap/auto-creation on startup, and structured logging of every delivery report (topic/partition/offset). This is the messaging backbone that TDP-DISP-01, TDP-DISP-03, and TDP-AI-02 depend on to move disputes through their lifecycle.

## 1. Context & Motivation

- **Background:** The portal sits at the customer-facing edge of the DMC Fin-Motion Kafka pipeline (`journalengine`, `settlement-processor`, `merchantpayoutservice`, etc.). SPEC §3.1 shows the API publishing dispute lifecycle events to Kafka and a hosted consumer subscribing to `dispute.submitted`. Nothing exists yet to produce those events — the dispute APIs cannot fulfil their acceptance criteria (e.g. AC-DISP-04 requires a `dispute.submitted` event within 1 second of HTTP 201) without a producer.
- **Business Impact:** Event publishing is the integration seam between the synchronous REST surface and the asynchronous AI classification workflow (SPEC §1.1 objective: "100% of submitted disputes carry an AI-assigned category and priority within 5 seconds"). Without reliable, observable production of these events, auto-classification and resolution notification cannot happen, and the portal cannot demonstrate its place in the DMC settlement layer.
- **User Story:** As the system, I want dispute lifecycle changes to be published as durable domain events so that downstream consumers (AI classification, future settlement services) can react asynchronously without coupling to the API's request path.
- **Dependencies:** TDP-INFRA-02 (Docker Compose provides the `kafka` / `zookeeper` services and the `Kafka__BootstrapServers` env var). Consumed by TDP-DISP-01, TDP-DISP-03, TDP-AI-02. Milestone: **Day 2 — Transaction & Dispute APIs** (SPEC §4.1).

## 2. Detailed Description

### 2.1 Package & configuration

Add `Confluent.Kafka` (9.x pinned to `confluentinc/cp-kafka` 7.6.0 broker compatibility) to `src/DisputePortal.Api`.

```xml
<PackageReference Include="Confluent.Kafka" Version="2.5.3" />
```

Configuration binds from `appsettings.json` / environment (Docker Compose sets `Kafka__BootstrapServers=kafka:9092`):

```json
"Kafka": {
  "BootstrapServers": "localhost:9092",
  "ClientId": "dispute-portal-api",
  "Topics": {
    "DisputeSubmitted": "dispute.submitted",
    "DisputeClassified": "dispute.classified",
    "DisputeResolved": "dispute.resolved"
  },
  "ProducerAcks": "All",
  "MessageTimeoutMs": 5000
}
```

Bound to a strongly-typed options class:

```csharp
public sealed class KafkaOptions
{
    public string BootstrapServers { get; init; } = "localhost:9092";
    public string ClientId { get; init; } = "dispute-portal-api";
    public KafkaTopics Topics { get; init; } = new();
    public string ProducerAcks { get; init; } = "All";
    public int MessageTimeoutMs { get; init; } = 5000;
}

public sealed class KafkaTopics
{
    public string DisputeSubmitted { get; init; } = "dispute.submitted";
    public string DisputeClassified { get; init; } = "dispute.classified";
    public string DisputeResolved { get; init; } = "dispute.resolved";
}
```

### 2.2 Directory layout

```
src/DisputePortal.Api/
  Messaging/
    KafkaOptions.cs
    IEventPublisher.cs
    KafkaEventPublisher.cs         # IEventPublisher impl, wraps IProducer<string,string>
    KafkaProducerFactory.cs        # builds & owns the singleton IProducer
    KafkaTopicInitializer.cs       # IHostedService — ensures topics exist on startup
    Events/
      DisputeSubmittedEvent.cs
      DisputeClassifiedEvent.cs
      DisputeResolvedEvent.cs
      IDomainEvent.cs
```

### 2.3 Event contracts (SPEC §3.4)

All events share an envelope: `eventId` (new GUID per publish), `occurredAt` (UTC ISO-8601), and `disputeId` / `reference`. Serialised with `System.Text.Json` using `camelCase` and `null` values written explicitly (SPEC shows `"category": null` on submission).

```csharp
public interface IDomainEvent
{
    Guid EventId { get; }
    DateTimeOffset OccurredAt { get; }
    string Topic { get; }
    string PartitionKey { get; }   // disputeId — guarantees per-dispute ordering
}

public sealed record DisputeSubmittedEvent(
    Guid DisputeId,
    string Reference,
    Guid TransactionId,
    Guid CustomerId,
    string? Category,
    string Description) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTimeOffset OccurredAt { get; } = DateTimeOffset.UtcNow;
    [JsonIgnore] public string Topic => "dispute.submitted";
    [JsonIgnore] public string PartitionKey => DisputeId.ToString();
}

public sealed record DisputeClassifiedEvent(
    Guid DisputeId,
    string Reference,
    string Category,
    string Priority,
    string ClassifiedBy) : IDomainEvent { /* envelope as above; Topic => "dispute.classified" */ }

public sealed record DisputeResolvedEvent(
    Guid DisputeId,
    string Reference,
    string Outcome,
    Guid ResolvedById,
    bool CustomerSummaryProvided) : IDomainEvent { /* Topic => "dispute.resolved" */ }
```

Serialised `dispute.submitted` payload (matches SPEC §3.4 exactly):

```json
{
  "eventId": "3f2504e0-4f89-41d3-9a0c-0305e82c3301",
  "occurredAt": "2026-07-14T09:31:22.104Z",
  "disputeId": "8a1b...",
  "reference": "DSP-20260714-00042",
  "transactionId": "b2c3...",
  "customerId": "c3d4...",
  "category": null,
  "description": "I was charged R450 twice at Shoprite on 14 July but I only shopped once."
}
```

### 2.4 IEventPublisher abstraction

Services depend only on this interface — never on `IProducer` directly — so tests can substitute a fake.

```csharp
public interface IEventPublisher
{
    Task PublishAsync(IDomainEvent domainEvent, CancellationToken ct = default);
}
```

### 2.5 KafkaEventPublisher implementation

The producer is a **singleton** (`Confluent.Kafka` producers are thread-safe and expensive to construct). It serialises the event, uses `disputeId` as the message key (co-partitioning → ordering per dispute), and logs the delivery report.

```csharp
public sealed class KafkaEventPublisher : IEventPublisher
{
    private readonly IProducer<string, string> _producer;
    private readonly ILogger<KafkaEventPublisher> _logger;
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never
    };

    public KafkaEventPublisher(IProducer<string, string> producer, ILogger<KafkaEventPublisher> logger)
    {
        _producer = producer;
        _logger = logger;
    }

    public async Task PublishAsync(IDomainEvent domainEvent, CancellationToken ct = default)
    {
        var payload = JsonSerializer.Serialize((object)domainEvent, Json);
        var message = new Message<string, string>
        {
            Key = domainEvent.PartitionKey,
            Value = payload,
            Headers = new Headers
            {
                { "eventId", Encoding.UTF8.GetBytes(domainEvent.EventId.ToString()) },
                { "eventType", Encoding.UTF8.GetBytes(domainEvent.GetType().Name) }
            }
        };

        try
        {
            var result = await _producer.ProduceAsync(domainEvent.Topic, message, ct);
            _logger.LogInformation(
                "Published {EventType} to {Topic} [partition {Partition}] @ offset {Offset} (eventId {EventId}, dispute {DisputeId})",
                domainEvent.GetType().Name, result.Topic, result.Partition.Value,
                result.Offset.Value, domainEvent.EventId, domainEvent.PartitionKey);
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
```

### 2.6 Producer factory & DI registration

```csharp
public static class KafkaServiceCollectionExtensions
{
    public static IServiceCollection AddKafkaProducer(this IServiceCollection services, IConfiguration config)
    {
        services.Configure<KafkaOptions>(config.GetSection("Kafka"));

        services.AddSingleton<IProducer<string, string>>(sp =>
        {
            var opts = sp.GetRequiredService<IOptions<KafkaOptions>>().Value;
            var logger = sp.GetRequiredService<ILogger<KafkaEventPublisher>>();
            var producerConfig = new ProducerConfig
            {
                BootstrapServers = opts.BootstrapServers,
                ClientId = opts.ClientId,
                Acks = Enum.Parse<Acks>(opts.ProducerAcks, ignoreCase: true),
                MessageTimeoutMs = opts.MessageTimeoutMs,
                EnableIdempotence = true
            };
            return new ProducerBuilder<string, string>(producerConfig)
                .SetErrorHandler((_, e) => logger.LogError("Kafka producer error: {Reason} (fatal={Fatal})", e.Reason, e.IsFatal))
                .SetLogHandler((_, m) => logger.LogDebug("librdkafka [{Facility}]: {Message}", m.Facility, m.Message))
                .Build();
        });

        services.AddSingleton<IEventPublisher, KafkaEventPublisher>();
        services.AddHostedService<KafkaTopicInitializer>();
        return services;
    }
}
```

### 2.7 Topic auto-creation on startup

Although the broker sets `KAFKA_AUTO_CREATE_TOPICS_ENABLE=true` (SPEC §3.1, §4.3), relying on lazy creation causes the first publish to race and can produce a single-partition default. `KafkaTopicInitializer` (an `IHostedService`) uses `AdminClient` to declaratively create the three topics with explicit partition/replication settings, ignoring `TopicAlreadyExists`.

```csharp
public sealed class KafkaTopicInitializer : IHostedService
{
    private readonly KafkaOptions _opts;
    private readonly ILogger<KafkaTopicInitializer> _logger;

    public async Task StartAsync(CancellationToken ct)
    {
        using var admin = new AdminClientBuilder(
            new AdminClientConfig { BootstrapServers = _opts.BootstrapServers }).Build();

        var specs = new[] { _opts.Topics.DisputeSubmitted, _opts.Topics.DisputeClassified, _opts.Topics.DisputeResolved }
            .Select(t => new TopicSpecification { Name = t, NumPartitions = 3, ReplicationFactor = 1 })
            .ToList();

        try
        {
            await admin.CreateTopicsAsync(specs);
            _logger.LogInformation("Kafka topics ensured: {Topics}", string.Join(", ", specs.Select(s => s.Name)));
        }
        catch (CreateTopicsException ex)
        {
            foreach (var r in ex.Results)
            {
                if (r.Error.Code == ErrorCode.TopicAlreadyExists)
                    _logger.LogInformation("Topic {Topic} already exists — skipping", r.Topic);
                else
                    _logger.LogError("Failed to create topic {Topic}: {Reason}", r.Topic, r.Error.Reason);
            }
        }
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}
```

Startup must tolerate Kafka not being immediately reachable (Compose `depends_on` does not wait for readiness): the initializer retries connection with a short backoff (e.g. Polly, 5 attempts × 3s) before giving up and logging a warning, so the API still starts and REST endpoints stay available even if the broker is briefly down.

### 2.8 Usage from services

`DisputeService` (TDP-DISP-01) injects `IEventPublisher` and calls it after the dispute row is committed:

```csharp
await _publisher.PublishAsync(new DisputeSubmittedEvent(
    dispute.Id, dispute.Reference, dispute.TransactionId,
    dispute.CustomerId, category: null, dispute.CustomerDescription), ct);
```

## 3. Acceptance Criteria

- An `IEventPublisher` abstraction exists; no controller/service references `IProducer<,>` directly.
- The producer is registered as a singleton with `Acks=All` and `EnableIdempotence=true`.
- `DisputeSubmittedEvent`, `DisputeClassifiedEvent`, and `DisputeResolvedEvent` serialise to JSON byte-for-byte matching the field sets in SPEC §3.4, including `camelCase` names and an explicit `"category": null` on submission.
- Each event is keyed by `disputeId` so all events for one dispute land on the same partition (ordering guarantee).
- On startup the three topics (`dispute.submitted`, `dispute.classified`, `dispute.resolved`) are created if absent; an existing topic is treated as success (no crash).
- Every successful publish logs topic, partition, and offset at Information level; every failure logs the error reason at Error level and rethrows (SPEC §3.6 observability NFR: "Kafka publish/consume events logged with topic, partition, offset").
- A publish of `dispute.submitted` completes well within the 1-second budget that AC-DISP-04 imposes on TDP-DISP-01.
- If Kafka is unreachable at startup, the API process still starts and serves non-messaging endpoints.

## 4. Technical Notes

- **Version pinning:** `Confluent.Kafka` 2.5.x bundles `librdkafka` compatible with broker 7.6.0. Do not mix major versions.
- **Idempotence:** `EnableIdempotence=true` requires `Acks=All`; the factory enforces this pairing. It gives exactly-once producer semantics per partition, protecting against duplicate submission events on retry.
- **Serialization gotcha:** serialise via `(object)domainEvent` (or the concrete type) so `System.Text.Json` emits derived-record properties; serialising through the `IDomainEvent` static type would only emit interface members. `[JsonIgnore]` on `Topic`/`PartitionKey` keeps routing metadata out of the payload.
- **Flush on shutdown:** the singleton producer is disposed by the DI container; ensure graceful shutdown flushes in-flight messages (`IProducer.Flush(TimeSpan)` in a hosted service `StopAsync`, or rely on `ProduceAsync` awaiting delivery). Since publishes are awaited, there is minimal in-flight risk.
- **Do not block the request thread:** `ProduceAsync` is awaited inside the request path for `dispute.submitted` to honour the 1s guarantee; keep payloads small. `dispute.classified` is published from the background consumer (TDP-AI-02), not the request path.
- **Config keys:** `Kafka__BootstrapServers` is the Docker override; local dev defaults to `localhost:9092`.
- **Security/scalability:** PLAINTEXT listener only (local scope, SPEC §3.1). Consumer `GroupId` is out of scope here (owned by TDP-AI-02) but topic partition count (3) is chosen to allow horizontal consumer scaling per SPEC §3.6.

## 5. Definition of Done

- [ ] `Confluent.Kafka` referenced; `KafkaOptions` binds from configuration/environment.
- [ ] `IEventPublisher` + `KafkaEventPublisher` implemented and registered via `AddKafkaProducer`.
- [ ] Three event records implemented with envelope and correct JSON shape; a serialization unit test asserts the exact field set against SPEC §3.4 fixtures.
- [ ] `KafkaTopicInitializer` hosted service creates topics idempotently with startup retry/backoff.
- [ ] Delivery-report logging (topic/partition/offset) verified in container logs during a manual `POST /api/v1/disputes`.
- [ ] `docker compose up --build` starts the API with Kafka; a submitted dispute produces an observable message on `dispute.submitted` (verified via `kafka-console-consumer` or the classification consumer log).
- [ ] Code reviewed and merged to `main`; no direct `IProducer` usage outside `Messaging/`.
