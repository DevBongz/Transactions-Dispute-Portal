namespace DisputePortal.Api.Contracts.Ai;

/// <summary>
/// Result of resolution-summary generation (TDP-AI-03 §2.1, SPEC §3.3): a plain-language,
/// 2–4 sentence customer-facing paragraph. Returned for preview/edit only — persisted later
/// by <c>POST /disputes/{id}/resolve</c> (TDP-DISP-03).
/// </summary>
public sealed record GenerateSummaryResponse(string Summary);
