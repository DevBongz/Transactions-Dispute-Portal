using Confluent.Kafka;
using Confluent.Kafka.Admin;
using Microsoft.Extensions.Options;

namespace DisputePortal.Api.Messaging;

/// <summary>
/// Ensures the three dispute topics exist on startup (TDP-KAFKA-01 §2.7). The broker
/// has auto-create enabled, but declaring topics explicitly avoids a first-publish
/// race and a single-partition default. Connection is retried with a short backoff
/// because compose <c>depends_on</c> does not wait for broker readiness; if Kafka is
/// unreachable the API still starts so non-messaging endpoints stay available.
/// </summary>
public sealed class KafkaTopicInitializer : IHostedService
{
    private const int MaxAttempts = 20;
    private static readonly TimeSpan Backoff = TimeSpan.FromSeconds(3);

    private readonly KafkaOptions _opts;
    private readonly ILogger<KafkaTopicInitializer> _logger;

    public KafkaTopicInitializer(IOptions<KafkaOptions> opts, ILogger<KafkaTopicInitializer> logger)
    {
        _opts = opts.Value;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken ct)
    {
        var specs = new[] { _opts.Topics.DisputeSubmitted, _opts.Topics.DisputeClassified, _opts.Topics.DisputeResolved }
            .Select(t => new TopicSpecification { Name = t, NumPartitions = 3, ReplicationFactor = 1 })
            .ToList();

        for (var attempt = 1; attempt <= MaxAttempts && !ct.IsCancellationRequested; attempt++)
        {
            try
            {
                using var admin = new AdminClientBuilder(
                    new AdminClientConfig { BootstrapServers = _opts.BootstrapServers }).Build();

                await admin.CreateTopicsAsync(specs);
                _logger.LogInformation("Kafka topics ensured: {Topics}",
                    string.Join(", ", specs.Select(s => s.Name)));
                return;
            }
            catch (CreateTopicsException ex)
            {
                // Partial success: existing topics are fine, only real errors are logged.
                foreach (var r in ex.Results)
                {
                    if (r.Error.Code == ErrorCode.TopicAlreadyExists)
                        _logger.LogInformation("Topic {Topic} already exists — skipping", r.Topic);
                    else
                        _logger.LogError("Failed to create topic {Topic}: {Reason}", r.Topic, r.Error.Reason);
                }
                return;
            }
            catch (Exception ex) when (attempt < MaxAttempts)
            {
                _logger.LogWarning(ex,
                    "Kafka topic init attempt {Attempt}/{Max} failed (broker not ready?); retrying in {Backoff}s",
                    attempt, MaxAttempts, Backoff.TotalSeconds);
                await Task.Delay(Backoff, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Kafka topic init gave up after {Max} attempts; API will start without ensured topics " +
                    "(broker auto-create remains enabled)", MaxAttempts);
                return;
            }
        }
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}
