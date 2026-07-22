namespace DisputePortal.Api.Contracts.Ai;

/// <summary>
/// Result of NL extraction (TDP-AI-01 §2.1, SPEC §3.3). Every field is optional/nullable; the
/// <see cref="Confidence"/> map always carries an entry (0.0–1.0) for each of the five field
/// names — even when the value is null — so TDP-FE-03 can apply the AC-DISP-02 "&lt; 0.6 →
/// leave blank" rule uniformly.
/// </summary>
public sealed record ExtractDisputeResponse(
    string? TransactionRef,
    string? Category,
    decimal? Amount,
    string? MerchantName,
    string? TransactionDate,
    IReadOnlyDictionary<string, double> Confidence);
