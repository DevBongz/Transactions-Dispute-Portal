using Confluent.Kafka;
using DisputePortal.Api.Messaging;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace DisputePortal.Api.Observability;

/// <summary>
/// Real Kafka readiness probe (TDP-KAFKA-01 / TDP-OBS-01 §2.6). Opens an
/// <see cref="IAdminClient"/> against the configured bootstrap servers and requests
/// cluster metadata with a short timeout; a reachable broker reports Healthy.
/// Reported as <see cref="HealthStatus.Degraded"/> on failure so a transient broker
/// blip does not fail the whole readiness endpoint (matches the startup tolerance in
/// TDP-KAFKA-01 §2.7). Replaces the earlier placeholder now that Kafka config exists.
/// </summary>
public sealed class KafkaHealthCheck : IHealthCheck
{
    private static readonly TimeSpan MetadataTimeout = TimeSpan.FromSeconds(3);
    private readonly KafkaOptions _opts;

    public KafkaHealthCheck(IOptions<KafkaOptions> opts) => _opts = opts.Value;

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            using var admin = new AdminClientBuilder(
                new AdminClientConfig { BootstrapServers = _opts.BootstrapServers }).Build();

            var metadata = admin.GetMetadata(MetadataTimeout);

            if (metadata.Brokers.Count == 0)
                return Task.FromResult(HealthCheckResult.Degraded(
                    $"No Kafka brokers reachable at {_opts.BootstrapServers}."));

            return Task.FromResult(HealthCheckResult.Healthy(
                $"Kafka reachable at {_opts.BootstrapServers} ({metadata.Brokers.Count} broker(s))."));
        }
        catch (Exception ex)
        {
            return Task.FromResult(HealthCheckResult.Degraded(
                $"Kafka metadata request failed for {_opts.BootstrapServers}.", ex));
        }
    }
}
