using DisputePortal.Api.Repositories;

namespace DisputePortal.Api.Services;

/// <summary>
/// <see cref="IDisputeReferenceGenerator"/> implementation (TDP-DISP-01 §2.4). Counts the
/// disputes already created on <c>date</c> (by reference prefix) and formats the
/// next zero-padded 5-digit sequence, e.g. <c>DSP-20260714-00042</c>. The count-then-format
/// approach can collide under concurrent same-day submissions; <see cref="DisputeService"/>
/// wraps generate+insert in a transaction and retries on the <c>reference</c> unique violation.
/// </summary>
public sealed class DisputeReferenceGenerator(IDisputeRepository repository) : IDisputeReferenceGenerator
{
    public async Task<string> GenerateAsync(DateOnly date, CancellationToken ct)
    {
        var prefix = $"DSP-{date:yyyyMMdd}-";
        var next = await repository.CountByReferencePrefixAsync(prefix, ct) + 1;
        return $"{prefix}{next:D5}";
    }
}
