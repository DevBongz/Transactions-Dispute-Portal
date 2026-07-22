using Confluent.Kafka;
using Microsoft.Extensions.Options;

namespace DisputePortal.Api.Messaging;

/// <summary>
/// Builds and owns the singleton <see cref="IProducer{TKey,TValue}"/> and wires the
/// messaging services (TDP-KAFKA-01 §2.6). The producer is a singleton because
/// <c>Confluent.Kafka</c> producers are thread-safe and expensive to construct;
/// <c>Acks=All</c> + <c>EnableIdempotence=true</c> gives exactly-once producer
/// semantics per partition.
/// </summary>
public static class KafkaProducerFactory
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
                EnableIdempotence = true   // requires Acks=All; guards against duplicate events on retry
            };

            return new ProducerBuilder<string, string>(producerConfig)
                .SetErrorHandler((_, e) =>
                    logger.LogError("Kafka producer error: {Reason} (fatal={Fatal})", e.Reason, e.IsFatal))
                .SetLogHandler((_, m) =>
                    logger.LogDebug("librdkafka [{Facility}]: {Message}", m.Facility, m.Message))
                .Build();
        });

        services.AddSingleton<IEventPublisher, KafkaEventPublisher>();
        services.AddHostedService<KafkaTopicInitializer>();
        return services;
    }
}
