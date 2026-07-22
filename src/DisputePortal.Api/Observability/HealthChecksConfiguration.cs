using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace DisputePortal.Api.Observability;

/// <summary>
/// Liveness / readiness health checks (TDP-OBS-01 §2.6). Readiness covers Postgres
/// (real Npgsql connectivity check) and Kafka. The Kafka check is a lightweight
/// placeholder pending the Kafka producer (TDP-KAFKA-01) — see <see cref="KafkaSelfCheck"/>.
/// All endpoints are public (no JWT) so probes work without auth.
/// </summary>
public static class HealthChecksConfiguration
{
    public static void AddAppHealthChecks(this WebApplicationBuilder builder)
    {
        builder.Services.AddHealthChecks()
            .AddNpgSql(
                builder.Configuration.GetConnectionString("Default")!,
                name: "postgres",
                tags: new[] { "ready" })
            .AddCheck<KafkaSelfCheck>(
                "kafka",
                failureStatus: HealthStatus.Degraded,
                tags: new[] { "ready" });
    }

    public static void MapAppHealthChecks(this WebApplication app)
    {
        // All health endpoints are public — AllowAnonymous() opts them out of the
        // global authenticated fallback policy so probes work without a JWT.

        // Liveness: process is up. No dependency gating — never fails on a DB blip.
        app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false })
            .AllowAnonymous();

        // Readiness: dependencies tagged "ready" reachable; JSON body enumerates each.
        app.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = c => c.Tags.Contains("ready"),
            ResponseWriter = WriteJsonResponse
        }).AllowAnonymous();

        // Aggregate.
        app.MapHealthChecks("/health", new HealthCheckOptions { ResponseWriter = WriteJsonResponse })
            .AllowAnonymous();
    }

    private static async Task WriteJsonResponse(HttpContext ctx, HealthReport report)
    {
        ctx.Response.ContentType = "application/json";
        var payload = new
        {
            status = report.Status.ToString(),
            totalDurationMs = report.TotalDuration.TotalMilliseconds,
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                description = e.Value.Description,
                durationMs = e.Value.Duration.TotalMilliseconds,
                error = e.Value.Exception?.Message
            })
        };
        await ctx.Response.WriteAsync(JsonSerializer.Serialize(payload,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
    }
}

/// <summary>
/// Placeholder Kafka readiness check. The real broker-metadata probe belongs with
/// the Kafka producer in TDP-KAFKA-01; until that lands this reports Healthy so
/// readiness reflects the components actually wired in this batch (Postgres).
/// Swap for <c>AspNetCore.HealthChecks.Kafka</c>'s <c>AddKafka(...)</c> when the
/// producer config is available. (TDP-OBS-01 §2.6, deviation noted in batch report.)
/// </summary>
public sealed class KafkaSelfCheck : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default) =>
        Task.FromResult(HealthCheckResult.Healthy(
            "Kafka connectivity check is a placeholder pending TDP-KAFKA-01."));
}
