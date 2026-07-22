namespace DisputePortal.Api.Observability;

/// <summary>
/// <see cref="ICorrelationAccessor"/> backed by <see cref="IHttpContextAccessor"/>.
/// Reads the correlation id stamped by <see cref="CorrelationIdMiddleware"/> for
/// request-path publishes; returns <c>null</c> when there is no HTTP context (e.g.
/// the background classification consumer, which derives the id from the Kafka
/// header instead — TDP-OBS-01 §2.5/§4).
/// </summary>
public sealed class HttpCorrelationAccessor(IHttpContextAccessor accessor) : ICorrelationAccessor
{
    public string? Current =>
        accessor.HttpContext?.Items.TryGetValue(CorrelationIdMiddleware.HeaderName, out var v) == true
            ? v?.ToString()
            : null;
}
