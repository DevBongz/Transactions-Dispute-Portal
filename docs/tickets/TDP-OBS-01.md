# TDP-OBS-01 — Serilog Structured Logging, Correlation IDs & Health Endpoints

**Jira summary:** Wire cross-cutting observability into the Dispute Portal API: Serilog configured for structured JSON logging, request-logging middleware that stamps a correlation ID on every request and logs method, path, status code, and duration, correlation propagation into Kafka publish/consume and AI calls, and liveness/readiness health endpoints. This satisfies the SPEC §3.6 observability NFRs and gives operators the traceability needed to debug the async dispute pipeline end-to-end.

## 1. Context & Motivation

- **Background:** The API touches Postgres, Kafka, and the Anthropic API across synchronous requests and a background classification consumer. Without structured logs and a correlation ID, tracing a single dispute from `POST /disputes` → `dispute.submitted` → classification → `dispute.classified` is impractical. SPEC §3.1 and §3.6 mandate Serilog structured JSON with correlation IDs and Kafka publish/consume logging.
- **Business Impact:** Observability underpins reliability objectives — proving classification lands "within 5 seconds" (SPEC §1.1), diagnosing `CLASSIFICATION_FAILED` fallbacks (AC-AI-02), and confirming `dispute.submitted` fires within 1 second (AC-DISP-04) all rely on timestamped, correlated logs. Health endpoints let Docker Compose and future orchestration gate readiness.
- **User Story:** As the developer/operator (Bongani), I want every request and event correlated and health-checkable so that I can trace a dispute across HTTP, Kafka, and AI boundaries and confirm the stack is up.
- **Dependencies:** TDP-INFRA-01 (solution scaffold + `Program.cs`). Enhances observability for every other Group B/C ticket (TDP-KAFKA-01, TDP-DISP-*, TDP-AI-*). Milestone: **Day 2** (SPEC §4.1), but foundational and should land early in the day.

## 2. Detailed Description

### 2.1 Packages

```xml
<PackageReference Include="Serilog.AspNetCore" Version="8.0.*" />
<PackageReference Include="Serilog.Sinks.Console" Version="6.0.*" />
<PackageReference Include="Serilog.Enrichers.Environment" Version="3.0.*" />
<PackageReference Include="Serilog.Enrichers.Thread" Version="4.0.*" />
<PackageReference Include="AspNetCore.HealthChecks.NpgSql" Version="8.0.*" />
```

### 2.2 Serilog bootstrap (JSON to stdout)

Logs go to the console as compact JSON (Docker captures stdout). Configured in `Program.cs` with a two-stage bootstrap so startup failures are also logged.

```csharp
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Database.Command", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .Enrich.WithMachineName()
    .Enrich.WithThreadId()
    .Enrich.WithProperty("Application", "DisputePortal.Api")
    .WriteTo.Console(new Serilog.Formatting.Compact.CompactJsonFormatter())
    .CreateLogger();

builder.Host.UseSerilog();
```

`appsettings.json` mirror (so levels are tunable without a rebuild):

```json
"Serilog": {
  "MinimumLevel": {
    "Default": "Information",
    "Override": { "Microsoft.AspNetCore": "Warning", "System": "Warning" }
  }
}
```

### 2.3 Correlation ID middleware

Runs first in the pipeline. Honours an inbound `X-Correlation-ID` header (so a caller/UI can supply one) or generates a GUID, pushes it into `LogContext` so every log line in the request scope carries it, and echoes it back on the response.

```csharp
public sealed class CorrelationIdMiddleware
{
    public const string HeaderName = "X-Correlation-ID";
    private readonly RequestDelegate _next;
    public CorrelationIdMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext ctx)
    {
        var correlationId = ctx.Request.Headers.TryGetValue(HeaderName, out var v) && !string.IsNullOrWhiteSpace(v)
            ? v.ToString()
            : Guid.NewGuid().ToString();

        ctx.Items[HeaderName] = correlationId;
        ctx.Response.Headers[HeaderName] = correlationId;

        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await _next(ctx);
        }
    }
}
```

Registered before `UseSerilogRequestLogging()`:

```csharp
app.UseMiddleware<CorrelationIdMiddleware>();
```

### 2.4 Request logging (method, path, status, duration)

Use Serilog's request logging, enriched so the message template carries status code and elapsed ms (SPEC §3.6: "All requests logged with correlation ID, status code, duration").

```csharp
app.UseSerilogRequestLogging(opts =>
{
    opts.MessageTemplate =
        "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0} ms";
    opts.EnrichDiagnosticContext = (diag, http) =>
    {
        diag.Set("RequestHost", http.Request.Host.Value);
        diag.Set("UserId", http.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "anonymous");
        // CorrelationId already flows via LogContext from the middleware.
    };
});
```

`Elapsed` is provided by Serilog's timing; the correlation id is attached via `LogContext`, so one JSON line per request looks like:

```json
{
  "@t": "2026-07-14T09:31:22.140Z",
  "@mt": "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0} ms",
  "RequestMethod": "POST", "RequestPath": "/api/v1/disputes",
  "StatusCode": 201, "Elapsed": 42.7,
  "CorrelationId": "3f2504e0-4f89-41d3-9a0c-0305e82c3301",
  "UserId": "c3d4...", "Application": "DisputePortal.Api"
}
```

### 2.5 Kafka publish/consume logging with correlation

TDP-KAFKA-01 already logs topic/partition/offset on publish. This ticket ensures the correlation id rides along: the `IEventPublisher` writes the current correlation id into a Kafka header (`correlationId`), and the classification consumer (TDP-AI-02) reads it back and pushes it into `LogContext` so consumer-side logs correlate with the originating HTTP request.

```csharp
// Publisher (extends TDP-KAFKA-01 KafkaEventPublisher):
message.Headers.Add("correlationId", Encoding.UTF8.GetBytes(_correlationAccessor.Current ?? "-"));

// Consumer (TDP-AI-02) start-of-message handling:
var cid = result.Message.Headers.TryGetLastBytes("correlationId", out var b)
    ? Encoding.UTF8.GetString(b) : Guid.NewGuid().ToString();
using (LogContext.PushProperty("CorrelationId", cid))
{
    _logger.LogInformation("Consumed {Topic}[{Partition}]@{Offset} eventId {EventId}",
        result.Topic, result.Partition.Value, result.Offset.Value, /* ... */);
    // ... classification work ...
}
```

A tiny `ICorrelationAccessor` (backed by `IHttpContextAccessor` for request-path publishes, or `AsyncLocal` fallback) exposes the current correlation id to the publisher.

### 2.6 Health endpoints

Expose ASP.NET Core health checks so Compose (and any future orchestrator) can probe liveness and readiness. Postgres and Kafka connectivity are readiness dependencies.

```csharp
builder.Services.AddHealthChecks()
    .AddNpgSql(builder.Configuration.GetConnectionString("Default")!, name: "postgres", tags: new[] { "ready" })
    .AddKafka(new ProducerConfig { BootstrapServers = builder.Configuration["Kafka:BootstrapServers"] },
              name: "kafka", tags: new[] { "ready" });

app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });          // process up
app.MapHealthChecks("/health/ready", new HealthCheckOptions {                                    // deps reachable
    Predicate = c => c.Tags.Contains("ready"),
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse                                 // JSON body
});
app.MapHealthChecks("/health");                                                                  // aggregate
```

`/health/live` returns `200` if the process is running (no dependency checks — never fails on a transient DB blip). `/health/ready` returns `200` only when Postgres and Kafka are reachable, and `503` otherwise, with a JSON body listing each check's status. These endpoints are **[Public]** (no JWT) so probes work without auth.

### 2.7 Directory layout

```
src/DisputePortal.Api/
  Observability/
    CorrelationIdMiddleware.cs
    ICorrelationAccessor.cs
    HttpCorrelationAccessor.cs
    SerilogConfiguration.cs      # extension: AddSerilogLogging(builder)
    HealthChecksConfiguration.cs # extension: AddAppHealthChecks / MapAppHealthChecks
```

## 3. Acceptance Criteria

- Logs are emitted as structured JSON to stdout (Docker-capturable), with `Application`, `MachineName`, and timestamp enrichers (SPEC §3.1/§3.6).
- Every HTTP request produces one summary log line containing correlation id, HTTP method, path, status code, and duration in ms (SPEC §3.6 observability).
- Each request has a correlation id: taken from an inbound `X-Correlation-ID` header if present, otherwise generated; it is echoed back in the response header and present on all log lines within the request scope.
- The correlation id propagates into Kafka messages (header) and is restored into the consumer's log scope, so a dispute can be traced HTTP → publish → consume (ties to SPEC §3.6 "Kafka publish/consume events logged with topic, partition, offset").
- `GET /health/live` returns `200` when the process is up (no dependency gating).
- `GET /health/ready` returns `200` when Postgres and Kafka are reachable and `503` otherwise, with a JSON body enumerating each dependency's status.
- Health endpoints require no authentication.
- Log levels are configurable via `appsettings.json` without recompilation; EF Core command noise is suppressed to Warning by default.

## 4. Technical Notes

- **Two-stage Serilog init:** create the bootstrap logger before `builder.Build()` so exceptions during startup (e.g. bad connection string) are logged; then `UseSerilog()` swaps to the configured pipeline. Wrap the app run in `try/catch` with `Log.CloseAndFlush()` in `finally`.
- **Correlation ordering:** `CorrelationIdMiddleware` must be registered before `UseSerilogRequestLogging()` and before auth so the id is present on auth-failure logs too.
- **Background-thread correlation:** the classification consumer runs off the HTTP pipeline, so it cannot use `IHttpContextAccessor`; it derives the correlation id from the Kafka header instead. The publisher's `ICorrelationAccessor` should fall back gracefully to `"-"` when no HTTP context exists (e.g. `dispute.classified` published from the consumer).
- **Health check dependency versions:** `AspNetCore.HealthChecks.NpgSql` and `.Kafka` 8.x align with .NET 8. The Kafka check does a lightweight metadata request; keep its timeout short (~2s) so `/health/ready` stays responsive.
- **PII/secrets:** never log passwords, JWTs, or the `ANTHROPIC_API_KEY` (SPEC §3.6 security). The request logger enriches with `UserId` (a GUID), not credentials. Ensure request/response bodies are NOT logged by default.
- **Compose integration:** `/health/ready` can back a Compose `healthcheck` for the `api` service so dependent services wait for genuine readiness (complements TDP-INFRA-02).
- **Performance:** structured logging must not blow the P95 < 300ms budget; console JSON sink is async-friendly and cheap at demo volume.

## 5. Definition of Done

- [ ] Serilog configured for compact JSON stdout with enrichers; levels tunable via `appsettings.json`.
- [ ] `CorrelationIdMiddleware` registered first; correlation id generated/honoured and echoed in `X-Correlation-ID` response header.
- [ ] `UseSerilogRequestLogging` emits one line per request with method, path, status, duration, and correlation id.
- [ ] Correlation id propagated into Kafka message headers and restored in the consumer log scope (coordinated with TDP-KAFKA-01 / TDP-AI-02).
- [ ] `/health/live`, `/health/ready`, and `/health` mapped; ready-check covers Postgres + Kafka and returns JSON; all public (no auth).
- [ ] Manual verification: a `POST /api/v1/disputes` shows a correlated request log and the resulting `dispute.submitted` publish log sharing the same `CorrelationId`; `/health/ready` returns `200` with the stack up and `503` with Postgres stopped.
- [ ] No secrets or bodies logged.
- [ ] Reviewed and merged to `main`.
