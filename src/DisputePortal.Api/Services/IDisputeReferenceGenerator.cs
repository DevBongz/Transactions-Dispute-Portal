namespace DisputePortal.Api.Services;

/// <summary>
/// Produces the human-readable dispute reference <c>DSP-YYYYMMDD-NNNNN</c> (TDP-DISP-01 §2.4,
/// SPEC §3.2). <c>NNNNN</c> is a zero-padded per-day sequence derived from the count of
/// disputes already created that day; the <c>reference</c> UNIQUE index is the race backstop.
/// </summary>
public interface IDisputeReferenceGenerator
{
    Task<string> GenerateAsync(DateOnly date, CancellationToken ct);
}
