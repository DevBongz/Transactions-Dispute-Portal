using System.Text.Json;
using DisputePortal.Api.Infrastructure;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace DisputePortal.Api.Observability;

/// <summary>
/// Liveness / readiness health checks (TDP-OBS-01 §2.6). Readiness covers Postgres
/// (real Npgsql connectivity check) and Kafka (real broker-metadata probe, see
/// <see cref="KafkaHealthCheck"/>, added with the producer in TDP-KAFKA-01).
/// All endpoints are public (no JWT) so probes work without auth.
/// </summary>
public static class HealthChecksConfiguration
{
    public static void AddAppHealthChecks(this WebApplicationBuilder builder)
    {
        // Normalise managed-host postgres:// URLs the same way DbContext does — Npgsql
        // health checks reject URI form with "Format of the initialization string…".
        var connectionString = NpgsqlConnectionString.Normalize(
            builder.Configuration.GetConnectionString("Default"))!;

        builder.Services.AddHealthChecks()
            .AddNpgSql(
                connectionString,
                name: "postgres",
                tags: new[] { "ready" })
            .AddCheck<KafkaHealthCheck>(
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
