using Serilog.Context;

namespace DisputePortal.Api.Observability;

/// <summary>
/// Stamps every request with a correlation id (TDP-OBS-01 §2.3). Honours an
/// inbound <c>X-Correlation-ID</c> header if present, otherwise generates a GUID;
/// pushes it into the Serilog <see cref="LogContext"/> so every log line in the
/// request scope carries it, stores it on <see cref="HttpContext.Items"/> for the
/// <see cref="ICorrelationAccessor"/>, and echoes it back on the response.
/// Must be registered first so it also covers auth-failure logs.
/// </summary>
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

        // Echo before the response starts, so it is present even on early failures.
        ctx.Response.OnStarting(() =>
        {
            ctx.Response.Headers[HeaderName] = correlationId;
            return Task.CompletedTask;
        });

        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await _next(ctx);
        }
    }
}
