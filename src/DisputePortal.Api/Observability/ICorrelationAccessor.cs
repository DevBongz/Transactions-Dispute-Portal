namespace DisputePortal.Api.Observability;

/// <summary>
/// Exposes the correlation id for the current logical operation so components off
/// the HTTP pipeline (e.g. the future Kafka publisher, TDP-KAFKA-01) can stamp it
/// onto outbound messages (TDP-OBS-01 §2.5). Falls back gracefully when there is
/// no ambient correlation id (returns <c>null</c>; callers substitute <c>"-"</c>).
/// </summary>
public interface ICorrelationAccessor
{
    string? Current { get; }
}
